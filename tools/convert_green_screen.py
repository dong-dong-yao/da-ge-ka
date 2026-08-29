#!/usr/bin/env python3
"""Convert a green-screen MP4 into a consistently cropped transparent PNG sequence."""

from __future__ import annotations

import argparse
import subprocess
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--fps", type=int, default=12)
    parser.add_argument("--max-size", type=int, default=520)
    parser.add_argument("--ffmpeg", default="ffmpeg")
    return parser.parse_args()


def estimate_background(rgb: np.ndarray) -> np.ndarray:
    border = 12
    samples = np.concatenate(
        [
            rgb[:border, :, :].reshape(-1, 3),
            rgb[-border:, :, :].reshape(-1, 3),
            rgb[:, :border, :].reshape(-1, 3),
            rgb[:, -border:, :].reshape(-1, 3),
        ],
        axis=0,
    )
    green_samples = samples[
        (samples[:, 1] > samples[:, 0] * 1.15)
        & (samples[:, 1] > samples[:, 2] * 1.08)
    ]
    source = green_samples if len(green_samples) else samples
    return np.median(source.astype(np.float32), axis=0)


def keyed_rgba(path: Path) -> np.ndarray:
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.uint8)
    background = estimate_background(rgb)
    rgb_float = rgb.astype(np.float32)
    distance = np.linalg.norm(rgb_float - background, axis=2)
    distance_alpha = np.clip((distance - 10.0) / 45.0 * 255.0, 0, 255)

    background_excess = max(12.0, background[1] - max(background[0], background[2]))
    green_excess = rgb_float[:, :, 1] - np.maximum(rgb_float[:, :, 0], rgb_float[:, :, 2])
    excess_alpha = np.clip(
        (background_excess - green_excess) / (background_excess - 4.0) * 255.0,
        0,
        255,
    )
    alpha = np.minimum(distance_alpha, excess_alpha).astype(np.uint8)

    despilled = rgb.copy()
    channel_limit = np.maximum(despilled[:, :, 0], despilled[:, :, 2]).astype(np.uint16) + 3
    despilled[:, :, 1] = np.minimum(despilled[:, :, 1], channel_limit).astype(np.uint8)
    rgba = np.dstack([despilled, alpha])
    rgba[alpha == 0, :3] = 0
    return rgba


def content_bounds(frames: list[Path]) -> tuple[int, int, int, int]:
    left = top = 1 << 30
    right = bottom = -1
    for frame in frames:
        alpha = keyed_rgba(frame)[:, :, 3]
        ys, xs = np.where(alpha >= 20)
        if len(xs) == 0:
            continue
        left = min(left, int(xs.min()))
        top = min(top, int(ys.min()))
        right = max(right, int(xs.max()) + 1)
        bottom = max(bottom, int(ys.max()) + 1)

    if right < 0:
        raise RuntimeError("No foreground content was detected.")

    width, height = Image.open(frames[0]).size
    padding = 4
    return (
        max(0, left - padding),
        max(0, top - padding),
        min(width, right + padding),
        min(height, bottom + padding),
    )


def main() -> None:
    args = parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    for old_frame in args.output.glob("frame_*.png"):
        old_frame.unlink()

    with tempfile.TemporaryDirectory(prefix="dagaka-frames-") as temp:
        raw_dir = Path(temp)
        subprocess.run(
            [
                args.ffmpeg,
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-i",
                str(args.input),
                "-an",
                "-r",
                str(args.fps),
                str(raw_dir / "frame_%04d.png"),
            ],
            check=True,
        )
        raw_frames = sorted(raw_dir.glob("frame_*.png"))
        if not raw_frames:
            raise RuntimeError("FFmpeg did not produce any frames.")

        bounds = content_bounds(raw_frames)
        for index, raw_frame in enumerate(raw_frames):
            image = Image.fromarray(keyed_rgba(raw_frame), mode="RGBA").crop(bounds)
            scale = min(1.0, args.max_size / max(image.size))
            if scale < 1.0:
                image = image.resize(
                    (round(image.width * scale), round(image.height * scale)),
                    Image.Resampling.LANCZOS,
                )
            image.save(args.output / f"frame_{index:04d}.png", optimize=True)

    print(f"frames={len(raw_frames)} bounds={bounds} output_size={image.size}")


if __name__ == "__main__":
    main()
