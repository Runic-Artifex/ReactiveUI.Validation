#!/usr/bin/env python3
"""Promote an already accepted RunicArtifex.ReactiveUI.Validation package pair.

resolve, download, verify  bind the release to the accepted Build evidence.
stage                      copies the verified Ubuntu pair into a package folder.
check, publish             check (read-only) or create the GitHub prerelease.
nuget-check, nuget-push    check nuget.org with keyless lookups, then push what it lacks.
wait-indexed               waits until the nuget.org flat container lists the pair.

See eng/release/README.md.
"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
PACKAGE_IDS = ("RunicArtifex.ReactiveUI.Validation", "RunicArtifex.ReactiveUI.Validation.Reactive")
# Earlier releases were GitHub-release assets under these IDs; a new release never uses them.
LEGACY_PACKAGE_IDS = ("Runic.ReactiveUI.Validation", "Runic.ReactiveUI.Validation.Reactive")
BUILD_WORKFLOW = ".github/workflows/ci-build.yml"
EXPECTED_JOBS = {
    "build (ubuntu-latest)", "build (windows-latest)",
    "native-consumers / native (ubuntu-latest, linux-x64)",
    "native-consumers / native (windows-latest, win-x64)",
}
EXPECTED_ARTIFACTS = {
    "packages-ubuntu-latest", "packages-windows-latest",
    "validation-ubuntu-latest", "validation-windows-latest",
    "native-validation-linux-x64", "native-validation-win-x64",
    "generated-validation-linux-x64", "generated-validation-win-x64",
}
NS = {"n": "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}
VERSION_PATTERN = r"9\.0\.\d+-runic\.\d+"
AUTHORS = "ReactiveUI and Contributors, Runic Artifex"
FORK_NAME = "Runic Artifex fork of ReactiveUI.Validation"


def fail(message):
    raise ValueError(message)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def json_file(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def api(endpoint, *args):
    gh = os.environ.get("GH_BIN", "gh")
    result = subprocess.run([gh, "api", *args, endpoint], text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError(f"GitHub API request failed for {endpoint}: {result.stderr.strip()}")
    return result.stdout


def github_environment():
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    source = os.environ.get("GITHUB_SHA", "")
    if not re.fullmatch(r"[\w.-]+/[\w.-]+", repository):
        fail("GITHUB_REPOSITORY must identify the current repository")
    if not re.fullmatch(r"[0-9a-f]{40}", source):
        fail("GITHUB_SHA must be a full commit SHA")
    if os.environ.get("GITHUB_REF") != "refs/heads/main":
        fail("releases are restricted to maintained main")
    return repository, source


def require_clean_source(source):
    default = api(f"repos/{os.environ['GITHUB_REPOSITORY']}", "--jq", ".default_branch").strip()
    if default != "main":
        fail("main must remain the repository default branch")
    main_ref = json.loads(api(f"repos/{os.environ['GITHUB_REPOSITORY']}/git/ref/heads/main"))
    if main_ref.get("object", {}).get("type") != "commit" or main_ref["object"].get("sha") != source:
        fail("remote maintained main no longer resolves to GITHUB_SHA")
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    if head != source:
        fail("checked out source does not match GITHUB_SHA")
    for command, message in ((["git", "diff", "--quiet", "--ignore-submodules", "--"], "unstaged"),
                             (["git", "diff", "--cached", "--quiet", "--ignore-submodules", "--"], "staged")):
        if subprocess.run(command, cwd=ROOT).returncode:
            fail(f"checked out source has {message} changes")
    if subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard"], cwd=ROOT, text=True).strip():
        fail("checked out source has unexpected untracked files")


def paged(endpoint):
    try:
        pages = json.loads(api(endpoint, "--paginate", "--slurp"))
    except json.JSONDecodeError as error:
        raise ValueError(f"invalid paged GitHub response: {error}") from error
    if not isinstance(pages, list):
        fail("GitHub paged response has unexpected shape")
    return pages


def select_run(pages, repository, source):
    runs = []
    for page in pages:
        if not isinstance(page, dict) or not isinstance(page.get("workflow_runs"), list):
            fail("Build workflow response has unexpected page shape")
        for run in page["workflow_runs"]:
            if not isinstance(run, dict):
                continue
            head_repo = run.get("head_repository")
            valid_path = run.get("path") in (BUILD_WORKFLOW, f"{BUILD_WORKFLOW}@refs/heads/main")
            if (run.get("head_sha") == source and isinstance(head_repo, dict) and
                    head_repo.get("full_name") == repository and run.get("head_branch") == "main" and
                    run.get("event") in {"push", "workflow_dispatch"} and valid_path and isinstance(run.get("id"), int)):
                runs.append(run)
    if not runs:
        fail("no current-fork Build run exists for the release source")
    latest = max(runs, key=lambda run: (run.get("created_at") or "", run["id"]))
    if latest.get("status") != "completed" or latest.get("conclusion") != "success":
        fail("latest eligible Build run did not complete successfully")
    return latest


def verify_jobs(pages):
    latest = {}
    for page in pages:
        if not isinstance(page, dict) or not isinstance(page.get("jobs"), list):
            fail("Build jobs response has unexpected page shape")
        for job in page["jobs"]:
            if not isinstance(job, dict) or job.get("name") not in EXPECTED_JOBS:
                continue
            name = job["name"]
            order = (job.get("run_attempt") or 0, job.get("completed_at") or "", job.get("id") or 0)
            if name not in latest or order > latest[name][0]:
                latest[name] = (order, job)
    missing = EXPECTED_JOBS - latest.keys()
    if missing:
        fail(f"Build is missing required jobs: {sorted(missing)}")
    failed = [name for name, (_, job) in latest.items()
              if job.get("status") != "completed" or job.get("conclusion") != "success"]
    if failed:
        fail(f"latest required Build jobs did not succeed: {sorted(failed)}")
    return {name: {"id": job.get("id"), "run_attempt": job.get("run_attempt"),
                   "status": job.get("status"), "conclusion": job.get("conclusion")}
            for name, (_, job) in latest.items()}


def resolve(output):
    repository, source = github_environment()
    require_clean_source(source)
    run = select_run(paged(f"repos/{repository}/actions/workflows/ci-build.yml/runs?head_sha={source}&per_page=100"), repository, source)
    jobs = verify_jobs(paged(f"repos/{repository}/actions/runs/{run['id']}/jobs?filter=all&per_page=100"))
    artifact_pages = paged(f"repos/{repository}/actions/runs/{run['id']}/artifacts?per_page=100")
    artifacts = {}
    for page in artifact_pages:
        if not isinstance(page, dict) or not isinstance(page.get("artifacts"), list):
            fail("Build artifact response has unexpected page shape")
        for artifact in page["artifacts"]:
            if isinstance(artifact, dict) and artifact.get("name") in EXPECTED_ARTIFACTS:
                artifacts.setdefault(artifact["name"], []).append(artifact)
    if set(artifacts) != EXPECTED_ARTIFACTS or any(len(items) != 1 or items[0].get("expired") for items in artifacts.values()):
        fail("Build artifacts are incomplete, duplicate, or expired")
    output.parent.mkdir(parents=True, exist_ok=True)
    if any(len(items) != 1 or items[0].get("expired") or not isinstance(items[0].get("id"), int) or
           not isinstance(items[0].get("size_in_bytes"), int) or items[0]["size_in_bytes"] <= 0 or
           not re.fullmatch(r"sha256:[0-9a-f]{64}", items[0].get("digest", ""))
           for items in artifacts.values()):
        fail("Build artifact metadata is incomplete or does not carry a SHA-256 digest")
    output.write_text(json.dumps({"repository": repository, "source": source, "run": run["id"],
                                  "jobs": jobs, "artifacts": {name: {"id": items[0]["id"], "digest": items[0]["digest"],
                                                       "size": items[0]["size_in_bytes"],
                                                       "archive_download_url": items[0].get("archive_download_url")}
                                                for name, items in artifacts.items()}}, indent=2) + "\n")


def extract_artifact(data, expected_digest, destination):
    if hashlib.sha256(data).hexdigest() != expected_digest.removeprefix("sha256:"):
        fail("downloaded Build artifact does not match its GitHub SHA-256 digest")
    try:
        archive = zipfile.ZipFile(io.BytesIO(data))
    except zipfile.BadZipFile as error:
        fail(f"downloaded Build artifact is not a ZIP archive: {error}")
    for member in archive.infolist():
        target = destination / member.filename
        if target.resolve().is_relative_to(destination.resolve()):
            archive.extract(member, destination)
        else:
            fail("Build artifact ZIP contains an unsafe path")


def download(source_report, output):
    repository, source = github_environment()
    report = json_file(source_report)
    artifacts = report.get("artifacts")
    if report.get("repository") != repository or report.get("source") != source or not isinstance(report.get("run"), int) or not isinstance(artifacts, dict) or set(artifacts) != EXPECTED_ARTIFACTS:
        fail("resolved Build artifact report is malformed or not for this release source")
    output.mkdir(parents=True, exist_ok=True)
    gh = os.environ.get("GH_BIN", "gh")
    downloaded = {}
    for name in sorted(EXPECTED_ARTIFACTS):
        artifact = artifacts[name]
        expected_url = f"https://api.github.com/repos/{repository}/actions/artifacts/{artifact.get('id')}/zip" if isinstance(artifact, dict) else ""
        if (not isinstance(artifact, dict) or not isinstance(artifact.get("id"), int) or
                not re.fullmatch(r"sha256:[0-9a-f]{64}", artifact.get("digest", "")) or
                artifact.get("archive_download_url") != expected_url):
            fail("resolved Build artifact digest is malformed")
        result = subprocess.run([gh, "api", artifact["archive_download_url"]], capture_output=True)
        if result.returncode:
            raise RuntimeError(f"GitHub artifact download failed for {name}: {result.stderr.decode(errors='replace').strip()}")
        destination = output / name
        destination.mkdir()
        extract_artifact(result.stdout, artifact["digest"], destination)
        downloaded[name] = {"id": artifact["id"], "digest": artifact["digest"], "size": len(result.stdout)}
    (output / "download-metadata.json").write_text(json.dumps({"source": source, "artifacts": downloaded}, indent=2) + "\n")


def package_pair(folder):
    found = sorted(Path(folder).glob("*.nupkg"))
    expected = {PACKAGE_IDS[0]: None, PACKAGE_IDS[1]: None}
    for path in found:
        for package_id in expected:
            if path.name.startswith(f"{package_id}.") and (package_id.endswith(".Reactive") or not path.name.startswith(f"{package_id}.Reactive.")):
                if expected[package_id] is not None:
                    fail(f"duplicate package {package_id}")
                expected[package_id] = path
    if len(found) != 2 or any(path is None for path in expected.values()):
        fail("expected exactly the branded Validation package pair")
    return expected


def verify_manifest(folder, pair, source):
    manifest = json_file(Path(folder) / "package-manifest.json")
    entries = manifest.get("packages")
    if manifest.get("source") != source or not isinstance(entries, list) or len(entries) != 2:
        fail(f"invalid package manifest in {folder}")
    expected = {path.name: (sha256(path), path.stat().st_size) for path in pair.values()}
    actual = {}
    for entry in entries:
        if not isinstance(entry, dict) or not isinstance(entry.get("filename"), str) or not re.fullmatch(r"[0-9a-f]{64}", entry.get("sha256", "")) or not isinstance(entry.get("size"), int) or entry["size"] <= 0:
            fail(f"invalid package manifest entry in {folder}")
        if entry["filename"] in actual:
            fail(f"package manifest repeats an entry in {folder}")
        actual[entry["filename"]] = (entry["sha256"], entry["size"])
    if actual != expected:
        fail(f"package manifest does not describe the exact package pair in {folder}")


def verify_attribution(name, metadata, text, reactive):
    """Keep upstream authorship and copyright, name the fork and its flavour, leave owners to nuget.org."""
    if text("authors") != AUTHORS:
        fail(f'{name} authors must be "{AUTHORS}"')
    if metadata.find("n:owners", NS) is not None:
        fail(f"{name} must not declare <owners>; nuget.org takes owners from the account")
    if not re.fullmatch(r"Copyright © 2019-.*\bReactiveUI and Contributors", text("copyright")):
        fail(f"{name} must keep the ReactiveUI and Contributors copyright")
    flavor = "System.Reactive" if reactive else "ReactiveUI.Primitives"
    for field in ("title", "description"):
        value = text(field)
        if not value.lower().startswith((FORK_NAME.lower(), f"the {FORK_NAME.lower()}")) or flavor not in value:
            fail(f"{name} <{field}> must start by naming the {FORK_NAME} and name {flavor}")


def verify_package(path, package_id, source):
    if package_id not in PACKAGE_IDS:
        fail(f"{package_id} is not a branded release package ID")
    reactive = package_id.endswith(".Reactive")
    assembly = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
    try:
        archive = zipfile.ZipFile(path)
    except zipfile.BadZipFile as error:
        fail(f"invalid package {path.name}: {error}")
    required = {f"{package_id}.nuspec", "README.md", "logo.png", "LICENSE/LICENSE",
                "analyzers/dotnet/roslyn5.9/cs/ReactiveUI.Validation.SourceGenerators.dll",
                f"buildTransitive/{package_id}.props", f"lib/net10.0/{assembly}.dll", f"lib/net10.0/{assembly}.xml"}
    names = archive.namelist()
    if any(names.count(entry) != 1 for entry in required) or not required <= set(names):
        fail(f"{path.name} is missing or repeats required package content")
    if archive.read("LICENSE/LICENSE") != (ROOT / "LICENSE").read_bytes():
        fail(f"{path.name} does not contain the repository MIT license")
    frameworks = {entry.split("/")[1] for entry in names if entry.startswith("lib/") and entry.endswith(".dll")}
    if frameworks != {"net10.0"}:
        fail(f"{path.name} does not contain only net10.0 assemblies")
    root = ET.fromstring(archive.read(f"{package_id}.nuspec"))
    metadata = root.find("n:metadata", NS)
    if metadata is None:
        fail(f"{path.name} has no nuspec metadata")
    text = lambda name: (metadata.findtext(f"n:{name}", namespaces=NS) or "").strip()
    version = text("version")
    if text("id") != package_id or not re.fullmatch(VERSION_PATTERN, version):
        fail(f"{path.name} does not have the expected branded version identity")
    verify_attribution(path.name, metadata, text, reactive)
    license_ = metadata.find("n:license", NS)
    repository = metadata.find("n:repository", NS)
    if (license_ is None or license_.get("type") != "expression" or license_.text != "MIT" or
            repository is None or repository.get("type") != "git" or
            repository.get("url") != "https://github.com/Runic-Artifex/ReactiveUI.Validation" or repository.get("commit") != source):
        fail(f"{path.name} has incorrect license or source provenance")
    groups = metadata.findall("n:dependencies/n:group", NS)
    if len(groups) != 1 or groups[0].get("targetFramework") != "net10.0":
        fail(f"{path.name} has incorrect dependency target")
    if metadata.findall("n:dependencies/n:dependency", NS):
        fail(f"{path.name} declares dependencies outside its net10.0 group")
    dependencies = {}
    for item in groups[0].findall("n:dependency", NS):
        name = item.get("id")
        folded = name.casefold() if isinstance(name, str) else None
        if not folded or folded in dependencies:
            fail(f"{path.name} has duplicate or malformed dependencies")
        dependencies[folded] = item.get("version")
    expected = (("ReactiveUI.Reactive", "26.0.1"), ("RunicArtifex.DynamicData.Reactive", "10.0.0-runic.118")) if reactive else (("ReactiveUI", "26.0.1"), ("RunicArtifex.DynamicData", "10.0.0-runic.118"))
    for name, version_pin in expected:
        if dependencies.get(name.casefold()) not in {version_pin, f"[{version_pin}]"}:
            fail(f"{path.name} does not pin {name} at {version_pin}")
    opposite_suffix = "" if reactive else ".Reactive"
    # The legacy Runic.* IDs are GitHub-release-only and never part of a new release.
    forbidden = {"DynamicData", "DynamicData.Reactive", "ReactiveUI.Validation", "ReactiveUI.Validation.Reactive",
                 "Runic.DynamicData", "Runic.DynamicData.Reactive", *LEGACY_PACKAGE_IDS,
                 f"RunicArtifex.DynamicData{opposite_suffix}", f"ReactiveUI{opposite_suffix}",
                 f"RunicArtifex.ReactiveUI.Validation{opposite_suffix}"}
    if not reactive:
        forbidden.add("System.Reactive")
    if set(dependencies) & {name.casefold() for name in forbidden}:
        fail(f"{path.name} declares an upstream or opposite-flavor dependency")
    return version, sha256(path)


def one_result(root, artifact, rid, report_path="results.json"):
    path = Path(root) / artifact / report_path
    if not path.is_file():
        fail(f"expected {report_path} in {artifact}")
    report = json_file(path)
    if ((rid is not None and report.get("rid") != rid) or report.get("source") != os.environ["GITHUB_SHA"] or
            report.get("dirty") is not False or report.get("sdk") != json_file(ROOT / "global.json")["sdk"]["version"] or
            report.get("completed") is not True):
        fail(f"{artifact} results are incomplete or not for the release source")
    return report


def gate_passed(report, mode, flavors, stages):
    """Accept a completed generated gate that ran every flavor stage and passed all of its own checks.

    The gate enforces its required case, negative and configuration set itself and only marks
    the report completed when they all pass, so the release guard does not duplicate that list.
    """
    checks = report.get("checks", [])
    required = {f"{flavor}-{stage}" for flavor in flavors for stage in stages}
    return (report.get("mode") == mode and required <= {item.get("name") for item in checks}
            and all(item.get("passed") is True for item in checks))


def require_package(report, key, package, label, require_repository_commit=False):
    actual = report.get("packages", {}).get(key, {})
    if any(actual.get(field) != package[field] for field in ("id", "version", "sha256")):
        fail(f"{label} does not prove the selected package bytes")
    if require_repository_commit and actual.get("repositoryCommit") != os.environ["GITHUB_SHA"]:
        fail(f"{label} does not prove the package repository commit")


def verify_trx(artifact):
    expected = {"ReactiveUI.Validation.Tests", "ReactiveUI.Validation.Reactive.Tests", "ReactiveUI.Validation.SourceGenerators.Tests"}
    reports = list((Path(artifact) / "src" / "TestResults").glob("*.trx"))
    names = {path.name.split("_net10.0", 1)[0] for path in reports}
    if names != expected or len(reports) != len(expected):
        fail(f"core test evidence is incomplete in {artifact}")
    for report in reports:
        root = ET.parse(report).getroot()
        summary = next((node for node in root.iter() if node.tag.endswith("ResultSummary")), None)
        counters = next((node for node in root.iter() if node.tag.endswith("Counters")), None)
        if summary is None or summary.get("outcome") not in {"Passed", "Completed"} or counters is None:
            fail(f"core test report did not pass: {report.name}")
        total = int(counters.get("total", "0"))
        if any(int(counters.get(name, "0")) != 0 for name in ("failed", "error", "aborted", "timeout", "notExecuted")) or total <= 0 or int(counters.get("passed", "0")) != total:
            fail(f"core test report has non-passing tests: {report.name}")


def verify(root, source_report, output):
    source = json_file(source_report)["source"]
    if source != os.environ.get("GITHUB_SHA"):
        fail("resolved Build source differs from GITHUB_SHA")
    hosts = {"ubuntu-latest": package_pair(Path(root) / "packages-ubuntu-latest"),
             "windows-latest": package_pair(Path(root) / "packages-windows-latest")}
    packages = {}
    for host, pair in hosts.items():
        verify_manifest(Path(root) / f"packages-{host}", pair, source)
        packages[host] = {}
        for package_id in PACKAGE_IDS:
            version, digest = verify_package(pair[package_id], package_id, source)
            packages[host]["Reactive" if package_id.endswith(".Reactive") else "Primitives"] = {"id": package_id, "version": version, "sha256": digest, "path": str(pair[package_id])}
        if packages[host]["Primitives"]["version"] != packages[host]["Reactive"]["version"]:
            fail(f"Validation package flavors have different versions on {host}")
    if packages["ubuntu-latest"]["Primitives"]["version"] != packages["windows-latest"]["Primitives"]["version"]:
        fail("core hosts produced different Validation versions")
    selected = packages["ubuntu-latest"]
    native_expected = {"candidate-Primitives-native", "candidate-Reactive-native", "baseline-Primitives-baseline", "baseline-Reactive-baseline"}
    for rid in ("linux-x64", "win-x64"):
        generated = one_result(root, f"generated-validation-{rid}", rid)
        native = one_result(root, f"native-validation-{rid}", rid)
        if not gate_passed(generated, "all", selected, ("managed", "trimmed", "native")):
            fail(f"generated validation report is incomplete for {rid}")
        if native.get("mode") != "native" or len(native.get("checks", [])) != len(native_expected) or {item.get("name") for item in native.get("checks", [])} != native_expected or not all(item.get("passed") is True for item in native["checks"]):
            fail(f"native validation report is incomplete for {rid}")
        for flavor, package in selected.items():
            require_package(generated, flavor, package, f"generated {rid} {flavor}")
            require_package(native, f"candidate-{flavor}", package, f"native {rid} {flavor}")
    for host in packages:
        artifact = f"validation-{host}"
        verify_trx(Path(root) / artifact)
        rid = "linux-x64" if host == "ubuntu-latest" else "win-x64"
        ordinary = one_result(root, artifact, None, "artifacts/verification/package-smoke/results.json")
        ordinary_expected = {f"{flavor}-managed" for flavor in packages[host]}
        if (len(ordinary.get("checks", [])) != len(ordinary_expected) or
                {item.get("name") for item in ordinary["checks"]} != ordinary_expected or
                not all(item.get("passed") is True for item in ordinary["checks"])):
            fail(f"ordinary package report is incomplete for {host}")
        for flavor, package in packages[host].items():
            require_package(ordinary, flavor, package, f"ordinary package {host} {flavor}", True)
        managed = one_result(root, artifact, rid, "artifacts/verification/generated-gates/results.json")
        if not gate_passed(managed, "managed", packages[host], ("managed",)):
            fail(f"generated managed report is incomplete for {host}")
        for flavor, package in packages[host].items():
            require_package(managed, flavor, package, f"generated managed {host} {flavor}")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({"source": source, "version": selected["Primitives"]["version"], "packages": selected}, indent=2) + "\n")


def selected_pair(folder):
    """Validate the package folder of this release: exactly the branded pair, one version."""
    repository, source = github_environment()
    pair = package_pair(folder)
    packages = {}
    for package_id in PACKAGE_IDS:
        version, digest = verify_package(pair[package_id], package_id, source)
        packages["Reactive" if package_id.endswith(".Reactive") else "Primitives"] = {
            "id": package_id, "version": version, "sha256": digest, "path": str(pair[package_id])}
    if packages["Primitives"]["version"] != packages["Reactive"]["version"]:
        fail("Validation package flavors have different versions")
    return repository, source, packages["Primitives"]["version"], packages


def stage(verified, output):
    """Copy the verified Ubuntu pair, and only it, into the release package folder."""
    _, source = github_environment()
    data = json_file(verified)
    if data.get("source") != source:
        fail("verified evidence is not for the current release source")
    if output.exists() and any(output.iterdir()):
        fail(f"{output} must be empty before staging the pair")
    output.mkdir(parents=True, exist_ok=True)
    for package in data["packages"].values():
        path = Path(package["path"])
        if sha256(path) != package["sha256"]:
            fail("selected local package bytes changed after verification")
        shutil.copyfile(path, output / path.name)
    selected_pair(output)


def absent(endpoint, label):
    gh = os.environ.get("GH_BIN", "gh")
    result = subprocess.run([gh, "api", "--silent", "--include", endpoint], text=True, capture_output=True)
    if result.returncode == 0:
        fail(f"{label} already exists and is immutable")
    if not re.search(r"^HTTP/[0-9.]+ 404(?: |$)", result.stdout + result.stderr, re.M):
        fail(f"could not determine whether {label} exists: {result.stderr.strip()}")


def require_new_release(repository, tag):
    absent(f"repos/{repository}/git/ref/tags/{tag}", f"tag {tag}")
    releases = paged(f"repos/{repository}/releases?per_page=100")
    if any(release.get("tag_name") == tag for page in releases for release in page):
        fail(f"release {tag} already exists and is immutable")


def require_draft(draft, tag, source):
    if (not isinstance(draft.get("id"), int) or draft.get("tag_name") != tag or draft.get("draft") is not True or
            draft.get("prerelease") is not True or draft.get("target_commitish") != source):
        fail("GitHub did not create the expected draft")


def check(folder):
    """Read-only: everything publish requires before it creates the draft."""
    repository, source, version, _ = selected_pair(folder)
    require_clean_source(source)
    require_new_release(repository, f"v{version}")
    print(f"release-assets: GitHub prerelease v{version} can be created.")


def publish(folder):
    repository, source, version, packages = selected_pair(folder)
    require_clean_source(source)
    tag = f"v{version}"
    require_new_release(repository, tag)
    draft = json.loads(api(f"repos/{repository}/releases", "--method", "POST", "-f", f"tag_name={tag}", "-f", f"target_commitish={source}", "-f", f"name=RunicArtifex.ReactiveUI.Validation {version}", "-F", "draft=true", "-F", "prerelease=true", "-F", "generate_release_notes=true"))
    require_draft(draft, tag, source)
    release_id = draft["id"]
    subprocess.run([os.environ.get("GH_BIN", "gh"), "release", "upload", tag, "--repo", repository,
                    packages["Primitives"]["path"], packages["Reactive"]["path"]], check=True)
    release = json.loads(api(f"repos/{repository}/releases/{release_id}"))
    try:
        require_draft(release, tag, source)
    except ValueError as error:
        fail(f"draft release metadata changed before publication: {error}")
    assets = {asset.get("name"): asset for asset in release.get("assets", [])}
    expected_names = {Path(package["path"]).name for package in packages.values()}
    if set(assets) != expected_names:
        fail("draft release does not contain exactly the selected package pair")
    for package in packages.values():
        if sha256(Path(package["path"])) != package["sha256"]:
            fail("selected local package bytes changed after verification")
        asset = assets.get(Path(package["path"]).name)
        if not isinstance(asset, dict) or asset.get("state") != "uploaded" or asset.get("size") != Path(package["path"]).stat().st_size:
            fail("draft assets are not the exact selected pair")
        if asset.get("digest") is not None and asset["digest"] != f"sha256:{package['sha256']}":
            fail("draft asset digest differs from selected package bytes")
        content = subprocess.check_output([os.environ.get("GH_BIN", "gh"), "api", "-H", "Accept: application/octet-stream", f"repos/{repository}/releases/assets/{asset['id']}"])
        if hashlib.sha256(content).hexdigest() != package["sha256"]:
            fail("uploaded draft bytes differ from selected package bytes")
    api(f"repos/{repository}/git/refs", "--method", "POST", "-f", f"ref=refs/tags/{tag}", "-f", f"sha={source}")
    reference = json.loads(api(f"repos/{repository}/git/ref/tags/{tag}"))
    if reference.get("object", {}).get("type") != "commit" or reference["object"].get("sha") != source:
        fail("created tag does not resolve directly to the release source")
    api(f"repos/{repository}/releases/{release_id}", "--method", "PATCH", "-F", "draft=false")


# nuget.org publishing. Every lookup below is keyless and read-only; only
# nuget_push sends the short-lived key from NuGet/login, to dotnet nuget push.
NUGET_SERVICE_INDEX = "https://api.nuget.org/v3/index.json"
NUGET_POLICY = Path(__file__).with_name("nuget-policy.json")
# nuget.org adds its repository signature to every package it accepts.
NUGET_SIGNATURE = ".signature.p7s"
NUGET_ATTEMPTS = 6


def test_override(name, default):
    """Offline-test hooks; refused in GitHub Actions so a release always uses the real values."""
    value = os.environ.get(name)
    if not value:
        return default
    if os.environ.get("GITHUB_ACTIONS") == "true":
        fail(f"{name} is only for the offline tests")
    return value


def nuget_fetch(url):
    """GET url; returns the body, or None for 404. Retries 429, 5xx and network errors."""
    last = "no response"
    for attempt in range(NUGET_ATTEMPTS):
        delay = min(30, 2 ** attempt)
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "runic-validation-release"})
            with urllib.request.urlopen(request, timeout=60) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            error.close()
            if error.code == 404:
                return None
            if error.code != 429 and error.code < 500:
                raise RuntimeError(f"nuget.org lookup failed: HTTP {error.code} {url}") from error
            retry_after = error.headers.get("Retry-After", "")
            if retry_after.isdigit():
                delay = int(retry_after)
            last = f"HTTP {error.code}"
        except (urllib.error.URLError, TimeoutError) as error:
            last = str(getattr(error, "reason", error))
        if attempt < NUGET_ATTEMPTS - 1:
            if delay > 120:
                break
            time.sleep(delay)
    raise RuntimeError(f"nuget.org lookup failed after retries ({last}): {url}")


def nuget_json(url):
    body = nuget_fetch(url)
    return None if body is None else json.loads(body)


class NuGet:
    def __init__(self):
        index = nuget_json(test_override("NUGET_TEST_SERVICE_INDEX", NUGET_SERVICE_INDEX))
        if not isinstance(index, dict):
            fail("the nuget.org service index is unavailable")

        def resource(prefix):
            for item in index.get("resources", []):
                if str(item.get("@type", "")).startswith(prefix) and isinstance(item.get("@id"), str):
                    return item["@id"]
            fail(f"the nuget.org service index has no {prefix}")

        self.search = resource("SearchQueryService")
        self.flat = resource("PackageBaseAddress/3.0.0").rstrip("/") + "/"

    def versions(self, package_id):
        """Versions the flat container (dotnet restore's source) lists, or None for an unknown ID."""
        listing = nuget_json(f"{self.flat}{package_id.lower()}/index.json")
        return None if listing is None else [str(version).lower() for version in listing.get("versions", [])]

    def download(self, package_id, version):
        lower_id, lower_version = package_id.lower(), version.lower()
        return nuget_fetch(f"{self.flat}{lower_id}/{lower_version}/{lower_id}.{lower_version}.nupkg")

    def owners(self, package_id):
        """('new', None), ('exists', [owners]) or ('exists', None) when search lists no version."""
        query = urllib.parse.urlencode({"q": f"packageid:{package_id}", "prerelease": "true",
                                        "semVerLevel": "2.0.0", "take": "1"})
        for entry in (nuget_json(f"{self.search}?{query}") or {}).get("data", []):
            if str(entry.get("id", "")).lower() == package_id.lower():
                owners = entry.get("owners") or []
                return "exists", [owners] if isinstance(owners, str) else [str(owner) for owner in owners]
        # Search omits an ID whose versions are all unlisted, or one pushed moments ago.
        return ("new", None) if self.versions(package_id) is None else ("exists", None)


def glob_matches(pattern, package_id):
    """nuget.org package globs: '*' matches any run of characters, case-insensitively."""
    expression = ".*".join(re.escape(part) for part in pattern.split("*"))
    return re.fullmatch(expression, package_id, re.IGNORECASE) is not None


def archive_entries(source, label):
    """Every archive entry except the nuget.org repository signature, by name."""
    try:
        with zipfile.ZipFile(io.BytesIO(source) if isinstance(source, bytes) else source) as archive:
            names = [item.filename for item in archive.infolist()]
            if len(names) != len(set(names)):
                fail(f"{label} repeats an archive entry")
            return {name: archive.read(name) for name in names if name != NUGET_SIGNATURE}
    except zipfile.BadZipFile as error:
        fail(f"{label} is not a valid package: {error}")


def declared_dependencies(path, package_id):
    with zipfile.ZipFile(path) as archive:
        metadata = ET.fromstring(archive.read(f"{package_id}.nuspec")).find("n:metadata", NS)
    for item in metadata.findall("n:dependencies/n:group/n:dependency", NS):
        # A plain version is NuGet's minimum; [x] and [x, y) ranges start at x.
        yield item.get("id"), (item.get("version") or "").strip("[( ").split(",")[0].strip()


def nuget_plan(folder):
    """Keyless checks of the pair against nuget.org; returns the packages still to push.

    Refuses, before anything is pushed, every condition the trusted-publishing key
    would fail on or that would break consumers: an ID outside the policy globs, a
    changed published version, an ID runic-artifex does not own, and a dependency
    nuget.org cannot restore.
    """
    _, _, version, packages = selected_pair(folder)
    policy = json.loads(Path(test_override("NUGET_POLICY", str(NUGET_POLICY))).read_text(encoding="utf-8"))
    owner, globs = policy["owner"], policy["packageGlobs"]
    nuget = NuGet()
    problems, pending = [], []
    for package in packages.values():
        package_id, path = package["id"], Path(package["path"])
        if not any(glob_matches(glob, package_id) for glob in globs):
            problems.append(f"{package_id} matches none of the trusted-publishing package globs ({', '.join(globs)}). "
                            f"Add a matching glob to the policy of {owner} on nuget.org and to eng/release/nuget-policy.json.")
        for dependency, minimum in declared_dependencies(path, package_id):
            if minimum.lower() not in (nuget.versions(dependency) or []):
                problems.append(f"{package_id} depends on {dependency} {minimum}, which nuget.org does not list; "
                                "publish that dependency to nuget.org first.")
        published = nuget.download(package_id, version)
        if published is None:
            pending.append(package)
            continue
        local, remote = archive_entries(path, path.name), archive_entries(published, f"nuget.org {package_id} {version}")
        if local != remote:
            changed = sorted(name for name in set(local) | set(remote) if local.get(name) != remote.get(name))
            problems.append(f"nuget.org already has {package_id} {version} with different contents ({', '.join(changed[:5])}). "
                            "Published versions are immutable; release a new version.")
        else:
            print(f"{package_id} {version} is already on nuget.org with identical contents; skipping it.")
    for package in pending:
        package_id = package["id"]
        state, owners = nuget.owners(package_id)
        if state == "new":
            if policy.get("pushNewPackages"):
                print(f"::notice::{package_id} is a new nuget.org package ID; the trusted-publishing policy of {owner} must cover it.")
            else:
                problems.append(f"{package_id} is a new nuget.org package ID, but the policy may push only existing packages. "
                                f"Allow the policy of {owner} to push new packages.")
        elif owners is None:
            problems.append(f"nuget.org knows {package_id}, but its search lists no version, so its owners cannot be checked. "
                            "If a version was pushed moments ago, wait until nuget.org search lists it and rerun; "
                            f"otherwise relist a version and make sure {owner} owns the ID.")
        elif not any(name.lower() == owner.lower() for name in owners):
            problems.append(f"{package_id} is owned on nuget.org by {', '.join(owners) or 'nobody listed'}, not by {owner}, "
                            f"so the trusted-publishing key cannot push it. Ask the owners to add {owner}, or choose another ID.")
    if problems:
        fail("\n".join(["nuget.org cannot accept this release as built; nothing was pushed.",
                         *(f"- {problem}" for problem in problems), 'See eng/release/README.md, "nuget.org publishing".']))
    names = ", ".join(f"{package['id']} {version}" for package in pending)
    print(f"{len(pending)} of {len(packages)} packages need a push to nuget.org: {names or 'none'}.")
    return pending


def nuget_push(folder):
    if os.environ.get("GITHUB_ACTIONS") != "true" and os.environ.get("RELEASE_ALLOW_LOCAL_PUBLISH") != "1":
        fail("nuget-push may run only in GitHub Actions")
    key = os.environ.get("NUGET_API_KEY")
    if not key:
        fail("NUGET_API_KEY from NuGet/login is required to push")
    dotnet = os.environ.get("DOTNET_BIN", "dotnet")
    for package in nuget_plan(folder):
        name = Path(package["path"]).name
        # The command line carries the key, so report only the package on failure.
        result = subprocess.run([dotnet, "nuget", "push", package["path"], "--source", NUGET_SERVICE_INDEX, "--api-key", key])
        if result.returncode:
            raise RuntimeError(f"dotnet nuget push failed for {name}; see eng/release/README.md, \"Retries\", before rerunning.")
        print(f"Pushed {name} to nuget.org.")


def wait_indexed(version, timeout_seconds, interval_seconds):
    if not re.fullmatch(VERSION_PATTERN, version):
        fail(f"invalid package version: {version}")
    nuget = NuGet()
    missing = list(PACKAGE_IDS)
    deadline = time.monotonic() + timeout_seconds
    while True:
        missing = [package_id for package_id in missing if version.lower() not in (nuget.versions(package_id) or [])]
        if not missing:
            print(f"nuget.org lists {', '.join(PACKAGE_IDS)} {version}.")
            return
        names = ", ".join(f"{package_id} {version}" for package_id in missing)
        if time.monotonic() + interval_seconds > deadline:
            fail(f"nuget.org still does not list {names} after {timeout_seconds:g} seconds; rerun this job later.")
        print(f"Waiting for nuget.org to list {names}", flush=True)
        time.sleep(interval_seconds)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    resolve_parser = commands.add_parser("resolve")
    resolve_parser.add_argument("--output", type=Path, required=True)
    download_parser = commands.add_parser("download")
    download_parser.add_argument("--source-report", type=Path, required=True)
    download_parser.add_argument("--output", type=Path, required=True)
    verify_parser = commands.add_parser("verify")
    verify_parser.add_argument("--input", type=Path, required=True)
    verify_parser.add_argument("--source-report", type=Path, required=True)
    verify_parser.add_argument("--output", type=Path, required=True)
    stage_parser = commands.add_parser("stage")
    stage_parser.add_argument("--verified", type=Path, required=True)
    stage_parser.add_argument("--output", type=Path, required=True)
    for name in ("check", "publish", "nuget-check", "nuget-push"):
        commands.add_parser(name).add_argument("--packages", type=Path, required=True)
    wait_parser = commands.add_parser("wait-indexed")
    wait_parser.add_argument("version")
    wait_parser.add_argument("--timeout-seconds", type=float, default=float(os.environ.get("NUGET_WAIT_TIMEOUT_SECONDS", 3600)))
    wait_parser.add_argument("--interval-seconds", type=float, default=float(os.environ.get("NUGET_WAIT_INTERVAL_SECONDS", 30)))
    args = parser.parse_args()
    if args.command == "resolve": resolve(args.output)
    elif args.command == "download": download(args.source_report, args.output)
    elif args.command == "verify": verify(args.input, args.source_report, args.output)
    elif args.command == "stage": stage(args.verified, args.output)
    elif args.command == "check": check(args.packages)
    elif args.command == "publish": publish(args.packages)
    elif args.command == "nuget-check": nuget_plan(args.packages)
    elif args.command == "nuget-push": nuget_push(args.packages)
    else: wait_indexed(args.version, args.timeout_seconds, args.interval_seconds)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"release-assets: {error}", file=sys.stderr)
        raise SystemExit(1)
