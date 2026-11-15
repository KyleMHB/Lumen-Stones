"""Run the canonical shared-tool tests from Lumen's existing check command."""
import importlib.util
from pathlib import Path

def load_tests(loader, tests, pattern):
    path = Path(__file__).resolve().parents[3] / "RimWorldMaskEditor/tests/test_layer_placement.py"
    spec = importlib.util.spec_from_file_location("shared_test_layer_placement", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return loader.loadTestsFromModule(module)
