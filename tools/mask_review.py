"""Compatibility entry point for the standalone RimWorld mask editor."""
import importlib.util
import os
from pathlib import Path
import runpy
import sys

project = Path(__file__).resolve().parents[1]
shared = project.parent / "RimWorldMaskEditor"
source = shared / "mask_review.py"
if not source.is_file():
    raise RuntimeError(f"Shared mask editor not found: {source}")
os.environ["RIMWORLD_ART_PROJECT"] = str(project)
sys.path.insert(0, str(shared))
if __name__ == "__main__":
    if source.name == "mask-editor.py":
        sys.argv[1:1] = ["--project", str(project)]
    runpy.run_path(str(source), run_name="__main__")
else:
    spec = importlib.util.spec_from_file_location(__name__, source)
    module = importlib.util.module_from_spec(spec)
    sys.modules[__name__] = module
    spec.loader.exec_module(module)
    globals().update(module.__dict__)
