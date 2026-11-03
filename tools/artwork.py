"""Check declared artwork and render a versioned CPU review sheet. No game files are edited."""
import argparse
import contextlib
import importlib.util
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw, ImageFilter

from artwork_family import CONFIG, familyChecks

ROOT = Path(__file__).resolve().parents[1]
DIRECTIONS = ("north", "east", "south", "west")


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def inside(root, relative):
    root = Path(root).resolve()
    path = (root / relative).resolve()
    if not path.is_relative_to(root) or path == root:
        raise ValueError(f"Path outside root: {relative}")
    return path


def definitions(root):
    result = {}
    files = sorted((root / "Defs").rglob("*.xml"))
    files += sorted((root / "Integrations").rglob("*.xml"))
    files += sorted((root / "Patches").rglob("*.xml"))
    for path in files:
        doc = ET.parse(path).getroot()
        for node in doc.findall("ThingDef"):
            for key in (node.get("Name"), node.findtext("defName")):
                if key:
                    result[key] = node
    return result, len(files)


def value(defs, name, field, default=None):
    visited = set()
    while name in defs:
        if name in visited:
            raise ValueError(f"Definition inheritance cycle: {name}")
        visited.add(name)
        node = defs[name]
        found = node.find(field)
        if found is not None:
            return found.text or ""
        parent_field = node.find(field.split("/")[0])
        if parent_field is not None and parent_field.get("Inherit") == "False":
            return default
        name = node.get("ParentName")
    return default


def pair(text):
    return tuple(float(n.strip()) for n in text.strip("() ").split(","))


def icon_settings(defs, name):
    draw = pair(value(defs, name, "graphicData/drawSize", "(1,1)"))
    footprint = pair(value(defs, name, "size", "(1,1)"))
    custom = value(defs, name, "uiIconPath", "")
    scale = float(value(defs, name, "uiIconScale", "1"))
    # Installed GenUI.IconDrawScale uses Min, including a 1x2 bed's footprint.
    if not custom:
        scale *= min(draw[0] / footprint[0], draw[1] / footprint[1])
    direction = value(defs, name, "defaultPlacingRot", "North").lower()
    if direction in ("east", "west"):
        draw = draw[::-1]
    return {"scale": scale, "aspect": draw[0] / draw[1], "direction": direction,
            "offset": pair(value(defs, name, "uiIconOffset", "(0,0)")), "custom": custom}


def sampled(image, size):
    # Resize channels separately: RGBA resizing premultiplies color by alpha.
    return [ch.resize(size, Image.Resampling.BILINEAR) for ch in image.split()]


def tinted(color, mask, size, material, bleed_radius):
    channels = sampled(color, size)
    if mask is None:
        return Image.merge("RGBA", channels)
    mask = mask.copy()
    if bleed_radius:
        # Controlled approximation of the loader's documented transparent RGB bleed.
        red = mask.getchannel("R")
        extended = red.filter(ImageFilter.MaxFilter(bleed_radius * 2 + 1))
        empty = mask.getchannel("A").point(lambda a: 255 if a == 0 else 0)
        mask_channels = list(mask.split())
        mask_channels[0] = Image.composite(extended, red, empty)
        mask = Image.merge("RGBA", mask_channels)
    red, _, _, alpha = sampled(mask, size)
    pixels = []
    for r, g, b, a, mr, ma in zip(*(ch.get_flattened_data() for ch in channels),
                                 red.get_flattened_data(), alpha.get_flattened_data()):
        gate = min(ma, 1)  # clamp((sampled alpha / 255) * 255, 0, 1)
        weight = mr / 255 * gate
        # ColorTwo is white for this registry's single construction-tint channel.
        pixels.append(tuple(round(c * (1 - weight + weight * tint))
                            for c, tint in zip((r, g, b), material)) + (a,))
    result = Image.new("RGBA", size)
    result.putdata(pixels)
    return result


def icon_geometry(color, settings, frame, command_scale):
    factor = settings["scale"] * command_scale
    aspect = settings["aspect"]
    size = (max(1, round(frame * min(aspect, 1) * factor)),
            max(1, round(frame * min(1 / aspect, 1) * factor)))
    position = tuple(round((frame - length) / 2 + frame * offset)
                     for length, offset in zip(size, settings["offset"]))
    alpha = color.getchannel("A").resize(size, Image.Resampling.BILINEAR)
    bounds = alpha.point(lambda a: 255 if a > 128 else 0).getbbox()
    if bounds is None:
        raise ValueError("Empty icon")
    bounds = tuple(n + position[i % 2] for i, n in enumerate(bounds))
    return size, position, bounds


def object_views(obj, asset_root):
    directions = obj.get("directions", [])
    for direction in directions or ["single"]:
        base = obj["texture"] + ("_" + direction if directions else "")
        mask = None
        if obj.get("maskEncoding"):
            mask = inside(asset_root, base + ("m.png" if directions else "_m.png"))
        yield direction, inside(asset_root, base + ".png"), mask


def external_roots(root, game_root, pyrinth_root):
    if not game_root:
        project = root / "Source/LumenStones.csproj"
        if project.exists():
            game_root = ET.parse(project).findtext("./PropertyGroup/RimWorldPath")
    game = Path(game_root) if game_root else None
    if not pyrinth_root and game and game.is_dir():
        pyrinth_root = game.parents[1] / "workshop/content/294100/3336544632"
    return game, Path(pyrinth_root) if pyrinth_root else None


def loadExporter(root):
    spec = importlib.util.spec_from_file_location("layered_export", Path(root) / "tools/export-layered-artwork.py")
    exporter = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(exporter)
    return exporter


def checkRecipe(root, recipe, colorPath, maskPath):
    manifestPath = inside(root, recipe)
    manifest = json.loads(manifestPath.read_text(encoding="utf-8"))
    scratch = Path(root) / "work"
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="lumen-recipe-", dir=scratch) as temp:
        output = Path(temp)
        with contextlib.redirect_stdout(io.StringIO()):
            loadExporter(root).export(manifestPath, output)
        for role, production in (("color", colorPath), ("mask", maskPath)):
            if production is None:
                continue
            with Image.open(output / manifest[role + "Filename"]) as fresh, Image.open(production) as saved:
                fresh, saved = fresh.convert("RGBA"), saved.convert("RGBA")
                if fresh.size != saved.size or fresh.tobytes() != saved.tobytes():
                    raise ValueError(f"Recipe {role} differs from production texture: {production.name}")


def check(root, registry_path="artwork/objects.json", game_root=None, pyrinth_root=None,
          preview=None):
    registry = json.loads(inside(root, registry_path).read_text(encoding="utf-8"))
    defs, xml_count = definitions(root)
    errors, notes, rows, icons = [], [], [], {}
    for name in registry.get("oneCellTallPylons", []):
        try:
            allowed = pair(registry.get("pylonFootprintExceptions", {}).get(name, "(1,1)"))
            if name not in defs or pair(value(defs, name, "size", "(1,1)")) != allowed:
                raise ValueError("Pylon must occupy one cell" if allowed == (1, 1)
                                 else f"Pylon footprint must match its recorded exception {allowed}")
            if value(defs, name, "drawerType") != "RealtimeOnly":
                raise ValueError("Pylon must use RealtimeOnly drawing")
            if value(defs, name, "altitudeLayer") != "BuildingOnTop":
                raise ValueError("Pylon must use BuildingOnTop altitude")
        except (ValueError, KeyError) as exc:
            errors.append(f"{name}: {exc}")
    for name, wood_cost in registry.get("fixedWoodTorches", {}).items():
        try:
            if name not in defs:
                raise ValueError("Missing ThingDef")
            if value(defs, name, "stuffCategories", None) is not None:
                raise ValueError("Fixed-material torch still has stuff categories")
            if value(defs, name, "costStuffCount", None) is not None:
                raise ValueError("Fixed-material torch still has selectable stuff cost")
            if value(defs, name, "costList/WoodLog", None) != str(wood_cost):
                raise ValueError(f"Fixed-material torch must require {wood_cost} WoodLog")
        except (ValueError, KeyError) as exc:
            errors.append(f"{name}: {exc}")
    if (Path(root) / CONFIG).exists():
        notes.extend(f"Family measurement: {finding}" for finding in familyChecks(root)[0])
    game, pyrinth = external_roots(root, game_root, pyrinth_root)
    renderer = registry["renderer"]
    if game and game.is_dir():
        for relative, expected in renderer["sourceHashes"].items():
            path = inside(game, relative)
            if not path.exists() or digest(path) != expected:
                errors.append(f"Renderer changed: {relative}; verify the profile before updating its hash")
    else:
        notes.append("Installed renderer not available; using the recorded versioned model")
    for obj in registry["objects"]:
        name = obj["id"]
        try:
            asset_root = root / "Textures"
            if obj.get("external"):
                if not pyrinth or not pyrinth.is_dir():
                    notes.append(f"{name}: optional Pyrinth absent; upstream textures/patch targets skipped")
                    continue
                upstream = ET.parse(inside(pyrinth, obj["definitionFile"])).getroot()
                for node in upstream.findall("ThingDef"):
                    if node.findtext("defName") == name:
                        defs[name] = node
                patch = ET.parse(inside(root, obj["patch"])).getroot()
                for operation in patch.findall(".//operations/li"):
                    xpath = operation.findtext("xpath")
                    targets = upstream.findall(xpath.removeprefix("Defs/"))
                    if len(targets) != 1:
                        raise ValueError(f"Patch target expected once: {xpath}")
                    replacement = operation.find("value")[0]
                    targets[0].text = replacement.text
                asset_root = pyrinth / "Textures"
            if name not in defs:
                raise ValueError("Missing ThingDef")
            for field in ("identity", "geometry", "materials", "camera", "protected", "allowedEdits"):
                if not obj["contract"].get(field):
                    raise ValueError(f"Master contract missing {field}")
            master = obj["approvedMaster"]
            if not master.get("approvedBy") or not master.get("approvedOn"):
                raise ValueError("Master approval not recorded")
            if digest(inside(asset_root, master["path"])) != master["sha256"]:
                raise ValueError("Approved master changed; record visual approval before replacing its hash")
            if value(defs, name, "graphicData/texPath") != obj["texture"]:
                raise ValueError("Registered texture differs from consuming XML")
            expected_class = "Graphic_Multi" if obj.get("directions") else "Graphic_Single"
            if value(defs, name, "graphicData/graphicClass", "Graphic_Single") != expected_class:
                raise ValueError("Registered directions differ from graphic class")
            if obj.get("directions") and set(obj["directions"]) != set(DIRECTIONS):
                raise ValueError("Four directional filenames must be declared")
            settings = icon_settings(defs, name)
            if obj.get("maskEncoding") and settings["custom"]:
                raise ValueError("Tintable icon bypasses the native UI material with uiIconPath")
            if obj.get("maskEncoding") and value(defs, name, "graphicData/shaderType") != "CutoutComplex":
                raise ValueError("Tintable graphic does not consume CutoutComplex masks")
            recipes = obj.get("recipes", {})
            validViews = set(obj.get("directions") or ["single"])
            if set(recipes) - validViews:
                raise ValueError("Recipe keys must name registered views")
            views = {}
            for direction, color_path, mask_path in object_views(obj, asset_root):
                with Image.open(color_path) as source:
                    if source.mode != "RGBA" or list(source.size) != obj["canvas"]:
                        raise ValueError(f"Wrong RGBA canvas: {color_path.name}")
                    color = source.copy()
                if not color.getchannel("A").getbbox():
                    raise ValueError(f"Empty color sprite: {color_path.name}")
                mask = None
                if mask_path:
                    with Image.open(mask_path) as source:
                        if source.mode != "RGBA" or source.size != color.size:
                            raise ValueError(f"Mask/color dimensions or mode differ: {mask_path.name}")
                        mask = source.copy()
                    if obj["maskEncoding"] == "opaque-red":
                        if mask.getchannel("A").getextrema() != (255, 255):
                            raise ValueError(f"Transparent exclusion in {mask_path.name}; encode coverage in red")
                        if any(mask.getchannel(ch).getextrema() != (0, 0) for ch in ("G", "B")):
                            raise ValueError(f"Unexpected second tint channel: {mask_path.name}")
                if direction in recipes:
                    checkRecipe(root, recipes[direction], color_path, mask_path)
                    notes.append(f"{name} / {direction}: recipe verified")
                else:
                    notes.append(f"{name} / {direction}: no recipe")
                views[direction] = (color, mask)
                rows.append((f"{obj['label']} / {direction}", color, mask))
            for alias, target in obj.get("aliases", {}).items():
                if inside(asset_root, alias).read_bytes() != inside(asset_root, target).read_bytes():
                    raise ValueError(f"Menu/color-mask alias differs: {alias}")
            direction = settings["direction"] if obj.get("directions") else "single"
            color, mask = views[direction]
            size, position, bounds = icon_geometry(color, settings, 128, renderer["commandScale"])
            height = (bounds[3] - bounds[1]) / 128
            low, high = obj["iconHeightFraction"]
            if not low <= height <= high:
                raise ValueError(f"Icon visible height {height:.3f} outside approved range {low}..{high}")
            if min(bounds[:2]) < 0 or max(bounds[2:]) > 128:
                raise ValueError(f"Icon clips its 128px review frame: {bounds}")
            icons[name] = {"effectiveScale": settings["scale"], "bounds128": bounds}
            rows.append((f"{obj['label']} / BUILD ICON", color, mask, size, position))
        except (ValueError, OSError, KeyError, ET.ParseError) as exc:
            errors.append(f"{name}: {exc}")
    # Icons can precede their gameplay definitions. Approved sets use a frozen
    # manifest; regenerating a candidate must not replace approved hashes.
    for icon_set in registry.get("iconSets", []) + registry.get("candidateIconSets", []):
        try:
            approved = icon_set in registry.get("iconSets", [])
            if approved and not (icon_set.get("approvedBy") and icon_set.get("approvedOn")):
                raise ValueError("Icon design approval not recorded")
            manifest = json.loads(inside(root, icon_set["manifest"]).read_text(encoding="utf-8"))
            asset_root = inside(root, icon_set["textureRoot"])
            for asset in manifest["assets"]:
                path = inside(asset_root, asset["texture"] + ".png")
                if digest(path) != asset["sha256"]:
                    raise ValueError(f"Registered icon changed: {asset['texture']}")
                with Image.open(path) as source:
                    if source.mode != "RGBA" or list(source.size) != asset["canvas"]:
                        raise ValueError(f"Wrong candidate RGBA canvas: {asset['texture']}")
                    if not source.getchannel("A").getbbox():
                        raise ValueError(f"Empty candidate icon: {asset['texture']}")
                    label = "APPROVED DESIGN" if approved else "CANDIDATE"
                    rows.append((f"{label} / {path.stem}", source.copy(), None))
            recipe = json.loads(inside(root, manifest["recipe"]).read_text(encoding="utf-8"))
            if digest(inside(root, manifest["recipe"])) != manifest["recipeSha256"]:
                raise ValueError("Candidate recipe changed; regenerate the icon manifest")
            source_root = inside(root, manifest["recipe"]).parent
            if digest(inside(source_root, recipe["shard"]["source"])) != recipe["shard"]["sha256"]:
                raise ValueError("Generated shard source changed")
            for name, expected in recipe["upstream"]["files"].items():
                if digest(inside(source_root, "upstream/" + name)) != expected:
                    raise ValueError(f"Upstream icon source changed: {name}")
            if not inside(root, recipe["upstream"]["license"]).is_file():
                raise ValueError("Missing Bionic Icons licence notice")
            bindings = manifest["integrationDefs"] + ([manifest["artworkPatch"]]
                                                       if manifest.get("artworkPatch") else [])
            for relative in bindings:
                binding = ET.parse(inside(root, relative))
                for field in ("texture", "texPath", "iconPath", "iconPathOverride"):
                    for texture in binding.findall(".//" + field):
                        if not inside(asset_root, texture.text + ".png").is_file():
                            raise ValueError(f"Missing integration texture: {texture.text}")
            state = "design approved" if approved else "design approval pending"
            pending = ", ".join(manifest.get("pendingConsumers", []) + ["in-game review"])
            notes.append(f"{icon_set['id']}: {len(manifest['assets'])} icons checked; {state}; {pending} pending")
        except (ValueError, OSError, KeyError, ET.ParseError) as exc:
            errors.append(f"{icon_set['id']}: {exc}")
    if preview and not errors:
        output = Path(preview)
        output.mkdir(parents=True, exist_ok=True)
        sheet = Image.new("RGB", (780, 210 * len(rows) + 30), "#51545a")
        draw = ImageDraw.Draw(sheet)
        draw.text((8, 6), "CPU model: independent bilinear sampling; not an in-game capture", fill="white")
        for row_index, row in enumerate(rows):
            label, color, mask, *geometry = row
            for col, (material_name, tint) in enumerate(registry["materials"].items()):
                x, y = col * 260, 30 + row_index * 210
                draw.text((x + 6, y + 3), label + " / " + material_name, fill="white")
                size, position = geometry if geometry else ((128, 128), (0, 0))
                result = tinted(color, mask, size, tint, renderer["modeledBleedRadius"])
                frame = Image.new("RGBA", (128, 128))
                frame.alpha_composite(result, position)
                sheet.paste(frame, (x + 66, y + 23), frame)
                thumb = frame.resize((48, 48), Image.Resampling.BILINEAR)
                sheet.paste(thumb, (x + 106, y + 156), thumb)
        sheet.save(output / "contact-sheet.png")
        (output / "report.json").write_text(json.dumps({"renderer": renderer, "icons": icons,
                                                       "notes": notes}, indent=2) + "\n")
    return errors, notes, len(icons), xml_count


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["check", "check-candidate", "prepare-candidate", "silhouettes"])
    parser.add_argument("manifest", nargs="?", type=Path)
    parser.add_argument("--output", type=Path, help="Candidate or silhouette sheet directory")
    parser.add_argument("--staged", action="store_true", help="Check the Git index, not unstaged files")
    parser.add_argument("--preview", type=Path, help="Write the contact sheet and model report here")
    parser.add_argument("--game-root")
    parser.add_argument("--pyrinth-root")
    args = parser.parse_args()
    if args.command in ("check-candidate", "prepare-candidate"):
        if not args.manifest or not args.output or args.staged or args.preview:
            parser.error("Candidate commands need MANIFEST and --output; use a working-tree candidate")
    elif args.command == "silhouettes":
        if args.manifest or args.staged or args.preview:
            parser.error("silhouettes takes only --output")
    elif args.manifest or args.output:
        parser.error("MANIFEST and --output belong to candidate commands")
    if args.staged and args.preview:
        parser.error("Generate previews from the reviewed working tree, not --staged")
    try:
        if args.command == "silhouettes":
            from artwork_silhouettes import render
            for sheet in render(ROOT, args.output or ROOT / "work/silhouettes"):
                print("Sheet:", sheet)
            return 0
        if args.command in ("check-candidate", "prepare-candidate"):
            from artwork_candidate import checkCandidate, prepareCandidate
            operation = prepareCandidate if args.command == "prepare-candidate" else checkCandidate
            report = operation(ROOT, args.manifest, args.output)
            for warning in report["warnings"]:
                print("WARN:", warning)
            for unavailable in report["unavailable"]:
                print("UNAVAILABLE:", unavailable)
            for error in report["errors"]:
                print("FAIL:", error)
            print("Report:", args.output / "candidate-check.json")
            return 0 if report["passed"] else 1
        if args.staged:
            scratch = ROOT / "work"
            scratch.mkdir(exist_ok=True)
            with tempfile.TemporaryDirectory(prefix="lumen-artwork-index-", dir=scratch) as temp:
                subprocess.run(["git", "checkout-index", "--all", "--prefix=" + Path(temp).as_posix() + "/"],
                               cwd=ROOT, check=True, capture_output=True)
                result = check(Path(temp), game_root=args.game_root, pyrinth_root=args.pyrinth_root)
        else:
            result = check(ROOT, game_root=args.game_root, pyrinth_root=args.pyrinth_root, preview=args.preview)
        errors, notes, count, xml_count = result
        for note in notes:
            print("NOTE:", note)
        for error in errors:
            print("FAIL:", error)
        if errors:
            return 1
        print(f"PASS: {count} artwork objects, {xml_count} runtime XML files; pylon placement, torch materials, masks and icons")
        if args.preview:
            print("Review sheet:", args.preview / "contact-sheet.png")
        return 0
    except (ValueError, OSError, KeyError, ET.ParseError, subprocess.CalledProcessError) as exc:
        print("FAIL:", exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
