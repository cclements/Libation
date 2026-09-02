#!/usr/bin/env python3
"""Write placeholder cover JPEGs for seeded demo books.

    python3 Scripts/seed-demo-covers.py <libation-files-folder>

Files follow PictureStorage's naming: <PictureId>_80x80.jpg, _300x300, _500x500 and Native.
Each cover is a solid color derived from the picture id with the title's initials, so it is
recognizable in screenshots without pretending to be real artwork.
"""
import hashlib
import os
import sqlite3
import sys

from PIL import Image, ImageDraw, ImageFont

SIZES = [("_80x80", 80), ("_300x300", 300), ("_500x500", 500), ("Native", 500)]


def initials(title: str) -> str:
    words = [word for word in title.replace(":", " ").split() if word[0].isalpha()]
    return "".join(word[0] for word in words[:2]).upper() or "?"


def color_for(picture_id: str) -> tuple[int, int, int]:
    digest = hashlib.sha1(picture_id.encode()).digest()
    return (60 + digest[0] % 120, 50 + digest[1] % 100, 60 + digest[2] % 120)


def main(folder: str) -> int:
    db = os.path.join(folder, "LibationContext.db")
    images = os.path.join(folder, "Images")
    os.makedirs(images, exist_ok=True)
    rows = sqlite3.connect(db).execute(
        "select PictureId, Title from Books where PictureId is not null and PictureId like 'DEMO%'"
    ).fetchall()
    for picture_id, title in rows:
        base = color_for(picture_id)
        for suffix, px in SIZES:
            image = Image.new("RGB", (px, px), base)
            draw = ImageDraw.Draw(image)
            draw.rectangle(
                [px * 0.08, px * 0.08, px * 0.92, px * 0.92],
                outline=(240, 230, 210),
                width=max(1, px // 60),
            )
            try:
                font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Georgia.ttf", px // 3)
            except OSError:
                font = ImageFont.load_default()
            text = initials(title)
            box = draw.textbbox((0, 0), text, font=font)
            draw.text(
                ((px - box[2]) / 2, (px - box[3]) / 2 - px * 0.05),
                text,
                fill=(245, 235, 215),
                font=font,
            )
            image.save(os.path.join(images, f"{picture_id}{suffix}.jpg"), quality=85)
    print(f"Wrote {len(rows) * len(SIZES)} cover files for {len(rows)} books into {images}")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))
