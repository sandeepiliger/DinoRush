"""Render a sequence of OBJs as one filmstrip, for judging an animation cycle.

A gait cannot be judged from a single frame: the phase between the legs, the lag in the tail
and whether the body bobs on the beat only show up in sequence.

    python tools/preview_strip.py tools/output/run_*.obj -o tools/output/run.png
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image

from preview_obj import VIEWS, load_obj, render


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("objs", nargs="+", type=Path)
    parser.add_argument("-o", "--out", type=Path, required=True)
    parser.add_argument("-s", "--size", type=int, default=300)
    parser.add_argument("--view", default="side")
    parser.add_argument("--columns", type=int, default=0, help="0 = one row")
    args = parser.parse_args()

    yaw, pitch = VIEWS[args.view]

    # One shared frame for every tile, taken from the union of all frames. Re-fitting each tile
    # would silently cancel out the very vertical bob the strip exists to show.
    meshes = [load_obj(path) for path in sorted(args.objs)]
    union = np.concatenate([vertices for vertices, _ in meshes])

    tiles = []
    for path, (vertices, faces) in zip(sorted(args.objs), meshes):
        padded = np.concatenate([vertices, union])
        padded_faces = faces
        tile = render(padded, padded_faces, args.size, yaw, pitch, True, False)
        tiles.append(tile)
        print(f"  {path.name}: {len(vertices)} verts")

    columns = args.columns or len(tiles)
    rows = [np.concatenate(tiles[i:i + columns], axis=1) for i in range(0, len(tiles), columns)]

    width = max(row.shape[1] for row in rows)
    rows = [np.pad(row, ((0, 0), (0, width - row.shape[1]), (0, 0))) for row in rows]

    sheet = np.concatenate(rows, axis=0)
    Image.fromarray((sheet * 255).astype(np.uint8)).save(args.out)
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
