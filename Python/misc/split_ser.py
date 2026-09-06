# -*- coding: utf-8 -*-
"""
Created on Sun Jun  7 23:38:00 2026

@author: jank
"""

#!/usr/bin/env python3
import argparse
import os
import struct
from pathlib import Path

HEADER_SIZE = 178
FRAMECOUNT_OFFSET = 38

# SER v3 full colour formats
RGB_COLOR_IDS = {100, 101}


def u32_le(buf, offset):
    return struct.unpack_from("<I", buf, offset)[0]


def set_u32_le(buf, offset, value):
    struct.pack_into("<I", buf, offset, value)


def split_ser(input_path, frames_per_chunk):
    input_path = Path(input_path)

    with input_path.open("rb") as f:
        header = bytearray(f.read(HEADER_SIZE))

        if len(header) != HEADER_SIZE:
            raise ValueError("File too small to be a valid SER file")

        if header[:14] != b"LUCAM-RECORDER":
            raise ValueError("Not a valid SER file: missing LUCAM-RECORDER signature")

        color_id = u32_le(header, 18)
        width = u32_le(header, 26)
        height = u32_le(header, 30)
        pixel_depth = u32_le(header, 34)
        frame_count = u32_le(header, FRAMECOUNT_OFFSET)

        bytes_per_sample = 1 if pixel_depth <= 8 else 2
        planes = 3 if color_id in RGB_COLOR_IDS else 1
        frame_size = width * height * bytes_per_sample * planes

        image_data_size = frame_count * frame_size
        expected_no_trailer = HEADER_SIZE + image_data_size
        file_size = input_path.stat().st_size

        has_trailer = file_size >= expected_no_trailer + frame_count * 8
        trailer_offset = expected_no_trailer

        print(f"Input: {input_path}")
        print(f"Resolution: {width} x {height}")
        print(f"Pixel depth: {pixel_depth}")
        print(f"ColorID: {color_id}")
        print(f"Frames: {frame_count}")
        print(f"Frame size: {frame_size:,} bytes")
        print(f"Timestamp trailer: {'yes' if has_trailer else 'no'}")

        stem = input_path.with_suffix("")
        chunk_index = 1

        for start_frame in range(0, frame_count, frames_per_chunk):
            count = min(frames_per_chunk, frame_count - start_frame)

            out_header = bytearray(header)
            set_u32_le(out_header, FRAMECOUNT_OFFSET, count)

            output_path = input_path.with_name(
                f"{stem.name}_part{chunk_index:03d}.ser"
            )

            with output_path.open("wb") as out:
                out.write(out_header)

                # Copy image frames
                f.seek(HEADER_SIZE + start_frame * frame_size)
                bytes_to_copy = count * frame_size
                copy_bytes(f, out, bytes_to_copy)

                # Copy matching timestamp trailer entries, if present
                if has_trailer:
                    f.seek(trailer_offset + start_frame * 8)
                    copy_bytes(f, out, count * 8)

            print(f"Wrote {output_path} with {count} frames")
            chunk_index += 1


def copy_bytes(src, dst, n, buffer_size=1024 * 1024 * 64):
    remaining = n
    while remaining:
        block = src.read(min(buffer_size, remaining))
        if not block:
            raise EOFError("Unexpected end of file while copying")
        dst.write(block)
        remaining -= len(block)


def main():
    parser = argparse.ArgumentParser(
        description="Split an astrophotography SER file into smaller SER chunks."
    )
    parser.add_argument("input", help="Input .ser file")
    parser.add_argument(
        "--frames",
        type=int,
        required=True,
        help="Number of frames per output chunk",
    )

    args = parser.parse_args()

    if args.frames <= 0:
        raise ValueError("--frames must be greater than zero")

    split_ser(args.input, args.frames)


if __name__ == "__main__":
    main()