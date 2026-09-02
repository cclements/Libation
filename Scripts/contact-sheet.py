#!/usr/bin/env python3
"""Place an implementation capture beside its reference mockup for review.

    python3 Scripts/contact-sheet.py capture.png mockup.png out.png
        [--crop x,y,w,h] [--left "Implementation"] [--right "Mockup"]

--crop trims the mockup (in mockup pixels) to the region a screen slice owns.
"""

import argparse

from PIL import Image, ImageDraw, ImageFont


HEIGHT = 1000
GUTTER = 24
LABEL_H = 36


def fit(image: Image.Image, height: int) -> Image.Image:
    width = round(image.width * height / image.height)
    return image.convert("RGB").resize((width, height), Image.Resampling.LANCZOS)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("capture")
    parser.add_argument("mockup")
    parser.add_argument("out")
    parser.add_argument("--crop", help="x,y,w,h in mockup pixels")
    parser.add_argument("--left", default="Implementation")
    parser.add_argument("--right", default="Mockup")
    args = parser.parse_args()

    with Image.open(args.capture) as capture:
        left = fit(capture, HEIGHT)
    with Image.open(args.mockup) as mockup:
        if args.crop:
            x, y, width, height = (int(value) for value in args.crop.split(","))
            mockup = mockup.crop((x, y, x + width, y + height))
        right = fit(mockup, HEIGHT)

    sheet = Image.new(
        "RGB", (left.width + GUTTER + right.width, HEIGHT + LABEL_H), (28, 22, 24)
    )
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 20)
    except OSError:
        font = ImageFont.load_default()
    draw.text((8, 8), args.left, fill=(243, 233, 215), font=font)
    draw.text((left.width + GUTTER + 8, 8), args.right, fill=(243, 233, 215), font=font)
    sheet.paste(left, (0, LABEL_H))
    sheet.paste(right, (left.width + GUTTER, LABEL_H))
    sheet.save(args.out)
    print(f"{args.out}: {sheet.width}x{sheet.height}")


if __name__ == "__main__":
    main()
