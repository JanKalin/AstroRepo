# -*- coding: utf-8 -*-
"""
Created on Mon Jun  8 00:01:59 2026

@author: jank
"""

#!/usr/bin/env python3
import argparse
import struct
from pathlib import Path

import numpy as np


HEADER_SIZE = 178

COLOR_ID_OFFSET = 18
WIDTH_OFFSET = 26
HEIGHT_OFFSET = 30
PIXEL_DEPTH_OFFSET = 34
FRAMECOUNT_OFFSET = 38

MONO_COLOR_ID = 0


def u32_le(buf, offset):
    return struct.unpack_from("<I", buf, offset)[0]


def set_u32_le(buf, offset, value):
    struct.pack_into("<I", buf, offset, value)


def green_slices(pattern):
    """
    Return array slices for the two green positions.
    Coordinates assume row 0, col 0 is the first Bayer pixel.
    """
    pattern = pattern.upper()

    if pattern == "RGGB":
        return (slice(0, None, 2), slice(1, None, 2)), (slice(1, None, 2), slice(0, None, 2))

    if pattern == "BGGR":
        return (slice(0, None, 2), slice(1, None, 2)), (slice(1, None, 2), slice(0, None, 2))

    if pattern == "GRBG":
        return (slice(0, None, 2), slice(0, None, 2)), (slice(1, None, 2), slice(1, None, 2))

    if pattern == "GBRG":
        return (slice(0, None, 2), slice(0, None, 2)), (slice(1, None, 2), slice(1, None, 2))

    raise ValueError("Pattern must be RGGB, BGGR, GRBG, or GBRG")


def extract_green_ser(input_path, output_path, pattern):
    input_path = Path(input_path)
    output_path = Path(output_path)

    with input_path.open("rb") as src:
        header = bytearray(src.read(HEADER_SIZE))

        if len(header) != HEADER_SIZE:
            raise ValueError("File too small to be a SER file")

        if header[:14] != b"LUCAM-RECORDER":
            raise ValueError("Not a valid SER file: missing LUCAM-RECORDER signature")

        width = u32_le(header, WIDTH_OFFSET)
        height = u32_le(header, HEIGHT_OFFSET)
        pixel_depth = u32_le(header, PIXEL_DEPTH_OFFSET)
        frame_count = u32_le(header, FRAMECOUNT_OFFSET)

        if width % 2 or height % 2:
            raise ValueError("Width and height must be even for 2x2 Bayer green extraction")

        if pixel_depth <= 8:
            dtype = np.uint8
            bytes_per_pixel = 1
        else:
            dtype = np.uint16
            bytes_per_pixel = 2

        in_frame_size = width * height * bytes_per_pixel
        out_width = width // 2
        out_height = height // 2
        out_frame_size = out_width * out_height * bytes_per_pixel

        image_data_size = frame_count * in_frame_size
        expected_no_trailer = HEADER_SIZE + image_data_size
        file_size = input_path.stat().st_size

        has_timestamp_trailer = file_size >= expected_no_trailer + frame_count * 8
        timestamp_offset = expected_no_trailer

        g1_slice, g2_slice = green_slices(pattern)

        out_header = bytearray(header)
        set_u32_le(out_header, COLOR_ID_OFFSET, MONO_COLOR_ID)
        set_u32_le(out_header, WIDTH_OFFSET, out_width)
        set_u32_le(out_header, HEIGHT_OFFSET, out_height)
        set_u32_le(out_header, FRAMECOUNT_OFFSET, frame_count)

        with output_path.open("wb") as dst:
            dst.write(out_header)

            for i in range(frame_count):
                raw = src.read(in_frame_size)

                if len(raw) != in_frame_size:
                    raise EOFError(f"Unexpected end of file at frame {i}")

                frame = np.frombuffer(raw, dtype=dtype).reshape(height, width)

                g1 = frame[g1_slice]
                g2 = frame[g2_slice]

                if dtype == np.uint8:
                    green = ((g1.astype(np.uint16) + g2.astype(np.uint16)) // 2).astype(np.uint8)
                else:
                    green = ((g1.astype(np.uint32) + g2.astype(np.uint32)) // 2).astype(np.uint16)

                dst.write(green.tobytes())

                if (i + 1) % 500 == 0:
                    print(f"Processed {i + 1}/{frame_count} frames")

            if has_timestamp_trailer:
                src.seek(timestamp_offset)
                dst.write(src.read(frame_count * 8))

        print()
        print(f"Wrote: {output_path}")
        print(f"Input size:  {width} x {height}")
        print(f"Output size: {out_width} x {out_height}")
        print(f"Frames: {frame_count}")
        print(f"Bayer pattern: {pattern.upper()}")
        print("Output is monochrome SER using only Bayer green pixels.")


def main():
    parser = argparse.ArgumentParser(
        description="Extract Bayer green photosites from a raw SER file into a monochrome SER."
    )
    parser.add_argument("input", help="Input raw Bayer SER file")
    parser.add_argument("output", help="Output monochrome SER file")
    parser.add_argument(
        "--pattern",
        required=True,
        choices=["RGGB", "BGGR", "GRBG", "GBRG", "rggb", "bggr", "grbg", "gbrg"],
        help="Bayer pattern of the input SER",
    )

    args = parser.parse_args()

    extract_green_ser(args.input, args.output, args.pattern)


if __name__ == "__main__":
    main()