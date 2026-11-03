#!/usr/bin/env python3
"""
The title's growth order (for the intro's grow-in, UI/TitleLogo.cs): for every solid pixel of the title, when it appears.

Growing by thickness alone (thickest first) stutters at the thin spikes: along a spike the distance to the edge is nearly the
same everywhere (and whole pixels at that), so long runs of it pass the line in the same frame. Here growth instead spreads like
a fire through the shape: any pixel may start on its own at a time set by its thickness (the thick cores first), and growth runs
on from every lit pixel to its neighbours, slower where the shape is thin. A spike is reached from its base and grows out along
its length, every pixel at its own moment, its point last.

  arrival(p) = min over q of  start(q)  +  path cost from q to p,  each step costing  STEP * length / sqrt(D)
  start(q)   = START * (maxD - D(q))  +  THIN_COST * max(0, THIN - D(q))     (the thin parts wait to be reached)

Writes title_g.png beside title_h.png: the arrival time, 0..1 of the latest, as 16 bits (red the high byte, green the low), and
"growMax" into title.json.

Usage: tools/logo_grow.py [ART_DIR]   (default godot/DaggerCave/Art/Title)
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import dijkstra
from scipy.ndimage import distance_transform_edt

START = 1.0   # time per pixel of thickness short of the thickest, for a pixel starting on its own
THIN = 3.0    # thinner than this (px to the edge), a pixel hardly starts on its own: it waits to be reached from a thicker part
THIN_COST = 12.0  # ...each pixel short of THIN delays its own start this much
STEP = 0.25   # time per pixel of travel through a part one pixel thick (thicker parts carry it faster, by the root)


def main():
    art = sys.argv[1] if len(sys.argv) > 1 else os.path.join("godot", "DaggerCave", "Art", "Title")
    meta_path = os.path.join(art, "title.json")
    meta = json.load(open(meta_path))
    h = np.asarray(Image.open(os.path.join(art, "title_h.png")).convert("RGB")).astype(np.float64)
    d = (h[:, :, 0] * 256 + h[:, :, 1]) / 65535.0 * meta["maxDist"]
    solid = d > 0
    hgt, wid = d.shape
    ids = -np.ones(d.shape, dtype=np.int64)
    ids[solid] = np.arange(solid.sum())
    n = int(solid.sum())
    src = n  # a virtual source: an edge to every pixel, as long as that pixel's own start
    rows, cols, wts = [], [], []
    dmax = d.max()
    ys, xs = np.nonzero(solid)
    rows.append(np.full(n, src)); cols.append(ids[ys, xs]); wts.append(START * (dmax - d[ys, xs]) + THIN_COST * np.maximum(0.0, THIN - d[ys, xs]) + 1e-6)
    speed = np.sqrt(np.maximum(d, 0.5))
    for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
        length = (dx * dx + dy * dy) ** 0.5
        # (the neighbour of (y, x) is (y+dy, x+dx): line up both windows the same way)
        y0, y1 = 0, hgt - dy
        x0, x1 = max(0, -dx), wid - max(0, dx)
        A = solid[y0:y1, x0:x1]
        B = solid[y0 + dy:y1 + dy, x0 + dx:x1 + dx]
        both = A & B
        yy, xx = np.nonzero(both)
        yy += y0; xx += x0
        ia = ids[yy, xx]; ib = ids[yy + dy, xx + dx]
        cost = STEP * length * 2.0 / (speed[yy, xx] + speed[yy + dy, xx + dx])
        rows += [ia, ib]; cols += [ib, ia]; wts += [cost, cost]
    rows = np.concatenate(rows); cols = np.concatenate(cols); wts = np.concatenate(wts)
    g = coo_matrix((wts, (rows, cols)), shape=(n + 1, n + 1)).tocsr()
    dist = dijkstra(g, directed=True, indices=src)
    t = dist[:n]
    t[~np.isfinite(t)] = t[np.isfinite(t)].max()
    tmax = float(t.max())
    q = np.zeros(d.shape)
    q[ys, xs] = t / tmax
    # (outside the shape: the time of the nearest solid pixel, so the mesh's edge cells, which blend toward their outside corners,
    # don't show a rim early)
    _, (iy, ix) = distance_transform_edt(~solid, return_indices=True)
    q = q[iy, ix]
    v = np.round(q * 65535).astype(np.uint32)
    out = np.zeros((hgt, wid, 3), dtype=np.uint8)
    out[:, :, 0] = (v >> 8).astype(np.uint8)
    out[:, :, 1] = (v & 255).astype(np.uint8)
    Image.fromarray(out, "RGB").save(os.path.join(art, "title_g.png"))
    meta["growMax"] = round(tmax, 3)
    json.dump(meta, open(meta_path, "w"), indent=1)
    print(f"growth order: {n} pixels, latest arrival {tmax:.2f} (start {START}, step {STEP})")


if __name__ == "__main__":
    main()
