"""Render an OBJ to PNG so a generated mesh can actually be looked at.

There is no Unity editor available to this pipeline and no Blender guaranteed to be installed,
but CLAUDE.md section 69 rule 17 forbids claiming something works without validating it — and
for geometry, validating means seeing it. This is a small z-buffered rasteriser: enough to judge
silhouette, proportion and surface, which is all the generator's output needs to be judged on.

    python tools/preview_obj.py tools/output/velociraptor_high.obj -o tools/output/raptor.png
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image

# Views worth having on a contact sheet: the side is the gameplay silhouette, the three-quarter
# rear is what the run camera actually shows, and the front/top catch the symmetry errors the
# other two hide.
VIEWS = {
    "side": (0.0, 0.0),
    "three_quarter": (35.0, 12.0),
    "rear_quarter": (145.0, 14.0),
    "front": (95.0, 5.0),
    "top": (20.0, 78.0),
}


def load_obj(path: Path) -> tuple[np.ndarray, np.ndarray]:
    positions: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int]] = []

    with path.open("r", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("v "):
                _, x, y, z = line.split()[:4]
                positions.append((float(x), float(y), float(z)))
            elif line.startswith("f "):
                # Only the vertex index of each "v/vt/vn" triplet matters here.
                idx = [int(part.split("/")[0]) - 1 for part in line.split()[1:]]
                for k in range(1, len(idx) - 1):
                    faces.append((idx[0], idx[k], idx[k + 1]))

    return np.array(positions, dtype=np.float64), np.array(faces, dtype=np.int32)


def rotation(yaw_deg: float, pitch_deg: float) -> np.ndarray:
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    ry = np.array([[math.cos(yaw), 0.0, math.sin(yaw)],
                   [0.0, 1.0, 0.0],
                   [-math.sin(yaw), 0.0, math.cos(yaw)]])
    rx = np.array([[1.0, 0.0, 0.0],
                   [0.0, math.cos(pitch), -math.sin(pitch)],
                   [0.0, math.sin(pitch), math.cos(pitch)]])
    return rx @ ry


def render(vertices: np.ndarray, faces: np.ndarray, size: int, yaw: float, pitch: float,
           smooth: bool, wire: bool, focus: tuple[float, float, float] | None = None,
           radius: float = 0.0) -> np.ndarray:
    view = rotation(yaw, pitch)
    camera = vertices @ view.T

    if focus is not None:
        # Frame a named point at a fixed radius, for inspecting one region — a face is a few
        # dozen triangles out of a couple of thousand and is invisible in a whole-body shot.
        centre = (np.array(focus) @ view.T)[:2]
        scale = (size * 0.86) / (radius * 2.0)
    else:
        # Fit the model to the frame with a small margin, preserving aspect so proportions stay
        # honest across views — a per-view stretch would hide exactly the errors this is for.
        lo, hi = camera[:, :2].min(axis=0), camera[:, :2].max(axis=0)
        centre = (lo + hi) * 0.5
        scale = (size * 0.86) / max(hi[0] - lo[0], hi[1] - lo[1])

    screen = np.empty((len(camera), 3))
    screen[:, 0] = (camera[:, 0] - centre[0]) * scale + size * 0.5
    screen[:, 1] = size * 0.5 - (camera[:, 1] - centre[1]) * scale
    screen[:, 2] = camera[:, 2]

    # Vertex normals, area-weighted, so shading shows the surface rather than the tessellation.
    tri = camera[faces]
    face_normals = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    if smooth:
        vertex_normals = np.zeros_like(camera)
        for k in range(3):
            np.add.at(vertex_normals, faces[:, k], face_normals)
        lengths = np.linalg.norm(vertex_normals, axis=1, keepdims=True)
        vertex_normals /= np.maximum(lengths, 1e-9)

    lengths = np.linalg.norm(face_normals, axis=1, keepdims=True)
    unit_face = face_normals / np.maximum(lengths, 1e-9)

    colour = np.zeros((size, size, 3), dtype=np.float64)
    colour[:] = (0.086, 0.075, 0.067)
    depth = np.full((size, size), -np.inf)

    key = np.array([0.42, 0.66, 0.62])
    key /= np.linalg.norm(key)
    fill = np.array([-0.55, 0.15, 0.40])
    fill /= np.linalg.norm(fill)

    order = np.argsort(-tri[:, :, 2].mean(axis=1))

    for f in order:
        # Back-face cull. A generated mesh with a winding mistake shows up here as a hole,
        # which is precisely the bug worth catching before anything reaches the engine.
        if unit_face[f, 2] <= 0.0:
            continue

        idx = faces[f]
        p = screen[idx]

        min_x = max(int(np.floor(p[:, 0].min())), 0)
        max_x = min(int(np.ceil(p[:, 0].max())), size - 1)
        min_y = max(int(np.floor(p[:, 1].min())), 0)
        max_y = min(int(np.ceil(p[:, 1].max())), size - 1)
        if min_x > max_x or min_y > max_y:
            continue

        x0, y0 = p[0, 0], p[0, 1]
        x1, y1 = p[1, 0], p[1, 1]
        x2, y2 = p[2, 0], p[2, 1]
        area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(area) < 1e-9:
            continue

        xs = np.arange(min_x, max_x + 1) + 0.5
        ys = np.arange(min_y, max_y + 1) + 0.5
        gx, gy = np.meshgrid(xs, ys)

        w1 = ((gx - x0) * (y2 - y0) - (x2 - x0) * (gy - y0)) / area
        w2 = ((x1 - x0) * (gy - y0) - (gx - x0) * (y1 - y0)) / area
        w0 = 1.0 - w1 - w2

        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not inside.any():
            continue

        z = w0 * p[0, 2] + w1 * p[1, 2] + w2 * p[2, 2]
        window = depth[min_y:max_y + 1, min_x:max_x + 1]
        visible = inside & (z > window)
        if not visible.any():
            continue

        if smooth:
            n = (w0[..., None] * vertex_normals[idx[0]] +
                 w1[..., None] * vertex_normals[idx[1]] +
                 w2[..., None] * vertex_normals[idx[2]])
            n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-9)
        else:
            n = np.broadcast_to(unit_face[f], (*inside.shape, 3))

        # Two directional lights and a rim term. The rim is not decoration: it is what makes a
        # silhouette readable in a flat render, and silhouette is the thing being judged.
        lambert = np.clip(n @ key, 0.0, 1.0)
        bounce = np.clip(n @ fill, 0.0, 1.0)
        rim = np.clip(1.0 - n[..., 2], 0.0, 1.0) ** 3

        shade = (0.16
                 + 0.78 * lambert[..., None] * np.array([1.00, 0.96, 0.88])
                 + 0.22 * bounce[..., None] * np.array([0.42, 0.50, 0.62])
                 + 0.35 * rim[..., None] * np.array([1.00, 0.62, 0.30]))

        window_colour = colour[min_y:max_y + 1, min_x:max_x + 1]
        window_colour[visible] = np.clip(shade, 0.0, 1.0)[visible]
        window[visible] = z[visible]

        if wire:
            edge = (np.minimum(np.minimum(w0, w1), w2) < 0.035) & visible
            window_colour[edge] = window_colour[edge] * 0.45

    return np.clip(colour, 0.0, 1.0)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("obj", type=Path)
    parser.add_argument("-o", "--out", type=Path, default=None)
    parser.add_argument("-s", "--size", type=int, default=420)
    parser.add_argument("--views", default="side,three_quarter,rear_quarter,front")
    parser.add_argument("--flat", action="store_true", help="face normals, to inspect topology")
    parser.add_argument("--wire", action="store_true", help="overlay triangle edges")
    parser.add_argument("--focus", default=None, help="x,y,z to centre on, e.g. the head")
    parser.add_argument("--radius", type=float, default=0.35, help="half-extent framed when focusing")
    args = parser.parse_args()

    vertices, faces = load_obj(args.obj)
    print(f"{args.obj.name}: {len(vertices)} vertices, {len(faces)} triangles")
    lo, hi = vertices.min(axis=0), vertices.max(axis=0)
    print(f"  bounds x [{lo[0]:.3f}, {hi[0]:.3f}]  y [{lo[1]:.3f}, {hi[1]:.3f}]  z [{lo[2]:.3f}, {hi[2]:.3f}]")

    focus = tuple(float(v) for v in args.focus.split(",")) if args.focus else None
    names = [v.strip() for v in args.views.split(",") if v.strip()]
    tiles = [render(vertices, faces, args.size, *VIEWS[name], not args.flat, args.wire, focus, args.radius)
             for name in names]

    sheet = np.concatenate(tiles, axis=1)
    out = args.out or args.obj.with_suffix(".png")
    Image.fromarray((sheet * 255).astype(np.uint8)).save(out)
    print(f"wrote {out}  ({' | '.join(names)})")


if __name__ == "__main__":
    main()
