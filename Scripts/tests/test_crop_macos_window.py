"""Regression checks for rejected/stale macOS window captures; no GUI required."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from PIL import Image

spec = importlib.util.spec_from_file_location('crop', Path(__file__).parents[1] / 'crop-macos-window.py')
crop = importlib.util.module_from_spec(spec)
spec.loader.exec_module(crop)


class CropWindowTests(unittest.TestCase):
    def test_rejects_stale_dimensions_and_unexpected_retina_scale_without_output(self):
        with tempfile.TemporaryDirectory() as directory:
            source, target = Path(directory) / 'raw.png', Path(directory) / 'client.png'
            Image.new('RGB', (1440, 1168), 'white').save(source)
            for dimensions in [(960, 744, 720, 560, 2), (720, 584, 720, 560, 1), (0, 584, 720, 560, 2)]:
                with self.assertRaises(ValueError):
                    crop.crop_window(source, target, *dimensions)
                self.assertFalse(target.exists())

    def test_retina_crop_removes_title_bar_and_preserves_exact_client_pixels(self):
        with tempfile.TemporaryDirectory() as directory:
            source, target = Path(directory) / 'raw.png', Path(directory) / 'client.png'
            image = Image.new('RGB', (1440, 1168), 'red')
            image.paste('blue', (0, 48, 1440, 1168))
            image.save(source)
            crop.crop_window(source, target, 720, 584, 720, 560, 2)
            with Image.open(target) as result:
                self.assertEqual(result.size, (1440, 1120))
                self.assertEqual(result.getpixel((0, 0)), (0, 0, 255))
                self.assertEqual(result.getpixel((1439, 1119)), (0, 0, 255))


if __name__ == '__main__':
    unittest.main()
