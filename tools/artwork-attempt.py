"""Record the exact request before generation, then retain and inspect its output."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil

from PIL import Image

from artwork import digest
from artwork_candidate import alphaEvidence


def begin(ledger, layer, request, methodChange=None):
    ledger = Path(ledger)
    ledger.mkdir(parents=True, exist_ok=True)
    paths = sorted(ledger.glob("attempt-*.json"))
    previous = [json.loads(path.read_text(encoding="utf-8")) for path in paths]
    same = [record for record in previous if record["layer"] == layer]
    if same and same[-1]["status"] == "pending":
        raise ValueError("Capture and inspect the pending output before another call")
    if (len(same) >= 2 and same[-1].get("defect")
            and same[-1].get("defect") == same[-2].get("defect")
            and not same[-1].get("methodChange") and not methodChange):
        raise ValueError("Two attempts repeat the same defect: change the method or stop")
    if not request.get("prompt"):
        raise ValueError("Record the exact prompt before generation")
    references = [{"path": path, "sha256": digest(Path(path))}
                  for path in request.get("referenced_image_paths", [])]
    if request.get("num_last_images_to_include") and not references:
        raise ValueError("Use saved reference paths so the attempt can record their hashes")
    path = ledger / f"attempt-{len(paths)+1:04}.json"
    record = {"layer": layer, "status": "pending", "tool": "image_gen.imagegen", "request": request,
              "references": references, "methodChange": methodChange,
              "createdUtc": datetime.now(timezone.utc).isoformat()}
    with path.open("x", encoding="utf-8") as file:
        file.write(json.dumps(record, indent=2) + "\n")
    return path


def finish(path, imagePath, finding, defect=None):
    path, imagePath = Path(path), Path(imagePath)
    record = json.loads(path.read_text(encoding="utf-8"))
    if record["status"] != "pending":
        raise ValueError("This attempt already has an output")
    with Image.open(imagePath) as source:
        evidence = {"mode": source.mode, "size": source.size,
                    "hasAlphaChannel": "A" in source.getbands(),
                    **alphaEvidence(source.convert("RGBA"))}
    retained = path.with_suffix(".png")
    if imagePath.resolve() != retained.resolve():
        shutil.copy2(imagePath, retained)
    record.update(status="inspected", output={"path": retained.name, "sha256": digest(retained)},
                  evidence=evidence, finding=finding, defect=defect)
    path.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    return retained


def fail(path, reason):
    path = Path(path)
    record = json.loads(path.read_text(encoding="utf-8"))
    if record["status"] != "pending":
        raise ValueError("This attempt is already closed")
    record.update(status="failed", finding=reason, defect="generation-failed")
    path.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    start = commands.add_parser("begin")
    start.add_argument("--ledger", type=Path, required=True)
    start.add_argument("--layer", required=True)
    start.add_argument("--request", type=Path, required=True)
    start.add_argument("--method-change")
    end = commands.add_parser("finish")
    end.add_argument("attempt", type=Path)
    end.add_argument("--image", type=Path, required=True)
    end.add_argument("--finding", required=True)
    end.add_argument("--defect")
    failed = commands.add_parser("fail", help="Record a tool failure that produced no recoverable image")
    failed.add_argument("attempt", type=Path)
    failed.add_argument("--reason", required=True)
    args = parser.parse_args()
    try:
        if args.command == "begin":
            print(begin(args.ledger, args.layer, json.loads(args.request.read_text(encoding="utf-8")),
                        args.method_change))
        elif args.command == "finish":
            print(finish(args.attempt, args.image, args.finding, args.defect))
        else:
            print(fail(args.attempt, args.reason))
        return 0
    except (ValueError, OSError) as exc:
        print("FAIL:", exc)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
