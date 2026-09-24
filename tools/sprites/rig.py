"""
2.5D vector rig renderer for Dagger Deep sprite sheets.

Every creature is described as a function  pose -> list of Parts  in *side view*, facing right
(+x), y down, in world pixels, with the entity's gameplay origin at (0, 0). Each part also has a
depth `z` (positive = nearer the camera when facing right).

A sprite frame is rendered for a facing angle theta by rotating the rig about the vertical axis:
    x' = x cos(theta) + z sin(theta)        z' = -x sin(theta) + z cos(theta)
theta = 0 is right-facing, theta = pi is left-facing. Because this is a real rotation rather than
a mirror, the far and near sides swap (a right-handed character keeps the dagger in its right
hand), and because lighting is applied after projection, the light always comes from the upper
left of the screen. Turn-around animations sweep theta through the camera-facing view.

Parts are drawn back-to-front by projected depth with an outline and a light-to-shadow gradient.
"""
import math
import cairo

LIGHT = (-0.55, -0.83)  # screen-space light direction (from upper left)


def hexc(h, a=1.0):
    h = h.lstrip('#')
    return (int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, a)


def mix(c1, c2, t):
    return tuple(c1[i] + (c2[i] - c1[i]) * t for i in range(4))


def lighten(c, t):
    return mix(c, (1, 1, 1, c[3]), t)


def darken(c, t):
    return mix(c, (0, 0, 0, c[3]), t)


class Part:
    """kind: 'ellipse' | 'poly' | 'limb' | 'line' | 'glow'"""

    def __init__(self, kind, color, z=0.0, thick=0.5, outline=True, shade=True, **kw):
        self.kind = kind
        self.color = color
        self.z = z
        self.thick = thick      # how much of its width the part keeps when seen edge-on
        self.outline = outline
        self.shade = shade
        self.kw = kw
        self.order = 0

    def center(self):
        k = self.kw
        if self.kind in ('ellipse', 'glow'):
            return k['c']
        pts = k['pts']
        return (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))


# ---- constructors -------------------------------------------------------------------------

def ellipse(c, rx, ry, color, z=0, rot=0.0, thick=0.8, **kw):
    return Part('ellipse', color, z, thick, c=c, rx=rx, ry=ry, rot=rot, **kw)


def circle(c, r, color, z=0, thick=0.9, **kw):
    return ellipse(c, r, r, color, z, 0.0, thick, **kw)


def poly(pts, color, z=0, thick=0.5, smooth=False, **kw):
    return Part('poly', color, z, thick, pts=list(pts), smooth=smooth, **kw)


def limb(pts, width, color, z=0, thick=1.0, **kw):
    """A round-capped stroke through pts (arms, legs, tails, tentacles). width may be a list."""
    return Part('limb', color, z, thick, pts=list(pts), width=width, **kw)


def line(pts, width, color, z=0, **kw):
    return Part('line', color, z, 1.0, outline=False, shade=False, pts=list(pts), width=width, **kw)


def glow(c, r, color, z=0, **kw):
    return Part('glow', color, z, 1.0, outline=False, shade=False, c=c, r=r, **kw)


# ---- geometry helpers ---------------------------------------------------------------------

def rot(p, a, around=(0, 0)):
    s, c = math.sin(a), math.cos(a)
    x, y = p[0] - around[0], p[1] - around[1]
    return (around[0] + x * c - y * s, around[1] + x * s + y * c)


def add(a, b):
    return (a[0] + b[0], a[1] + b[1])


def polar(origin, length, angle):
    return (origin[0] + math.cos(angle) * length, origin[1] + math.sin(angle) * length)


def ik2(root, target, l1, l2, bend=1):
    """Two-bone IK: returns the joint position. bend=+1/-1 picks the elbow/knee side."""
    dx, dy = target[0] - root[0], target[1] - root[1]
    d = max(1e-4, min(math.hypot(dx, dy), l1 + l2 - 1e-3))
    a = math.atan2(dy, dx)
    cosb = (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d)
    b = math.acos(max(-1, min(1, cosb)))
    return polar(root, l1, a - bend * b)


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(t):
    return t * t * (3 - 2 * t)


def ease_out(t):
    return 1 - (1 - t) ** 3


def ease_in(t):
    return t ** 3


def keys(u, table, ease=smooth, base=None):
    """Interpolate a dict pose over keyframes [(u0, pose0), (u1, pose1), ...] at u in [0,1].
    With `base`, every keyframe is first filled in from it (so {} means "the base pose")."""
    if base is not None:
        keyset = set()
        for _, p in table:
            keyset |= set(p)
        table = [(t, {k: p.get(k, base[k]) for k in keyset}) for t, p in table]
    if u <= table[0][0]:
        return dict(table[0][1])
    for (u0, p0), (u1, p1) in zip(table, table[1:]):
        if u <= u1:
            t = 0 if u1 == u0 else (u - u0) / (u1 - u0)
            t = ease(t)
            out = dict(p0)
            for k, v in p1.items():
                a = p0.get(k, v)
                out[k] = lerp(a, v, t)
            return out
    return dict(table[-1][1])


# ---- rendering ----------------------------------------------------------------------------

def _catmull(ctx, pts, closed=True):
    n = len(pts)
    if n < 3:
        ctx.move_to(*pts[0])
        for p in pts[1:]:
            ctx.line_to(*p)
        return
    ctx.move_to(*pts[0])
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p0 = pts[(i - 1) % n] if closed or i > 0 else pts[0]
        p1 = pts[i]
        p2 = pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if closed or i + 2 < n else pts[-1]
        c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
        ctx.curve_to(c1[0], c1[1], c2[0], c2[1], p2[0], p2[1])
    if closed:
        ctx.close_path()


def render_parts(ctx, parts, theta, outline_w=1.0):
    ct, st = math.cos(theta), math.sin(theta)
    items = []
    for i, p in enumerate(parts):
        cx, cy = p.center()
        px = cx * ct + p.z * st
        pz = -cx * st + p.z * ct + p.kw.get('bias', 0.0)
        sx = math.copysign(math.sqrt(ct * ct + (p.thick * st) ** 2), ct if abs(ct) > 1e-6 else 1)
        items.append((pz, i, p, cx, cy, px, sx))
    items.sort(key=lambda it: (it[0], it[1]))
    zs = [it[0] for it in items]
    zmin, zmax = (min(zs), max(zs)) if zs else (0, 0)

    for pz, _, p, cx, cy, px, sx in items:
        def tx(pt):
            return (px + (pt[0] - cx) * sx, pt[1])

        k = p.kw
        base = p.color
        # parts further from the camera are a little darker
        depth = 0.0 if zmax == zmin else (zmax - pz) / (zmax - zmin)
        base = darken(base, 0.18 * depth) if p.shade else base

        ctx.save()
        if p.kind == 'glow':
            r = k['r']
            g = cairo.RadialGradient(px, cy, 0, px, cy, r)
            g.add_color_stop_rgba(0, *base)
            g.add_color_stop_rgba(1, base[0], base[1], base[2], 0)
            ctx.set_operator(cairo.OPERATOR_ADD)
            ctx.set_source(g)
            ctx.arc(px, cy, r, 0, math.tau)
            ctx.fill()
            ctx.restore()
            continue

        if p.kind == 'ellipse':
            ctx.translate(px, cy)
            ctx.scale(sx, 1)
            ctx.rotate(k.get('rot', 0))
            ctx.scale(k['rx'], k['ry'])
            ctx.arc(0, 0, 1, 0, math.tau)
            ctx.restore()
            ctx.save()
            bbox = (px - k['rx'] * abs(sx), cy - k['ry'], px + k['rx'] * abs(sx), cy + k['ry'])
            _fill_shaded(ctx, p, base, bbox, outline_w)
        elif p.kind == 'poly':
            pts = [tx(q) for q in k['pts']]
            if k.get('smooth'):
                _catmull(ctx, pts, True)
            else:
                ctx.move_to(*pts[0])
                for q in pts[1:]:
                    ctx.line_to(*q)
                ctx.close_path()
            xs = [q[0] for q in pts]
            ys = [q[1] for q in pts]
            _fill_shaded(ctx, p, base, (min(xs), min(ys), max(xs), max(ys)), outline_w)
        elif p.kind in ('limb', 'line'):
            pts = [tx(q) for q in k['pts']]
            w = k['width']
            ctx.set_line_cap(cairo.LINE_CAP_ROUND)
            ctx.set_line_join(cairo.LINE_JOIN_ROUND)
            widths = w if isinstance(w, (list, tuple)) else [w] * len(pts)
            if p.outline:
                ctx.set_source_rgba(*darken(base, 0.6))
                for a, b, wa in zip(pts, pts[1:], widths):
                    ctx.set_line_width(wa + outline_w * 2)
                    ctx.move_to(*a)
                    ctx.line_to(*b)
                    ctx.stroke()
            for idx, (a, b, wa) in enumerate(zip(pts, pts[1:], widths)):
                col = base
                if p.shade:
                    # light the upper edge a little
                    col = lighten(base, 0.08) if (b[1] - a[1]) * LIGHT[0] - (b[0] - a[0]) * LIGHT[1] > 0 else base
                ctx.set_source_rgba(*col)
                ctx.set_line_width(wa)
                ctx.move_to(*a)
                ctx.line_to(*b)
                ctx.stroke()
        ctx.restore()


def _fill_shaded(ctx, p, base, bbox, outline_w):
    x0, y0, x1, y1 = bbox
    if p.shade:
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        r = max(x1 - x0, y1 - y0) / 2 + 0.01
        g = cairo.LinearGradient(cx + LIGHT[0] * r, cy + LIGHT[1] * r, cx - LIGHT[0] * r, cy - LIGHT[1] * r)
        g.add_color_stop_rgba(0.0, *lighten(base, 0.28))
        g.add_color_stop_rgba(0.45, *base)
        g.add_color_stop_rgba(1.0, *darken(base, 0.35))
        ctx.set_source(g)
    else:
        ctx.set_source_rgba(*base)
    if p.outline:
        ctx.fill_preserve()
        ctx.set_source_rgba(*darken(p.color, 0.62))
        ctx.set_line_width(outline_w)
        ctx.set_line_join(cairo.LINE_JOIN_ROUND)
        ctx.stroke()
    else:
        ctx.fill()


class Sheet:
    """Collects frames for one creature and writes <name>.png + <name>.json."""

    def __init__(self, name, box_w, box_h, origin, scale=2.0, outline_w=1.0, fps=24):
        self.name = name
        self.bw, self.bh = box_w, box_h
        self.origin = origin          # rig (0,0) inside the frame box, world px
        self.scale = scale
        self.fw, self.fh = int(math.ceil(box_w * scale)), int(math.ceil(box_h * scale))
        self.outline_w = outline_w
        self.fps = fps
        self.frames = []
        self.anims = {}

    def render(self, parts, theta):
        surf = cairo.ImageSurface(cairo.FORMAT_ARGB32, self.fw, self.fh)
        ctx = cairo.Context(surf)
        ctx.scale(self.scale, self.scale)
        ctx.translate(self.origin[0], self.origin[1])
        render_parts(ctx, parts, theta, self.outline_w)
        return surf

    def add_anim(self, name, n, loop, build, theta_fn=None):
        """build(u, frame_index) -> parts. Renders a right- and left-facing copy unless theta_fn is
        given (turns), in which case one sequence is rendered with the supplied facing angles."""
        if theta_fn is None:
            for suffix, theta in (('_r', 0.0), ('_l', math.pi)):
                idx = []
                for i in range(n):
                    u = i / n if loop else (i / (n - 1) if n > 1 else 0)
                    self.frames.append(self.render(build(u, i), theta))
                    idx.append(len(self.frames) - 1)
                self.anims[name + suffix] = {'frames': idx, 'loop': loop}
        else:
            idx = []
            for i in range(n):
                u = i / (n - 1) if n > 1 else 0
                self.frames.append(self.render(build(u, i), theta_fn(u)))
                idx.append(len(self.frames) - 1)
            self.anims[name] = {'frames': idx, 'loop': loop}

    def add_turns(self, n, build):
        # right -> left passes through the camera-facing view (theta = -pi/2)
        self.add_anim('turn_r2l', n, False, build, lambda u: -math.pi * smooth(u))
        self.add_anim('turn_l2r', n, False, build, lambda u: math.pi + math.pi * smooth(u))

    def save(self, out_dir):
        import json
        import os
        cols = max(1, min(len(self.frames), 4096 // self.fw))
        rows = (len(self.frames) + cols - 1) // cols
        sheet = cairo.ImageSurface(cairo.FORMAT_ARGB32, cols * self.fw, rows * self.fh)
        ctx = cairo.Context(sheet)
        for i, f in enumerate(self.frames):
            ctx.set_source_surface(f, (i % cols) * self.fw, (i // cols) * self.fh)
            ctx.paint()
        os.makedirs(out_dir, exist_ok=True)
        sheet.write_to_png(os.path.join(out_dir, self.name + '.png'))
        meta = {
            'frame_w': self.fw, 'frame_h': self.fh, 'columns': cols, 'scale': self.scale,
            'origin': [self.origin[0] * self.scale, self.origin[1] * self.scale],
            'fps': self.fps, 'animations': self.anims,
        }
        with open(os.path.join(out_dir, self.name + '.json'), 'w') as fh:
            json.dump(meta, fh, indent=1)
        return len(self.frames), cols * self.fw, rows * self.fh

    def contact_sheet(self, path, anims=None, max_frames=12):
        """Debug image: one row per animation, frames left to right, on a mid-grey backdrop."""
        names = anims or list(self.anims.keys())
        w = self.fw * max_frames
        h = self.fh * len(names)
        surf = cairo.ImageSurface(cairo.FORMAT_ARGB32, w + 150, h)
        ctx = cairo.Context(surf)
        ctx.set_source_rgb(0.22, 0.21, 0.24)
        ctx.paint()
        for r, nm in enumerate(names):
            fr = self.anims[nm]['frames']
            step = max(1, math.ceil(len(fr) / max_frames))
            for c, fi in enumerate(fr[::step][:max_frames]):
                ctx.set_source_surface(self.frames[fi], 150 + c * self.fw, r * self.fh)
                ctx.paint()
            ctx.set_source_rgb(1, 1, 1)
            ctx.select_font_face('Sans')
            ctx.set_font_size(13)
            ctx.move_to(6, r * self.fh + self.fh / 2)
            ctx.show_text(f'{nm} ({len(fr)})')
        surf.write_to_png(path)


def transform(parts, angle=0.0, around=(0, 0), sx=1.0, sy=1.0, offset=(0, 0)):
    """Rotate/scale/translate already-built parts in the side-view plane (rolls, squash, lunges)."""
    def f(p):
        x = around[0] + (p[0] - around[0]) * sx
        y = around[1] + (p[1] - around[1]) * sy
        q = rot((x, y), angle, around)
        return (q[0] + offset[0], q[1] + offset[1])
    for p in parts:
        k = p.kw
        if p.kind in ('ellipse', 'glow'):
            k['c'] = f(k['c'])
            if p.kind == 'ellipse':
                k['rx'] *= sx
                k['ry'] *= sy
                k['rot'] = k.get('rot', 0) + angle
            else:
                k['r'] *= (sx + sy) / 2
        else:
            k['pts'] = [f(q) for q in k['pts']]
            if 'width' in k:
                w = k['width']
                s = (abs(sx) + abs(sy)) / 2
                k['width'] = [x * s for x in w] if isinstance(w, (list, tuple)) else w * s
    return parts
