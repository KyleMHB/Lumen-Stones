"""Run the outline layer over every deployed sprite (docs/rimworld-art-guide.md#outline-layer).

Run on approved art only when the user names the pass. Each registered object in artwork/objects.json with
a recipe is layered at its role's aim, from the consuming ThingDef's draw size, into --output/<piece>/ (the
same files `outline-layer` writes). Deployed building and item textures without a usable recipe (stacks,
implants, a paintedColor recipe) are layered from the PNG itself (`outline-layer --png`). The batch then
writes game-scale before/after sheets, a flagged-only sheet and REPORT.md: the width table, the flags, the
skipped pieces and the textures left out. Nothing is promoted to Textures/.

A piece whose visible line is already within 0.25px of its aim, or wider, is left as drawn. One more than
--max-add under its aim is not stroked: that much added line fattens the silhouette and closes the valleys
between parts, which is a redo of the art, not a width fix. The report lists both apart.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import re
import subprocess
import sys

from PIL import Image, ImageDraw

import artwork
from artwork_outline import RANGES, VANILLA_PPC

ROOT = artwork.ROOT
AIMS = {role: aim for role, (_, _, aim) in RANGES.items()}
# Pieces a pass leaves alone, with the reason the report gives.
SKIP = {
    "KM_LumenCircle": "floor decal drawn with a white line; a black layer makes it heavy and busy",
    "KM_GlaciteBlade": "user chose the greatsword's generated outline without the layer (614e4266)",
    "KM_VerditeChew":"drawn brown bowl rim; black outside it is a double line, so it keeps its art until a redo",
}
SKIP_COLUMNS = "column set approved with its 2.0px outline layer (020e2b8a)"
OFF_AIM = 0.15        # game px from the aim before the report flags it
UNEVEN = 1.0          # evenness above this is flagged, as the outline rule's evenness limit
MAX_ADD = 0.75        # game px of line the layer may add; a piece further under its aim needs a redo instead
NOTCH = 30.0          # game px² of extra line filling valleys and notches before the report flags it
BACKGROUND = (138, 122, 94, 255)


def role(texture, obj=None):
    """The registry's `outlineRole` when set, otherwise the role the texture folder implies."""
    if obj and obj.get("outlineRole"):
        return obj["outlineRole"]
    if "/Weapon" in texture:
        return "weapon"
    if texture.startswith("Things/Item/Resource/"):
        return "resource"
    return "item" if texture.startswith("Things/Item/") else "building"


def folder(texture):
    """The Textures/ folder a texPath's files sit in: the path itself for a folder graphic, else its parent."""
    base = ROOT / "Textures" / texture
    return str((base if base.is_dir() else base.parent).relative_to(ROOT)).replace(os.sep, "/")


def pixels(path):
    with Image.open(path) as image:
        image = image.convert("RGBA")
        return image.size, image.tobytes()


def deployPlan(done, output):
    """Map each layered export onto the Textures/ files its baseline matches, pixel for pixel: the named file and
    any alias in the same folder (SlabBed.png beside SlabBed_south.png). Writes a tools/deploy.py plan whose
    entries come `from` the export, so a test deploy never touches Textures/. Masks for objects that ship no
    mask are removed from the export, since the game never loads them; anything else unmatched is returned."""
    entries, unmatched, claimed = {}, [], {}
    for job, _report, _found in done:
        textures = ROOT / job["textures"]
        for export in sorted((Path(job["out"]) / "export").glob("*.png")):
            base = pixels(Path(job["out"]) / "baseline" / export.name)
            named = textures / export.name
            matches = [named] if named.exists() and pixels(named) == base else []
            matches += [f for f in textures.glob("*.png") if f.name != export.name and pixels(f) == base]
            if not matches:
                if re.search(r"(_m|_(north|east|south|west)m)$", export.stem) and not named.exists():
                    export.unlink()   # no shipped mask: nothing to deploy it over
                else:
                    unmatched.append(str(export.relative_to(ROOT)))
                continue
            for target in matches:
                path = str(target.relative_to(ROOT)).replace(os.sep, "/")
                # The named file wins over an alias claimed by another piece (Cluster.png is north's, not east's).
                if path in claimed and (claimed[path] or target != named):
                    continue
                claimed[path] = target == named
                entries[path] = {"path": path, "from": str(export.relative_to(ROOT)).replace(os.sep, "/")}
    plan = {"files": sorted(entries.values(), key=lambda e: e["path"])}
    (output / "deploy-plan.json").write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8")
    return len(plan["files"]), unmatched


def drawSize(defs, name):
    draw = artwork.value(defs, name, "graphicData/drawSize") if name else None
    draw = artwork.pair(draw) if draw else None
    return draw * 2 if draw and len(draw) == 1 else draw


def family(objectId):
    """The first word of the def name (KM_GlaciteWallLamp -> Glacite), used to group sheets."""
    words = re.findall(r"[A-Z][a-z]+", objectId.split("_", 1)[-1])
    return words[0] if words else "Misc"


def jobs(objects, defs, output):
    """One job per distinct recipe and draw size; skipped pieces with their reasons."""
    found, skipped = {}, []
    for obj in objects:
        recipes = obj.get("recipes") or {}
        if not recipes:
            continue
        if obj["id"].endswith("Column") or obj["id"] in SKIP:
            skipped.append((obj["id"], SKIP.get(obj["id"], SKIP_COLUMNS)))
            continue
        draw = drawSize(defs, obj["id"])
        part = role(obj["texture"], obj)
        for view, recipe in recipes.items():
            manifest = json.loads((ROOT / recipe).read_text(encoding="utf-8"))
            if manifest.get("paintedColor"):
                break   # its colour is a painted PNG, not the recipe's source: layered from the PNG instead
            size = draw or tuple(manifest.get("candidateChecks", {}).get("drawSize") or (1, 1))
            job = found.setdefault((recipe, size), dict(
                id=obj["id"], recipe=recipe, draw=list(size), role=part, aim=AIMS[part], textures=folder(obj["texture"]),
                family=family(obj["id"]) if part in ("building", "wall-lamp") else part + "s", views=[]))
            if view != "single":
                job["views"].append(view)
    for job in found.values():
        job["piece"] = job["id"].split("_", 1)[-1] + ("-" + "+".join(job["views"]) if job["views"] else "")
        job["out"] = str(output / job["piece"])
    return list(found.values()), skipped


def unrecorded(objects, defs):
    """Deployed building and item textures no usable recipe covers: (texture, def name, reason)."""
    registered = {o["texture"]: o for o in objects}
    rows = {}
    for name, node in defs.items():
        if node.get("Abstract") == "True" or node.findtext("defName") != name:
            continue
        texture = artwork.value(defs, name, "graphicData/texPath")
        if not texture or not texture.startswith(("Things/Building", "Things/Item", "Lumenstone/")):
            continue
        obj = registered.get(texture)
        if obj is None:
            rows.setdefault(texture, (name, "not registered in artwork/objects.json"))
        elif not obj.get("recipes"):
            rows.setdefault(texture, (name, "registered but has no recipe"))
        elif any(json.loads((ROOT / r).read_text(encoding="utf-8")).get("paintedColor") for r in obj["recipes"].values()):
            rows.setdefault(texture, (name, "recipe overrides colour with paintedColor"))
    return sorted((texture, name, why) for texture, (name, why) in rows.items())


def pngJobs(defs, objects, output):
    """Jobs that layer deployed PNGs directly (`outline-layer --png`) for textures without a usable recipe,
    plus the textures left out, with the reason."""
    found, left = [], []
    for texture, name, why in unrecorded(objects, defs):
        if texture.endswith("Column") or name in SKIP:
            left.append((texture, SKIP.get(name, SKIP_COLUMNS)))
            continue
        base = ROOT / "Textures" / texture
        cls = artwork.value(defs, name, "graphicData/graphicClass", "Graphic_Single")
        # Mineable rock declares Graphic_Single but links through its atlas, so the path names it too.
        if "Linked" in cls or "/Linked/" in texture or texture.endswith("_Atlas"):
            left.append((texture, "linked atlas: its tiles join edge to edge, so an outline would draw seams"))
            continue
        if base.is_dir():
            files = sorted(f for f in base.glob("*.png") if not f.stem.endswith("_m"))
        elif cls == "Graphic_Multi":
            files = [base.with_name(f"{base.name}_{v}.png") for v in ("north", "east", "south", "west")]
        else:
            files = [base.with_name(base.name + ".png")]
        files = [f for f in files if f.exists()]
        if not files:
            left.append((texture, why + "; no deployed PNG found under Textures/"))
            continue
        part = role(texture)
        for f in files:
            piece = name.split("_", 1)[-1] + ("" if f.stem == base.name else "-" + f.stem.rsplit("_", 1)[-1])
            found.append(dict(id=name, png=str(f), draw=list(drawSize(defs, name) or (1, 1)), role=part,
                              textures=str(f.parent.relative_to(ROOT)).replace(os.sep, "/"),
                              aim=AIMS[part], family=family(name) if part in ("building", "wall-lamp") else part + "s",
                              piece=piece, out=str(output / piece), recipe=str(output / piece / "png" / "master.json")))
    return found, left


def layer(job, python, resume):
    out = Path(job["out"])
    if resume and (out / "outline-layer.json").exists():
        return job["piece"], "skip"
    source = ["--png", job["png"]] if "png" in job else [job["recipe"]]
    command = [python, str(ROOT / "tools/artwork.py"), "outline-layer", *source, "--target", str(job["aim"]),
               "--draw-size", ",".join(f"{n:g}" for n in job["draw"]), "--output", str(out),
               "--kind", job["role"]]
    if job.get("maxAdd") is not None:
        command += ["--max-add", str(job["maxAdd"])]
    result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
    out.mkdir(parents=True, exist_ok=True)
    (out / "run.log").write_text(" ".join(command) + "\n\n" + result.stdout + result.stderr, encoding="utf-8")
    if result.returncode:
        return job["piece"], "FAIL " + ((result.stdout + result.stderr).strip().splitlines() or ["?"])[-1]
    return job["piece"], "ok"


def flags(job, report):
    found = [f"canvas cut {', '.join(f'{k} {v}' for k, v in report['cutByCanvas'].items())}"] if report["cutByCanvas"] else []
    found += [note for note in report["notes"] if not note.startswith(("trimmed", "rebuilt", "left the edge open", "padded"))]
    low, _, high = report["after"]["bandPx"]
    if report["after"]["evennessPx"] > UNEVEN or high - low > UNEVEN:
        found.append(f"uneven: the visible line runs {low:g}-{high:g}px")
    filled = report["after"]["notchPx2"] - report["before"]["notchPx2"]
    if filled > NOTCH:
        found.append(f"{filled:.0f} game px² more line in valleys and notches")
    canvas = json.loads((ROOT / job["recipe"]).read_text(encoding="utf-8"))["canvas"]
    step = max(VANILLA_PPC * d / c for d, c in zip(job["draw"], canvas))   # one export pixel in game px
    if abs(report["after"]["bandPx"][1] - job["aim"]) > max(OFF_AIM, step / 2):
        found.append("off aim")
    return found


def sheet(items, path, zoom):
    """Current export beside the layered one at game scale, enlarged `zoom` times without smoothing."""
    cells = []
    for job, report in items:
        name = json.loads((ROOT / job["recipe"]).read_text(encoding="utf-8"))["colorFilename"]
        size = tuple(max(1, round(n * VANILLA_PPC)) for n in job["draw"])
        pair = [Image.open(Path(job["out"]) / sub / name).convert("RGBA").resize(size, Image.Resampling.LANCZOS)
                .resize((size[0] * zoom, size[1] * zoom), Image.Resampling.NEAREST) for sub in ("baseline", "export")]
        label = f"{job['piece']}  {report['before']['bandPx'][1]:g} -> {report['after']['bandPx'][1]:g}px (aim {job['aim']})"
        cells.append((label, pair))
    width, x, y, shelf, placed = 1400, 0, 0, 0, []
    for label, pair in cells:
        w, h = max(pair[0].width * 2 + 30, len(label) * 6 + 10), pair[0].height + 34
        if x + w > width and x:
            x, y, shelf = 0, y + shelf, 0
        placed.append((x, y, label, pair))
        x, shelf = x + w, max(shelf, h)
    image = Image.new("RGBA", (width, y + shelf), BACKGROUND)
    draw = ImageDraw.Draw(image)
    for x, y, label, (before, after) in placed:
        image.alpha_composite(before, (x + 5, y + 16))
        image.alpha_composite(after, (x + 15 + before.width, y + 16))
        draw.text((x + 5, y + 2), label, fill="white")
    path.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGB").save(path)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", type=Path, required=True, help="a folder under work/")
    parser.add_argument("--only", help="regular expression: layer only pieces whose name matches")
    parser.add_argument("--jobs", type=int, default=4)
    parser.add_argument("--zoom", type=int, default=2, help="sheet enlargement")
    parser.add_argument("--resume", action="store_true", help="keep pieces that already have outline-layer.json")
    parser.add_argument("--max-add", type=float, default=MAX_ADD, metavar="PX",
                        help="leave a piece unlayered when its drawn line is more than this many game px under "
                             "its aim (default %(default)s); a negative value layers everything")
    args = parser.parse_args(argv)
    output = Path(os.path.abspath(args.output))
    if not output.is_relative_to(ROOT / "work"):
        raise SystemExit("--output must be under work/")
    output.mkdir(parents=True, exist_ok=True)

    objects = json.loads((ROOT / "artwork/objects.json").read_text(encoding="utf-8"))["objects"]
    defs, _ = artwork.definitions(ROOT)
    todo, skipped = jobs(objects, defs, output)
    fromPng, left = pngJobs(defs, objects, output)
    todo += fromPng
    if args.only:
        todo = [job for job in todo if re.search(args.only, job["piece"])]
        skipped = [(name, why) for name, why in skipped if re.search(args.only, name.split("_", 1)[-1])]
    for job in todo:
        job["maxAdd"] = args.max_add if args.max_add >= 0 else None
    with ThreadPoolExecutor(args.jobs) as pool:
        for piece, status in pool.map(lambda job: layer(job, sys.executable, args.resume), todo):
            print(f"{status:4} {piece}", flush=True)

    done, failed, groups, flagged, redo, asDrawn = [], [], {}, [], [], []
    for job in todo:
        path = Path(job["out"]) / "outline-layer.json"
        if not path.exists():
            failed.append(job["piece"])
            continue
        report = json.loads(path.read_text(encoding="utf-8"))
        if "skipped" in report:
            (asDrawn if report.get("asDrawn") else redo).append((job, report))
            continue
        found = flags(job, report)
        done.append((job, report, found))
        groups.setdefault(job["family"], []).append((job, report))
        if found:
            flagged.append((job, report))
    for group, items in groups.items():
        sheet(items, output / "sheets" / f"{group}.png", args.zoom)
    if flagged:
        sheet(flagged, output / "sheets" / "_flagged.png", args.zoom)

    lines = ["# Outside stroke across deployed art", "",
             "Aims: " + ", ".join(f"{k} {v}px" for k, v in AIMS.items()) + " at 64 px per cell. Each folder's "
             "`export/` is a candidate; nothing is approved or deployed. Sheets in `sheets/` show the current "
             "export (left) and the stroked one (right) at game scale; `_flagged.png` holds only flagged pieces. "
             "The stroke is black added under the art, outside its drawn edge; the art itself is unchanged. "
             "Before and After are the visible line's median (black plus any coloured keyline); Range is its "
             "5th to 95th percentile after; Black is the pure-black width after.", "",
             "| Piece | From | Role | Draw | Aim | Before | After | Range | Stroke | Black | Flags |",
             "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    for job, report, found in done:
        lines.append(f"| {job['piece']} | {'PNG' if 'png' in job else 'recipe'} | {job['role']} | "
                     f"{','.join(f'{n:g}' for n in job['draw'])} | {job['aim']} | "
                     f"{report['before']['bandPx'][1]:g} | {report['after']['bandPx'][1]:g} | "
                     f"{report['after']['bandPx'][0]:g}-{report['after']['bandPx'][2]:g} | {report['added']:g} | "
                     f"{report['after']['widthPx']} | {'; '.join(found)} |")
    if asDrawn:
        lines += ["", "## Left as drawn", "",
                  "The visible line is already within 0.25px of the aim, or wider (nothing is trimmed).", "",
                  "| Piece | Role | Aim | Visible line |", "| --- | --- | --- | --- |"]
        lines += [f"| {job['piece']} | {job['role']} | {job['aim']} | {report['before']['bandPx'][1]:g} |"
                  for job, report in asDrawn]
    if redo:
        lines += ["", "## Not stroked: needs a redo", "",
                  f"The stroke adds at most {args.max_add:g}px of black, and only outside an edge that is mostly "
                  "black already. These keep their art; bringing them to the aim is a redo of the piece, which only "
                  "the user names.", "",
                  "| Piece | Role | Aim | Visible line | Short by | Why |", "| --- | --- | --- | --- | --- | --- |"]
        lines += [f"| {job['piece']} | {job['role']} | {job['aim']} | {report['before']['bandPx'][1]:g} | "
                  f"{report['needed']:g} | {report['skipped']} |" for job, report in redo]
    planned, unmatched = deployPlan(done, output)
    lines += ["", "## Test in game", "",
              f"`deploy-plan.json` puts {planned} layered textures over the installed mod; `Textures/` is untouched:",
              "", "```", f"python tools/deploy.py stage --plan {output.relative_to(ROOT).as_posix()}/deploy-plan.json "
              f"--destination <installed mod> --output {output.relative_to(ROOT).as_posix()}/deploy",
              f"python tools/deploy.py deploy {output.relative_to(ROOT).as_posix()}/deploy/manifest.json",
              f"python tools/deploy.py restore {output.relative_to(ROOT).as_posix()}/deploy/manifest.json", "```"]
    if unmatched:
        lines += ["", "Exports with no matching texture, left out of the plan:", ""] + [f"- {u}" for u in unmatched]
    if failed:
        lines += ["", "## Failed", ""] + [f"- {piece}: see its run.log" for piece in failed]
    lines += ["", "## Skipped", ""] + [f"- {name}: {why}" for name, why in skipped]
    lines += ["", "## Deployed textures not layered", "", "| Texture | Reason |", "| --- | --- |"]
    lines += [f"| {texture} | {why} |" for texture, why in left]
    (output / "REPORT.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(done)} stroked, {len(asDrawn)} left as drawn, {len(redo)} left for a redo, {len(failed)} failed, "
          f"{len(flagged)} flagged; "
          f"{output / 'REPORT.md'}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
