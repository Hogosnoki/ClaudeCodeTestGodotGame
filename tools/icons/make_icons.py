#!/usr/bin/env python3
"""
Paints the game's icons (run from the repo root):  python3 tools/icons/make_icons.py

  godot/DaggerCave/Art/Icons/ability_*.png   full-colour ability icons (HUD, bottom)
  godot/DaggerCave/Art/Icons/relic_*.png     full-colour relic icons (HUD, top; relic cards)
  godot/DaggerCave/Art/Icons/cat_*.png       minimalist white symbols for the kinds of reward
                                             (tinted in game; the top of each card)

Everything is drawn big (512 px) and shrunk, glyph by glyph, then given an ink outline, a
little shading and a drop shadow, on a dark rounded badge tinted with the icon's colour.
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

S = 512
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "godot", "DaggerCave", "Art", "Icons")
FINAL = 128


def P(x, y):
    return (x * S, y * S)


def pts(l):
    return [P(x, y) for x, y in l]


def col(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def light(c, t=0.35):
    return mix(c, (255, 255, 255, 255), t)


def dark(c, t=0.4):
    return mix(c, (0, 0, 0, 255), t)


# ------------------------------------------------------------------ primitives
def poly(d, l, fill):
    d.polygon(pts(l), fill=fill)


def ell(d, cx, cy, rx, ry, fill):
    d.ellipse([P(cx - rx, cy - ry), P(cx + rx, cy + ry)], fill=fill)


def rect(d, x0, y0, x1, y1, fill, r=0.0):
    if r > 0:
        d.rounded_rectangle([P(x0, y0), P(x1, y1)], radius=r * S, fill=fill)
    else:
        d.rectangle([P(x0, y0), P(x1, y1)], fill=fill)


def line(d, a, b, w, fill):
    d.line([P(*a), P(*b)], fill=fill, width=int(w * S))
    r = w * S / 2
    for q in (a, b):
        x, y = P(*q)
        d.ellipse([x - r, y - r, x + r, y + r], fill=fill)


def arc(d, cx, cy, r, a0, a1, w, fill):
    d.arc([P(cx - r, cy - r), P(cx + r, cy + r)], a0, a1, fill=fill, width=int(w * S))


def curve(f, n=24, t0=0.0, t1=1.0):
    return [f(t0 + (t1 - t0) * i / n) for i in range(n + 1)]


# ------------------------------------------------------------------ glyphs (upright, on a transparent layer)
def g_sword(d, c1, c2):
    poly(d, [(0.5, 0.06), (0.585, 0.18), (0.585, 0.62), (0.415, 0.62), (0.415, 0.18)], c1)
    poly(d, [(0.5, 0.1), (0.5, 0.62), (0.415, 0.62), (0.415, 0.18)], light(c1, 0.3))
    rect(d, 0.3, 0.62, 0.7, 0.69, c2, 0.02)
    rect(d, 0.465, 0.69, 0.535, 0.84, dark(c2, 0.35), 0.02)
    ell(d, 0.5, 0.87, 0.055, 0.045, c2)


def g_dagger(d, c1, c2):
    poly(d, [(0.5, 0.1), (0.57, 0.3), (0.56, 0.6), (0.44, 0.6), (0.43, 0.3)], c1)
    poly(d, [(0.5, 0.14), (0.5, 0.6), (0.44, 0.6), (0.43, 0.3)], light(c1, 0.3))
    poly(d, [(0.3, 0.6), (0.7, 0.6), (0.66, 0.67), (0.34, 0.67)], c2)
    rect(d, 0.47, 0.67, 0.53, 0.8, dark(c2, 0.35), 0.02)
    ell(d, 0.5, 0.83, 0.05, 0.04, c2)


def g_shield(d, c1, c2):
    outer = [(0.22, 0.16), (0.78, 0.16), (0.78, 0.5), (0.5, 0.88), (0.22, 0.5)]
    poly(d, outer, c1)
    poly(d, [(0.29, 0.23), (0.71, 0.23), (0.71, 0.48), (0.5, 0.78), (0.29, 0.48)], c2)
    poly(d, [(0.5, 0.23), (0.71, 0.23), (0.71, 0.48), (0.5, 0.78)], light(c2, 0.18))
    ell(d, 0.5, 0.44, 0.07, 0.07, light(c1, 0.4))


def g_tower(d, c1, c2):
    poly(d, [(0.28, 0.1), (0.72, 0.1), (0.72, 0.62), (0.5, 0.92), (0.28, 0.62)], c1)
    poly(d, [(0.34, 0.16), (0.66, 0.16), (0.66, 0.6), (0.5, 0.82), (0.34, 0.6)], c2)
    rect(d, 0.47, 0.18, 0.53, 0.72, light(c1, 0.35))
    rect(d, 0.36, 0.34, 0.64, 0.4, light(c1, 0.35))


def g_heart(d, c1, c2):
    ell(d, 0.375, 0.38, 0.19, 0.19, c1)
    ell(d, 0.625, 0.38, 0.19, 0.19, c1)
    poly(d, [(0.2, 0.46), (0.8, 0.46), (0.5, 0.84)], c1)
    ell(d, 0.33, 0.33, 0.06, 0.045, light(c1, 0.55))


def g_flame(d, c1, c2):
    outer = [(0.5, 0.06), (0.62, 0.26), (0.74, 0.4), (0.77, 0.58), (0.7, 0.76), (0.5, 0.9), (0.3, 0.76), (0.23, 0.58), (0.3, 0.42), (0.38, 0.5), (0.42, 0.3)]
    poly(d, outer, c1)
    poly(d, [(0.5, 0.42), (0.6, 0.58), (0.62, 0.72), (0.5, 0.84), (0.38, 0.72), (0.4, 0.58)], c2)
    poly(d, [(0.5, 0.6), (0.55, 0.7), (0.5, 0.8), (0.45, 0.7)], light(c2, 0.5))


def g_drop(d, c1, c2):
    poly(d, [(0.5, 0.08), (0.68, 0.46), (0.32, 0.46)], c1)
    ell(d, 0.5, 0.6, 0.22, 0.22, c1)
    ell(d, 0.42, 0.56, 0.06, 0.09, light(c1, 0.55))


def g_snow(d, c1, c2):
    for a in (0, 60, 120):
        r = math.radians(a)
        dx, dy = math.cos(r) * 0.36, math.sin(r) * 0.36
        line(d, (0.5 - dx, 0.5 - dy), (0.5 + dx, 0.5 + dy), 0.065, c1)
        for s in (-1, 1):
            bx, by = 0.5 + dx * 0.62 * s, 0.5 + dy * 0.62 * s
            for k in (-1, 1):
                r2 = r + k * math.radians(50)
                line(d, (bx, by), (bx - math.cos(r2) * 0.1 * s, by - math.sin(r2) * 0.1 * s), 0.045, c1)
    ell(d, 0.5, 0.5, 0.07, 0.07, c2)


def g_bolt(d, c1, c2):
    poly(d, [(0.58, 0.06), (0.26, 0.54), (0.46, 0.54), (0.38, 0.94), (0.76, 0.4), (0.54, 0.4)], c1)
    poly(d, [(0.57, 0.14), (0.36, 0.5), (0.5, 0.5), (0.45, 0.78), (0.64, 0.42), (0.5, 0.42)], c2)


def g_leaf(d, c1, c2):
    f = lambda t: (0.5 + 0.2 * math.sin(math.pi * t) * 1.0, 0.1 + 0.8 * t)
    right = curve(lambda t: (0.5 + 0.24 * math.sin(math.pi * t), 0.08 + 0.8 * t))
    left = curve(lambda t: (0.5 - 0.24 * math.sin(math.pi * t), 0.08 + 0.8 * t))
    poly(d, right + left[::-1], c1)
    line(d, (0.5, 0.14), (0.5, 0.96), 0.03, c2)
    for y in (0.35, 0.5, 0.65):
        line(d, (0.5, y + 0.08), (0.64, y - 0.04), 0.02, c2)
        line(d, (0.5, y + 0.08), (0.36, y - 0.04), 0.02, c2)


def g_feather(d, c1, c2):
    right = curve(lambda t: (0.3 + 0.38 * t + 0.1 * math.sin(math.pi * t), 0.86 - 0.8 * t - 0.05 * math.sin(math.pi * t)))
    left = curve(lambda t: (0.3 + 0.38 * t - 0.05 * math.sin(math.pi * t), 0.86 - 0.8 * t + 0.1 * math.sin(math.pi * t)))
    poly(d, right + left[::-1], c1)
    line(d, (0.26, 0.92), (0.7, 0.1), 0.03, c2)
    for t in (0.3, 0.5, 0.7):
        x, y = 0.3 + 0.38 * t, 0.86 - 0.8 * t
        line(d, (x, y), (x + 0.08, y + 0.1), 0.02, light(c1, 0.4))


def g_eye(d, c1, c2):
    top = curve(lambda t: (0.1 + 0.8 * t, 0.5 - 0.3 * math.sin(math.pi * t)))
    bot = curve(lambda t: (0.1 + 0.8 * t, 0.5 + 0.3 * math.sin(math.pi * t)))
    poly(d, top + bot[::-1], light(c1, 0.75))
    ell(d, 0.5, 0.5, 0.2, 0.2, c1)
    ell(d, 0.5, 0.5, 0.09, 0.09, dark(c1, 0.8))
    ell(d, 0.45, 0.44, 0.04, 0.04, (255, 255, 255, 255))


def g_boot(d, c1, c2):
    poly(d, [(0.36, 0.1), (0.62, 0.1), (0.62, 0.52), (0.82, 0.6), (0.86, 0.78), (0.2, 0.78), (0.24, 0.52), (0.36, 0.5)], c1)
    rect(d, 0.2, 0.74, 0.88, 0.84, c2, 0.03)
    rect(d, 0.36, 0.1, 0.62, 0.2, light(c1, 0.35), 0.02)
    line(d, (0.4, 0.34), (0.6, 0.34), 0.025, dark(c1, 0.4))


def g_key(d, c1, c2):
    ring = lambda r0, r1: None
    ell(d, 0.34, 0.34, 0.22, 0.22, c1)
    ell(d, 0.34, 0.34, 0.1, 0.1, (0, 0, 0, 0))
    d.ellipse([P(0.24, 0.24), P(0.44, 0.44)], fill=(0, 0, 0, 0))
    poly(d, [(0.47, 0.5), (0.54, 0.43), (0.9, 0.79), (0.83, 0.86)], c1)
    poly(d, [(0.72, 0.68), (0.8, 0.6), (0.86, 0.66), (0.78, 0.74)], c1)
    poly(d, [(0.62, 0.58), (0.7, 0.5), (0.76, 0.56), (0.68, 0.64)], c1)


def g_ring(d, c1, c2):
    ell(d, 0.5, 0.58, 0.28, 0.28, c1)
    ell(d, 0.5, 0.58, 0.19, 0.19, (0, 0, 0, 0))
    d.ellipse([P(0.31, 0.39), P(0.69, 0.77)], fill=(0, 0, 0, 0))
    poly(d, [(0.4, 0.2), (0.6, 0.2), (0.68, 0.34), (0.5, 0.42), (0.32, 0.34)], c2)
    poly(d, [(0.4, 0.2), (0.5, 0.24), (0.5, 0.42), (0.32, 0.34)], light(c2, 0.4))


def g_gem(d, c1, c2):
    poly(d, [(0.28, 0.2), (0.72, 0.2), (0.88, 0.4), (0.5, 0.88), (0.12, 0.4)], c1)
    poly(d, [(0.28, 0.2), (0.5, 0.4), (0.12, 0.4)], light(c1, 0.4))
    poly(d, [(0.28, 0.2), (0.72, 0.2), (0.5, 0.4)], light(c1, 0.2))
    poly(d, [(0.72, 0.2), (0.88, 0.4), (0.5, 0.4)], dark(c1, 0.1))
    poly(d, [(0.5, 0.4), (0.88, 0.4), (0.5, 0.88)], dark(c1, 0.3))
    poly(d, [(0.12, 0.4), (0.5, 0.4), (0.5, 0.88)], dark(c1, 0.12))


def g_crystal(d, c1, c2):
    poly(d, [(0.5, 0.05), (0.7, 0.28), (0.66, 0.74), (0.5, 0.95), (0.34, 0.74), (0.3, 0.28)], c1)
    poly(d, [(0.5, 0.05), (0.7, 0.28), (0.5, 0.36)], light(c1, 0.5))
    poly(d, [(0.5, 0.36), (0.7, 0.28), (0.66, 0.74), (0.5, 0.95)], dark(c1, 0.25))
    poly(d, [(0.3, 0.28), (0.5, 0.36), (0.5, 0.95), (0.34, 0.74)], light(c1, 0.12))
    poly(d, [(0.12, 0.5), (0.22, 0.42), (0.26, 0.64), (0.2, 0.72)], c2)
    poly(d, [(0.88, 0.5), (0.78, 0.42), (0.74, 0.64), (0.8, 0.72)], c2)


def g_coin(d, c1, c2):
    ell(d, 0.5, 0.5, 0.38, 0.38, c1)
    ell(d, 0.5, 0.5, 0.28, 0.28, c2)
    poly(d, [(0.5, 0.28), (0.56, 0.45), (0.74, 0.45), (0.6, 0.55), (0.66, 0.72), (0.5, 0.62), (0.34, 0.72), (0.4, 0.55), (0.26, 0.45), (0.44, 0.45)], light(c1, 0.35))


def g_star(d, c1, c2):
    p = []
    for i in range(10):
        r = 0.42 if i % 2 == 0 else 0.18
        a = -math.pi / 2 + i * math.pi / 5
        p.append((0.5 + r * math.cos(a), 0.54 + r * math.sin(a)))
    poly(d, p, c1)
    p2 = []
    for i in range(10):
        r = 0.26 if i % 2 == 0 else 0.11
        a = -math.pi / 2 + i * math.pi / 5
        p2.append((0.5 + r * math.cos(a), 0.54 + r * math.sin(a)))
    poly(d, p2, c2)


def g_skull(d, c1, c2):
    ell(d, 0.5, 0.42, 0.33, 0.31, c1)
    rect(d, 0.33, 0.55, 0.67, 0.84, c1, 0.05)
    ell(d, 0.37, 0.45, 0.1, 0.11, c2)
    ell(d, 0.63, 0.45, 0.1, 0.11, c2)
    poly(d, [(0.5, 0.55), (0.45, 0.65), (0.55, 0.65)], c2)
    for x in (0.4, 0.5, 0.6):
        line(d, (x, 0.72), (x, 0.84), 0.025, c2)


def g_flask(d, c1, c2):
    poly(d, [(0.42, 0.1), (0.58, 0.1), (0.58, 0.36), (0.8, 0.78), (0.74, 0.9), (0.26, 0.9), (0.2, 0.78), (0.42, 0.36)], light(c1, 0.65))
    poly(d, [(0.34, 0.6), (0.66, 0.6), (0.8, 0.78), (0.74, 0.9), (0.26, 0.9), (0.2, 0.78)], c1)
    rect(d, 0.38, 0.06, 0.62, 0.14, c2, 0.02)
    ell(d, 0.4, 0.74, 0.04, 0.04, light(c1, 0.6))
    ell(d, 0.58, 0.68, 0.03, 0.03, light(c1, 0.6))


def g_bottle(d, c1, c2):
    poly(d, [(0.43, 0.08), (0.57, 0.08), (0.57, 0.3), (0.72, 0.46), (0.72, 0.86), (0.28, 0.86), (0.28, 0.46), (0.43, 0.3)], light(c1, 0.7))
    poly(d, [(0.28, 0.55), (0.72, 0.55), (0.72, 0.86), (0.28, 0.86)], c1)
    for y in (0.62, 0.72):
        pts_ = curve(lambda t, y=y: (0.28 + 0.44 * t, y + 0.03 * math.sin(t * 6.3)))
        d.line(pts(pts_), fill=light(c1, 0.5), width=int(0.025 * S))
    rect(d, 0.4, 0.04, 0.6, 0.12, c2, 0.02)


def g_scroll(d, c1, c2):
    rect(d, 0.16, 0.2, 0.84, 0.8, c1, 0.04)
    ell(d, 0.16, 0.5, 0.045, 0.3, dark(c1, 0.25))
    ell(d, 0.84, 0.5, 0.045, 0.3, dark(c1, 0.25))
    line(d, (0.34, 0.36), (0.66, 0.64), 0.07, c2)
    line(d, (0.66, 0.36), (0.34, 0.64), 0.07, c2)
    for x in (0.26, 0.4, 0.54, 0.7):
        ell(d, x, 0.74, 0.012, 0.012, dark(c1, 0.5))


def g_fist(d, c1, c2):
    rect(d, 0.24, 0.3, 0.76, 0.76, c1, 0.1)
    for i in range(4):
        ell(d, 0.3 + i * 0.13, 0.3, 0.07, 0.09, c1)
        line(d, (0.3 + i * 0.13 + 0.065, 0.24), (0.3 + i * 0.13 + 0.065, 0.42), 0.012, dark(c1, 0.4))
    poly(d, [(0.2, 0.46), (0.32, 0.5), (0.32, 0.72), (0.2, 0.68)], light(c1, 0.15))
    rect(d, 0.3, 0.76, 0.7, 0.9, c2, 0.03)


def g_sun(d, c1, c2):
    for i in range(12):
        a = math.radians(i * 30)
        poly(d, [(0.5 + 0.28 * math.cos(a - 0.12), 0.5 + 0.28 * math.sin(a - 0.12)), (0.5 + 0.44 * math.cos(a), 0.5 + 0.44 * math.sin(a)), (0.5 + 0.28 * math.cos(a + 0.12), 0.5 + 0.28 * math.sin(a + 0.12))], c1)
    ell(d, 0.5, 0.5, 0.25, 0.25, c1)
    ell(d, 0.5, 0.5, 0.17, 0.17, c2)
    ell(d, 0.44, 0.44, 0.05, 0.05, light(c2, 0.6))


def g_banner(d, c1, c2):
    rect(d, 0.24, 0.08, 0.3, 0.94, c2, 0.02)
    ell(d, 0.27, 0.08, 0.05, 0.05, light(c2, 0.4))
    poly(d, [(0.3, 0.14), (0.84, 0.14), (0.72, 0.3), (0.84, 0.46), (0.3, 0.46)], c1)
    poly(d, [(0.3, 0.14), (0.84, 0.14), (0.8, 0.2), (0.3, 0.2)], light(c1, 0.3))
    poly(d, [(0.42, 0.26), (0.55, 0.26), (0.55, 0.38), (0.42, 0.38)], light(c1, 0.5))


def g_prism(d, c1, c2):
    poly(d, [(0.5, 0.18), (0.8, 0.74), (0.2, 0.74)], light(c1, 0.55))
    poly(d, [(0.5, 0.18), (0.8, 0.74), (0.5, 0.74)], light(c1, 0.3))
    poly(d, [(0.5, 0.3), (0.67, 0.64), (0.33, 0.64)], c1)
    line(d, (0.05, 0.5), (0.36, 0.5), 0.04, (255, 255, 255, 255))
    for i, c in enumerate(("#ff5d5d", "#ffb347", "#fff06a", "#6dff7a", "#5db8ff", "#b27dff")):
        line(d, (0.62, 0.5 + 0.0), (0.95, 0.34 + i * 0.065), 0.04, col(c))


def g_cloak(d, c1, c2):
    poly(d, [(0.5, 0.08), (0.7, 0.2), (0.82, 0.88), (0.5, 0.78), (0.18, 0.88), (0.3, 0.2)], c1)
    ell(d, 0.5, 0.3, 0.14, 0.16, dark(c1, 0.7))
    poly(d, [(0.5, 0.08), (0.7, 0.2), (0.5, 0.18)], light(c1, 0.3))
    poly(d, [(0.5, 0.18), (0.3, 0.2), (0.5, 0.08)], light(c1, 0.15))
    ell(d, 0.5, 0.56, 0.035, 0.035, c2)


def g_mountain(d, c1, c2):
    poly(d, [(0.08, 0.84), (0.38, 0.22), (0.56, 0.5), (0.68, 0.34), (0.94, 0.84)], c1)
    poly(d, [(0.38, 0.22), (0.46, 0.38), (0.38, 0.34), (0.3, 0.4)], light(c1, 0.7))
    poly(d, [(0.38, 0.22), (0.56, 0.5), (0.5, 0.84), (0.34, 0.84)], dark(c1, 0.2))
    d.line(pts([(0.5, 0.5), (0.56, 0.62), (0.48, 0.7), (0.56, 0.84)]), fill=c2, width=int(0.035 * S))


def g_anvil(d, c1, c2):
    poly(d, [(0.1, 0.3), (0.84, 0.3), (0.74, 0.46), (0.62, 0.5), (0.66, 0.66), (0.78, 0.74), (0.78, 0.86), (0.22, 0.86), (0.22, 0.74), (0.34, 0.66), (0.38, 0.5), (0.24, 0.46)], c1)
    poly(d, [(0.1, 0.3), (0.84, 0.3), (0.82, 0.37), (0.12, 0.37)], light(c1, 0.35))
    poly(d, [(0.1, 0.3), (0.0, 0.34), (0.2, 0.44)], c1)
    rect(d, 0.22, 0.8, 0.78, 0.9, c2, 0.02)


def g_chevrons(d, c1, c2):
    for k, y in enumerate((0.16, 0.4, 0.64)):
        poly(d, [(0.2, y), (0.5, y + 0.2), (0.8, y), (0.8, y + 0.1), (0.5, y + 0.3), (0.2, y + 0.1)], c1 if k < 2 else c2)


def g_dome(d, c1, c2):
    d.pieslice([P(0.1, 0.22), P(0.9, 1.02)], 180, 360, fill=mix(c1, (255, 255, 255, 255), 0.0))
    d.pieslice([P(0.2, 0.32), P(0.8, 0.92)], 180, 360, fill=c2)
    rect(d, 0.08, 0.62, 0.92, 0.72, c1, 0.02)
    line(d, (0.5, 0.34), (0.5, 0.62), 0.02, light(c2, 0.4))
    line(d, (0.34, 0.42), (0.28, 0.62), 0.02, light(c2, 0.4))
    line(d, (0.66, 0.42), (0.72, 0.62), 0.02, light(c2, 0.4))


def g_chain(d, c1, c2):
    for cx, cy, c in ((0.38, 0.42, c1), (0.62, 0.58, c2)):
        ell(d, cx, cy, 0.24, 0.17, c)
        d.ellipse([P(cx - 0.15, cy - 0.09), P(cx + 0.15, cy + 0.09)], fill=(0, 0, 0, 0))


def g_hammer(d, c1, c2):
    rect(d, 0.45, 0.36, 0.55, 0.92, dark(c2, 0.1), 0.02)
    poly(d, [(0.16, 0.12), (0.84, 0.12), (0.88, 0.4), (0.12, 0.4)], c1)
    poly(d, [(0.16, 0.12), (0.84, 0.12), (0.85, 0.2), (0.15, 0.2)], light(c1, 0.35))
    rect(d, 0.36, 0.12, 0.42, 0.4, dark(c1, 0.3))
    rect(d, 0.58, 0.12, 0.64, 0.4, dark(c1, 0.3))


def g_burst(d, c1, c2):
    p = []
    for i in range(16):
        r = 0.46 if i % 2 == 0 else 0.22
        a = i * math.pi / 8
        p.append((0.5 + r * math.cos(a), 0.5 + r * math.sin(a)))
    poly(d, p, c1)
    ell(d, 0.5, 0.5, 0.17, 0.17, c2)


def g_cross(d, c1, c2):
    rect(d, 0.4, 0.16, 0.6, 0.84, c1, 0.04)
    rect(d, 0.16, 0.4, 0.84, 0.6, c1, 0.04)
    rect(d, 0.44, 0.2, 0.52, 0.8, light(c1, 0.35))


def g_wellring(d, c1, c2):
    ell(d, 0.5, 0.5, 0.4, 0.4, c1)
    ell(d, 0.5, 0.5, 0.3, 0.3, c2)
    ell(d, 0.5, 0.5, 0.2, 0.2, dark(c2, 0.5))
    ell(d, 0.5, 0.5, 0.1, 0.1, dark(c2, 0.8))


def g_swirl(d, c1, c2):
    for k, w in ((0, 0.09), (1, 0.07)):
        p = curve(lambda t, k=k: (0.5 + (0.08 + 0.34 * t) * math.cos(t * 9 + k * 3.2), 0.5 + (0.08 + 0.34 * t) * math.sin(t * 9 + k * 3.2)), 60)
        d.line(pts(p), fill=c1 if k == 0 else c2, width=int(w * S), joint="curve")


def g_hand(d, c1, c2):
    rect(d, 0.3, 0.48, 0.7, 0.86, c1, 0.08)
    for i, h in enumerate((0.18, 0.1, 0.06, 0.12)):
        rect(d, 0.3 + i * 0.1, 0.14 + (0.08 if i in (0, 3) else 0.0) + h * 0.0, 0.38 + i * 0.1, 0.56, c1, 0.04)
    poly(d, [(0.22, 0.62), (0.34, 0.54), (0.34, 0.7)], c1)
    line(d, (0.84, 0.3), (0.94, 0.2), 0.03, c2)
    line(d, (0.84, 0.46), (0.96, 0.46), 0.03, c2)


def g_ox(d, c1, c2):
    poly(d, [(0.24, 0.26), (0.76, 0.26), (0.9, 0.42), (0.1, 0.42)], c1)
    ell(d, 0.5, 0.56, 0.28, 0.28, c1)
    ell(d, 0.38, 0.5, 0.06, 0.06, c2)
    ell(d, 0.62, 0.5, 0.06, 0.06, c2)


def g_arrowup(d, c1, c2):
    poly(d, [(0.5, 0.08), (0.84, 0.46), (0.62, 0.46), (0.62, 0.9), (0.38, 0.9), (0.38, 0.46), (0.16, 0.46)], c1)
    poly(d, [(0.5, 0.08), (0.5, 0.9), (0.38, 0.9), (0.38, 0.46), (0.16, 0.46)], light(c1, 0.3))


def g_blooddrops(d, c1, c2):
    for (x, y, s) in ((0.36, 0.34, 1.0), (0.66, 0.5, 0.8), (0.42, 0.72, 0.65)):
        poly(d, [(x, y - 0.22 * s), (x + 0.13 * s, y + 0.02 * s), (x - 0.13 * s, y + 0.02 * s)], c1)
        ell(d, x, y + 0.08 * s, 0.14 * s, 0.14 * s, c1)
        ell(d, x - 0.04 * s, y + 0.05 * s, 0.035 * s, 0.05 * s, light(c1, 0.55))


GLYPHS = {n[2:]: f for n, f in globals().items() if n.startswith("g_")}


# ------------------------------------------------------------------ composition
def layer_for(parts):
    """parts: list of (glyph, c1, c2, scale, dx, dy, rot_degrees)"""
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for glyph, c1, c2, sc, dx, dy, rot in parts:
        g = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        GLYPHS[glyph](ImageDraw.Draw(g), col(c1), col(c2))
        if rot:
            g = g.rotate(rot, resample=Image.BICUBIC, center=(S / 2, S / 2))
        if sc != 1.0 or dx or dy:
            sz = int(S * sc)
            g = g.resize((sz, sz), Image.LANCZOS)
            canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
            canvas.alpha_composite(g, (int(S / 2 - sz / 2 + dx * S), int(S / 2 - sz / 2 + dy * S)))
            g = canvas
        layer.alpha_composite(g)
    return layer


def shade(layer):
    """Volume: lighter at the top, darker at the bottom, on the glyph's pixels only."""
    px = layer.load()
    for y in range(S):
        k = 1.14 - 0.34 * (y / S)
        for x in range(S):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), a)
    return layer


def outline(layer, w=11, color=(14, 10, 20, 255)):
    a = layer.split()[3]
    grown = a.filter(ImageFilter.MaxFilter(w * 2 + 1))
    out = Image.new("RGBA", (S, S), color)
    out.putalpha(grown)
    return out


def badge(accent):
    c = col(accent)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    top, bot = mix(dark(c, 0.62), (30, 28, 40, 255), 0.3), dark(c, 0.86)
    for y in range(S):
        t = y / S
        d.line([(0, y), (S, y)], fill=mix(top, bot, t))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([14, 14, S - 14, S - 14], radius=84, fill=255)
    img.putalpha(mask)
    rim = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rim)
    rd.rounded_rectangle([14, 14, S - 14, S - 14], radius=84, outline=mix(c, (255, 255, 255, 255), 0.15), width=14)
    img.alpha_composite(rim)
    # a soft glow of the accent behind the glyph
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([S * 0.18, S * 0.18, S * 0.82, S * 0.82], fill=mix(c, (0, 0, 0, 255), 0.45)[:3] + (120,))
    glow = glow.filter(ImageFilter.GaussianBlur(40))
    base = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    base.alpha_composite(img)
    base.alpha_composite(Image.composite(glow, Image.new("RGBA", (S, S), (0, 0, 0, 0)), mask))
    return base


def icon(accent, parts):
    base = badge(accent)
    g = shade(layer_for(parts))
    ol = outline(g)
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sa = ol.split()[3].filter(ImageFilter.GaussianBlur(10))
    shadow.putalpha(sa.point(lambda v: int(v * 0.55)))
    shadow = ImageChops.offset(shadow, 0, 12)
    base.alpha_composite(shadow)
    base.alpha_composite(ol)
    base.alpha_composite(g)
    return base.resize((FINAL, FINAL), Image.LANCZOS)


def symbol(parts_fn):
    """A white, minimal symbol on transparent."""
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    parts_fn(ImageDraw.Draw(layer))
    return layer.resize((FINAL, FINAL), Image.LANCZOS)


W = (255, 255, 255, 255)


def s_generic(d):  # universal: a ring with a plus
    d.ellipse([P(0.1, 0.1), P(0.9, 0.9)], outline=W, width=int(0.07 * S))
    rect(d, 0.46, 0.28, 0.54, 0.72, W)
    rect(d, 0.28, 0.46, 0.72, 0.54, W)


def s_class(d):  # character specific: a crest (shield) with a star
    poly(d, [(0.18, 0.12), (0.82, 0.12), (0.82, 0.5), (0.5, 0.92), (0.18, 0.5)], W)
    poly(d, [(0.26, 0.2), (0.74, 0.2), (0.74, 0.48), (0.5, 0.8), (0.26, 0.48)], (0, 0, 0, 0))
    d.polygon(pts([(0.26, 0.2), (0.74, 0.2), (0.74, 0.48), (0.5, 0.8), (0.26, 0.48)]), fill=(0, 0, 0, 0))
    star = []
    for i in range(10):
        r = 0.17 if i % 2 == 0 else 0.07
        a = -math.pi / 2 + i * math.pi / 5
        star.append((0.5 + r * math.cos(a), 0.44 + r * math.sin(a)))
    poly(d, star, W)


def s_alteration(d):  # two arrows chasing round
    for a0 in (200, 20):
        d.arc([P(0.14, 0.14), P(0.86, 0.86)], a0, a0 + 130, fill=W, width=int(0.075 * S))
    for ang, flip in ((330, 1), (150, -1)):
        r = math.radians(ang)
        cx, cy = 0.5 + 0.36 * math.cos(r), 0.5 + 0.36 * math.sin(r)
        t = r + math.pi / 2 * flip
        poly(d, [(cx + 0.14 * math.cos(t), cy + 0.14 * math.sin(t)), (cx + 0.09 * math.cos(r), cy + 0.09 * math.sin(r)), (cx - 0.09 * math.cos(r), cy - 0.09 * math.sin(r))], W)


def s_conditional(d):  # a fork: one road into two
    line(d, (0.5, 0.92), (0.5, 0.5), 0.08, W)
    line(d, (0.5, 0.5), (0.24, 0.2), 0.08, W)
    line(d, (0.5, 0.5), (0.76, 0.2), 0.08, W)
    poly(d, [(0.1, 0.3), (0.3, 0.06), (0.36, 0.34)], W)
    poly(d, [(0.9, 0.3), (0.7, 0.06), (0.64, 0.34)], W)


def s_sidegrade(d):  # the scales: risk and reward
    line(d, (0.5, 0.16), (0.5, 0.88), 0.07, W)
    line(d, (0.14, 0.3), (0.86, 0.3), 0.07, W)
    rect(d, 0.3, 0.84, 0.7, 0.92, W, 0.02)
    for cx in (0.22, 0.78):
        line(d, (cx, 0.3), (cx - 0.12, 0.58), 0.03, W)
        line(d, (cx, 0.3), (cx + 0.12, 0.58), 0.03, W)
        d.pieslice([P(cx - 0.16, 0.44), P(cx + 0.16, 0.76)], 0, 180, fill=W)


def s_relic(d):  # a faceted gem
    poly(d, [(0.26, 0.18), (0.74, 0.18), (0.92, 0.4), (0.5, 0.92), (0.08, 0.4)], W)
    for a, b in (((0.08, 0.4), (0.92, 0.4)), ((0.26, 0.18), (0.38, 0.4)), ((0.74, 0.18), (0.62, 0.4)), ((0.38, 0.4), (0.5, 0.92)), ((0.62, 0.4), (0.5, 0.92))):
        line(d, a, b, 0.025, (0, 0, 0, 0))
        d.line([P(*a), P(*b)], fill=(0, 0, 0, 0), width=int(0.03 * S))


def s_multi(d):  # two heads and shoulders: for a party
    for cx, scale in ((0.36, 1.0), (0.66, 0.9)):
        r = 0.16 * scale
        d.ellipse([P(cx - r, 0.2), P(cx + r, 0.2 + 2 * r)], fill=W)
        d.pieslice([P(cx - 0.24 * scale, 0.56), P(cx + 0.24 * scale, 1.1)], 180, 360, fill=W)


def s_skip(d):  # leave it: an arrow out
    line(d, (0.16, 0.5), (0.74, 0.5), 0.08, W)
    poly(d, [(0.6, 0.22), (0.92, 0.5), (0.6, 0.78)], W)
    line(d, (0.16, 0.16), (0.16, 0.84), 0.07, W)


# ------------------------------------------------------------------ the icons
def one(glyph, c1, c2, sc=0.9, dx=0, dy=0, rot=0):
    return (glyph, c1, c2, sc, dx, dy, rot)


ABILITIES = {
    # swordsman
    "charge": ("#ff7a2a", [one("sword", "#dfe6f0", "#c9922e", 0.9, 0, 0, 38), one("flame", "#ff7a2a", "#ffd23a", 0.5, 0.16, -0.18)]),
    "heave": ("#9fb8d8", [one("hammer", "#aab6c8", "#8a5a30", 0.95, 0, 0, -28), one("burst", "#fff3b0", "#ffd23a", 0.34, 0.2, 0.22)]),
    # warden
    "dash": ("#4a8dff", [one("shield", "#5f9bff", "#2f5cc4", 0.82, 0.06, 0), one("chevrons", "#bfe0ff", "#ffffff", 0.4, -0.26, 0.02, -90)]),
    "bash": ("#ffd24a", [one("shield", "#e0b040", "#7a5a1a", 0.84), one("burst", "#fff6c0", "#ffffff", 0.5, 0.12, -0.12)]),
    # vitalist
    "heal": ("#48d67a", [one("heart", "#ff5c78", "#ffb0b8", 0.86), one("cross", "#7dffa6", "#ffffff", 0.4, 0.0, 0.0)]),
    "rupture": ("#e34a6a", [one("burst", "#ff4a6a", "#7a0f2a", 0.95), one("drop", "#ffd0d8", "#ffffff", 0.4)]),
    # rogue
    "recall": ("#e8c050", [one("dagger", "#d8e0ea", "#c9922e", 0.82, 0, 0, 35), one("swirl", "#ffe08a", "#fff6c0", 0.55, 0.18, 0.18)]),
    "tether": ("#e8c050", [one("dagger", "#d8e0ea", "#c9922e", 0.7, 0.08, -0.08, 35), one("chain", "#ffe08a", "#c9922e", 0.62, -0.14, 0.18)]),
    # aegis
    "barrier": ("#8fd6ff", [one("dome", "#9fdcff", "#4aa0e0", 0.95), one("star", "#ffffff", "#d6f1ff", 0.3, 0, 0.06)]),
    "burden": ("#f0b64a", [one("chain", "#ffd36a", "#e8923a", 0.9), one("heart", "#ff6a7a", "#ffc0c8", 0.3, 0, 0.0)]),
    # elementalist
    "blizzard": ("#6fd0ff", [one("snow", "#d8f4ff", "#7fd8ff", 0.95), one("swirl", "#6fd0ff", "#b8ecff", 0.5)]),
    "firestorm": ("#ff7a2a", [one("flame", "#ff6a1a", "#ffd23a", 0.95), one("swirl", "#ff9a3a", "#ffe08a", 0.5)]),
    "snap": ("#b48cff", [one("bolt", "#d4b8ff", "#fff6ff", 0.9, 0, 0, 10), one("crystal", "#9ad8ff", "#e0f6ff", 0.42, -0.2, 0.22)]),
}

RELICS = {
    "relic_leap": ("#4cc86a", [one("boot", "#8a5a30", "#4cc86a", 0.85), one("chevrons", "#c8ffd0", "#ffffff", 0.3, 0.26, -0.22, 180)]),
    "relic_anvil": ("#c88a4a", [one("anvil", "#8a94a6", "#c88a4a", 0.9)]),
    "relic_quicksilver": ("#bcd0e8", [one("drop", "#cfd9e8", "#8aa0c0", 0.8, 0, 0.02), one("bolt", "#ffffff", "#ffffff", 0.34, 0.2, -0.18)]),
    "relic_amphibian": ("#3ac8c0", [one("bottle", "#3ac8c0", "#8a5a30", 0.8), one("drop", "#c8fff6", "#ffffff", 0.3, 0.0, 0.08)]),
    "relic_flask": ("#e0405a", [one("flask", "#e0405a", "#8a5a30", 0.9)]),
    "relic_prodigy": ("#ffd23a", [one("star", "#ffd23a", "#fff0a0", 0.9), one("flame", "#ff7a2a", "#ffd23a", 0.3, 0.22, 0.24)]),
    "relic_treasure": ("#d8b070", [one("scroll", "#e8d0a0", "#c8402a", 0.9)]),
    "relic_fount": ("#5ee08a", [one("heart", "#5ee08a", "#c8ffd0", 0.84), one("drop", "#c8f4ff", "#ffffff", 0.3, 0.0, 0.06)]),
    "relic_spring": ("#5ab8ff", [one("drop", "#5ab8ff", "#c8e8ff", 0.88), one("swirl", "#c8e8ff", "#ffffff", 0.3, 0, 0.1)]),
    "relic_locksmith": ("#ffd23a", [one("key", "#ffd23a", "#fff0a0", 0.9, 0, 0, 0)]),
    "relic_deeper": ("#8a6aff", [one("wellring", "#4a3a8a", "#8a6aff", 0.85), one("eye", "#d8c8ff", "#ffffff", 0.35)]),
    "relic_shortcut": ("#ff9a4a", [one("chevrons", "#ff9a4a", "#ffd23a", 0.9)]),
    "relic_assassin": ("#e0405a", [one("dagger", "#d8e0ea", "#8a1a2a", 0.92, 0, 0, 30), one("drop", "#e0405a", "#ffb0b8", 0.3, 0.2, 0.2)]),
    "relic_sniper": ("#4ac8ff", [one("eye", "#4ac8ff", "#ffffff", 0.95)]),
    "relic_brawler": ("#ff8a3a", [one("fist", "#ffb070", "#8a5a30", 0.9)]),
    "relic_bait": ("#ff5ca8", [one("gem", "#ff5ca8", "#ffd0e8", 0.85)]),
    "relic_shroud": ("#7a8aa8", [one("cloak", "#6a7a98", "#c8d4e8", 0.92)]),
    "relic_e_blood": ("#e0405a", [one("blooddrops", "#e0405a", "#ffb0b8", 0.92)]),
    "relic_e_ember": ("#ff7a2a", [one("heart", "#ff5c2a", "#ffd23a", 0.82), one("flame", "#ffd23a", "#fff6c0", 0.4, 0, -0.02)]),
    "relic_e_rime": ("#6fd0ff", [one("heart", "#6fb8e8", "#d8f4ff", 0.82), one("snow", "#ffffff", "#d8f4ff", 0.42, 0, -0.02)]),
    "relic_e_vessel": ("#b48cff", [one("flask", "#b48cff", "#8a5a30", 0.9), one("drop", "#e8d8ff", "#ffffff", 0.22, 0.2, -0.28)]),
    "relic_v_fury": ("#e0405a", [one("skull", "#f0e0d0", "#8a1a2a", 0.86), one("flame", "#ff5c2a", "#ffd23a", 0.3, 0.26, 0.26)]),
    "relic_v_siphon": ("#48d67a", [one("crystal", "#48d67a", "#c8ffd0", 0.92)]),
    "relic_v_well": ("#48d67a", [one("wellring", "#7a6a5a", "#48d67a", 0.9)]),
    "relic_s_fire": ("#ff7a2a", [one("flame", "#ff6a1a", "#ffd23a", 0.92)]),
    "relic_s_quake": ("#c8a06a", [one("mountain", "#9a7a4a", "#2a1a10", 0.92)]),
    "relic_s_sash": ("#e8f0d8", [one("feather", "#e8f0d8", "#8ac8a0", 0.92)]),
    "relic_w_siphon": ("#5f9bff", [one("shield", "#5f9bff", "#2f5cc4", 0.86), one("drop", "#ff5c78", "#ffd0d8", 0.3, 0.0, 0.04)]),
    "relic_w_ram": ("#c8a06a", [one("hammer", "#aab6c8", "#8a5a30", 0.9, 0, 0, 90), one("chevrons", "#ffd23a", "#ffffff", 0.3, -0.26, 0.0, -90)]),
    "relic_w_plate": ("#9fb8d8", [one("tower", "#9fb8d8", "#4a6a98", 0.92)]),
    "relic_w_banner": ("#e0405a", [one("banner", "#e0405a", "#c9a24a", 0.92)]),
    "relic_a_crest": ("#ffd23a", [one("sun", "#ffb02a", "#fff0a0", 0.95)]),
    "relic_a_mantle": ("#e8c050", [one("cloak", "#c8402a", "#ffd23a", 0.9), one("cross", "#ffe08a", "#ffffff", 0.2, 0, 0.14)]),
    "relic_a_prism": ("#b48cff", [one("prism", "#dff0ff", "#ffffff", 0.95)]),
    "relic_a_sea": ("#3a9aff", [one("bottle", "#3a9aff", "#8a5a30", 0.92)]),
    "relic_r_lone": ("#e8e8f0", [one("dagger", "#e8e8f0", "#7a8aa8", 0.95, 0, 0, 20)]),
    "relic_r_hemo": ("#e0405a", [one("dagger", "#d8e0ea", "#6a1a2a", 0.8, -0.08, -0.04, 25), one("blooddrops", "#e0405a", "#ffb0b8", 0.52, 0.2, 0.16)]),
    "relic_r_wound": ("#c84a6a", [one("heart", "#c84a6a", "#ffc0c8", 0.84), one("cross", "#e8d8b0", "#ffffff", 0.34, 0.02, 0.02)]),
    "relic_r_quick": ("#ffd23a", [one("hand", "#f0c898", "#8a5a30", 0.88), one("bolt", "#ffd23a", "#ffffff", 0.28, 0.24, -0.24)]),
}

SYMBOLS = {
    "cat_generic": s_generic, "cat_class": s_class, "cat_alteration": s_alteration, "cat_conditional": s_conditional,
    "cat_sidegrade": s_sidegrade, "cat_relic": s_relic, "cat_skip": s_skip, "cat_multi": s_multi,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    n = 0
    for name, (accent, parts) in ABILITIES.items():
        icon(accent, parts).save(os.path.join(OUT, f"ability_{name}.png"))
        n += 1
    for name, (accent, parts) in RELICS.items():
        icon(accent, parts).save(os.path.join(OUT, f"{name}.png"))
        n += 1
    for name, fn in SYMBOLS.items():
        symbol(fn).save(os.path.join(OUT, f"{name}.png"))
        n += 1
    print(f"{n} icons in {os.path.normpath(OUT)}")


if __name__ == "__main__":
    main()
