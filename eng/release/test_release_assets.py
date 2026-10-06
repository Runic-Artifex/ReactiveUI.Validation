#!/usr/bin/env python3
"""Focused promotion-evidence fixtures; no GitHub mutations."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("release_assets", Path(__file__).with_name("release_assets.py"))
RELEASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RELEASE)
EVIDENCE = Path(os.environ.get("VALIDATION_RELEASE_EVIDENCE", ROOT.parents[1] / "ReactiveUI.Validation/artifacts/verification/dynamicdata-upgrade-2026-10-06/hosted-37391350972/artifacts"))
SOURCE = "c5afc80b55a4c65f262ca0a60879f9319c62991e"


def manifest(folder):
    packages = []
    for path in sorted(folder.glob("*.nupkg")):
        packages.append({"filename": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "size": path.stat().st_size})
    (folder / "package-manifest.json").write_text(json.dumps({"source": SOURCE, "packages": packages}))


def package(folder, package_id, marker):
    reactive = package_id.endswith(".Reactive")
    assembly = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
    version = "8.1.0-runic.0.1"
    path = folder / f"{package_id}.{version}.nupkg"
    reactive_ui = "ReactiveUI.Reactive" if reactive else "ReactiveUI"
    dynamic_data = "Runic.DynamicData.Reactive" if reactive else "Runic.DynamicData"
    nuspec = f'''<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>
<id>{package_id}</id><version>{version}</version><authors>ReactiveUI and Contributors</authors>
<license type="expression">MIT</license><repository type="git" url="https://github.com/Runic-Artifex/ReactiveUI.Validation" commit="{SOURCE}" />
<dependencies><group targetFramework="net10.0"><dependency id="{reactive_ui}" version="26.0.1" /><dependency id="{dynamic_data}" version="10.0.0-runic.30" /></group></dependencies>
</metadata></package>'''
    with RELEASE.zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        archive.writestr("README.md", "readme")
        archive.writestr("logo.png", b"logo")
        archive.writestr("LICENSE/LICENSE", (ROOT / "LICENSE").read_bytes())
        archive.writestr("analyzers/dotnet/roslyn5.9/cs/ReactiveUI.Validation.SourceGenerators.dll", marker)
        archive.writestr(f"buildTransitive/{package_id}.props", marker)
        archive.writestr(f"lib/net10.0/{assembly}.dll", marker)
        archive.writestr(f"lib/net10.0/{assembly}.xml", marker)
    return path


def report(packages, rid, mode, checks, repository_commit=False):
    data = {"source": SOURCE, "dirty": False, "sdk": json.loads((ROOT / "global.json").read_text())["sdk"]["version"],
            "rid": rid, "mode": mode, "packages": packages, "checks": [{"name": name, "passed": True} for name in checks], "completed": True}
    if repository_commit:
        for value in data["packages"].values():
            value["repositoryCommit"] = SOURCE
    return data


def make_evidence(target):
    all_generated = {f"{flavor}-{stage}" for flavor in ("Primitives", "Reactive") for stage in ("managed", "trimmed", "native")}
    all_generated |= {f"{flavor}-NEGATIVE_{case}" for flavor in ("Primitives", "Reactive") for case in ("STORED_SELECTOR", "COMPUTED_SELECTOR", "INDEXER", "NESTED_TARGET", "NONNOTIFY_RULE", "STRUCT_OWNER")}
    host_packages = {}
    for host in ("ubuntu-latest", "windows-latest"):
        folder = target / f"packages-{host}"
        folder.mkdir()
        pair = {package_id: package(folder, package_id, host.encode()) for package_id in RELEASE.PACKAGE_IDS}
        manifest(folder)
        host_packages[host] = {"Primitives": {"id": RELEASE.PACKAGE_IDS[0], "version": "8.1.0-runic.0.1", "sha256": RELEASE.sha256(pair[RELEASE.PACKAGE_IDS[0]])},
                               "Reactive": {"id": RELEASE.PACKAGE_IDS[1], "version": "8.1.0-runic.0.1", "sha256": RELEASE.sha256(pair[RELEASE.PACKAGE_IDS[1]])}}
    for rid in ("linux-x64", "win-x64"):
        for prefix, mode, checks in (("native-validation", "native", {"candidate-Primitives-native", "candidate-Reactive-native", "baseline-Primitives-baseline", "baseline-Reactive-baseline"}), ("generated-validation", "all", all_generated)):
            folder = target / f"{prefix}-{rid}"
            folder.mkdir()
            data = report(host_packages["ubuntu-latest"], rid, mode, checks)
            if prefix == "native-validation":
                data["packages"] = {f"candidate-{name}": value for name, value in data["packages"].items()}
            (folder / "results.json").write_text(json.dumps(data))
    for host, rid in (("ubuntu-latest", "linux-x64"), ("windows-latest", "win-x64")):
        folder = target / f"validation-{host}"
        generated = folder / "artifacts/verification/generated-gates"
        ordinary = folder / "artifacts/verification/package-smoke"
        generated.mkdir(parents=True)
        ordinary.mkdir(parents=True)
        managed_checks = {"Primitives-managed", "Reactive-managed"}
        managed_checks |= {f"{flavor}-NEGATIVE_{case}" for flavor in ("Primitives", "Reactive")
                           for case in ("STORED_SELECTOR", "COMPUTED_SELECTOR", "INDEXER", "NESTED_TARGET", "NONNOTIFY_RULE", "STRUCT_OWNER")}
        (generated / "results.json").write_text(json.dumps(report(host_packages[host], rid, "managed", managed_checks)))
        smoke = report(host_packages[host], rid, "smoke", {"Primitives-managed", "Reactive-managed"}, True)
        smoke.pop("rid")
        smoke.pop("mode")
        (ordinary / "results.json").write_text(json.dumps(smoke))
        trx = folder / "src/TestResults"
        trx.mkdir(parents=True)
        for name in ("ReactiveUI.Validation.Tests", "ReactiveUI.Validation.Reactive.Tests", "ReactiveUI.Validation.SourceGenerators.Tests"):
            (trx / f"{name}_net10.0_x64.trx").write_text('<TestRun><ResultSummary outcome="Completed"><Counters total="1" passed="1" failed="0" error="0" aborted="0" timeout="0" notExecuted="0" /></ResultSummary></TestRun>')
    source_report = target / "source.json"
    source_report.write_text(json.dumps({"source": SOURCE}))
    return source_report


class ReleaseEvidenceTests(unittest.TestCase):
    def test_hermetic_complete_evidence_and_representative_report_failures(self):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary)
            source_report = make_evidence(target)
            output = target / "verified.json"
            with mock.patch.dict(os.environ, {"GITHUB_SHA": SOURCE}):
                RELEASE.verify(target, source_report, output)
            self.assertEqual(json.loads(output.read_text())["version"], "8.1.0-runic.0.1")
            generated = target / "validation-ubuntu-latest/artifacts/verification/generated-gates/results.json"
            value = json.loads(generated.read_text())
            value["source"] = "0" * 40
            generated.write_text(json.dumps(value))
            with mock.patch.dict(os.environ, {"GITHUB_SHA": SOURCE}):
                with self.assertRaisesRegex(ValueError, "incomplete or not for the release source"):
                    RELEASE.verify(target, source_report, output)

    def test_latest_eligible_run_must_succeed(self):
        base = {"head_sha": SOURCE, "head_repository": {"full_name": "Runic-Artifex/ReactiveUI.Validation"},
                "head_branch": "main", "event": "push", "path": RELEASE.BUILD_WORKFLOW}
        pages = [{"workflow_runs": [{**base, "id": 1, "created_at": "2026-10-06T00:00:00Z", "status": "completed", "conclusion": "success"},
                                    {**base, "id": 2, "created_at": "2026-10-06T01:00:00Z", "status": "in_progress", "conclusion": None}]}]
        with self.assertRaisesRegex(ValueError, "latest eligible"):
            RELEASE.select_run(pages, "Runic-Artifex/ReactiveUI.Validation", SOURCE)

    def test_untrusted_or_wrong_source_runs_do_not_mask_a_trusted_build(self):
        trusted = {"head_sha": SOURCE, "head_repository": {"full_name": "Runic-Artifex/ReactiveUI.Validation"},
                   "head_branch": "main", "event": "push", "path": RELEASE.BUILD_WORKFLOW,
                   "id": 1, "created_at": "2026-10-06T00:00:00Z", "status": "completed", "conclusion": "success"}
        pages = [{"workflow_runs": [trusted,
                                     {**trusted, "id": 3, "created_at": "2026-10-06T02:00:00Z", "head_repository": {"full_name": "someone/else"}, "conclusion": "failure"},
                                     {**trusted, "id": 4, "created_at": "2026-10-06T03:00:00Z", "head_sha": "0" * 40, "conclusion": "failure"}]}]
        self.assertEqual(RELEASE.select_run(pages, "Runic-Artifex/ReactiveUI.Validation", SOURCE)["id"], 1)

    def test_latest_job_retry_must_succeed(self):
        jobs = []
        for index, name in enumerate(sorted(RELEASE.EXPECTED_JOBS), 1):
            jobs.append({"name": name, "id": index, "run_attempt": 1, "status": "completed", "completed_at": "2026-10-06T00:00:00Z", "conclusion": "success"})
        jobs.append({**jobs[0], "id": 9, "run_attempt": 2, "conclusion": "failure"})
        with self.assertRaisesRegex(ValueError, "did not succeed"):
            RELEASE.verify_jobs([{ "jobs": jobs }])

    def test_actual_hosted_evidence_shape_with_completed_reports(self):
        if not EVIDENCE.is_dir():
            self.skipTest("retained hosted evidence is unavailable")
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "evidence"
            shutil.copytree(EVIDENCE, target)
            for host in ("ubuntu-latest", "windows-latest"):
                manifest(target / f"packages-{host}")
            for path in target.rglob("results.json"):
                value = json.loads(path.read_text())
                value["completed"] = True
                path.write_text(json.dumps(value))
            # Current retained evidence predates the ordinary package-smoke
            # report. Add its final two-check shape beside generated-gates so
            # this fixture also proves the release guard selects both paths.
            for host in ("ubuntu-latest", "windows-latest"):
                generated = json.loads((target / f"validation-{host}" / "artifacts/verification/generated-gates/results.json").read_text())
                ordinary = {key: generated[key] for key in ("source", "dirty", "sdk", "rid", "packages", "completed")}
                ordinary.pop("rid")
                for package in ordinary["packages"].values():
                    package["repositoryCommit"] = SOURCE
                ordinary["checks"] = [{"name": "Primitives-managed", "passed": True}, {"name": "Reactive-managed", "passed": True}]
                destination = target / f"validation-{host}" / "artifacts/verification/package-smoke"
                destination.mkdir(parents=True)
                (destination / "results.json").write_text(json.dumps(ordinary))
            source_report = target / "source.json"
            source_report.write_text(json.dumps({"source": SOURCE}))
            output = target / "verified.json"
            old = os.environ.get("GITHUB_SHA")
            os.environ["GITHUB_SHA"] = SOURCE
            try:
                RELEASE.verify(target, source_report, output)
            finally:
                if old is None:
                    del os.environ["GITHUB_SHA"]
                else:
                    os.environ["GITHUB_SHA"] = old
            self.assertEqual(json.loads(output.read_text())["source"], SOURCE)

    def test_manifest_corruption_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            package = folder / "Runic.ReactiveUI.Validation.8.1.0-runic.0.1.nupkg"
            package.write_bytes(b"wrong")
            (folder / "package-manifest.json").write_text(json.dumps({"source": SOURCE, "packages": [{"filename": package.name, "sha256": "0" * 64, "size": 5}]}))
            with self.assertRaises(ValueError):
                RELEASE.verify_manifest(folder, {RELEASE.PACKAGE_IDS[0]: package, RELEASE.PACKAGE_IDS[1]: package}, SOURCE)

    def test_artifact_digest_is_checked_before_extraction(self):
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / "artifact"
            with tempfile.SpooledTemporaryFile() as archive:
                with RELEASE.zipfile.ZipFile(archive, "w") as zip_file:
                    zip_file.writestr("safe/report.json", "{}")
                archive.seek(0)
                payload = archive.read()
            RELEASE.extract_artifact(payload, "sha256:" + hashlib.sha256(payload).hexdigest(), destination)
            self.assertEqual((destination / "safe/report.json").read_text(), "{}")
            with self.assertRaisesRegex(ValueError, "SHA-256"):
                RELEASE.extract_artifact(payload, "sha256:" + "0" * 64, Path(temporary) / "bad")

    def test_wrong_draft_tag_is_rejected_before_upload_or_publish(self):
        with self.assertRaisesRegex(ValueError, "expected draft"):
            RELEASE.require_draft({"id": 7, "tag_name": "runic-vwrong", "draft": True, "prerelease": True,
                                   "target_commitish": SOURCE}, "runic-v8.1.0-runic.0.1", SOURCE)

    def test_mocked_publication_downloads_exact_draft_bytes_before_tag_and_publish(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            first, second = folder / "Runic.ReactiveUI.Validation.8.1.0-runic.0.1.nupkg", folder / "Runic.ReactiveUI.Validation.Reactive.8.1.0-runic.0.1.nupkg"
            first.write_bytes(b"first")
            second.write_bytes(b"second")
            packages = {"Primitives": {"path": str(first), "sha256": RELEASE.sha256(first)},
                        "Reactive": {"path": str(second), "sha256": RELEASE.sha256(second)}}
            verified = folder / "verified.json"
            verified.write_text(json.dumps({"source": SOURCE, "version": "8.1.0-runic.0.1", "packages": packages}))
            tag = "runic-v8.1.0-runic.0.1"
            calls = []
            assets = [{"id": 1, "name": first.name, "state": "uploaded", "size": 5, "digest": "sha256:" + packages["Primitives"]["sha256"]},
                      {"id": 2, "name": second.name, "state": "uploaded", "size": 6, "digest": "sha256:" + packages["Reactive"]["sha256"]}]
            draft = {"id": 9, "tag_name": tag, "draft": True, "prerelease": True, "target_commitish": SOURCE}
            def fake_api(endpoint, *args):
                calls.append(endpoint)
                if endpoint.endswith("/releases?per_page=100"): return "[[]]"
                if endpoint.endswith("/releases"): return json.dumps(draft)
                if endpoint.endswith("/releases/9"): return json.dumps({**draft, "assets": assets})
                if endpoint.endswith("/git/ref/tags/" + tag): return json.dumps({"object": {"type": "commit", "sha": SOURCE}})
                return "{}"
            def fake_run(command, **_):
                if "--include" in command:
                    return subprocess.CompletedProcess(command, 1, "HTTP/2 404\n", "")
                return subprocess.CompletedProcess(command, 0, "", "")
            with mock.patch.object(RELEASE, "github_environment", return_value=("Runic-Artifex/ReactiveUI.Validation", SOURCE)), \
                 mock.patch.object(RELEASE, "require_clean_source"), mock.patch.object(RELEASE, "api", side_effect=fake_api), \
                 mock.patch.object(RELEASE.subprocess, "run", side_effect=fake_run), \
                 mock.patch.object(RELEASE.subprocess, "check_output", side_effect=[b"first", b"second"]):
                RELEASE.publish(verified)
            self.assertLess(calls.index("repos/Runic-Artifex/ReactiveUI.Validation/git/refs"),
                            max(index for index, call in enumerate(calls) if call.endswith("/releases/9")))
            calls.clear()
            with mock.patch.object(RELEASE, "github_environment", return_value=("Runic-Artifex/ReactiveUI.Validation", SOURCE)), \
                 mock.patch.object(RELEASE, "require_clean_source"), mock.patch.object(RELEASE, "api", side_effect=fake_api), \
                 mock.patch.object(RELEASE.subprocess, "run", side_effect=fake_run), \
                 mock.patch.object(RELEASE.subprocess, "check_output", side_effect=[b"corrupt"]):
                with self.assertRaisesRegex(ValueError, "uploaded draft bytes differ"):
                    RELEASE.publish(verified)
            self.assertNotIn("repos/Runic-Artifex/ReactiveUI.Validation/git/refs", calls)
            self.assertEqual(calls.count("repos/Runic-Artifex/ReactiveUI.Validation/releases/9"), 1)

    def test_completed_trx_with_a_failed_counter_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary) / "src/TestResults"
            folder.mkdir(parents=True)
            for name in ("ReactiveUI.Validation.Tests", "ReactiveUI.Validation.Reactive.Tests", "ReactiveUI.Validation.SourceGenerators.Tests"):
                failed = "1" if name == "ReactiveUI.Validation.Tests" else "0"
                (folder / f"{name}_net10.0_x64.trx").write_text(f'<TestRun><ResultSummary outcome="Completed"><Counters total="1" passed="0" failed="{failed}" error="0" aborted="0" timeout="0" notExecuted="0" /></ResultSummary></TestRun>')
            with self.assertRaisesRegex(ValueError, "non-passing"):
                RELEASE.verify_trx(Path(temporary))


if __name__ == "__main__":
    unittest.main()
