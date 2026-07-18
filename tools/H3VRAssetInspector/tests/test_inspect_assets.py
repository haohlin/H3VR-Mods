import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


TOOL = Path(__file__).resolve().parents[1] / "inspect_assets.py"


class InspectAssetsTests(unittest.TestCase):
    def test_list_candidates_records_hashes_without_unitypy(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / "scope.zip"
            bundle = b"UnityFS\x00scope-bundle"
            with zipfile.ZipFile(archive, "w") as zip_file:
                zip_file.writestr("bundle/scope", bundle)
                zip_file.writestr("README.md", "not an asset bundle")
            output = root / "manifest.json"

            result = subprocess.run(
                [
                    sys.executable,
                    str(TOOL),
                    "--input",
                    str(archive),
                    "--output",
                    str(output),
                    "--list-candidates",
                ],
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            manifest = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual("zip", manifest["source"]["kind"])
            self.assertEqual(hashlib.sha256(archive.read_bytes()).hexdigest(), manifest["source"]["sha256"])
            self.assertEqual(1, len(manifest["candidates"]))
            self.assertEqual("bundle/scope", manifest["candidates"][0]["label"])
            self.assertEqual(hashlib.sha256(bundle).hexdigest(), manifest["candidates"][0]["sha256"])
            self.assertEqual([], manifest["bundles"])

    def test_expected_hash_rejects_different_input(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            bundle = root / "scope.bundle"
            bundle.write_bytes(b"UnityFS\x00scope-bundle")
            output = root / "manifest.json"
            result = subprocess.run(
                [
                    sys.executable,
                    str(TOOL),
                    "--input",
                    str(bundle),
                    "--output",
                    str(output),
                    "--expected-sha256",
                    "0" * 64,
                    "--list-candidates",
                ],
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertNotEqual(0, result.returncode)
            self.assertIn("does not match", result.stderr)
            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
