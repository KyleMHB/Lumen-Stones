"""Mechanical evidence for an explicit layered candidate, without promoting it."""
import contextlib
import io
import json
from pathlib import Path
import tempfile

from PIL import Image, ImageDraw

import artwork


def alphaEvidence(image):
    alpha = image.getchannel("A")
    bounds = {str(cut): alpha.point(lambda value: 255 if value > cut else 0).getbbox()
              for cut in (0, 1, 8, 128)}
    raw, substantial = bounds["0"], bounds["8"]
    difference = max(abs(a-b) for a, b in zip(raw, substantial)) if raw and substantial else None
    histogram = image.getchannel("A").histogram()
    visible = sum(histogram[1:])
    opaque = histogram[255]
    opaqueSpread = 0
    weightedSpread = 0
    weight = 0
    for red, green, blue, alpha in image.get_flattened_data():
        spread = max(red, green, blue) - min(red, green, blue)
        if alpha == 255:
            opaqueSpread += spread
        weightedSpread += alpha * spread
        weight += alpha
    return {"alphaHistogram": histogram, "boundsAboveAlpha": bounds,
            "lowAlphaBoundsDifference": difference,
            "visiblePixels": visible, "opaquePixels": opaque,
            "opaqueFractionOfVisible": opaque / visible if visible else 0,
            "opaqueMeanSpread": opaqueSpread / opaque if opaque else None,
            "alphaWeightedMeanSpread": weightedSpread / weight if weight else None}


def checkCandidate(root, manifestPath, output):
    root, manifestPath, output = Path(root), Path(manifestPath).resolve(), Path(output).resolve()
    manifest = json.loads(manifestPath.read_text(encoding="utf-8"))
    checks = manifest["candidateChecks"]
    mode = checks.get("mode", "replacement")
    if mode not in ("replacement", "new-design"):
        raise ValueError("Candidate mode must be replacement or new-design")
    exporter = artwork.loadExporter(root)
    errors, warnings = [], []
    report = {"manifestSha256": artwork.digest(manifestPath), "limits": checks, "layers": [], "baseline": {},
              "exports": {}, "errors": errors, "warnings": warnings, "mode": mode,
              "unavailable": [],
              "scope": "mechanical candidate evidence; human review outstanding"}
    if manifest.get("paintedMask"):
        warnings.append("Painted mask overrides ownership polygons; polygon neutrality does not describe the painted coverage")

    def require(condition, message):
        if not condition:
            errors.append(message)

    baselinePaths = {}
    for role in (("color", "mask") if mode == "replacement" else ()):
        baseline = checks["baseline"][role]
        path = artwork.inside(root, baseline["path"])
        actual = artwork.digest(path)
        require(actual == baseline["sha256"], f"Baseline {role} hash changed")
        report["baseline"][role] = {"path": baseline["path"], "sha256": actual}
        baselinePaths[role] = path
    crop, target = manifest["crop"], manifest["targetBox"]
    exportScale = min((target[2]-target[0])/(crop[2]-crop[0]),
                      (target[3]-target[1])/(crop[3]-crop[1]))
    for entry in manifest["layers"]:
        path = manifestPath.parent / entry["path"]
        with Image.open(path) as source:
            image = source.convert("RGBA")
        evidence = {"path": entry["path"], "sha256": artwork.digest(path), "size": image.size,
                    "scale": entry.get("scale", 1), **alphaEvidence(image)}
        report["layers"].append(evidence)
        require(min(image.size) >= checks["minSourceSize"], f"Source too small: {entry['path']}")
        if not 0 < evidence["scale"] <= 1:
            raise ValueError(f"Invalid layer scale: {entry['path']}")
        density = 1 / (evidence["scale"] * exportScale)
        evidence["sourcePixelsPerOutputPixel"] = density
        meaningful = evidence["boundsAboveAlpha"]["8"]
        evidence["meaningfulSourceSize"] = ([meaningful[2]-meaningful[0], meaningful[3]-meaningful[1]]
                                            if meaningful else [0, 0])
        evidence["projectedVisibleSize"] = [size / density for size in evidence["meaningfulSourceSize"]]
        require(density >= checks["minSourcePixelsPerOutputPixel"],
                f"Insufficient source detail for output: {entry['path']}")
        if evidence["lowAlphaBoundsDifference"] is not None and evidence["lowAlphaBoundsDifference"] > checks.get("alphaDebrisTolerance", 8):
            warnings.append(f"Inspect faint alpha outside substantial bounds: {entry['path']}")
        if "ownership" in entry:
            label = exporter.ownershipMap(entry, image.size)
            selected = image.copy()
            selected.putalpha(Image.composite(image.getchannel("A"), Image.new("L", image.size), label))
            ownershipEvidence = alphaEvidence(selected)
            evidence["ownershipNeutrality"] = ownershipEvidence
            spread = ownershipEvidence["alphaWeightedMeanSpread"]
            require(spread is not None and spread <= checks["maxOpaqueSpread"],
                    f"Alpha-weighted tinted spread exceeds limit or has no sample: {entry['path']}")
        elif entry["constructionTint"]:
            spread = evidence["opaqueMeanSpread"]
            require(spread is not None and spread <= checks["maxOpaqueSpread"],
                    f"Opaque tinted spread exceeds limit or has no sample: {entry['path']}")

    scratch = root / "work"
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="lumen-candidate-", dir=scratch) as temp:
        first, second = Path(temp) / "first", Path(temp) / "second"
        with contextlib.redirect_stdout(io.StringIO()):
            exporter.export(manifestPath, first)
            exporter.export(manifestPath, second)
        for role, filename in (("color", manifest["colorFilename"]), ("mask", manifest["maskFilename"])):
            expected, repeated, candidate = first / filename, second / filename, output / filename
            identical = expected.read_bytes() == repeated.read_bytes()
            untouched = candidate.is_file() and candidate.read_bytes() == expected.read_bytes()
            report["exports"][role] = {"sha256": artwork.digest(expected),
                                       "candidateSha256": artwork.digest(candidate) if candidate.is_file() else None,
                                       "repeatIdentical": identical, "candidateUntouched": untouched}
            require(identical, f"Repeated {role} export differs")
            require(untouched, f"Candidate {role} is missing or differs from untouched exporter output")
        with Image.open(first / manifest["colorFilename"]) as color:
            candidateBounds = color.getchannel("A").getbbox()
            report["colorAlpha"] = alphaEvidence(color.convert("RGBA"))
        with Image.open(first / manifest["maskFilename"]) as mask:
            require(mask.mode == "RGBA", "Mask must be RGBA")
            report["maskChannels"] = {channel: mask.getchannel(channel).getextrema() for channel in "RGBA"}
        require(report["maskChannels"]["A"] == (255, 255), "Mask alpha is not uniformly 255")
        for channel in "GB":
            require(report["maskChannels"][channel] == (0, 0), f"Mask {channel} is not zero")

    if mode == "replacement":
        with Image.open(baselinePaths["color"]) as baseline:
            baselineBounds = baseline.getchannel("A").getbbox()
            baselineScale = checks.get("baselineScale", 1)
            require(baselineScale > 0 and tuple(manifest["canvas"]) ==
                    tuple(size * baselineScale for size in baseline.size),
                    "Candidate canvas differs from uniformly scaled baseline")
        report["baselineBounds"] = baselineBounds
        baselineBounds = tuple(value * baselineScale for value in baselineBounds) if baselineBounds else None
        expectedFoot = baselineBounds[3] if baselineBounds else None
    else:
        baselineBounds = tuple(checks["expectedBounds"])
        expectedFoot = checks["footLine"]
        report["unavailable"].append("Approved-baseline comparison: new design")
    if mode == "new-design":
        report["baselineBounds"] = None
    report["expectedBounds"], report["candidateBounds"] = baselineBounds, candidateBounds
    if baselineBounds and candidateBounds:
        deltas = [b-a for a, b in zip(baselineBounds, candidateBounds)]
        report["boundsDeltas"] = deltas
        require(all(abs(delta) <= checks["boundsTolerance"] for delta in deltas), "Visible bounds exceed tolerance")
        require(candidateBounds[3] == expectedFoot, "Foot line differs from declared anchor")
    else:
        require(False, "Baseline or candidate is empty")
    if mode == "replacement" or checks.get("definition"):
        defs, _ = artwork.definitions(root)
        require(checks["definition"] in defs, "Consuming definition is missing")
        draw = artwork.pair(artwork.value(defs, checks["definition"], "graphicData/drawSize", "(1,1)"))
        report["drawSize"] = draw
        require(draw == tuple(checks["drawSize"]), "Draw size changed")
    else:
        report["unavailable"].append("Runtime placement: no consuming definition")
    if checks.get("materialOwnership") == "unresolved":
        report["unavailable"].append("Material ownership: flattened concept; mask is not a material partition")
    difference = report["colorAlpha"]["lowAlphaBoundsDifference"]
    if difference is not None and difference > checks.get("alphaDebrisTolerance", 8):
        warnings.append("Inspect exported faint alpha outside substantial bounds")
    report["passed"] = not errors
    report["readyForTintReview"] = report["passed"] and checks.get("materialOwnership") != "unresolved"
    if all((output / manifest[key]).is_file() for key in ("colorFilename", "maskFilename")):
        writeTintViews(manifest, output, report, warnings)

    # Evidence lives beside existing exports; the candidate images are never written here.
    output.mkdir(parents=True, exist_ok=True)
    (output / "candidate-check.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


def writeTintViews(manifest, output, report, warnings):
    # Thresholds apply to exported pixels: RGB 0-255, coverage 0-255, alpha > 8.
    with Image.open(output / manifest["colorFilename"]) as source, Image.open(output / manifest["maskFilename"]) as maskSource:
        color, mask = source.convert("RGBA"), maskSource.convert("RGBA")
    brightMissing = coloredTinted = 0
    for (r, g, b, a), (coverage, _, _, _) in zip(color.get_flattened_data(), mask.get_flattened_data()):
        spread = max(r, g, b) - min(r, g, b)
        if a > 8 and min(r, g, b) >= 180 and spread <= 20 and coverage < 32:
            brightMissing += 1
        if a > 8 and spread >= 60 and coverage >= 80:
            coloredTinted += 1
    report["boundaryDiagnostics"] = {"brightNeutralWithoutTint": brightMissing,
        "strongColorWithTint": coloredTinted,
        "thresholds": "export pixels: alpha > 8; bright min RGB >= 180, spread <= 20, coverage < 32; colored spread >= 60, coverage >= 80"}
    if brightMissing:
        warnings.append(f"Inspect {brightMissing} bright neutral pixels without tint coverage")
    if coloredTinted:
        warnings.append(f"Inspect {coloredTinted} strongly colored pixels with tint coverage")
    crop, target = manifest["crop"], manifest["targetBox"]
    scale = min((target[2]-target[0])/(crop[2]-crop[0]), (target[3]-target[1])/(crop[3]-crop[1]))
    size = [round((crop[i+2]-crop[i])*scale) for i in range(2)]
    origin = [target[i] + (target[i+2]-target[i]-size[i])//2 for i in range(2)]
    regions = []
    for entry in manifest["layers"]:
        for name, points in entry.get("ownership", {}).items():
            dx, dy = entry.get("offset", [0, 0])
            regions.append((name, [[x+dx, y+dy] for x, y in points]))
    for name, bounds in manifest.get("reviewJoins", {}).items():
        if (not name or not name.replace("-", "").replace("_", "").isalnum()
            or not isinstance(bounds, list) or len(bounds) != 4
            or any(type(v) is not int for v in bounds)
            or not (0 <= bounds[0] < bounds[2] <= manifest["sourceSize"][0]
                    and 0 <= bounds[1] < bounds[3] <= manifest["sourceSize"][1])):
            raise ValueError(f"Invalid review join rectangle: {name}")
        regions.append((name, [[bounds[0], bounds[1]], [bounds[2], bounds[3]]]))
    if len(manifest["layers"]) > 1 and not regions:
        warnings.append("Layer stack has no named reviewJoins; declare material joins for zoomed inspection")
    report["joinViews"] = []
    output.mkdir(parents=True, exist_ok=True)
    for material, tint in (("neutral", (1, 1, 1)), ("brown", (0.55, 0.35, 0.2)), ("dark", (0.2, 0.2, 0.2))):
        view = artwork.tinted(color, mask, color.size, tint, 0)
        labelled = Image.new("RGBA", (view.width, view.height+24), (40, 40, 40, 255))
        labelled.paste(view, (0, 24))
        ImageDraw.Draw(labelled).text((4, 4), f"CPU model: {material}", fill="white")
        labelled.save(output / f"cpu-{material}.png")
        for name, points in regions:
            xs = [origin[0] + (point[0]-crop[0])*scale for point in points]
            ys = [origin[1] + (point[1]-crop[1])*scale for point in points]
            box = (max(0, int(min(xs))-12), max(0, int(min(ys))-12),
                   min(color.width, int(max(xs))+13), min(color.height, int(max(ys))+13))
            if box[2] <= box[0] or box[3] <= box[1]:
                continue
            zoom = view.crop(box).resize(((box[2]-box[0])*3, (box[3]-box[1])*3), Image.Resampling.NEAREST)
            labelled = Image.new("RGBA", (zoom.width, zoom.height+24), (40, 40, 40, 255))
            labelled.paste(zoom, (0, 24))
            ImageDraw.Draw(labelled).text((4, 4), f"CPU model: {name} / {material}", fill="white")
            filename = f"cpu-{material}-{name}-joins.png"
            labelled.save(output / filename)
            report["joinViews"].append({"name": name, "material": material, "outputBounds": box, "path": filename})


def prepareCandidate(root, manifestPath, output):
    root, manifestPath, output = Path(root).resolve(), Path(manifestPath).resolve(), Path(output).resolve()
    scratch = root / "work"
    if not output.is_relative_to(scratch) or output == scratch:
        raise ValueError("Candidate output must be a directory inside work/")
    with contextlib.redirect_stdout(io.StringIO()):
        artwork.loadExporter(root).export(manifestPath, output)
    return checkCandidate(root, manifestPath, output)
