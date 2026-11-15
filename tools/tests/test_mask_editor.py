"""Run the canonical shared-tool tests from Lumen's existing check command."""
import importlib.util
from pathlib import Path

def load_tests(loader, tests, pattern):
    path = Path(__file__).resolve().parents[3] / "RimWorldMaskEditor/tests/test_mask_editor.py"
    spec = importlib.util.spec_from_file_location("shared_test_mask_editor", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return loader.loadTestsFromModule(module)
