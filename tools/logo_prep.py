# Cuts the title logo off its black background for the 3D title (Art/Title), the same way tools/shrine_prep.py does the shrines:
#   title.png        the logo's solid parts (stone letters, flourishes, gems, the red blade) on transparent
#   title_h.png      each solid pixel's distance to the nearest edge (16 bits: red = high byte, green = low byte)
#   title_glow.png   the red smoke and glow alone (the solid parts taken out), drawn additively behind the 3D logo
#   title.json       sizes and the largest distance
# usage: python3 tools/logo_prep.py <logo picture> <out dir>
import sys, json
import numpy as np
from PIL import Image
from scipy import ndimage

src, out = sys.argv[1], sys.argv[2]
im = Image.open(src).convert('RGB')
W = 1200
im = im.resize((W, round(im.height * W / im.width)), Image.LANCZOS)
q = np.asarray(im).astype(np.float32) / 255
mx, mn = q.max(2), q.min(2)
r, g = q[..., 0], q[..., 1]
# the stone: bright and grey; its cracks and pits (small holes) count as stone, the letters' openings (big holes) stay open
silver = (mn > 0.24) & (mx - mn < 0.26)
solid = ndimage.binary_closing(silver, iterations=2)
holes = ndimage.binary_fill_holes(solid) & ~solid
lab, n = ndimage.label(holes)
sizes = ndimage.sum(holes, lab, range(1, n + 1))
solid |= np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s < 320])
# the gems and red blades: compact bright red pieces with a pale highlight, or touching the stone (not the smoke)
redc = (r > 0.62) & (r > g * 1.5)
lab, n = ndimage.label(redc)
near = ndimage.binary_dilation(solid, iterations=2)
idx = range(1, n + 1)
area = ndimage.sum(redc, lab, idx); gmax = ndimage.maximum(g, lab, idx); rmean = ndimage.mean(r, lab, idx); touch = ndimage.maximum(near, lab, idx)
keep = [i + 1 for i in range(n) if 15 <= area[i] <= 2600 and rmean[i] > 0.72 and (gmax[i] > 0.38 or touch[i])]
solid = ndimage.binary_closing(solid | np.isin(lab, keep), iterations=1)
lab, n = ndimage.label(solid)
sizes = ndimage.sum(solid, lab, range(1, n + 1))
solid = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s >= 40])
ys, xs = np.nonzero(solid | (mx > 0.08)); pad = 6
y0, y1 = max(0, ys.min() - pad), min(q.shape[0], ys.max() + pad + 1)
x0, x1 = max(0, xs.min() - pad), min(q.shape[1], xs.max() + pad + 1)
q, solid = q[y0:y1, x0:x1], solid[y0:y1, x0:x1]
dist = ndimage.distance_transform_edt(solid)
alpha = np.clip(dist - ndimage.distance_transform_edt(~solid) + 0.5, 0, 1)
ii = ndimage.distance_transform_edt(~(solid & (dist > 1.5)), return_distances=False, return_indices=True)
rgb = np.where((dist <= 1.5)[..., None], q[ii[0], ii[1]], q)
Image.fromarray((np.dstack([rgb, alpha]) * 255).astype(np.uint8), 'RGBA').save(f'{out}/title.png')
maxd = float(dist.max())
h16 = np.round(dist / maxd * 65535).astype(np.uint32)
Image.fromarray(np.dstack([(h16 >> 8).astype(np.uint8), (h16 & 255).astype(np.uint8), np.zeros_like(h16, dtype=np.uint8)]), 'RGB').save(f'{out}/title_h.png')
# the glow alone: the picture with the solid parts (a little grown) taken out, softly
cut = ndimage.gaussian_filter(ndimage.binary_dilation(solid, iterations=2).astype(np.float32), 1.2)
glow = q * (1 - np.clip(cut, 0, 1))[..., None]
Image.fromarray((np.clip(glow, 0, 1) * 255).astype(np.uint8), 'RGB').save(f'{out}/title_glow.png')
json.dump({'w': int(q.shape[1]), 'h': int(q.shape[0]), 'maxDist': round(maxd, 3)}, open(f'{out}/title.json', 'w'), indent=1)
print('title', q.shape[1], 'x', q.shape[0], 'max distance', round(maxd, 2))
