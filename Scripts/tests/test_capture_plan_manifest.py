"""Check the production driver manifest without starting a native application."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest


class CaptureManifestTests(unittest.TestCase):
    def test_native_matrix_names_match_plan_defaults_and_keep_kind_identity(self):
        scripts = Path(__file__).parents[1]
        source = (scripts / 'capture-ui.sh').read_text()
        program = source.split('<<\'PY\'\n', 1)[1].split('\nPY\n', 1)[0]
        with tempfile.TemporaryDirectory() as directory:
            manifest = Path(directory) / 'entries.tsv'
            plan = scripts / 'capture-plans' / 'ic1-native-windows.json'
            result = subprocess.run(['python3', '-', str(plan), str(manifest)], input=program,
                                    capture_output=True, text=True, timeout=10)
            self.assertEqual(result.returncode, 0, result.stderr)
            rows = [row.split('\t') for row in manifest.read_text().splitlines()]
            self.assertEqual(len(rows), 14)
            self.assertEqual(len({row[1] for row in rows}), 14)
            self.assertEqual(rows[0], ['0', 'cellar-dialog-settings-natural-1280x900.png', '1280', '900', 'dialog', 'settings'])
            self.assertEqual(rows[3], ['3', 'cellar-window-about-clamped-360x400.png', '360', '400', 'window', 'about'])
            self.assertEqual(rows[5], ['5', 'cellar-message-removeconfirmation-clamped-265x110.png', '265', '110', 'message', 'removeconfirmation'])
            self.assertEqual(rows[13], ['13', 'tastingroom-owner-restored.png', '720', '560', 'route', '-'])

    def test_attended_final_fixture_gets_full_bounded_stage_budget(self):
        source = (Path(__file__).parents[1] / 'capture-ui.sh').read_text()
        program = source.split("<<'CLOSE_GRACE'\n", 1)[1].split('\nCLOSE_GRACE\n', 1)[0]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'plan.json'
            for wait, timeout, expected in [(False, 30000, 15), (True, 20000, 25), (True, 120000, 125), (True, 100, 6)]:
                path.write_text(json.dumps({'stageTimeoutMs': timeout, 'entries': [{'waitForNativeClose': wait}]}))
                result = subprocess.run(['python3', '-', str(path)], input=program, capture_output=True, text=True, timeout=10)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(int(result.stdout), expected)


if __name__ == '__main__':
    unittest.main()
