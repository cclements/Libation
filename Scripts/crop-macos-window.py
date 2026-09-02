#!/usr/bin/env python3
"""Crop a macOS whole-window screenshot down to Avalonia's client area."""

from pathlib import Path
import sys

from PIL import Image


def main() -> int:
    if len(sys.argv) != 7:
        raise SystemExit(
            "usage: crop-macos-window.py <source> <target> "
            "<window-width> <window-height> <content-width> <content-height>"
        )

    source, target = map(Path, sys.argv[1:3])
    window_width, window_height, content_width, content_height = map(
        int, sys.argv[3:]
    )

    with Image.open(source) as image:
        scale_x = image.width / window_width
        scale_y = image.height / window_height
        if abs(scale_x - scale_y) > 0.01:
            raise SystemExit(
                f"inconsistent capture scale: horizontal {scale_x:g}, vertical {scale_y:g}"
            )

        target_width = round(content_width * scale_x)
        target_height = round(content_height * scale_y)
        if target_width > image.width or target_height > image.height:
            raise SystemExit(
                f"requested content {target_width}x{target_height} exceeds "
                f"captured window {image.width}x{image.height}"
            )

        left = (image.width - target_width) // 2
        top = image.height - target_height
        image.crop((left, top, left + target_width, image.height)).save(target)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
