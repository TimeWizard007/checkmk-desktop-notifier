#!/usr/bin/env python3
"""Build a macOS .icns from the canonical Windows app.ico.

Source of truth: src/CheckmkDesktopNotifier.App/Assets/app.ico
Native ICO PNG frames are reused for 16/32/64/128/256. 512 and 1024 are
Lanczos-upscaled from the 256x256 frame via ImageMagick (or sips on macOS).

On macOS, iconutil is preferred. Elsewhere a PNG-based ICNS is written.
"""
from __future__ import annotations

import argparse
import os
import shutil
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

ICONSET_FILES = (
    ("icon_16x16.png", 16, False),
    ("icon_16x16@2x.png", 32, False),
    ("icon_32x32.png", 32, False),
    ("icon_32x32@2x.png", 64, False),
    ("icon_128x128.png", 128, False),
    ("icon_128x128@2x.png", 256, False),
    ("icon_256x256.png", 256, False),
    ("icon_256x256@2x.png", 512, True),
    ("icon_512x512.png", 512, True),
    ("icon_512x512@2x.png", 1024, True),
)

# Apple PNG ICNS types used by modern Finder / iconutil output.
ICNS_TYPES = {
    "icon_16x16.png": "icp4",
    "icon_16x16@2x.png": "ic11",
    "icon_32x32.png": "icp5",
    "icon_32x32@2x.png": "ic12",
    "icon_128x128.png": "ic07",
    "icon_128x128@2x.png": "ic13",
    "icon_256x256.png": "ic08",
    "icon_256x256@2x.png": "ic14",
    "icon_512x512.png": "ic09",
    "icon_512x512@2x.png": "ic10",
}

PNG_MAGIC = b"\x89PNG\r\n\x1a\n"


def extract_png_frames(ico_path: Path) -> dict[int, bytes]:
    data = ico_path.read_bytes()
    if len(data) < 6:
        raise SystemExit(f"ICO is too small: {ico_path}")
    reserved, image_type, count = struct.unpack_from("<HHH", data, 0)
    if reserved != 0 or image_type != 1 or count < 1:
        raise SystemExit(f"Not a valid ICO: {ico_path}")
    frames: dict[int, bytes] = {}
    offset = 6
    for _ in range(count):
        width, height, _colors, _reserved, _planes, _bitcount, nbytes, img_off = struct.unpack_from(
            "<BBBBHHII", data, offset
        )
        width = 256 if width == 0 else width
        height = 256 if height == 0 else height
        blob = data[img_off : img_off + nbytes]
        if not blob.startswith(PNG_MAGIC):
            raise SystemExit(f"ICO frame {width}x{height} is not an embedded PNG")
        if width == height:
            frames[width] = blob
        offset += 16
    if 256 not in frames:
        raise SystemExit("ICO does not contain a 256x256 PNG frame")
    return frames


def magick_resize(src_png: Path, dest_png: Path, size: int) -> None:
    magick = shutil.which("magick")
    convert = shutil.which("convert")
    args = [
        str(src_png),
        "-filter",
        "Lanczos",
        "-resize",
        f"{size}x{size}",
        f"PNG32:{dest_png}",
    ]
    if magick:
        subprocess.check_call([magick, *args])
        return
    if convert:
        subprocess.check_call([convert, *args])
        return
    sips = shutil.which("sips")
    if sips:
        subprocess.check_call(
            [sips, "-z", str(size), str(size), str(src_png), "--out", str(dest_png)]
        )
        return
    raise SystemExit(
        "ImageMagick (magick/convert) or macOS sips is required to scale the 256px icon"
    )


def write_iconset(frames: dict[int, bytes], iconset: Path, work: Path) -> None:
    iconset.mkdir(parents=True, exist_ok=True)
    native_dir = work / "native"
    native_dir.mkdir(parents=True, exist_ok=True)
    for size, blob in frames.items():
        (native_dir / f"{size}.png").write_bytes(blob)

    src_256 = native_dir / "256.png"
    scaled: dict[int, Path] = {}
    for size in (512, 1024):
        dest = work / f"scaled-{size}.png"
        magick_resize(src_256, dest, size)
        scaled[size] = dest

    for name, size, upscaled in ICONSET_FILES:
        dest = iconset / name
        if upscaled:
            shutil.copyfile(scaled[size], dest)
        else:
            native = native_dir / f"{size}.png"
            if not native.is_file():
                raise SystemExit(f"ICO is missing a {size}x{size} PNG frame required for {name}")
            shutil.copyfile(native, dest)


def write_icns_from_pngs(iconset: Path, icns_path: Path) -> None:
    chunks = []
    for name, ostype in ICNS_TYPES.items():
        png = iconset / name
        data = png.read_bytes()
        if not data.startswith(PNG_MAGIC):
            raise SystemExit(f"Iconset file is not PNG: {png}")
        payload = ostype.encode("ascii") + struct.pack(">I", 8 + len(data)) + data
        chunks.append(payload)
    body = b"".join(chunks)
    icns_path.parent.mkdir(parents=True, exist_ok=True)
    icns_path.write_bytes(b"icns" + struct.pack(">I", 8 + len(body)) + body)


def iconutil_icns(iconset: Path, icns_path: Path) -> bool:
    iconutil = shutil.which("iconutil")
    if not iconutil:
        return False
    icns_path.parent.mkdir(parents=True, exist_ok=True)
    subprocess.check_call(
        [iconutil, "-c", "icns", "-o", str(icns_path), str(iconset)]
    )
    return True


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate CheckmkDesktopNotifier.icns from app.ico")
    parser.add_argument("--ico", type=Path, help="Source ICO (defaults to the Windows app.ico)")
    parser.add_argument("--icns", type=Path, required=True, help="Output .icns path")
    parser.add_argument("--iconset", type=Path, help="Optional iconset directory to keep")
    args = parser.parse_args()

    root = Path(__file__).resolve().parent.parent
    ico = args.ico or (root / "src/CheckmkDesktopNotifier.App/Assets/app.ico")
    if not ico.is_file():
        raise SystemExit(f"Source icon not found: {ico}")

    frames = extract_png_frames(ico)
    with tempfile.TemporaryDirectory(prefix="cdn-iconset-") as tmp:
        tmp_path = Path(tmp)
        iconset = args.iconset or (tmp_path / "AppIcon.iconset")
        if args.iconset:
            if iconset.exists():
                shutil.rmtree(iconset)
        write_iconset(frames, iconset, tmp_path / "work")
        if os.uname().sysname == "Darwin" and iconutil_icns(iconset, args.icns):
            method = "iconutil"
        else:
            write_icns_from_pngs(iconset, args.icns)
            method = "png-icns"
        if not args.icns.is_file() or args.icns.stat().st_size < 16:
            raise SystemExit("Failed to write .icns")
        print(f"Wrote {args.icns} via {method}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
