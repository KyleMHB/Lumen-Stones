"""Create and import a PhotoCraft OpenRaster document for the altar mask."""

import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
COLOR = ROOT / "Textures/Things/Building/Dark/SacrificialAltar.png"
MASK = ROOT / "Textures/Things/Building/Dark/SacrificialAltar_m.png"
RECIPE = ROOT / "artwork/design-sources/sacrificial-altar/master.json"
PHOTOCRAFT = Path(r"C:\Program Files\ArtCraft\PhotoCraft\photocraft.exe")
LAYER = "MASK - paint red, erase to remove"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def png(image):
    output = io.BytesIO()
    image.save(output, format="PNG")
    return output.getvalue()


def create(path):
    if path.exists() or path.with_suffix(".json").exists():
        raise ValueError("Document or its metadata already exists; choose a new filename")
    with Image.open(COLOR) as source:
        color = source.convert("RGBA")
    with Image.open(MASK) as source:
        mask = source.convert("RGBA")
    red, green, blue, alpha = mask.split()
    if mask.size != color.size or green.getextrema() != (0, 0) or blue.getextrema() != (0, 0) or alpha.getextrema() != (255, 255):
        raise ValueError("The altar mask must match the image and use opaque-red encoding")
    overlay = Image.new("RGBA", color.size, (255, 0, 0, 0))
    overlay.putalpha(red)
    image = ET.Element("image", {"version": "0.0.3", "w": str(color.width), "h": str(color.height), "name": "Sacrificial altar"})
    stack = ET.SubElement(image, "stack")
    ET.SubElement(stack, "layer", {"name": LAYER, "src": "data/mask.png", "opacity": "0.5", "visibility": "visible", "composite-op": "svg:src-over", "x": "0", "y": "0"})
    ET.SubElement(stack, "layer", {"name": "Altar colour - reference", "src": "data/color.png", "opacity": "1.0", "visibility": "visible", "composite-op": "svg:src-over", "x": "0", "y": "0", "edit-locked": "true"})
    preview = color.copy()
    preview_overlay = overlay.copy()
    preview_overlay.putalpha(red.point(lambda value: value // 2))
    preview.alpha_composite(preview_overlay)
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("mimetype", "image/openraster", compress_type=zipfile.ZIP_STORED)
        archive.writestr("stack.xml", ET.tostring(image, encoding="utf-8", xml_declaration=True))
        archive.writestr("data/mask.png", png(overlay))
        archive.writestr("data/color.png", png(color))
        archive.writestr("mergedimage.png", png(preview))
    path.with_suffix(".json").write_text(json.dumps({"maskSha256": digest(MASK), "colorSha256": digest(COLOR)}, indent=2) + "\n", encoding="utf-8")


def import_mask(path):
    metadata = json.loads(path.with_suffix(".json").read_text(encoding="utf-8"))
    if digest(MASK) != metadata["maskSha256"] or digest(COLOR) != metadata["colorSha256"]:
        raise ValueError("The source image or mask changed since this document was created; do not overwrite those changes")
    with Image.open(COLOR) as source:
        size = source.size
    with zipfile.ZipFile(path) as archive:
        document = ET.fromstring(archive.read("stack.xml"))
        if (int(document.attrib["w"]), int(document.attrib["h"])) != size:
            raise ValueError("Document dimensions changed")
        layers = [node for node in document.iter("layer") if node.get("name") == LAYER]
        if len(layers) != 1:
            raise ValueError("Keep exactly one mask layer with its original name")
        layer = layers[0]
        with Image.open(io.BytesIO(archive.read(layer.attrib["src"]))) as source:
            overlay = source.convert("RGBA")
        coverage = Image.new("L", size)
        coverage.paste(overlay.getchannel("A"), (int(layer.get("x", 0)), int(layer.get("y", 0))))
    zero = Image.new("L", size)
    result = Image.merge("RGBA", (coverage, zero, zero, Image.new("L", size, 255)))
    recipe = json.loads(RECIPE.read_text(encoding="utf-8"))
    result.save(RECIPE.parent / recipe["paintedMask"])
    result.save(MASK)
    metadata["maskSha256"] = digest(MASK)
    path.with_suffix(".json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print(f"Updated {MASK} and the altar recipe mask")


def watch(path):
    if not path.is_file() or not path.with_suffix(".json").is_file():
        raise ValueError("The OpenRaster document and its adjacent JSON file are required")
    if digest(MASK) != json.loads(path.with_suffix(".json").read_text(encoding="utf-8"))["maskSha256"]:
        raise ValueError("The mod mask changed since this document was created or last imported")
    subprocess.Popen([str(PHOTOCRAFT), str(path.resolve())], cwd=ROOT)
    print(f"Watching {path} for PhotoCraft saves. Keep this window open; press Ctrl+C to stop.", flush=True)
    last = digest(path)
    pending = None
    while True:
        time.sleep(1)
        try:
            current = digest(path)
            if current == last:
                pending = None
            elif current != pending:
                pending = current
            else:
                import_mask(path)
                last = current
                pending = None
        except (OSError, ValueError, KeyError, zipfile.BadZipFile, ET.ParseError) as error:
            print(f"Automatic import stopped: {error}", flush=True)
            return


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("open", "watch", "import"))
    parser.add_argument("document", type=Path)
    args = parser.parse_args()
    try:
        if args.action == "open":
            create(args.document)
            watch(args.document)
        elif args.action == "watch":
            watch(args.document)
        else:
            import_mask(args.document)
    except KeyboardInterrupt:
        print("Stopped watching.")
    except (OSError, ValueError, KeyError, zipfile.BadZipFile, ET.ParseError) as error:
        parser.exit(1, f"{error}\n")
