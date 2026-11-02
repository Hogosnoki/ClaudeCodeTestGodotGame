# Cuts the four shrine-flower pictures out of the reference sheet, and for each writes:
#   shrine_N.png       the flower on a transparent background (colour un-darkened at its anti-aliased rim)
#   shrine_N_h.png     its height: each solid pixel's distance to the nearest edge (16 bits: red = high byte, green = low byte)
#   shrine_N_glow.png  the picture itself, black and all (drawn additively behind, it gives the red glow round the finer ones)
import sys, json
import numpy as np
from PIL import Image
from scipy import ndimage

src, out = sys.argv[1], sys.argv[2]
a = np.asarray(Image.open(src).convert('RGB')).astype(np.float32) / 255.0
H, W, _ = a.shape
quads = [(0, 0, W // 2, 490), (W // 2, 0, W, 490), (0, 490, W // 2, H), (W // 2, 490, W, H)]
meta = {}
for n, (x0, y0, x1, y1) in enumerate(quads):
    q = a[y0:y1, x0:x1]
    mx, mn = q.max(axis=2), q.min(axis=2)
    r, gch = q[..., 0], q[..., 1]
    # the solid flower: the silver (bright and grey) and the bright red set in it, its holes filled; not the dim red halo round it
    silver = (mn > 0.28) & (mx - mn < 0.25)
    red = (r > 0.55) & (r > gch * 1.8)
    solid = ndimage.binary_fill_holes(ndimage.binary_closing(silver | red, iterations=2))
    lab, cnt = ndimage.label(solid)
    sizes = ndimage.sum(solid, lab, range(1, cnt + 1))
    keep = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s >= 12])
    solid = keep
    ys, xs = np.nonzero(solid | (mx > 0.08))
    pad = 6
    by0, by1 = max(0, ys.min() - pad), min(q.shape[0], ys.max() + pad + 1)
    bx0, bx1 = max(0, xs.min() - pad), min(q.shape[1], xs.max() + pad + 1)
    q, solid, mx = q[by0:by1, bx0:bx1], solid[by0:by1, bx0:bx1], mx[by0:by1, bx0:bx1]
    # distance to the nearest edge (pixels); a soft alpha one pixel wide at the rim
    dist = ndimage.distance_transform_edt(solid)
    alpha = np.clip(ndimage.distance_transform_edt(solid) - ndimage.distance_transform_edt(~solid) + 0.5, 0, 1)
    rgb = q.copy()
    # (colour at the rim: borrow it from just inside, so the cut edge isn't darkened by the black it was drawn on)
    idx = ndimage.distance_transform_edt(~(solid & (dist > 1.5)), return_distances=False, return_indices=True)
    inner = q[idx[0], idx[1]]
    rgb = np.where((dist <= 1.5)[..., None], inner, rgb)
    img = np.dstack([rgb, alpha])
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), 'RGBA').save(f'{out}/shrine_{n}.png')
    maxd = float(dist.max())
    h16 = np.round(dist / maxd * 65535).astype(np.uint32)
    himg = np.dstack([(h16 >> 8).astype(np.uint8), (h16 & 255).astype(np.uint8), np.zeros_like(h16, dtype=np.uint8)])
    Image.fromarray(himg, 'RGB').save(f'{out}/shrine_{n}_h.png')
    Image.fromarray((np.clip(q, 0, 1) * 255).astype(np.uint8), 'RGB').save(f'{out}/shrine_{n}_glow.png')
    # where it swings from: its lowest solid point, at its middle
    sy, sx = np.nonzero(solid)
    meta[n] = {'w': int(q.shape[1]), 'h': int(q.shape[0]), 'maxDist': round(maxd, 3), 'pivotX': round(float((sx.min() + sx.max()) / 2), 1), 'pivotY': int(sy.max()), 'top': int(sy.min())}
    print(n, meta[n], 'solid px', int(solid.sum()))
json.dump(meta, open(f'{out}/shrines.json', 'w'), indent=1)
