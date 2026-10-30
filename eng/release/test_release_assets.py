#!/usr/bin/env python3
"""Focused promotion-evidence fixtures and a mocked nuget.org; no GitHub or nuget.org access."""
import hashlib
import http.server
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import threading
import unittest
from unittest import mock
import urllib.parse
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("release_assets", Path(__file__).with_name("release_assets.py"))
RELEASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RELEASE)
SOURCE = "c5afc80b55a4c65f262ca0a60879f9319c62991e"


def manifest(folder):
    packages = []
    for path in sorted(folder.glob("*.nupkg")):
        packages.append({"filename": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "size": path.stat().st_size})
    (folder / "package-manifest.json").write_text(json.dumps({"source": SOURCE, "packages": packages}))


def metadata_fields(reactive):
    flavor = "System.Reactive" if reactive else "ReactiveUI.Primitives"
    return {"authors": "ReactiveUI and Contributors, Runic Artifex",
            "title": f"Runic Artifex fork of ReactiveUI.Validation for {flavor}",
            "description": f"The Runic Artifex fork of ReactiveUI.Validation for .NET 10 and {flavor}.",
            "copyright": "Copyright © 2019- 2026 ReactiveUI and Contributors"}


def package(folder, package_id, marker, dynamic_data=None, dynamic_data_version=RELEASE.DYNAMIC_DATA_VERSION, version="9.0.0-runic.1", fields=None):
    reactive = package_id.endswith(".Reactive")
    assembly = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
    path = folder / f"{package_id}.{version}.nupkg"
    fields = {**metadata_fields(reactive), **(fields or {})}
    extra = "".join(f"<{name}>{value}</{name}>" for name, value in fields.items() if value is not None)
    reactive_ui = "ReactiveUI.Reactive" if reactive else "ReactiveUI"
    dynamic_data = dynamic_data or ("RunicArtifex.DynamicData.Reactive" if reactive else "RunicArtifex.DynamicData")
    nuspec = f'''<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>
<id>{package_id}</id><version>{version}</version>{extra}
<license type="expression">MIT</license><repository type="git" url="https://github.com/Runic-Artifex/ReactiveUI.Validation" commit="{SOURCE}" />
<dependencies><group targetFramework="net10.0"><dependency id="{reactive_ui}" version="26.0.1" /><dependency id="{dynamic_data}" version="{dynamic_data_version}" /></group></dependencies>
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
    all_generated |= {f"{flavor}-NEGATIVE_{case}" for flavor in ("Primitives", "Reactive") for case in ("OPAQUE_SELECTOR", "UNMARKED_INIT", "VALUE_VIEW")}
    host_packages = {}
    for host in ("ubuntu-latest", "windows-latest"):
        folder = target / f"packages-{host}"
        folder.mkdir()
        pair = {package_id: package(folder, package_id, host.encode()) for package_id in RELEASE.PACKAGE_IDS}
        manifest(folder)
        host_packages[host] = {"Primitives": {"id": RELEASE.PACKAGE_IDS[0], "version": "9.0.0-runic.1", "sha256": RELEASE.sha256(pair[RELEASE.PACKAGE_IDS[0]])},
                               "Reactive": {"id": RELEASE.PACKAGE_IDS[1], "version": "9.0.0-runic.1", "sha256": RELEASE.sha256(pair[RELEASE.PACKAGE_IDS[1]])}}
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
                           for case in ("OPAQUE_SELECTOR", "UNMARKED_INIT", "VALUE_VIEW")}
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
            self.assertEqual(json.loads(output.read_text())["version"], "9.0.0-runic.1")
            generated = target / "validation-ubuntu-latest/artifacts/verification/generated-gates/results.json"
            value = json.loads(generated.read_text())
            value["source"] = "0" * 40
            generated.write_text(json.dumps(value))
            with mock.patch.dict(os.environ, {"GITHUB_SHA": SOURCE}):
                with self.assertRaisesRegex(ValueError, "incomplete or not for the release source"):
                    RELEASE.verify(target, source_report, output)

    def test_generated_gate_requires_every_stage_and_passing_checks(self):
        for mutate in (lambda value: value.update(checks=[item for item in value["checks"] if item["name"] != "Reactive-native"]),
                       lambda value: value["checks"][0].update(passed=False),
                       lambda value: value.update(mode="managed")):
            with tempfile.TemporaryDirectory() as temporary:
                target = Path(temporary)
                source_report = make_evidence(target)
                generated = target / "generated-validation-linux-x64/results.json"
                value = json.loads(generated.read_text())
                mutate(value)
                generated.write_text(json.dumps(value))
                with mock.patch.dict(os.environ, {"GITHUB_SHA": SOURCE}):
                    with self.assertRaisesRegex(ValueError, "generated validation report is incomplete for linux-x64"):
                        RELEASE.verify(target, source_report, target / "verified.json")

    def test_package_must_depend_on_the_current_nuget_org_dynamic_data_pair(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            for package_id in RELEASE.PACKAGE_IDS:
                RELEASE.verify_package(package(folder, package_id, b"current"), package_id, SOURCE)
            reactive_id = RELEASE.PACKAGE_IDS[1]
            for dynamic_data, version, message in (
                    ("Runic.DynamicData", "10.0.0-runic.118", "does not pin RunicArtifex.DynamicData"),
                    ("RunicArtifex.DynamicData", "10.0.0-runic.30", "does not pin RunicArtifex.DynamicData"),
                    ("RunicArtifex.DynamicData", "10.0.0-runic.118", "does not pin RunicArtifex.DynamicData at 10.0.0"),
                    ("RunicArtifex.DynamicData.Reactive", "10.0.0-runic.118", "does not pin RunicArtifex.DynamicData.Reactive at 10.0.0"),
                    ("Runic.DynamicData.Reactive", "10.0.0-runic.30", "does not pin RunicArtifex.DynamicData.Reactive")):
                package_id = reactive_id if dynamic_data.endswith(".Reactive") else RELEASE.PACKAGE_IDS[0]
                with self.subTest(dynamic_data=dynamic_data, version=version), tempfile.TemporaryDirectory() as other:
                    path = package(Path(other), package_id, b"stale", dynamic_data, version)
                    with self.assertRaisesRegex(ValueError, message):
                        RELEASE.verify_package(path, package_id, SOURCE)

    def test_package_version_is_stable_or_a_runic_prerelease_of_major_9(self):
        for version in ("9.0.0", "9.0.0-runic.9", "9.1.0-runic.1", "9.0.1"):
            with self.subTest(version=version), tempfile.TemporaryDirectory() as temporary:
                package_id = RELEASE.PACKAGE_IDS[0]
                self.assertEqual(RELEASE.verify_package(package(Path(temporary), package_id, b"v", version=version), package_id, SOURCE)[0], version)
        for version in ("10.0.0", "8.1.0", "9.0.0-beta.1", "9.0.0-runic.1.gabc", "9.0", "9.0.0+build"):
            with self.subTest(version=version), tempfile.TemporaryDirectory() as temporary:
                package_id = RELEASE.PACKAGE_IDS[0]
                with self.assertRaisesRegex(ValueError, "expected branded version identity"):
                    RELEASE.verify_package(package(Path(temporary), package_id, b"v", version=version), package_id, SOURCE)
        self.assertTrue(RELEASE.is_prerelease("9.1.0-runic.1"))
        self.assertFalse(RELEASE.is_prerelease("9.0.0"))

    def test_package_names_the_fork_and_keeps_upstream_attribution(self):
        upstream = {"authors": "ReactiveUI and Contributors", "owners": "xanaisbettsx;ghuntley",
                    "title": None, "description": "Validations library for ReactiveUI."}
        for reactive, package_id in zip((False, True), RELEASE.PACKAGE_IDS):
            opposite = "ReactiveUI.Primitives" if reactive else "System.Reactive"
            for fields, message in (({"authors": upstream["authors"]}, "authors must be"),
                                    ({"owners": upstream["owners"]}, "must not declare <owners>"),
                                    ({"copyright": "Copyright (c) Runic Artifex"}, "copyright"),
                                    ({"title": None}, "<title> must start"),
                                    ({"title": "ReactiveUI.Validation"}, "<title> must start"),
                                    ({"description": upstream["description"]}, "<description> must start"),
                                    ({"description": f"The Runic Artifex fork of ReactiveUI.Validation for {opposite}."}, "<description> must start")):
                with self.subTest(package_id=package_id, fields=fields), tempfile.TemporaryDirectory() as temporary:
                    path = package(Path(temporary), package_id, b"metadata", fields=fields)
                    with self.assertRaisesRegex(ValueError, message):
                        RELEASE.verify_package(path, package_id, SOURCE)

    def test_legacy_runic_ids_are_not_a_release(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            for package_id in RELEASE.LEGACY_PACKAGE_IDS:
                package(folder, package_id, b"legacy")
            with self.assertRaisesRegex(ValueError, "exactly the branded Validation package pair"):
                RELEASE.package_pair(folder)
            with self.assertRaisesRegex(ValueError, "not a branded release package ID"):
                RELEASE.verify_package(next(folder.glob("Runic.ReactiveUI.Validation.9*.nupkg")), RELEASE.LEGACY_PACKAGE_IDS[0], SOURCE)

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


    def test_manifest_corruption_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            package = folder / f"{RELEASE.PACKAGE_IDS[0]}.9.0.0-runic.1.nupkg"
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
            RELEASE.require_draft({"id": 7, "tag_name": "vwrong", "draft": True, "prerelease": True,
                                   "target_commitish": SOURCE}, "v9.0.0-runic.1", SOURCE)

    def test_draft_prerelease_flag_must_match_the_version(self):
        for tag, prerelease in (("v9.0.0-runic.1", True), ("v9.0.0", False)):
            draft = {"id": 7, "tag_name": tag, "draft": True, "target_commitish": SOURCE}
            RELEASE.require_draft({**draft, "prerelease": prerelease}, tag, SOURCE)
            with self.subTest(tag=tag), self.assertRaisesRegex(ValueError, "expected draft"):
                RELEASE.require_draft({**draft, "prerelease": not prerelease}, tag, SOURCE)

    def test_mocked_publication_downloads_exact_draft_bytes_before_tag_and_publish(self):
        for version, prerelease in (("9.0.0-runic.1", True), ("9.0.0", False)):
            with self.subTest(version=version):
                self.mocked_publication(version, prerelease)

    def mocked_publication(self, version, prerelease):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            first, second = (package(folder, package_id, b"published", version=version) for package_id in RELEASE.PACKAGE_IDS)
            tag = f"v{version}"
            calls = []
            created = []
            assets = [{"id": 1, "name": first.name, "state": "uploaded", "size": first.stat().st_size, "digest": "sha256:" + RELEASE.sha256(first)},
                      {"id": 2, "name": second.name, "state": "uploaded", "size": second.stat().st_size, "digest": "sha256:" + RELEASE.sha256(second)}]
            draft = {"id": 9, "tag_name": tag, "draft": True, "prerelease": prerelease, "target_commitish": SOURCE}
            def fake_api(endpoint, *args):
                calls.append(endpoint)
                if endpoint.endswith("/releases?per_page=100"): return "[[]]"
                if endpoint.endswith("/releases"):
                    created.append(args)
                    return json.dumps(draft)
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
                 mock.patch.object(RELEASE.subprocess, "check_output", side_effect=[first.read_bytes(), second.read_bytes()]):
                RELEASE.publish(folder)
            self.assertLess(calls.index("repos/Runic-Artifex/ReactiveUI.Validation/git/refs"),
                            max(index for index, call in enumerate(calls) if call.endswith("/releases/9")))
            self.assertIn(f"prerelease={str(prerelease).lower()}", created[0])
            calls.clear()
            with mock.patch.object(RELEASE, "github_environment", return_value=("Runic-Artifex/ReactiveUI.Validation", SOURCE)), \
                 mock.patch.object(RELEASE, "require_clean_source"), mock.patch.object(RELEASE, "api", side_effect=fake_api), \
                 mock.patch.object(RELEASE.subprocess, "run", side_effect=fake_run), \
                 mock.patch.object(RELEASE.subprocess, "check_output", side_effect=[b"corrupt"]):
                with self.assertRaisesRegex(ValueError, "uploaded draft bytes differ"):
                    RELEASE.publish(folder)
            self.assertNotIn("repos/Runic-Artifex/ReactiveUI.Validation/git/refs", calls)
            self.assertEqual(calls.count("repos/Runic-Artifex/ReactiveUI.Validation/releases/9"), 1)

    def test_github_check_is_read_only_and_refuses_an_existing_release(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            for package_id in RELEASE.PACKAGE_IDS:
                package(folder, package_id, b"checked")
            for releases, error in (("[[]]", None), ('[[{"tag_name": "v9.0.0-runic.1"}]]', "already exists")):
                calls = []
                def fake_api(endpoint, *args):
                    calls.append((endpoint, args))
                    return releases
                with self.subTest(error=error), \
                     mock.patch.object(RELEASE, "github_environment", return_value=("Runic-Artifex/ReactiveUI.Validation", SOURCE)), \
                     mock.patch.object(RELEASE, "require_clean_source"), mock.patch.object(RELEASE, "api", side_effect=fake_api), \
                     mock.patch.object(RELEASE.subprocess, "run", return_value=subprocess.CompletedProcess([], 1, "HTTP/2 404\n", "")):
                    if error:
                        with self.assertRaisesRegex(ValueError, error):
                            RELEASE.check(folder)
                    else:
                        RELEASE.check(folder)
                    self.assertFalse([call for call in calls if "--method" in call[1]])

    def test_stage_copies_only_the_verified_pair(self):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary)
            source_report = make_evidence(target)
            verified = target / "verified.json"
            with mock.patch.dict(os.environ, {"GITHUB_SHA": SOURCE, "GITHUB_REPOSITORY": "Runic-Artifex/ReactiveUI.Validation",
                                              "GITHUB_REF": "refs/heads/main"}):
                RELEASE.verify(target, source_report, verified)
                RELEASE.stage(verified, target / "packages")
                self.assertEqual(sorted(path.name for path in (target / "packages").iterdir()),
                                 sorted(path.name for path in (target / "packages-ubuntu-latest").glob("*.nupkg")))
                with self.assertRaisesRegex(ValueError, "must be empty"):
                    RELEASE.stage(verified, target / "packages")
                data = json.loads(verified.read_text())
                Path(data["packages"]["Reactive"]["path"]).write_bytes(b"changed")
                with self.assertRaisesRegex(ValueError, "changed after verification"):
                    RELEASE.stage(verified, target / "other")

    def test_completed_trx_with_a_failed_counter_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary) / "src/TestResults"
            folder.mkdir(parents=True)
            for name in ("ReactiveUI.Validation.Tests", "ReactiveUI.Validation.Reactive.Tests", "ReactiveUI.Validation.SourceGenerators.Tests"):
                failed = "1" if name == "ReactiveUI.Validation.Tests" else "0"
                (folder / f"{name}_net10.0_x64.trx").write_text(f'<TestRun><ResultSummary outcome="Completed"><Counters total="1" passed="0" failed="{failed}" error="0" aborted="0" timeout="0" notExecuted="0" /></ResultSummary></TestRun>')
            with self.assertRaisesRegex(ValueError, "non-passing"):
                RELEASE.verify_trx(Path(temporary))


# Dependencies the fixture packages declare; the mock lists them so restore would succeed.
DEPENDENCIES = {"reactiveui": ["26.0.1"], "reactiveui.reactive": ["26.0.1"],
                "runicartifex.dynamicdata": ["10.0.0"], "runicartifex.dynamicdata.reactive": ["10.0.0"]}
CREDENTIAL_HEADERS = ("authorization", "x-nuget-apikey")
MOCK_DOTNET = r"""
import os, sys, xml.etree.ElementTree as ET, zipfile
assert sys.argv[1:3] == ["nuget", "push"], sys.argv
package, options = sys.argv[3], dict(zip(sys.argv[4::2], sys.argv[5::2]))
assert set(options) == {"--source", "--api-key"} and options["--source"] == "https://api.nuget.org/v3/index.json" and options["--api-key"], options
case = os.environ["MOCK_NUGET_CASE"]
with zipfile.ZipFile(package) as archive:
    nuspec = next(name for name in archive.namelist() if "/" not in name and name.endswith(".nuspec"))
    metadata = {child.tag.rsplit("}", 1)[-1]: child.text for child in ET.fromstring(archive.read(nuspec))[0]}
    package_id, version = metadata["id"], metadata["version"]
    if package_id == os.environ.get("MOCK_DOTNET_FAIL"):
        sys.exit("Response status code does not indicate success: 403 (Forbidden).")
    directory = os.path.join(case, "packages", package_id.lower())
    os.makedirs(directory, exist_ok=True)
    # nuget.org repository-signs every package it accepts.
    with zipfile.ZipFile(os.path.join(directory, version.lower() + ".nupkg"), "w") as published:
        for item in archive.infolist():
            published.writestr(item, archive.read(item))
        published.writestr(".signature.p7s", b"repository signature")
os.makedirs(os.path.join(case, "owners"), exist_ok=True)
with open(os.path.join(case, "owners", package_id.lower()), "w") as owners:
    owners.write("runic-artifex\n")
with open(os.path.join(case, "pushes"), "a") as pushes:
    pushes.write(f"{package_id} {version}\n")
"""


class MockNuGetHandler(http.server.BaseHTTPRequestHandler):
    """Serves one case directory: packages/<id>/<version>.nupkg (flat container),
    owners/<id> (search lists the ID with these owners) and listed/<id> (versions
    the flat container lists without bytes, for dependencies)."""
    def log_message(self, *_):
        pass

    def send(self, status, body=b"", content_type="application/json"):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        case, url = self.server.case, urllib.parse.urlsplit(self.path)
        self.server.requests.append((self.path, [name for name in CREDENTIAL_HEADERS if self.headers.get(name)]))
        base = f"http://127.0.0.1:{self.server.server_port}"
        parts = url.path.strip("/").split("/")
        if parts == ["v3", "index.json"]:
            return self.send(200, json.dumps({"version": "3.0.0", "resources": [
                {"@id": f"{base}/query", "@type": "SearchQueryService/3.5.0"},
                {"@id": f"{base}/flat/", "@type": "PackageBaseAddress/3.0.0"}]}).encode())
        if parts == ["query"]:
            query = urllib.parse.parse_qs(url.query).get("q", [""])[0].removeprefix("packageid:")
            owners = case / "owners" / query.lower()
            data = [{"id": query, "owners": owners.read_text().split()}] if query and owners.is_file() else []
            return self.send(200, json.dumps({"totalHits": len(data), "data": data}).encode())
        if parts[0] == "flat" and len(parts) == 3 and parts[2] == "index.json":
            versions = sorted(path.stem for path in (case / "packages" / parts[1]).glob("*.nupkg"))
            listed = case / "listed" / parts[1]
            versions += listed.read_text().split() if listed.is_file() else []
            versions += DEPENDENCIES.get(parts[1], [])
            return self.send(200, json.dumps({"versions": versions}).encode()) if versions else self.send(404)
        if parts[0] == "flat" and len(parts) == 4 and parts[3] == f"{parts[1]}.{parts[2]}.nupkg":
            package = case / "packages" / parts[1] / f"{parts[2]}.nupkg"
            if package.is_file():
                return self.send(200, package.read_bytes(), "application/octet-stream")
        return self.send(404)


class NuGetPublishingTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.case = self.root / "nuget"
        self.case.mkdir()
        self.packages = self.root / "packages"
        self.packages.mkdir()
        self.pair = {package_id: package(self.packages, package_id, package_id.encode()) for package_id in RELEASE.PACKAGE_IDS}
        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), MockNuGetHandler)
        server.case, server.requests = self.case, []
        self.requests = server.requests
        threading.Thread(target=server.serve_forever, kwargs={"poll_interval": 0.05}, daemon=True).start()
        self.addCleanup(server.server_close)
        self.addCleanup(server.shutdown)
        dotnet = self.root / "tools" / "dotnet"
        dotnet.parent.mkdir()
        dotnet.write_text(f"#!{sys.executable}\n{MOCK_DOTNET}")
        dotnet.chmod(0o755)
        environment = {"GITHUB_REPOSITORY": "Runic-Artifex/ReactiveUI.Validation", "GITHUB_SHA": SOURCE,
                       "GITHUB_REF": "refs/heads/main", "DOTNET_BIN": str(dotnet), "MOCK_NUGET_CASE": str(self.case),
                       "NUGET_TEST_SERVICE_INDEX": f"http://127.0.0.1:{server.server_port}/v3/index.json"}
        patcher = mock.patch.dict(os.environ, environment)
        patcher.start()
        self.addCleanup(patcher.stop)
        for name in ("GITHUB_ACTIONS", "NUGET_API_KEY", "NUGET_POLICY", "RELEASE_ALLOW_LOCAL_PUBLISH", "MOCK_DOTNET_FAIL"):
            os.environ.pop(name, None)

    def publish_on_mock(self, package_id, contents=None, owners="runic-artifex"):
        """Place a version on the mock nuget.org, repository-signed as nuget.org would."""
        source = self.pair[package_id]
        directory = self.case / "packages" / package_id.lower()
        directory.mkdir(parents=True, exist_ok=True)
        with RELEASE.zipfile.ZipFile(source) as archive, RELEASE.zipfile.ZipFile(directory / "9.0.0-runic.1.nupkg", "w") as published:
            for item in archive.infolist():
                published.writestr(item, (contents or {}).get(item.filename, archive.read(item)))
            published.writestr(".signature.p7s", b"repository signature")
        if owners is not None:
            (self.case / "owners").mkdir(exist_ok=True)
            (self.case / "owners" / package_id.lower()).write_text(owners + "\n")

    def pushes(self):
        path = self.case / "pushes"
        return path.read_text().splitlines() if path.is_file() else []

    def push(self):
        with mock.patch.dict(os.environ, {"RELEASE_ALLOW_LOCAL_PUBLISH": "1", "NUGET_API_KEY": "short-lived-key"}):
            RELEASE.nuget_push(self.packages)

    def tearDown(self):
        leaked = [path for path, credentials in self.requests if credentials]
        self.assertEqual(leaked, [], "every nuget.org lookup must be keyless")

    def test_new_ids_need_a_push_and_dry_run_check_pushes_nothing(self):
        pending = RELEASE.nuget_plan(self.packages)
        self.assertEqual([item["id"] for item in pending], list(RELEASE.PACKAGE_IDS))
        self.assertEqual(self.pushes(), [])
        self.assertTrue(any(path.startswith("/query?") for path, _ in self.requests))

    def test_new_ids_are_refused_when_the_policy_may_not_push_new_packages(self):
        policy = self.root / "policy.json"
        policy.write_text(json.dumps({"owner": "runic-artifex", "packageGlobs": ["RunicArtifex.*"], "pushNewPackages": False}))
        with mock.patch.dict(os.environ, {"NUGET_POLICY": str(policy)}), self.assertRaisesRegex(ValueError, "new nuget.org package ID"):
            RELEASE.nuget_plan(self.packages)

    def test_identical_published_versions_are_skipped(self):
        for package_id in RELEASE.PACKAGE_IDS:
            self.publish_on_mock(package_id)
        self.assertEqual(RELEASE.nuget_plan(self.packages), [])
        self.push()
        self.assertEqual(self.pushes(), [])

    def test_a_different_published_version_fails(self):
        self.publish_on_mock(RELEASE.PACKAGE_IDS[1], {"lib/net10.0/ReactiveUI.Validation.Reactive.dll": b"other build"})
        with self.assertRaisesRegex(ValueError, r"already has RunicArtifex\.ReactiveUI\.Validation\.Reactive 9\.0\.0-runic\.1 with different contents \(lib/net10\.0/ReactiveUI\.Validation\.Reactive\.dll\)"):
            self.push()
        self.assertEqual(self.pushes(), [])

    def test_an_id_owned_by_another_account_fails(self):
        (self.case / "listed").mkdir()
        (self.case / "listed" / RELEASE.PACKAGE_IDS[0].lower()).write_text("1.0.0\n")
        (self.case / "owners").mkdir()
        (self.case / "owners" / RELEASE.PACKAGE_IDS[0].lower()).write_text("someone-else\n")
        with self.assertRaisesRegex(ValueError, "owned on nuget.org by someone-else, not by runic-artifex"):
            self.push()
        self.assertEqual(self.pushes(), [])

    def test_an_id_with_unknown_owners_fails(self):
        (self.case / "listed").mkdir()
        (self.case / "listed" / RELEASE.PACKAGE_IDS[1].lower()).write_text("1.0.0\n")
        with self.assertRaisesRegex(ValueError, "owners cannot be checked"):
            RELEASE.nuget_plan(self.packages)

    def test_an_id_outside_the_package_glob_fails(self):
        policy = self.root / "policy.json"
        policy.write_text(json.dumps({"owner": "runic-artifex", "packageGlobs": ["RunicArtifex.DynamicData*"], "pushNewPackages": True}))
        with mock.patch.dict(os.environ, {"NUGET_POLICY": str(policy)}):
            with self.assertRaisesRegex(ValueError, "RunicArtifex.ReactiveUI.Validation matches none of the trusted-publishing package globs"):
                self.push()
        self.assertEqual(self.pushes(), [])
        self.assertTrue(RELEASE.glob_matches("RunicArtifex.*", "runicartifex.reactiveui.validation.reactive"))
        self.assertFalse(RELEASE.glob_matches("RunicArtifex.*", "Runic.ReactiveUI.Validation"))

    def test_a_dependency_missing_from_nuget_org_fails(self):
        with mock.patch.dict(DEPENDENCIES, {"runicartifex.dynamicdata": ["10.0.0-runic.118"]}):
            with self.assertRaisesRegex(ValueError, "depends on RunicArtifex.DynamicData 10.0.0, which nuget.org does not list"):
                RELEASE.nuget_plan(self.packages)

    def test_a_partial_push_is_completed_by_a_retry(self):
        with mock.patch.dict(os.environ, {"MOCK_DOTNET_FAIL": RELEASE.PACKAGE_IDS[1]}):
            with self.assertRaisesRegex(RuntimeError, "dotnet nuget push failed for RunicArtifex.ReactiveUI.Validation.Reactive.9.0.0-runic.1.nupkg"):
                self.push()
        self.assertEqual(self.pushes(), [f"{RELEASE.PACKAGE_IDS[0]} 9.0.0-runic.1"])
        # The retry compares the signed nuget.org copy entry by entry and pushes only the rest.
        self.push()
        self.assertEqual(self.pushes(), [f"{package_id} 9.0.0-runic.1" for package_id in RELEASE.PACKAGE_IDS])
        self.assertEqual(RELEASE.nuget_plan(self.packages), [])

    def test_push_requires_actions_and_the_login_key(self):
        with self.assertRaisesRegex(ValueError, "only in GitHub Actions"):
            RELEASE.nuget_push(self.packages)
        with mock.patch.dict(os.environ, {"RELEASE_ALLOW_LOCAL_PUBLISH": "1"}), self.assertRaisesRegex(ValueError, "NUGET_API_KEY"):
            RELEASE.nuget_push(self.packages)
        self.assertEqual(self.pushes(), [])

    def test_test_hooks_are_refused_in_github_actions(self):
        with mock.patch.dict(os.environ, {"GITHUB_ACTIONS": "true"}), self.assertRaisesRegex(ValueError, "only for the offline tests"):
            RELEASE.nuget_plan(self.packages)

    def test_wait_indexed_polls_until_both_packages_are_listed(self):
        self.publish_on_mock(RELEASE.PACKAGE_IDS[0])
        waits = []
        def publish_reactive(seconds):
            waits.append(seconds)
            self.publish_on_mock(RELEASE.PACKAGE_IDS[1])
        with mock.patch.object(RELEASE.time, "sleep", side_effect=publish_reactive):
            RELEASE.wait_indexed("9.0.0-runic.1", 60, 5)
        self.assertEqual(waits, [5])
        clock = [0.0]
        def advance(seconds):
            clock[0] += seconds
        with self.assertRaisesRegex(ValueError, "still does not list RunicArtifex.ReactiveUI.Validation 9.0.0-runic.2"), \
             mock.patch.object(RELEASE.time, "monotonic", side_effect=lambda: clock[0]), \
             mock.patch.object(RELEASE.time, "sleep", side_effect=advance):
            RELEASE.wait_indexed("9.0.0-runic.2", 10, 5)
        self.assertEqual(clock[0], 10)
        with self.assertRaisesRegex(ValueError, "invalid package version"):
            RELEASE.wait_indexed("9.0.0-runic.2.gabc", 10, 5)


class WorkflowTests(unittest.TestCase):
    """The release workflow keeps the dry-run default and the job permission split."""
    def jobs(self):
        text = (ROOT / ".github/workflows/release.yml").read_text()
        head, _, body = text.partition("\njobs:\n")
        blocks = re.split(r"^  (?=[a-z][\w-]*:\n)", body, flags=re.M)
        # Comments may name another job's permissions; check only the YAML itself.
        strip = lambda block: "\n".join(line for line in block.splitlines() if not line.lstrip().startswith("#"))
        return head, {block.split(":", 1)[0]: strip(block) for block in blocks if block.strip() and not block.startswith((" ", "#"))}

    def test_dry_run_default_and_job_permissions(self):
        head, jobs = self.jobs()
        self.assertRegex(head, r"dry-run:\n(?:\s+\w.*\n)*?\s+default: true\n")
        self.assertRegex(head, r"\npermissions:\n  contents: read\n")
        self.assertEqual(list(jobs), ["packages", "github-release", "nuget", "indexed"])
        self.assertIn("if: ${{ !inputs.dry-run }}", jobs["github-release"])
        self.assertIn("needs: [packages, github-release]", jobs["nuget"])
        self.assertIn("needs: [packages, nuget]", jobs["indexed"])
        self.assertIn("environment: nuget", jobs["nuget"])
        self.assertIn("NuGet/login@8d196754b4036150537f80ac539e15c2f1028841", jobs["nuget"])
        self.assertIn("user: ${{ vars.NUGET_USER }}", jobs["nuget"])
        for name, block in jobs.items():
            self.assertEqual("id-token: write" in block, name == "nuget", name)
            self.assertEqual("contents: write" in block, name == "github-release", name)
            self.assertNotIn("secrets.", block, name)
        self.assertLess(jobs["nuget"].index("nuget-check"), jobs["nuget"].index("NuGet/login"))
        self.assertLess(jobs["packages"].index("release_assets.py resolve"), jobs["packages"].index("nuget-check"))


if __name__ == "__main__":
    unittest.main()
