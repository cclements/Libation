#!/usr/bin/env python3
"""Crop a macOS whole-window screenshot down to Avalonia's client area."""

from pathlib import Path
import math
import os
import sys

from PIL import Image


def crop_window(source, target, window_width, window_height, content_width, content_height, expected_scale):
    if min(window_width, window_height, content_width, content_height) <= 0:
        raise ValueError("window and content dimensions must be positive")
    if not math.isfinite(expected_scale) or expected_scale <= 0:
        raise ValueError("render scale must be finite and positive")
    # Native macOS windows have a top title bar, not horizontal client padding.
    # A larger width is stale window metadata, not permission to crop a different frame.
    if window_width != content_width or not 0 <= window_height - content_height <= 80:
        raise ValueError("window metadata does not match the requested client extent")

    with Image.open(source) as image:
        scale_x = image.width / window_width
        scale_y = image.height / window_height
        if abs(scale_x - scale_y) > 0.01:
            raise ValueError(
                f"inconsistent capture scale: horizontal {scale_x:g}, vertical {scale_y:g}"
            )
        if abs(scale_x - expected_scale) > 0.01 or abs(scale_y - expected_scale) > 0.01:
            raise ValueError(
                f"capture scale {scale_x:g}x{scale_y:g} differs from app render scale {expected_scale:g}"
            )
        target_width = round(content_width * expected_scale)
        target_height = round(content_height * expected_scale)
        if target_width > image.width or target_height > image.height:
            raise ValueError("requested content exceeds the captured window")
        top = image.height - target_height
        # Do not leave a partial or rejected image looking like an accepted frame.
        temporary = Path(str(target) + ".tmp")
        try:
            image.crop((0, top, target_width, image.height)).save(temporary, format="PNG")
            os.replace(temporary, target)
        finally:
            temporary.unlink(missing_ok=True)


def main() -> int:
    if len(sys.argv) != 8:
        raise SystemExit(
            "usage: crop-macos-window.py <source> <target> "
            "<window-width> <window-height> <content-width> <content-height> <render-scale>"
        )

    source, target = map(Path, sys.argv[1:3])
    window_width, window_height, content_width, content_height = map(
        int, sys.argv[3:7]
    )
    crop_window(source, target, window_width, window_height, content_width, content_height, float(sys.argv[7]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
