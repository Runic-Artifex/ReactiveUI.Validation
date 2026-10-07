#!/usr/bin/env python3
"""Promote an already accepted Runic ReactiveUI.Validation package pair."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
PACKAGE_IDS = ("Runic.ReactiveUI.Validation", "Runic.ReactiveUI.Validation.Reactive")
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


def verify_package(path, package_id, source):
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
    text = lambda name: metadata.findtext(f"n:{name}", namespaces=NS)
    version = text("version")
    if text("id") != package_id or text("authors") != "ReactiveUI and Contributors" or version is None or not re.fullmatch(r"8\.1\.\d+-runic\.\d+(?:\.\d+)*", version):
        fail(f"{path.name} does not have the expected branded MinVer identity")
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
    expected = (("ReactiveUI.Reactive", "26.0.1"), ("Runic.DynamicData.Reactive", "10.0.0-runic.30")) if reactive else (("ReactiveUI", "26.0.1"), ("Runic.DynamicData", "10.0.0-runic.30"))
    for name, version_pin in expected:
        if dependencies.get(name.casefold()) not in {version_pin, f"[{version_pin}]"}:
            fail(f"{path.name} does not pin {name} at {version_pin}")
    opposite_suffix = "" if reactive else ".Reactive"
    forbidden = {"DynamicData", "DynamicData.Reactive", "ReactiveUI.Validation", "ReactiveUI.Validation.Reactive",
                 f"Runic.DynamicData{opposite_suffix}", f"ReactiveUI{opposite_suffix}",
                 f"Runic.ReactiveUI.Validation{opposite_suffix}"}
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


def absent(endpoint, label):
    gh = os.environ.get("GH_BIN", "gh")
    result = subprocess.run([gh, "api", "--silent", "--include", endpoint], text=True, capture_output=True)
    if result.returncode == 0:
        fail(f"{label} already exists and is immutable")
    if not re.search(r"^HTTP/[0-9.]+ 404(?: |$)", result.stdout + result.stderr, re.M):
        fail(f"could not determine whether {label} exists: {result.stderr.strip()}")


def require_draft(draft, tag, source):
    if (not isinstance(draft.get("id"), int) or draft.get("tag_name") != tag or draft.get("draft") is not True or
            draft.get("prerelease") is not True or draft.get("target_commitish") != source):
        fail("GitHub did not create the expected draft")


def publish(verified):
    repository, source = github_environment()
    require_clean_source(source)
    data = json_file(verified)
    if data.get("source") != source:
        fail("verified evidence is not for the current release source")
    version = data["version"]
    tag = f"runic-v{version}"
    absent(f"repos/{repository}/git/ref/tags/{tag}", f"tag {tag}")
    releases = paged(f"repos/{repository}/releases?per_page=100")
    if any(release.get("tag_name") == tag for page in releases for release in page):
        fail(f"release {tag} already exists and is immutable")
    draft = json.loads(api(f"repos/{repository}/releases", "--method", "POST", "-f", f"tag_name={tag}", "-f", f"target_commitish={source}", "-f", f"name=Runic ReactiveUI.Validation {version}", "-F", "draft=true", "-F", "prerelease=true", "-F", "generate_release_notes=true"))
    require_draft(draft, tag, source)
    release_id = draft["id"]
    subprocess.run([os.environ.get("GH_BIN", "gh"), "release", "upload", tag, "--repo", repository,
                    data["packages"]["Primitives"]["path"], data["packages"]["Reactive"]["path"]], check=True)
    release = json.loads(api(f"repos/{repository}/releases/{release_id}"))
    try:
        require_draft(release, tag, source)
    except ValueError as error:
        fail(f"draft release metadata changed before publication: {error}")
    assets = {asset.get("name"): asset for asset in release.get("assets", [])}
    expected_names = {Path(package["path"]).name for package in data["packages"].values()}
    if set(assets) != expected_names:
        fail("draft release does not contain exactly the selected package pair")
    for package in data["packages"].values():
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


def main():
    parser = argparse.ArgumentParser()
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
    publish_parser = commands.add_parser("publish")
    publish_parser.add_argument("--verified", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "resolve": resolve(args.output)
    elif args.command == "download": download(args.source_report, args.output)
    elif args.command == "verify": verify(args.input, args.source_report, args.output)
    else: publish(args.verified)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"release-assets: {error}", file=sys.stderr)
        raise SystemExit(1)
