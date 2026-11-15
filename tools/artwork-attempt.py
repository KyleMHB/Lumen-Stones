"""Record the exact request before generation, then retain and inspect its output."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil

from PIL import Image

from artwork import digest
from artwork_candidate import alphaEvidence


def renderingPrompt(request, manifest):
    from artwork_outline import RANGES
    checks = manifest["candidateChecks"]
    kind = checks.get("outlineKind", "building")
    low, high, aim = RANGES[kind]
    canvas, draw = manifest["canvas"], checks["drawSize"]
    widths = [aim * pixels / (64 * cells) for pixels, cells in zip(canvas, draw)]
    ppc = canvas[1] / draw[1]
    thinBelow, thinWidth = 8 * ppc / 64, 1.5 * ppc / 64   # outline rule 9, on the final canvas
    crop, target = manifest["crop"], manifest["targetBox"]
    scale = min((target[i+2]-target[i])/(crop[i+2]-crop[i]) for i in range(2))
    visibleHeight = (crop[3]-crop[1]) * scale
    marker = "\n\nRendering contract (from recipe): "
    clause = (f"Final canvas {canvas[0]}x{canvas[1]}, draw size {draw[0]}x{draw[1]} cells. "
              f"Pure black exterior contour (all RGB channels <=25), continuous and inside the silhouette. "
              f"At 64 pixels per cell its width must be {low}-{high}px, aim {aim}px; "
              f"on the final canvas aim {widths[1]:.2f}px at top/bottom and {widths[0]:.2f}px at sides. "
              f"Top/bottom border is about {100*widths[1]/visibleHeight:.2f}% of the framed object height; "
              "retain that proportion at any generated resolution. Local width variation <=1px at game scale. "
              f"Thin parts (crystal shards, posts, spikes) narrower than {thinBelow:.0f}px on the final canvas use a "
              f"lighter contour of about {thinWidth:.2f}px so the colour stays readable; never let the contour fill a shard. "
              "Black material joins 1-2px at game scale; crystal facet edges use value changes. "
              "Broad crystal planes, simple neutral-grey housing, no glow or ground shadow.")
    return {**request, "prompt": request["prompt"].split(marker)[0] + marker + clause}


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
    rendering = start.add_mutually_exclusive_group(required=True)
    rendering.add_argument("--rendering-recipe", type=Path,
                           help="Append the recipe's rendering requirements to the saved exact request before generation")
    rendering.add_argument("--non-fixture", action="store_true", help="Capture a task outside fixture rendering")
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
            request = json.loads(args.request.read_text(encoding="utf-8"))
            if args.rendering_recipe:
                request = renderingPrompt(request, json.loads(args.rendering_recipe.read_text(encoding="utf-8")))
            attempt = begin(args.ledger, args.layer, request, args.method_change)
            args.request.write_text(json.dumps(request, indent=2) + "\n", encoding="utf-8")
            print(attempt)
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
