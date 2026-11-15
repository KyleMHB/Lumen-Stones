"""Compatibility entry point for the shared artwork recipe exporter."""
import importlib.util
from pathlib import Path
import runpy
import sys
# Search upward: the staged pre-commit check runs a copy of this tree under work/, one level deeper.
source = next(p / "RimWorldMaskEditor/export-layered-artwork.py" for p in Path(__file__).resolve().parents
              if (p / "RimWorldMaskEditor/export-layered-artwork.py").is_file())
if __name__ == "__main__":
    runpy.run_path(str(source), run_name="__main__")
else:
    spec = importlib.util.spec_from_file_location(__name__, source)
    module = importlib.util.module_from_spec(spec)
    sys.modules[__name__] = module
    spec.loader.exec_module(module)
    globals().update(module.__dict__)
