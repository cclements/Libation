"""Exercise the production shell cleanup without starting a GUI or subprocess owner."""
import json
from pathlib import Path
import shlex
import subprocess
import tempfile
import unittest


class CaptureCleanupTests(unittest.TestCase):
    def run_cleanup(self, previous_frame, native=False):
        script = (Path(__file__).parents[1] / 'capture-ui.sh').read_text()
        # Execute the real EXIT-trap function; the rest of the driver requires macOS UI.
        cleanup = 'cleanup() {' + script.split('cleanup() {', 1)[1].split('\ntrap cleanup EXIT', 1)[0]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            out, handshake = root / 'output', root / 'handshake'
            out.mkdir()
            handshake.mkdir()
            name = 'window-0001.png' if previous_frame else 'window-0000.png'
            manifest = handshake / 'entries.tsv'
            manifest.write_text(f'0\t{name}\t720\t560' + ('\tmessage\tremoveconfirmation' if native else '') + '\n')
            (handshake / name).write_bytes(b'rejected raw screenshot')
            if previous_frame:
                (out / name).write_bytes(b'previous valid client screenshot')
            if native:
                (out / 'frame-0000.json').write_text(json.dumps({'ClientWidth': 290, 'ClientHeight': 113}))
                (out / 'closed-0000.json').write_text(json.dumps({'IsClosed': True}))
            assignments = {
                'OUT': str(out), 'HANDSHAKE': str(handshake), 'MANIFEST': str(manifest),
                'CAPTURE_LOG': str(out / 'driver-log.txt'), 'APP_PID': '', 'CAFFEINATE_PID': '',
            }
            command = 'set -euo pipefail\n' + cleanup + '\n'
            command += '\n'.join(f'{key}={shlex.quote(value)}' for key, value in assignments.items())
            command += '\ntrap cleanup EXIT\nexit 1\n'
            result = subprocess.run(['bash', '-c', command], capture_output=True, text=True, timeout=10)
            self.assertEqual(result.returncode, 1, result.stderr)
            record = json.loads((out / 'result.json').read_text())
            self.assertEqual(record['driverExitCode'], 1)
            self.assertEqual(record['entries'][0]['produced'], previous_frame)
            if native:
                entry = record['entries'][0]
                self.assertEqual(entry['fixture'], 'removeconfirmation')
                self.assertEqual(entry['surface'], 'message')
                self.assertIsNone(entry['requestedClientDips'])
                self.assertEqual(entry['requestedViewportDips'], [720, 560])
                self.assertEqual(entry['actualClientDips'], [290, 113])
                self.assertEqual(entry['closeReceipt'], 'closed-0000.json')
            if previous_frame:
                self.assertEqual((out / name).read_bytes(), b'previous valid client screenshot')
            else:
                self.assertFalse((out / name).exists())
            self.assertEqual((out / 'capture-diagnostics' / name).read_bytes(), b'rejected raw screenshot')
            self.assertFalse(handshake.exists())

    def test_rejected_raw_cannot_become_the_planned_frame(self):
        self.run_cleanup(previous_frame=False)

    def test_later_rejected_raw_cannot_overwrite_an_earlier_frame(self):
        self.run_cleanup(previous_frame=True)

    def test_native_result_distinguishes_viewport_actual_client_and_close_identity(self):
        self.run_cleanup(previous_frame=True, native=True)


if __name__ == '__main__':
    unittest.main()
