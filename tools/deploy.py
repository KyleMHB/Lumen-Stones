"""Stage, apply and verify explicit mod updates with baseline checks and backups."""
import argparse
import copy
from datetime import datetime, timezone
import difflib
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

from artwork import ROOT, digest, inside

RUNTIME_FOLDERS = {"About", "Defs", "Textures", "Assemblies", "Integrations", "Patches", "Extras"}
RUNTIME_FILES = {"LoadFolders.xml", "LICENSE", "README.md", "CHANGELOG.md", "CREDITS.md"}


def file_hash(path):
    return digest(path) if path.is_file() else None


def runtime_path(root, relative):
    relative_path = Path(relative)
    if relative_path.parts[0] not in RUNTIME_FOLDERS and relative not in RUNTIME_FILES:
        raise ValueError(f"Not a declared mod runtime/document path: {relative}")
    return inside(root, relative)


def package_id(root):
    return ET.parse(root / "About/About.xml").findtext("packageId")


def validate_destination(source, destination):
    source, destination = source.resolve(), destination.resolve()
    if source.is_relative_to(destination) or destination.is_relative_to(source):
        raise ValueError("Source and installation must be separate directories")
    if package_id(source) != package_id(destination):
        raise ValueError("Installed packageId does not match the source mod")


def patch_xml(source_bytes, installed_bytes, selectors):
    def parse(data):
        return ET.fromstring(data, parser=ET.XMLParser(target=ET.TreeBuilder(insert_comments=True)))
    source, installed = parse(source_bytes), parse(installed_bytes)
    for selector in selectors:
        desired, targets = source.findall(selector), installed.findall(selector)
        if len(desired) != 1 or len(targets) != 1:
            raise ValueError(f"XML selector must match exactly once in source and installed file: {selector}")
        target = targets[0]
        parent = next(node for node in installed.iter() if target in list(node))
        replacement = copy.deepcopy(desired[0])
        replacement.tail = target.tail
        index = list(parent).index(target)
        parent.remove(target)
        parent.insert(index, replacement)
    ET.indent(installed, space="  ")
    return ET.tostring(installed, encoding="utf-8", xml_declaration=True) + b"\n"


def stage(source, destination, plan, output):
    source, destination, output = source.resolve(), destination.resolve(), output.resolve()
    validate_destination(source, destination)
    if not output.is_relative_to(source / "work"):
        raise ValueError("Deployment candidates and backups belong under source/work")
    if output.exists():
        raise ValueError(f"Use a fresh staging directory: {output}")
    entries = plan["files"]
    if not entries or len({entry["path"] for entry in entries}) != len(entries):
        raise ValueError("A plan needs a nonempty, unique file list")
    files, differences = [], []
    for entry in entries:
        relative = entry["path"]
        source_path, installed_path = runtime_path(source, relative), runtime_path(destination, relative)
        source_bytes = source_path.read_bytes()
        installed_bytes = installed_path.read_bytes() if installed_path.is_file() else None
        selectors = entry.get("xmlSelectors")
        if selectors is not None:
            if not selectors or installed_bytes is None:
                raise ValueError(f"XML field updates need existing targets: {relative}")
            candidate = patch_xml(source_bytes, installed_bytes, selectors)
        else:
            # A copy entry explicitly replaces the whole file; review.diff shows the full change.
            candidate = source_bytes
        candidate_path = inside(output / "candidate", relative)
        candidate_path.parent.mkdir(parents=True, exist_ok=True)
        candidate_path.write_bytes(candidate)
        files.append({"path": relative, "mode": "xml-fields" if selectors else "copy",
                      "xmlSelectors": selectors, "sourceHash": digest(source_path),
                      "baselineHash": file_hash(installed_path), "candidateHash": digest(candidate_path)})
        if relative.endswith((".xml", ".md")):
            before = (installed_bytes or b"").decode("utf-8-sig").splitlines(keepends=True)
            after = candidate.decode("utf-8-sig").splitlines(keepends=True)
            differences.extend(difflib.unified_diff(before, after,
                                                   fromfile="installed/" + relative,
                                                   tofile="candidate/" + relative))
        else:
            differences.append(f"{relative}: {file_hash(installed_path)} -> {digest(candidate_path)}\n")
    manifest = {"schema": 1, "status": "staged", "createdUtc": datetime.now(timezone.utc).isoformat(),
                "sourceRoot": str(source), "destination": str(destination),
                "packageId": package_id(source), "files": files}
    (output / "review.diff").write_text("".join(differences), encoding="utf-8")
    manifest_path = output / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return manifest_path


def deploy(manifest_path):
    manifest_path = manifest_path.resolve()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest["status"] != "staged":
        raise ValueError("This manifest is not a fresh staged deployment")
    source, destination = Path(manifest["sourceRoot"]), Path(manifest["destination"])
    validate_destination(source, destination)
    if not manifest_path.is_relative_to(source / "work"):
        raise ValueError("Manifest is not inside the recorded workspace's work directory")
    # Verify every input before making any installed change.
    for entry in manifest["files"]:
        relative = entry["path"]
        if digest(runtime_path(source, relative)) != entry["sourceHash"]:
            raise ValueError(f"Source changed after staging: {relative}")
        if file_hash(runtime_path(destination, relative)) != entry["baselineHash"]:
            raise ValueError(f"Installed file changed after staging: {relative}")
        if digest(inside(manifest_path.parent / "candidate", relative)) != entry["candidateHash"]:
            raise ValueError(f"Candidate changed after review: {relative}")
    for entry in manifest["files"]:
        if entry["baselineHash"] is not None:
            backup = inside(manifest_path.parent / "before", entry["path"])
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(runtime_path(destination, entry["path"]), backup)
    touched = []
    try:
        for entry in manifest["files"]:
            target = runtime_path(destination, entry["path"])
            target.parent.mkdir(parents=True, exist_ok=True)
            touched.append(entry)
            shutil.copy2(inside(manifest_path.parent / "candidate", entry["path"]), target)
            if digest(target) != entry["candidateHash"]:
                raise ValueError(f"Installed verification failed: {entry['path']}")
    except Exception:
        for entry in reversed(touched):
            target = runtime_path(destination, entry["path"])
            if entry["baselineHash"] is None:
                target.unlink(missing_ok=True)
            else:
                shutil.copy2(inside(manifest_path.parent / "before", entry["path"]), target)
        raise
    manifest["status"] = "deployed"
    manifest["deployedUtc"] = datetime.now(timezone.utc).isoformat()
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return len(manifest["files"])


def verify(manifest_path):
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest["status"] != "deployed":
        raise ValueError("Manifest has no completed deployment")
    destination = Path(manifest["destination"])
    if package_id(destination) != manifest["packageId"]:
        raise ValueError("Installed packageId changed")
    for entry in manifest["files"]:
        if file_hash(runtime_path(destination, entry["path"])) != entry["candidateHash"]:
            raise ValueError(f"Installed file differs from reviewed candidate: {entry['path']}")
    return len(manifest["files"])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    staging = commands.add_parser("stage")
    staging.add_argument("--plan", required=True, type=Path)
    staging.add_argument("--destination", required=True, type=Path)
    staging.add_argument("--output", required=True, type=Path)
    for name in ("deploy", "verify"):
        commands.add_parser(name).add_argument("manifest", type=Path)
    args = parser.parse_args()
    try:
        if args.command == "stage":
            plan = json.loads(args.plan.read_text(encoding="utf-8"))
            print("Staged:", stage(ROOT, args.destination, plan, args.output))
            print("Review the file list and review.diff before deploying.")
        else:
            manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
            if Path(manifest["sourceRoot"]).resolve() != ROOT:
                raise ValueError("Use the deployment tool from the manifest's source workspace")
            count = deploy(args.manifest) if args.command == "deploy" else verify(args.manifest)
            print(f"PASS: {count} installed files verified against reviewed candidates")
        return 0
    except (ValueError, OSError, KeyError, ET.ParseError) as exc:
        print("FAIL:", exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
