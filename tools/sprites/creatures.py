"""Enemy, boss and critter rigs. Every creature faces right in its build function, origin at the
gameplay position (body center), y down, world pixels. See rig.py for the projection."""
import math
from rig import (hexc, lighten, darken, mix, ellipse, circle, poly, limb, line, glow, rot, add,
                 polar, ik2, keys, lerp, smooth, ease_out, ease_in, transform, Sheet)

TAU = math.tau


def K(base):
    return lambda u, table, ease=smooth: keys(u, table, ease, base=base)


def P_(base, pose):
    p = dict(base)
    p.update(pose)
    return p


def lerp_pt(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def death_fade(parts, u, fall=0.0, spin=0.0):
    return transform(parts, spin * u, (0, 0), offset=(0, fall * u))


# ============================================================================================ BAT
BAT_BODY = hexc('4d3a5c')
BAT_WING = hexc('3b2b49')
BAT_EYE = hexc('ff4a3a')
BAT_BASE = dict(flap=0.0, fold=0.0, pitch=0.0, by=0.0, curl=0.0, spin=0.0, mouth=0.0)
bk = K(BAT_BASE)


def bat(p):
    p = P_(BAT_BASE, p)
    parts = []
    sh = (0.0, -1.0 + p['by'])

    def wing(z, phase_lag, col):
        fold = p['fold']
        f = math.sin(p['flap'] - phase_lag) * (1 - fold)
        # side view: the wing sweeps up and down from the shoulder, trailing edge back to the body
        el = add(sh, (-1.0 - 1.5 * fold, -7.5 * f - fold * 1.0))
        tip = add(sh, (-4.0 - 3.5 * (1 - abs(f)) - fold * 1.0, -15.5 * f + fold * 4.0))
        tip2 = add(sh, (-9.5 + fold * 6.0, -9.0 * f + 1.5 + fold * 4.5))
        back = (-4.5, 3.0 + p['by'])
        pts = [sh, el, tip, add(tip, (-1.2, 2.6 * (1 if f >= 0 else -1) * 0.8 + 1.0)), tip2, add(lerp_pt(tip2, back, 0.5), (0.5, 1.2)), back]
        parts.append(poly(pts, col, z=z, thick=0.35))
        parts.append(line([sh, el, tip], 0.9, darken(col, 0.35), z=z + 0.05))
        parts.append(line([el, tip2], 0.6, darken(col, 0.35), z=z + 0.05))

    wing(-2.2, 0.35, darken(BAT_WING, 0.1))
    parts.append(ellipse((0, 1 + p['by']), 4.4, 5.2, BAT_BODY, z=0, rot=0.2))
    head = (2.6, -3.2 + p['by'])
    parts.append(circle(head, 3.3, BAT_BODY, z=0.2))
    for ez in (1.4, -1.4):
        parts.append(poly([add(head, (-1.5, -2.2)), add(head, (-0.3, -6.2)), add(head, (0.8, -2.4))], darken(BAT_BODY, 0.15), z=ez * 0.6, thick=0.4))
        parts.append(circle(add(head, (1.8, -0.4)), 0.75, BAT_EYE, z=ez, outline=False, shade=False))
    parts.append(glow(add(head, (1.8, -0.4)), 3.0, hexc('ff3020', 0.35), z=2))
    parts.append(line([add(head, (2.6, 1.2)), add(head, (3.3, 1.2 + p['mouth'] * 1.5))], 0.7, hexc('f0e6e0'), z=1.5, bias=0.5))
    parts.append(line([(-1, 5.5 + p['by']), (-1.5, 7.5 + p['by'])], 0.8, darken(BAT_BODY, 0.3), z=0.5))
    parts.append(line([(1, 5.5 + p['by']), (1.2, 7.5 + p['by'])], 0.8, darken(BAT_BODY, 0.3), z=-0.5))
    wing(2.2, 0.0, BAT_WING)
    transform(parts, p['pitch'], (0, 0))
    if p['spin']:
        transform(parts, p['spin'], (0, 0))
    return parts


def make_bat():
    sh = Sheet('bat', 44, 36, origin=(22, 18), scale=2.0, outline_w=0.8)
    sh.add_anim('roost', 24, True, lambda u, i: bat(dict(fold=1, spin=math.pi + 0.08 * math.sin(u * TAU), by=0)))
    sh.add_anim('wake', 6, False, lambda u, i: bat(bk(u, [(0, dict(fold=1, spin=math.pi)), (0.5, dict(fold=0.5, spin=math.pi * 0.35, flap=1.0)), (1, dict(fold=0, spin=0, flap=2.5))])))
    sh.add_anim('fly', 8, True, lambda u, i: bat(dict(flap=u * TAU, by=-math.sin(u * TAU) * 1.2, pitch=0.1)))
    sh.add_turns(4, lambda u, i: bat(dict(flap=u * TAU * 0.5 + 1, by=0)))
    sh.add_anim('dive', 6, True, lambda u, i: bat(dict(flap=1.2 + 0.3 * math.sin(u * TAU), fold=0.45, pitch=0.55, mouth=1)))
    sh.add_anim('hurt', 5, False, lambda u, i: bat(bk(u, [(0, dict(pitch=-0.6, fold=0.6, flap=2, mouth=1)), (1, dict(pitch=0, fold=0, flap=4))])))
    sh.add_anim('death', 12, False, lambda u, i: death_fade(bat(dict(fold=lerp(0.2, 0.9, u), flap=u * 8, spin=u * 5, mouth=1)), u, fall=8))
    return sh


# ============================================================================================ FROG
FROG = hexc('4f9d3e')
FROG_BELLY = hexc('d3d98a')
FROG_SPOT = hexc('2f6b27')
FROG_BASE = dict(crouch=0.0, ext=0.0, open=0.0, sac=0.0, pitch=0.0, by=0.0, swim=0.0, belly_up=0.0, blink=0.0, kick=0.0)
fk = K(FROG_BASE)


def frog(p):
    p = P_(FROG_BASE, p)
    parts = []
    cr, ext = p['crouch'], p['ext']
    by = p['by'] + cr * 2.0
    body_c = (0.0, 1.0 + by)
    # hind legs (far then near)
    for z, col in ((-3.0, darken(FROG, 0.12)), (3.0, FROG)):
        hip = add(body_c, (-4.5, 2.5))
        if ext > 0.01:
            kick = p['kick'] * (1 if z > 0 else -1)
            foot = add(hip, (-8.5 * ext - 1 + kick, 4.5 * (1 - ext) + 2 + ext * 1.5))
            knee = add(hip, (lerp(1.5, -4.0, ext), lerp(1.5, 2.5, ext)))
        else:
            foot = add(hip, (2.5 - cr * 0.5, 5.5 - cr * 1.0 + (0 if p['swim'] == 0 else 0)))
            knee = add(hip, (3.5, 1.0))
        parts.append(ellipse(add(hip, (1.0, 0.5)), 4.0, 3.2, col, z=z, rot=-0.3))
        parts.append(limb([hip, knee, foot], [3.0, 2.4, 2.0], col, z=z + 0.1))
        parts.append(ellipse(add(foot, (1.2, 0.3)), 2.4, 1.0, col, z=z + 0.2))
    parts.append(ellipse(body_c, 8.0, 5.8 - cr * 0.6, FROG, z=0, rot=-0.15 - 0.1 * cr, thick=0.9))
    parts.append(ellipse(add(body_c, (1.6, 2.2)), 5.6, 3.4 + p['sac'] * 1.2, FROG_BELLY, z=0, bias=0.6, thick=0.9))
    if p['sac'] > 0.05:
        parts.append(ellipse(add(body_c, (4.8, 2.8)), 2.4 * p['sac'] + 0.5, 2.2 * p['sac'] + 0.5, lighten(FROG_BELLY, 0.2), z=0, bias=0.9))
    for (sx, sy, r) in ((-3, -3.5, 1.3), (-0.5, -4.5, 1.0), (-5.5, -1.0, 1.1)):
        parts.append(circle(add(body_c, (sx, sy)), r, FROG_SPOT, z=0, bias=0.4, outline=False))
    # mouth
    o = p['open']
    mc = add(body_c, (7.6, 0.8))
    parts.append(poly([add(mc, (-4.5, 0)), add(mc, (0.6, -0.6)), add(mc, (0.4, 0.4 + o * 3.2)), add(mc, (-3.5, 0.6 + o * 1.2))],
                      hexc('8c2f3a') if o > 0.1 else darken(FROG, 0.4), z=0, bias=0.8, thick=0.6))
    # front arms
    for z in (-2.5, 2.5):
        sh = add(body_c, (4.0, 3.0))
        hand = add(sh, (1.5 + ext * 3.0, 3.0 - ext * 1.5 + cr * 0.5))
        parts.append(limb([sh, hand], [1.8, 1.5], FROG if z > 0 else darken(FROG, 0.12), z=z))
    # eyes on top
    for z in (-2.2, 2.2):
        ec = add(body_c, (4.0, -5.0))
        parts.append(circle(ec, 2.4, FROG, z=z * 0.8))
        if p['blink'] > 0.5:
            parts.append(line([add(ec, (-1.3, 0)), add(ec, (1.4, 0))], 0.7, darken(FROG, 0.5), z=z * 0.8 + 0.2))
        else:
            parts.append(circle(add(ec, (0.4, -0.2)), 1.6, hexc('f3e79a'), z=z * 0.8 + 0.2))
            parts.append(ellipse(add(ec, (0.8, -0.2)), 0.7, 1.1, hexc('141414'), z=z * 0.8 + 0.3, outline=False, shade=False))
    transform(parts, p['pitch'], (0, 5))
    if p['belly_up']:
        transform(parts, math.pi * p['belly_up'], (0, 2))
    return parts


def make_frog():
    sh = Sheet('frog', 40, 34, origin=(20, 19), scale=2.0, outline_w=0.8)
    sh.add_anim('idle', 24, True, lambda u, i: frog(dict(by=math.sin(u * TAU) * 0.3, sac=0.15 + 0.1 * math.sin(u * TAU * 2), blink=1 if i in (15, 16) else 0)))
    sh.add_anim('croak', 12, False, lambda u, i: frog(dict(sac=abs(math.sin(u * TAU)) * 1.0, open=0.1, by=-0.3 * math.sin(u * TAU))))
    sh.add_turns(4, lambda u, i: frog(dict(crouch=0.3)))
    sh.add_anim('crouch', 4, False, lambda u, i: frog(fk(u, [(0, dict()), (1, dict(crouch=1.0, pitch=0.15))], ease_out)))
    sh.add_anim('hop', 4, True, lambda u, i: frog(dict(ext=1.0, pitch=-0.35, kick=math.sin(u * TAU) * 0.8)))
    sh.add_anim('fall', 4, True, lambda u, i: frog(dict(ext=0.5, pitch=0.15, kick=math.sin(u * TAU) * 0.5)))
    sh.add_anim('land', 5, False, lambda u, i: frog(fk(u, [(0, dict(crouch=1.2, pitch=0.2)), (1, dict())], ease_out)))
    sh.add_anim('tongue', 9, False, lambda u, i: frog(fk(u, [(0, dict(crouch=0.3)), (0.2, dict(open=1.0, pitch=-0.12, crouch=0.1)), (0.7, dict(open=1.0, pitch=-0.12)), (1, dict(open=0.0))])))
    sh.add_anim('swim', 12, True, lambda u, i: frog(dict(ext=0.5 + 0.5 * math.sin(u * TAU), kick=math.sin(u * TAU) * 1.5, pitch=-0.15)))
    sh.add_anim('hurt', 5, False, lambda u, i: frog(fk(u, [(0, dict(pitch=-0.5, open=0.7, blink=1, crouch=0.5)), (1, dict())], ease_out)))
    sh.add_anim('death', 12, False, lambda u, i: frog(fk(u, [(0, dict(pitch=-0.5, open=0.7, blink=1)), (0.6, dict(belly_up=1.0, ext=0.6, blink=1, open=0.4, by=2)), (1, dict(belly_up=1.0, ext=0.8, blink=1, open=0.4, by=3))])))
    return sh


# ============================================================================================ GOBLIN
GOB_SKIN = hexc('6b8f3a')
GOB_SKIN2 = hexc('7f9448')
GOB_TUNIC = hexc('6a4a2c')
GOB_PANTS = hexc('3d3029')
WOOD = hexc('7a5431')
STONE = hexc('8b857c')
GOB_BASE = dict(bx=0.0, by=0.0, lean=0.1, lfx=-2.0, lfy=9.0, lnx=2.2, lny=9.0, rhx=4.0, rhy=2.0, wa=-0.6,
                lhx=-2.5, lhy=2.5, ht=0.0, sq=1.0, rot=0.0, mouth=0.0, weapon=1.0, whirl=0.0, blink=0.0, armz=-2.5)
gk = K(GOB_BASE)


def goblin(p, slinger=False):
    p = P_(GOB_BASE, p)
    skin = GOB_SKIN2 if slinger else GOB_SKIN
    parts = []
    hip = (p['bx'], 1.5 + p['by'])

    def B(pt):
        return add(rot(pt, p['lean']), hip)

    sh = B((0.8, -6.0))
    head = rot(B((1.8, -10.5)), p['ht'], B((1.2, -7.5)))
    for side, z in (('f', -1.5), ('n', 1.5)):
        foot = (p['l' + side + 'x'], p['l' + side + 'y'])
        knee = ik2(add(hip, (-0.4 if side == 'f' else 0.4, 0)), foot, 4.3, 4.3, bend=1)
        col = darken(GOB_PANTS, 0.12) if side == 'f' else GOB_PANTS
        parts.append(limb([add(hip, (0, 0)), knee, foot], [3.0, 2.6, 2.4], col, z=z))
        parts.append(ellipse(add(foot, (1.0, 0.3)), 2.1, 1.2, darken(skin, 0.25), z=z + 0.2))
    torso = [(-3.6, -6.5), (2.8, -6.8), (4.0, -1.0), (3.6, 2.5), (-3.8, 2.8), (-4.4, -2.0)]
    parts.append(poly([B(q) for q in torso], GOB_TUNIC, z=0, thick=0.8, smooth=True))
    parts.append(line([B((-3.8, 0.8)), B((3.8, 0.8))], 1.1, darken(GOB_TUNIC, 0.35), z=0, bias=0.4))
    # head: big ears, nose
    for ez in (-1.5, 1.5):
        parts.append(poly([add(head, (-2.5, -1.0)), add(head, (-9.0, -4.0 + (ez * 0.3))), add(head, (-2.0, 1.5))],
                          skin if ez > 0 else darken(skin, 0.12), z=ez, thick=0.35))
    parts.append(circle(head, 4.6, skin, z=0, thick=1.0))
    parts.append(poly([add(head, (3.0, -1.0)), add(head, (7.2, 1.2)), add(head, (3.2, 2.2))], darken(skin, 0.05), z=0, bias=0.8, thick=0.5))
    for ez in (1.3, -1.3):
        ec = add(head, (2.5, -1.3))
        if p['blink'] > 0.5:
            parts.append(line([add(ec, (-0.8, 0)), add(ec, (0.8, 0))], 0.6, hexc('201808'), z=ez + 0.5))
        else:
            parts.append(circle(ec, 1.0, hexc('ffd84a'), z=ez + 0.5, outline=False))
            parts.append(circle(add(ec, (0.35, 0)), 0.45, hexc('1a1208'), z=ez + 0.6, outline=False, shade=False))
    parts.append(line([add(head, (1.5, 2.6)), add(head, (3.5, 2.8 + p['mouth']))], 0.8, hexc('2a1a10'), z=0, bias=0.9))
    if p['mouth'] > 0.3:
        parts.append(poly([add(head, (2.1, 2.7)), add(head, (2.5, 3.8)), add(head, (2.9, 2.8))], hexc('f2ecd8'), z=0, bias=1.0, outline=False))
    # hood/hair tuft
    parts.append(poly([add(head, (-3.5, -3.0)), add(head, (0.0, -5.6)), add(head, (-1.0, -2.5))], darken(skin, 0.35), z=0, bias=0.2))

    def arm(s, hand, z):
        el = ik2(s, hand, 3.7, 3.7, bend=-1)
        parts.append(limb([s, el, hand], [2.2, 2.0], skin if z > 0 else darken(skin, 0.12), z=z))
        parts.append(circle(hand, 1.2, skin, z=z + 0.1))

    rz = p['armz']
    hand = (p['rhx'], p['rhy'])
    arm(add(sh, (-0.3, 0)), hand, rz)
    if p['weapon'] > 0.5:
        d = (math.cos(p['wa']), math.sin(p['wa']))
        if not slinger:
            tip = add(hand, (d[0] * 11, d[1] * 11))
            parts.append(limb([add(hand, (-d[0] * 1.5, -d[1] * 1.5)), tip], [1.8, 3.2], WOOD, z=rz + 0.2))
            parts.append(circle(tip, 3.0, darken(WOOD, 0.05), z=rz + 0.25))
            for k in range(3):
                parts.append(circle(add(tip, rot((2.6, 0), k * 2.1 + 0.4)), 0.6, hexc('c9c1b0'), z=rz + 0.3, outline=False))
        else:
            # sling: two cords to a pouch; whirl spins the pouch around the hand
            w = p['whirl']
            pouch = add(hand, (math.cos(w) * 6.0, math.sin(w) * 6.0)) if w else add(hand, (0.5, 6.0))
            parts.append(line([hand, pouch], 0.6, hexc('d6c4a0'), z=rz + 0.2))
            parts.append(ellipse(pouch, 1.7, 1.3, hexc('8a6a42'), z=rz + 0.3))
            if p['weapon'] > 1.5:
                parts.append(circle(pouch, 1.3, STONE, z=rz + 0.35))
    arm(add(sh, (0.3, 0)), (p['lhx'], p['lhy']), 2.3)
    if p['sq'] != 1.0:
        transform(parts, 0, (0, 9), sx=1 / math.sqrt(p['sq']), sy=p['sq'])
    if p['rot']:
        transform(parts, p['rot'], (0, 2))
    return parts


def gob_anims(sh, slinger):
    G = lambda d: goblin(d, slinger)
    wk = 2.0 if slinger else 1.0  # slinger carries a loaded stone when idle

    def idle(u, i):
        b = math.sin(u * TAU)
        return G(dict(by=b * 0.35, rhy=2 + b * 0.3, blink=1 if i in (9, 10) else 0, weapon=wk))

    def run(u, i):
        ph = u * TAU
        c, s = math.cos(ph), math.sin(ph)
        return G(dict(lean=0.3, by=-abs(c) * 0.8 + 0.5, lfx=4.2 * c, lfy=9 - max(0, s) * 3.2, lnx=-4.2 * c, lny=9 - max(0, -s) * 3.2,
                      rhx=2.5 - 2.5 * c, rhy=1.0, wa=-0.9 - 0.3 * c, lhx=1.5 + 2.5 * c, lhy=1.5, mouth=0.5, weapon=wk))

    sh.add_anim('idle', 24, True, idle)
    sh.add_anim('run', 12, True, run)
    sh.add_turns(4, lambda u, i: G(dict(lean=0.0, weapon=wk)))
    sh.add_anim('jump', 4, False, lambda u, i: G(gk(u, [(0, dict(by=2, sq=0.9)), (1, dict(lfy=7, lny=6, lfx=-3, lnx=3, rhy=-1, lhy=-1, sq=1.05, mouth=1))])))
    sh.add_anim('fall', 4, True, lambda u, i: G(dict(lfy=9, lny=8, lfx=-2.5, lnx=3, rhx=5, rhy=-3 + math.sin(u * TAU) * 0.4, lhx=-4, lhy=-3, mouth=1, weapon=wk)))
    sh.add_anim('land', 4, False, lambda u, i: G(gk(u, [(0, dict(by=2.4, sq=0.85, lean=0.3)), (1, dict())], ease_out)))
    sh.add_anim('hurt', 5, False, lambda u, i: G(gk(u, [(0, dict(lean=-0.5, bx=-1.2, ht=-0.4, mouth=1, rhy=-2, lhy=-3, blink=1, sq=0.9)), (1, dict())], ease_out)))
    sh.add_anim('death', 14, False, lambda u, i: G(gk(u, [(0, dict(lean=-0.5, ht=-0.4, mouth=1, blink=1)), (0.4, dict(lean=-0.9, bx=-2, by=3, mouth=1, blink=1, lfx=0, lnx=4)),
                                                         (1, dict(lean=-1.5, bx=-4, by=7, mouth=1, blink=1, lfx=2, lfy=9, lnx=6, lny=9, rhx=6, rhy=8, lhx=-8, lhy=7, weapon=0))], ease_in)))
    if not slinger:
        sh.add_anim('windup', 6, False, lambda u, i: G(gk(u, [(0, dict()), (1, dict(lean=-0.25, bx=-1, rhx=-2, rhy=-7, wa=-2.2, armz=2.5, lhx=3, lhy=0, mouth=1, lfx=-3.5, lnx=3.5))], ease_out)))
        sh.add_anim('strike', 6, False, lambda u, i: G(gk(u, [(0, dict(lean=-0.25, bx=-1, rhx=-2, rhy=-7, wa=-2.2, armz=2.5, mouth=1, lfx=-3.5, lnx=3.5)),
                                                             (0.35, dict(lean=0.45, bx=2, rhx=7, rhy=4, wa=0.9, armz=2.5, mouth=1, lfx=-3.5, lnx=5.5, sq=0.95)),
                                                             (1, dict(lean=0.4, bx=1.6, rhx=6.5, rhy=5, wa=1.2, armz=2.5, lfx=-3.5, lnx=5.5))], ease_out)))
        sh.add_anim('recover', 6, False, lambda u, i: G(gk(u, [(0, dict(lean=0.4, bx=1.6, rhx=6.5, rhy=5, wa=1.2, armz=2.5, lfx=-3.5, lnx=5.5)), (1, dict())])))
    else:
        def throw(u, i):
            p = gk(u, [(0, dict(rhx=3, rhy=-5, lean=-0.1)), (0.6, dict(rhx=3, rhy=-5, lean=-0.15)), (0.75, dict(rhx=7, rhy=-3, lean=0.35, bx=1)), (1, dict(rhx=5, rhy=1, lean=0.1))])
            w = -u * TAU * 2.5 if u < 0.75 else 0
            return G(dict(p, armz=2.0, whirl=w, weapon=2 if u < 0.72 else 1, mouth=1 if u > 0.6 else 0))
        sh.add_anim('throw', 12, False, throw)


def make_goblin():
    sh = Sheet('goblin', 52, 44, origin=(26, 26), scale=2.0, outline_w=0.8)
    gob_anims(sh, False)
    return sh


def make_slinger():
    sh = Sheet('slinger', 48, 44, origin=(24, 26), scale=2.0, outline_w=0.8)
    gob_anims(sh, True)
    return sh


# ============================================================================================ SPIDER
SP_BODY = hexc('2e2733')
SP_LEG = hexc('51465a')
SP_MARK = hexc('d02a2a')
SP_BASE = dict(gait=0.0, splay=0.0, curl=0.0, pitch=0.0, by=0.0, lunge=0.0, fang=0.0, flip=0.0)
sk = K(SP_BASE)


def spider(p):
    p = P_(SP_BASE, p)
    parts = []
    by = p['by']
    ceph = (3.0 + p['lunge'], 0.5 + by)
    abd = (-3.2, -0.5 + by)
    # legs: 4 per side, knees high, feet fanned from front to back; near side drawn in front
    reach = (9.5, 4.5, -2.5, -8.0)
    for side, z, col in ((-1, -2.5, darken(SP_LEG, 0.2)), (1, 2.5, SP_LEG)):
        for k in range(4):
            root = add(ceph, (1.0 - k * 1.1, 0.8))
            phase = p['gait'] + k * math.pi / 2 + (0 if side > 0 else math.pi)
            lift = max(0, math.sin(phase)) * 2.4
            swing = math.cos(phase) * 1.8
            fx = reach[k] + (0.8 if side < 0 else 0) + swing
            foot = add(root, (fx, 6.0 - by - lift))
            if p['splay']:
                foot = (lerp(foot[0], root[0] + reach[k] * 1.1, p['splay']), lerp(foot[1], root[1] - 4.5 + k * 0.6, p['splay']))
            if p['curl']:
                foot = (lerp(foot[0], root[0] + reach[k] * 0.15, p['curl']), lerp(foot[1], root[1] - 1.0, p['curl']))
            knee = (root[0] + (foot[0] - root[0]) * 0.45, min(root[1], foot[1]) - 4.2 + lift * 0.3 + p['curl'] * 2.0)
            parts.append(limb([root, knee, foot], [1.5, 1.2, 0.8], col, z=z + (k - 1.5) * 0.1))
    parts.append(ellipse(abd, 5.4, 4.4, SP_BODY, z=0, rot=-0.2, thick=0.9))
    parts.append(poly([add(abd, (-1.5, 1.2)), add(abd, (0.2, 2.4)), add(abd, (1.8, 1.2)), add(abd, (0.2, 3.2))], SP_MARK, z=0, bias=0.6, outline=False))
    parts.append(ellipse(ceph, 3.4, 2.8, lighten(SP_BODY, 0.05), z=0.2, thick=0.9))
    for dz in (-0.8, 0.8):
        f = p['fang']
        parts.append(limb([add(ceph, (2.6, 0.8)), add(ceph, (3.8 + f, 2.4 + f))], [1.2, 0.8], hexc('4a2830'), z=dz))
    for (ex, ey, r) in ((2.4, -1.2, 0.6), (1.6, -1.6, 0.45), (2.9, -0.4, 0.4)):
        for ez in (0.9, -0.9):
            parts.append(circle(add(ceph, (ex, ey)), r, hexc('ff3b30'), z=ez, outline=False, shade=False))
    parts.append(glow(add(ceph, (2.3, -1.0)), 2.5, hexc('ff2010', 0.3), z=2))
    transform(parts, p['pitch'], (0, 0))
    if p['flip']:
        transform(parts, math.pi * p['flip'], (0, 0))
    return parts


def make_spider():
    sh = Sheet('spider', 44, 32, origin=(22, 16), scale=2.0, outline_w=0.7)
    sh.add_anim('idle', 16, True, lambda u, i: spider(dict(by=math.sin(u * TAU) * 0.3, gait=0.3 * math.sin(u * TAU))))
    sh.add_anim('crawl', 8, True, lambda u, i: spider(dict(gait=u * TAU, by=math.sin(u * TAU * 2) * 0.3)))
    sh.add_turns(4, lambda u, i: spider(dict(gait=u * 2)))
    sh.add_anim('drop', 4, True, lambda u, i: spider(dict(splay=0.9, gait=u * TAU, fang=0.6)))
    sh.add_anim('hang', 12, True, lambda u, i: spider(dict(splay=0.5, gait=math.sin(u * TAU) * 1.5, fang=0.5 + 0.5 * math.sin(u * TAU * 2), pitch=0.1 * math.sin(u * TAU))))
    sh.add_anim('pounce', 6, False, lambda u, i: spider(sk(u, [(0, dict(by=1.5, curl=0.3)), (0.4, dict(lunge=2.0, pitch=-0.3, splay=0.6, fang=1.0)), (1, dict(lunge=1.0, pitch=-0.1, splay=0.3, fang=1.0))], ease_out)))
    sh.add_anim('hurt', 5, False, lambda u, i: spider(sk(u, [(0, dict(curl=0.6, pitch=-0.4, fang=1)), (1, dict())], ease_out)))
    sh.add_anim('death', 12, False, lambda u, i: spider(sk(u, [(0, dict(curl=0.4, pitch=-0.3)), (1, dict(curl=1.0, flip=1.0, by=-2))])))
    return sh


# ============================================================================================ MAGMA
LAVA = hexc('ff7a1c')
LAVA_HOT = hexc('ffd35a')
CRUST = hexc('3d1d14')
MG_BASE = dict(wob=0.0, swell=0.0, lean=0.0, rax=5.0, ray=2.0, fax=-5.0, fay=2.0, step=0.0, cool=0.0, sink=0.0, blink=0.0, sq=1.0)
mk = K(MG_BASE)


def magma(p):
    p = P_(MG_BASE, p)
    parts = []
    cool = p['cool']
    hot = mix(LAVA, hexc('55453f'), cool)
    hotter = mix(LAVA_HOT, hexc('6a5a52'), cool)
    crust = mix(CRUST, hexc('2a2624'), cool)
    s = 1 + p['swell'] * 0.12
    c = (0.0, 0.5 + p['sink'])
    # stubby legs
    for z, ph in ((-2.0, 0.0), (2.0, math.pi)):
        lift = max(0, math.sin(p['step'] + ph)) * 1.8
        fx = math.cos(p['step'] + ph) * 2.5
        parts.append(ellipse(add(c, (fx - 1.0, 9.5 - lift)), 3.4, 2.6, darken(crust, 0.1) if z < 0 else crust, z=z))
    # far arm
    parts.append(limb([add(c, (-1, -2)), (p['fax'], p['fay'] + p['sink'])], [4.0, 3.2], darken(hot, 0.15), z=-3.0))
    parts.append(circle((p['fax'], p['fay'] + p['sink']), 2.4, darken(crust, 0.1), z=-2.9))
    pts = []
    for k in range(14):
        a = k * TAU / 14
        r = (11.0 + math.sin(a * 3 + p['wob']) * 1.2 + math.sin(a * 5 - p['wob'] * 1.3) * 0.6) * s
        pts.append(add(c, (math.cos(a) * r * 1.05, math.sin(a) * r * 0.88 * p['sq'] - (1.5 if math.sin(a) < 0 else 0))))
    pts = [rot(q, p['lean'], add(c, (0, 8))) for q in pts]
    parts.append(glow(add(c, (0, 0)), 20 * s, mix(hexc('ff5a10', 0.35), hexc('000000', 0.0), cool), z=-5))
    parts.append(poly(pts, hot, z=0, thick=0.9, smooth=True))
    for (plate, pz) in (([(-8, -5), (-3, -9.5), (1, -6), (-4, -2)], 0.3), ([(2, 1), (8, -3), (10, 3), (4, 7)], 0.3), ([(-8, 3), (-3, 2.5), (-4, 8)], 0.3), ([(2, -9), (6, -8), (5, -5)], 0.3)):
        parts.append(poly([rot(add(c, q), p['lean'], add(c, (0, 8))) for q in plate], crust, z=0, bias=pz, thick=0.8))
    # glowing cracks
    for crk in ([(-2, -4), (0, -1), (-1, 3)], [(4, -6), (6, -1)], [(-6, 0), (-3, 1)]):
        parts.append(line([rot(add(c, q), p['lean'], add(c, (0, 8))) for q in crk], 1.0, hotter, z=0, bias=0.5))
    # eyes
    for ez in (1.2, -1.2):
        e = rot(add(c, (6.5, -3.5)), p['lean'], add(c, (0, 8)))
        if p['blink'] > 0.5 or cool > 0.6:
            parts.append(line([add(e, (-1.2, 0)), add(e, (1.2, 0))], 0.8, darken(crust, 0.3), z=ez + 0.5, bias=0.5))
        else:
            parts.append(poly([add(e, (-1.6, 0.4)), add(e, (1.6, -0.6)), add(e, (1.2, 0.6))], hexc('fff2a0'), z=ez + 0.5, bias=0.5, outline=False))
    # near arm
    parts.append(limb([add(c, (1, -1)), (p['rax'], p['ray'] + p['sink'])], [4.2, 3.4], hot, z=3.0))
    parts.append(circle((p['rax'], p['ray'] + p['sink']), 2.6, crust, z=3.1))
    if p['swell'] > 0.3:
        parts.append(glow((p['rax'], p['ray'] - 2), 5 * p['swell'], hexc('ffcc40', 0.6), z=4))
    return parts


def make_magma():
    sh = Sheet('magma', 52, 48, origin=(26, 28), scale=2.0, outline_w=0.8)
    sh.add_anim('idle', 24, True, lambda u, i: magma(dict(wob=u * TAU, ray=2 + math.sin(u * TAU) * 0.5, blink=1 if i in (18, 19) else 0)))
    sh.add_anim('walk', 16, True, lambda u, i: magma(dict(wob=u * TAU * 2, step=u * TAU, lean=0.08, rax=5 + math.cos(u * TAU) * 1.5, fax=-4 - math.cos(u * TAU) * 1.5, sq=1 - 0.04 * abs(math.sin(u * TAU)))))
    sh.add_turns(6, lambda u, i: magma(dict(wob=u * 3)))
    sh.add_anim('lob_windup', 8, False, lambda u, i: magma(mk(u, [(0, dict()), (1, dict(swell=1.0, lean=-0.25, rax=-2, ray=-10, fax=-7, fay=-8, wob=3))], ease_out)))
    sh.add_anim('lob', 6, False, lambda u, i: magma(mk(u, [(0, dict(swell=1.0, lean=-0.25, rax=-2, ray=-10, fax=-7, fay=-8, wob=3)), (0.35, dict(swell=0.2, lean=0.3, rax=12, ray=-4, fax=9, fay=-2, wob=4)), (1, dict(wob=5))], ease_out)))
    sh.add_anim('hurt', 5, False, lambda u, i: magma(mk(u, [(0, dict(lean=-0.35, sq=0.85, blink=1, swell=-0.3)), (1, dict())], ease_out)))
    sh.add_anim('death', 16, False, lambda u, i: magma(mk(u, [(0, dict(lean=-0.3, blink=1)), (1, dict(cool=1.0, sink=5.0, sq=0.55, swell=-0.3, rax=7, ray=9, fax=-7, fay=9, blink=1))])))
    return sh


# ============================================================================================ GOLEM
GSTONE = hexc('7d7a74')
GSTONE_D = hexc('55524f')
GMOSS = hexc('5d7a3e')
GEYE = hexc('6ff0ff')
GO_BASE = dict(bx=0.0, by=0.0, lean=0.0, rhx=10.0, rhy=4.0, lhx=-10.0, lhy=4.0, lfx=-4.5, lfy=13.5, lnx=4.5, lny=13.5,
               eye=1.0, crumble=0.0, ht=0.0)
gok = K(GO_BASE)


def golem(p):
    p = P_(GO_BASE, p)
    parts = []
    cr = p['crumble']
    hip = (p['bx'], 3.0 + p['by'])

    def B(q):
        return add(rot(q, p['lean']), hip)

    def piece(pts, col, z, drift, bias=0.0):
        # crumbling: each block slides/rotates away from the body
        q = [B(x) for x in pts]
        if cr > 0:
            cx = sum(a[0] for a in q) / len(q)
            cy = sum(a[1] for a in q) / len(q)
            ang = drift[2] * cr
            q = [add(rot(a, ang, (cx, cy)), (drift[0] * cr, drift[1] * cr)) for a in q]
        parts.append(poly(q, col, z=z, thick=0.85, bias=bias))

    for side, z in (('f', -3.0), ('n', 3.0)):
        foot = (p['l' + side + 'x'], p['l' + side + 'y'])
        h = add(hip, (-2 if side == 'f' else 2, 0))
        knee = ik2(h, foot, 5.0, 5.5, bend=1)
        col = darken(GSTONE_D, 0.1) if side == 'f' else GSTONE_D
        if cr > 0:
            knee = add(knee, (0, cr * 3))
        parts.append(limb([h, knee, foot], [6.0, 5.5, 5.0], col, z=z))
        parts.append(poly([add(foot, (-3.5, -1.5)), add(foot, (4.0, -1.5)), add(foot, (4.5, 1.2)), add(foot, (-3.5, 1.2))], col, z=z + 0.2))
    # far arm
    def arm(s, hand, z, col):
        el = ik2(s, hand, 6.5, 6.5, bend=-1)
        parts.append(limb([s, el], 5.5, col, z=z))
        parts.append(limb([el, hand], 5.0, col, z=z + 0.05))
        parts.append(poly([add(hand, (-3.4, -3.2)), add(hand, (3.4, -3.4)), add(hand, (3.8, 3.2)), add(hand, (-3.2, 3.6))], darken(col, 0.05), z=z + 0.1))
    arm(B((-3.5, -12)), (p['lhx'], p['lhy'] + cr * 8), -4.5, darken(GSTONE, 0.18))
    piece([(-11, -15), (8, -17), (11, -4), (8, 3), (-10, 3.5), (-12, -6)], GSTONE, 0, (-3, 6, -0.3))
    piece([(-8, -15.5), (-2, -17), (-4, -12)], GMOSS, 0, (-4, 8, -0.4), bias=0.3)
    piece([(-2, -6), (4, -7), (3, -1)], darken(GSTONE, 0.2), 0, (2, 7, 0.4), bias=0.3)
    # head
    hd = [(-3, -24), (5.5, -24.5), (6.5, -16.5), (-3.5, -16)]
    hd = [rot(q, p['ht'], (1, -16)) for q in hd]
    piece(hd, lighten(GSTONE, 0.06), 0.2, (5, 12, 0.9), bias=0.2)
    eyep = rot((3.5, -20.5), p['ht'], (1, -16))
    e = B(eyep)
    if cr > 0:
        e = add(e, (5 * cr, 12 * cr))
    if p['eye'] > 0.05:
        parts.append(line([add(e, (-1.8, 0)), add(e, (1.8, 0))], 1.6, mix(GSTONE_D, GEYE, p['eye']), z=0.3, bias=1.0))
        parts.append(glow(e, 6 * p['eye'], hexc('40e8ff', 0.5 * p['eye']), z=3))
    arm(B((4.5, -12)), (p['rhx'], p['rhy'] + cr * 8), 4.5, GSTONE)
    return parts


def make_golem():
    sh = Sheet('golem', 64, 64, origin=(32, 38), scale=2.0, outline_w=0.9)
    sh.add_anim('idle', 24, True, lambda u, i: golem(dict(by=math.sin(u * TAU) * 0.4, rhy=4 + math.sin(u * TAU) * 0.4, lhy=4 + math.sin(u * TAU) * 0.4, eye=0.85 + 0.15 * math.sin(u * TAU * 2))))

    def walk(u, i):
        ph = u * TAU
        c, s = math.cos(ph), math.sin(ph)
        return golem(dict(lfx=5 * c, lfy=13.5 - max(0, s) * 2.5, lnx=-5 * c, lny=13.5 - max(0, -s) * 2.5, by=abs(s) * -0.8 + 0.4,
                          lean=0.05 * s, rhx=9 - 2 * c, lhx=-9 + 2 * c))
    sh.add_anim('walk', 16, True, walk)
    sh.add_turns(6, lambda u, i: golem(dict()))
    up = dict(lean=-0.2, rhx=5, rhy=-20, lhx=-2, lhy=-21, ht=-0.2, by=-1, eye=1.3)
    sh.add_anim('slam_windup', 12, False, lambda u, i: golem(gok(u, [(0, dict()), (1, up)], ease_out)))
    down = dict(lean=0.4, bx=2, by=3, rhx=16, rhy=12, lhx=12, lhy=12, ht=0.2, lnx=7, eye=1.4)
    sh.add_anim('slam', 6, False, lambda u, i: golem(gok(u, [(0, up), (0.35, down), (1, dict(down, by=3.5))], ease_in)))
    sh.add_anim('recover', 10, False, lambda u, i: golem(gok(u, [(0, dict(down, by=3.5)), (1, dict())])))
    sh.add_anim('hurt', 5, False, lambda u, i: golem(gok(u, [(0, dict(lean=-0.12, bx=-1, eye=0.4, ht=-0.1)), (1, dict())], ease_out)))
    sh.add_anim('death', 18, False, lambda u, i: golem(gok(u, [(0, dict(lean=-0.1, eye=0.6)), (0.3, dict(lean=0.2, by=3, eye=0.2)), (1, dict(lean=0.3, by=5, eye=0.0, crumble=1.0, lnx=6, lfx=-7))])))
    return sh


# ============================================================================================ FISH
FISH_BASE = dict(tail=0.0, bend=0.0, pitch=0.0, mouth=0.0, side=0.0, belly=0.0, fin=0.0)
fik = K(FISH_BASE)


def fish(p, pal):
    p = P_(FISH_BASE, p)
    body_c, fin_c, belly_c = pal
    parts = []
    b = p['bend']
    tailroot = (-6.0, 0.0)
    tw = math.sin(p['tail']) * 3.0 + b * 4
    parts.append(poly([tailroot, add(tailroot, (-5.5, -4.0 + tw)), add(tailroot, (-3.8, 0.0 + tw * 0.8)), add(tailroot, (-5.5, 4.0 + tw))], fin_c, z=0, thick=0.35))
    parts.append(poly([(-1.5, -3.8), (2.5, -3.9), (-3.0, -7.5 - p['fin'])], darken(fin_c, 0.1), z=0, thick=0.3, bias=-0.3))
    body = [(7.5, -0.2 + p['mouth'] * 0.4), (4.5, -3.8), (-2.0, -4.2), (-6.5, -1.5 + b * 1.2), (-6.5, 1.5 + b * 1.2), (-2.0, 4.0), (4.5, 3.6), (7.5, 0.8)]
    parts.append(poly(body, body_c, z=0, thick=0.7, smooth=True))
    parts.append(poly([(5.0, 1.2), (0.0, 3.8), (-4.5, 2.2), (0, 1.5)], belly_c, z=0, bias=0.5, thick=0.7, smooth=True, outline=False))
    for k in range(3):
        parts.append(line([(-1 + k * 2.2, -2.8), (0.2 + k * 2.2, 2.2)], 0.5, darken(body_c, 0.15), z=0, bias=0.45))
    for z in (-2.0, 2.0):
        e = (4.6, -1.1)
        parts.append(circle(e, 1.35, hexc('f5f2e6'), z=z))
        parts.append(circle(add(e, (0.4, 0)), 0.75, hexc('101010'), z=z + 0.1, outline=False, shade=False))
    parts.append(line([(7.4, 0.5), (5.5, 1.2 + p['mouth'])], 0.6, darken(body_c, 0.5), z=0, bias=0.6))
    for z, col in ((-1.5, darken(fin_c, 0.15)), (1.5, fin_c)):
        f = math.sin(p['tail'] * 1.5 + (0 if z > 0 else 1)) * 1.2
        parts.append(poly([(2.0, 1.2), (-1.5, 4.0 + f), (-0.5, 1.2)], col, z=z, thick=0.4))
    transform(parts, p['pitch'], (0, 0))
    if p['belly']:
        transform(parts, math.pi * p['belly'], (0, 0))
    if p['side']:
        transform(parts, 0, (0, 0), sy=1 - 0.35 * p['side'])
    return parts


FISH_PALS = {'fish': (hexc('ef8a2e'), hexc('e0632a'), hexc('f8d49a')), 'fish2': (hexc('4fb3c2'), hexc('2d8a9e'), hexc('cfeef0'))}


def make_fish(name):
    pal = FISH_PALS[name]
    F = lambda d: fish(d, pal)
    sh = Sheet(name, 36, 26, origin=(19, 13), scale=2.0, outline_w=0.7)
    sh.add_anim('swim', 8, True, lambda u, i: F(dict(tail=u * TAU, bend=math.sin(u * TAU) * 0.2, fin=math.sin(u * TAU) * 0.5)))
    sh.add_anim('dart', 4, True, lambda u, i: F(dict(tail=u * TAU, bend=math.sin(u * TAU) * 0.5, mouth=1.0, pitch=0)))
    sh.add_turns(4, lambda u, i: F(dict(tail=u * TAU, bend=0.4 * math.sin(u * math.pi))))
    sh.add_anim('flop', 6, True, lambda u, i: F(dict(tail=u * TAU * 2, bend=math.sin(u * TAU) * 1.2, pitch=math.sin(u * TAU) * 0.35, side=0.4, mouth=0.8 * abs(math.sin(u * TAU)))))
    sh.add_anim('leap', 4, True, lambda u, i: F(dict(tail=u * TAU, bend=0.8, mouth=1.0, fin=1.0)))
    sh.add_anim('hurt', 5, False, lambda u, i: F(fik(u, [(0, dict(bend=-1.0, mouth=1, pitch=-0.3)), (1, dict())], ease_out)))
    sh.add_anim('death', 10, False, lambda u, i: F(fik(u, [(0, dict(bend=-0.8, mouth=1)), (1, dict(belly=1.0, mouth=1, bend=0.3))])))
    return sh


# ============================================================================================ URCHIN
UR_BODY = hexc('4a1f5a')
UR_SPIKE = hexc('9a6ab5')
UR_DOT = hexc('e39aff')


def urchin_spike_len(c):
    # mirrors Urchin.SpikeLen() in WaterEnemies.cs (3 s cycle)
    if c < 1.6:
        return 7
    if c < 2.1:
        return 7 - (c - 1.6) * 8
    if c < 2.25:
        return 3 + (c - 2.1) / 0.15 * 17
    if c < 2.6:
        return 20
    return 20 - (c - 2.6) / 0.4 * 13


def urchin(spike, t, shrink=1.0, droop=0.0, tremble=0.0):
    parts = []
    n = 22
    for k in range(n):
        a = k * TAU / n + t * 0.15
        z = 1.5 * math.cos(a * 3)  # a rough 3D arrangement so some spikes sit behind the body
        L = spike * (1 if k % 2 == 0 else 0.75) * shrink
        d = (math.cos(a), math.sin(a))
        start = (d[0] * 8, d[1] * 8)
        end = (d[0] * (10 + L), d[1] * (10 + L) + droop * L * 0.6)
        if tremble:
            end = add(end, (math.sin(k * 7 + t * 60) * tremble, 0))
        parts.append(limb([start, end], [1.8, 0.5], UR_SPIKE if z > 0 else darken(UR_SPIKE, 0.25), z=z * 3, thick=1.0, outline=True))
    parts.append(circle((0, 0), 10 * (0.8 + 0.2 * shrink), UR_BODY, z=0, thick=1.0))
    for k in range(6):
        pos = rot((5.5, 0), k * 1.05 + 0.3)
        parts.append(circle(pos, 1.2, UR_DOT, z=0, bias=0.5, outline=False, shade=False))
        parts.append(glow(pos, 2.4, hexc('e080ff', 0.3 + 0.2 * math.sin(t * 3 + k)), z=1))
    return parts


def make_urchin():
    sh = Sheet('urchin', 68, 68, origin=(34, 34), scale=2.0, outline_w=0.7)
    # 72 frames = the 3 s spike cycle at 24 fps; the game drives this in sync with its damage timer
    def pulse(u, i):
        c = u * 3.0
        return urchin(urchin_spike_len(c), c, tremble=1.0 if 1.6 < c < 2.1 else 0.0)
    sh.add_anim('pulse', 72, True, pulse)
    sh.add_anim('hurt', 5, False, lambda u, i: urchin(7 * (0.6 + 0.4 * u), i * 0.1, shrink=0.8 + 0.2 * u))
    sh.add_anim('death', 12, False, lambda u, i: urchin(7 * (1 - u * 0.6), 0, shrink=1 - u * 0.4, droop=u))
    return sh


# ============================================================================================ EEL (head)
EEL = hexc('3c5a34')
EEL_BELLY = hexc('b9c064')
EL_BASE = dict(jaw=0.0, eye=0.8, recoil=0.0, dead=0.0)
ek = K(EL_BASE)


def eel_head(p):
    p = P_(EL_BASE, p)
    parts = []
    j = p['jaw']
    x0 = -p['recoil']
    parts.append(poly([(x0 - 6, -4.0), (x0 + 3, -4.5), (x0 + 9.5, -1.0 - j * 2.2), (x0 + 9, 0.0), (x0 - 6, 0.5)], EEL, z=0, thick=0.6, smooth=False))
    parts.append(poly([(x0 - 6, 0.3), (x0 + 1, 0.5), (x0 + 8.5, 1.0 + j * 4.0), (x0 + 1, 4.0), (x0 - 6, 4.2)], darken(EEL, 0.1), z=0, thick=0.6))
    parts.append(poly([(x0 - 5, 2.0), (x0 + 1, 2.3), (x0 + 6, 2.0 + j * 2.5), (x0 + 1, 3.6), (x0 - 5, 3.8)], EEL_BELLY, z=0, bias=0.4, outline=False))
    if j > 0.2:
        for k in range(4):
            x = x0 + 2.5 + k * 1.7
            parts.append(poly([(x, -0.6 - j * k * 0.45), (x + 0.5, 0.8), (x + 1.0, -0.6 - j * k * 0.45)], hexc('f2efe2'), z=0, bias=0.6, outline=False))
    parts.append(poly([(x0 - 5, -4), (x0 - 2, -7.5), (x0 + 1, -4.3)], darken(EEL, 0.2), z=0, bias=-0.3, thick=0.3))
    for z in (1.6, -1.6):
        e = (x0 + 3.5, -2.4)
        if p['dead'] > 0.5:
            parts.append(line([add(e, (-0.8, -0.8)), add(e, (0.8, 0.8))], 0.5, hexc('1a1a10'), z=z + 0.2))
            parts.append(line([add(e, (-0.8, 0.8)), add(e, (0.8, -0.8))], 0.5, hexc('1a1a10'), z=z + 0.2))
        else:
            parts.append(circle(e, 1.1, hexc('ffe36a'), z=z, outline=False))
            parts.append(ellipse(add(e, (0.3, 0)), 0.3, 0.9, hexc('101008'), z=z + 0.1, outline=False, shade=False))
    parts.append(glow((x0 + 3.5, -2.4), 4 * p['eye'], hexc('ffe36a', 0.35 * p['eye']), z=3))
    return parts


def make_eel():
    sh = Sheet('eel', 32, 22, origin=(12, 11), scale=2.0, outline_w=0.7)
    sh.add_anim('lurk', 16, True, lambda u, i: eel_head(dict(jaw=0.1 + 0.1 * math.sin(u * TAU), eye=0.6 + 0.4 * math.sin(u * TAU))))
    sh.add_anim('bite', 6, False, lambda u, i: eel_head(ek(u, [(0, dict(jaw=0.2)), (0.4, dict(jaw=1.0, eye=1.2)), (0.8, dict(jaw=0.0, eye=1.0)), (1, dict(jaw=0.3))])))
    sh.add_anim('hold', 8, True, lambda u, i: eel_head(dict(jaw=0.5 + 0.4 * math.sin(u * TAU * 2), eye=1.0)))
    sh.add_anim('hurt', 5, False, lambda u, i: eel_head(ek(u, [(0, dict(recoil=2.0, jaw=0.8, eye=0.3)), (1, dict())], ease_out)))
    sh.add_anim('death', 10, False, lambda u, i: eel_head(ek(u, [(0, dict(jaw=0.8)), (1, dict(jaw=0.5, eye=0.0, dead=1.0))])))
    return sh


# ============================================================================================ COLOSSUS
CO_PLATE = hexc('6f6964')
CO_DARK = hexc('3e3a3b')
CO_CORE = hexc('66e6ff')
CO_BASE = dict(bx=0.0, by=0.0, crouch=0.0, headdown=0.0, clawa=0.3, clawo=0.2, gait=0.0, stride=0.0, roar=0.0, tilt=0.0,
               crumble=0.0, core=1.0, stun=0.0)
ck = K(CO_BASE)


def colossus(p):
    p = P_(CO_BASE, p)
    parts = []
    cr = p['crouch'] * 6
    by = p['by'] + cr
    cm = p['crumble']
    O = (p['bx'], by)

    def T(q):
        q = rot(q, p['tilt'], (0, 10))
        return add(q, O)

    # legs: 3 per side
    for side, z in ((-1, -8.0), (1, 8.0)):
        for k in range(3):
            root = T((-16 + k * 15, 8))
            ph = p['gait'] + k * 2.1 + (0 if side > 0 else math.pi)
            lift = max(0, math.sin(ph)) * 5 * p['stride']
            foot = (root[0] - 6 + math.cos(ph) * 6 * p['stride'], 27 - lift + cm * 2)
            knee = ik2(root, foot, 11, 12, bend=-1)
            col = darken(CO_DARK, 0.15) if side < 0 else CO_DARK
            parts.append(limb([root, knee, foot], [8.0, 6.5, 5.0], col, z=z + k * 0.2))
            parts.append(poly([add(foot, (-4, -1)), add(foot, (5, -1)), add(foot, (3, 3)), add(foot, (-4, 3))], col, z=z + k * 0.2 + 0.1))

    def claw(z, col):
        sh = T((22, 0))
        a = p['clawa'] + (0.15 if z < 0 else 0)
        wrist = polar(sh, 20, a)
        o = p['clawo']
        parts.append(limb([sh, wrist], [9, 7], darken(col, 0.1), z=z))
        d = (math.cos(a), math.sin(a))
        n = (-d[1], d[0])
        up = [add(wrist, (n[0] * -2, n[1] * -2)), add(wrist, (d[0] * 16 + n[0] * (-6 - o * 6), d[1] * 16 + n[1] * (-6 - o * 6))), add(wrist, (d[0] * 6 + n[0] * 3, d[1] * 6 + n[1] * 3))]
        lo = [add(wrist, (n[0] * 2, n[1] * 2)), add(wrist, (d[0] * 15 + n[0] * (6 + o * 6), d[1] * 15 + n[1] * (6 + o * 6))), add(wrist, (d[0] * 6 - n[0] * 3, d[1] * 6 - n[1] * 3))]
        parts.append(poly(up, col, z=z + 0.2, thick=0.6))
        parts.append(poly(lo, darken(col, 0.08), z=z + 0.2, thick=0.6))

    claw(-12.0, darken(CO_PLATE, 0.15))

    def piece(pts, col, z, drift, bias=0.0, smooth_=False):
        q = [T(x) for x in pts]
        if cm > 0:
            cx = sum(a[0] for a in q) / len(q)
            cy = sum(a[1] for a in q) / len(q)
            q = [add(rot(a, drift[2] * cm, (cx, cy)), (drift[0] * cm, drift[1] * cm)) for a in q]
        parts.append(poly(q, col, z=z, thick=0.9, bias=bias, smooth=smooth_))

    hd = p['headdown']
    piece([(-36, 14), (-31, -10), (-14, -27), (10, -29 + hd * 8), (30, -12 + hd * 10), (35, 14)], CO_PLATE, 0, (-6, 16, -0.2), smooth_=False)
    for (pl, dr) in (([(-30, -9), (-14, -25), (-10, -8), (-22, 4)], (-10, 14, -0.5)), ([(-9, -26), (9, -27.5 + hd * 8), (8, -9), (-6, -8)], (3, 18, 0.4)),
                     ([(10, -26 + hd * 8), (28, -12 + hd * 10), (20, 2), (10, -6)], (12, 15, 0.6))):
        piece(pl, lighten(CO_PLATE, 0.06), 0, dr, bias=0.3)
    piece([(-34, 10), (30, 10), (32, 15), (-34, 15)], CO_DARK, 0, (0, 8, 0.05), bias=0.2)
    # core
    core_col = mix(CO_DARK, CO_CORE, p['core'])
    cc = T((-3, 0))
    if cm > 0:
        cc = add(cc, (0, 10 * cm))
    parts.append(circle(cc, 7.5, darken(CO_DARK, 0.2), z=0, bias=0.35))
    parts.append(circle(cc, 5.0, core_col, z=0, bias=0.4, outline=False))
    parts.append(glow(cc, 16 * p['core'], hexc('50e0ff', 0.55 * p['core']), z=5))
    # head
    h = T((30, -8 + hd * 14))
    if cm > 0:
        h = add(h, (8 * cm, 20 * cm))
    jaw = p['roar']
    parts.append(poly([add(h, (-8, -10)), add(h, (12, -6)), add(h, (14, 2)), add(h, (-6, 3))], lighten(CO_PLATE, 0.03), z=0.5, bias=0.5))
    parts.append(poly([add(h, (-6, 3)), add(h, (12, 2 + jaw * 6)), add(h, (-4, 8 + jaw * 3))], darken(CO_PLATE, 0.15), z=0.5, bias=0.45))
    if jaw > 0.2:
        parts.append(poly([add(h, (-4, 3)), add(h, (11, 2)), add(h, (10, 2 + jaw * 5))], hexc('2a0f0f'), z=0.5, bias=0.48, outline=False))
    parts.append(poly([add(h, (-4, -9)), add(h, (6, -22)), add(h, (4, -8))], CO_DARK, z=0.5, bias=0.55))
    parts.append(poly([add(h, (2, -8)), add(h, (13, -17)), add(h, (9, -6))], darken(CO_DARK, 0.1), z=0.5, bias=0.3))
    eye_col = hexc('fff27a') if p['stun'] > 0.5 else core_col
    for ez in (2.0, -2.0):
        parts.append(poly([add(h, (4, -4)), add(h, (10, -3.4)), add(h, (9.5, -1.4)), add(h, (4.5, -2))], eye_col, z=ez + 0.5, bias=0.5, outline=False))
    parts.append(glow(add(h, (7, -3)), 7 * max(p['core'], p['stun']), hexc('60e8ff', 0.5), z=5))
    claw(12.0, CO_PLATE)
    return parts


def make_colossus():
    sh = Sheet('colossus', 124, 112, origin=(60, 70), scale=1.5, outline_w=1.1)
    sh.add_anim('idle', 24, True, lambda u, i: colossus(dict(by=math.sin(u * TAU) * 0.8, clawa=0.3 + 0.05 * math.sin(u * TAU), core=0.8 + 0.2 * math.sin(u * TAU * 2))))
    sh.add_anim('walk', 16, True, lambda u, i: colossus(dict(gait=u * TAU, stride=1.0, by=abs(math.sin(u * TAU * 1.5)) * -1.2, clawa=0.35 + 0.1 * math.sin(u * TAU), tilt=0.03 * math.sin(u * TAU))))
    sh.add_turns(8, lambda u, i: colossus(dict(by=1)))
    sh.add_anim('crouch', 8, False, lambda u, i: colossus(ck(u, [(0, dict()), (1, dict(crouch=1.0, clawa=0.9, tilt=0.08, clawo=0.6))], ease_out)))
    sh.add_anim('leap', 4, True, lambda u, i: colossus(dict(by=-2, clawa=-0.4 + 0.1 * math.sin(u * TAU), clawo=0.8, gait=1.5, stride=0.5, tilt=-0.12)))
    sh.add_anim('land', 8, False, lambda u, i: colossus(ck(u, [(0, dict(crouch=1.3, clawa=1.2, tilt=0.1, clawo=0.2)), (1, dict())], ease_out)))
    sh.add_anim('roar', 20, False, lambda u, i: colossus(ck(u, [(0, dict()), (0.2, dict(tilt=-0.15, roar=1.0, clawa=-0.7, clawo=1.0, core=1.4)), (0.8, dict(tilt=-0.16, roar=1.0, clawa=-0.8, clawo=1.0, core=1.4, by=math.sin(u * 60) * 0.8)), (1, dict())])))
    sh.add_anim('charge_windup', 10, False, lambda u, i: colossus(ck(u, [(0, dict()), (1, dict(headdown=1.0, bx=-4, clawa=1.1, tilt=0.12, crouch=0.4))], ease_out)))
    sh.add_anim('charge', 8, True, lambda u, i: colossus(dict(headdown=1.0, gait=u * TAU * 2, stride=1.3, clawa=1.0, tilt=0.12, by=abs(math.sin(u * TAU * 2)) * -1.5)))
    sh.add_anim('stunned', 16, True, lambda u, i: colossus(dict(tilt=math.sin(u * TAU) * 0.08, headdown=0.6, crouch=0.5, clawa=1.2, core=0.3, stun=1.0, by=1)))
    sh.add_anim('hurt', 5, False, lambda u, i: colossus(ck(u, [(0, dict(tilt=-0.08, bx=-2, core=1.5)), (1, dict())], ease_out)))
    sh.add_anim('death', 30, False, lambda u, i: colossus(ck(u, [(0, dict(tilt=-0.1, roar=1.0, core=1.5)), (0.4, dict(crouch=1.2, core=0.4, roar=0.6, headdown=0.6)), (1, dict(crouch=1.5, crumble=1.0, core=0.0, headdown=1.0))])))
    return sh


# ============================================================================================ CRITTERS
MOTH = hexc('cfe9e6')


def moth(flap, glow_a=1.0):
    parts = []
    f = math.sin(flap)
    for z, col in ((-1.0, darken(MOTH, 0.2)), (1.0, MOTH)):
        tip = (-2.0, -5.5 * f - 0.5)
        parts.append(poly([(0.5, -0.5), tip, (-4.5, -1.5 * f + 1.0), (-1.0, 1.2)], col, z=z, thick=0.3, smooth=True))
    parts.append(ellipse((0, 0.5), 2.6, 1.3, hexc('8fb7b0'), z=0))
    parts.append(line([(2.2, 0), (4.0, -2.0)], 0.4, hexc('e8fff6'), z=0.5))
    parts.append(glow((0, 0), 7 * glow_a, hexc('a0fff0', 0.35), z=2))
    return parts


CRAB = hexc('c0573a')
CR_BASE = dict(gait=0.0, eyes=1.0, claw=0.0, low=0.0)
crk = K(CR_BASE)


def crab(p):
    p = P_(CR_BASE, p)
    parts = []
    low = p['low']
    for side, z in ((-1, -2.0), (1, 2.0)):
        for k in range(3):
            root = (-2.5 + k * 2.2, 1.5 + low)
            ph = p['gait'] + k * 2.0 + (0 if side > 0 else math.pi)
            foot = (root[0] + (k - 1) * 2.5 + math.cos(ph) * 1.2, 5.5 - max(0, math.sin(ph)) * 1.2)
            knee = add(ik2(root, foot, 2.6, 3.0, bend=-1 if k >= 1 else 1), (0, 0))
            parts.append(limb([root, knee, foot], [1.1, 0.8], darken(CRAB, 0.25 if side < 0 else 0.1), z=z))
    for z, col in ((-2.5, darken(CRAB, 0.15)), (2.5, CRAB)):
        c = (5.5 + p['claw'], -0.5 + low - p['claw'])
        parts.append(limb([(3, 1 + low), c], 1.4, col, z=z))
        parts.append(ellipse(c, 1.9, 1.3, col, z=z + 0.1, rot=-0.4))
    parts.append(ellipse((0, 0.5 + low), 5.2, 3.2, CRAB, z=0, thick=0.9))
    parts.append(ellipse((0.5, -0.8 + low), 3.2, 1.2, lighten(CRAB, 0.2), z=0, bias=0.4, outline=False))
    for z in (-1.0, 1.0):
        top = (3.0, -2.5 - 2.5 * p['eyes'] + low)
        parts.append(line([(2.6, -1.5 + low), top], 0.6, darken(CRAB, 0.3), z=z))
        parts.append(circle(top, 0.8, hexc('141414'), z=z + 0.1, outline=False))
    return parts


def make_moth():
    sh = Sheet('moth', 18, 16, origin=(9, 8), scale=2.0, outline_w=0.5)
    sh.add_anim('flutter', 6, True, lambda u, i: moth(u * TAU))
    sh.add_turns(4, lambda u, i: moth(u * TAU))
    return sh


def make_crab():
    sh = Sheet('crab', 26, 18, origin=(13, 11), scale=2.0, outline_w=0.6)
    sh.add_anim('idle', 16, True, lambda u, i: crab(dict(claw=0.4 * max(0, math.sin(u * TAU * 2)), eyes=1.0 if i not in (11, 12) else 0.4)))
    sh.add_anim('scuttle', 8, True, lambda u, i: crab(dict(gait=u * TAU, claw=0.2)))
    sh.add_turns(4, lambda u, i: crab(dict(gait=u * 2)))
    sh.add_anim('hide', 6, False, lambda u, i: crab(crk(u, [(0, dict()), (1, dict(eyes=0.0, low=1.2, claw=-0.5))], ease_out)))
    return sh


MAKERS = {
    'bat': make_bat, 'frog': make_frog, 'goblin': make_goblin, 'slinger': make_slinger, 'spider': make_spider,
    'magma': make_magma, 'golem': make_golem, 'fish': lambda: make_fish('fish'), 'fish2': lambda: make_fish('fish2'),
    'urchin': make_urchin, 'eel': make_eel, 'colossus': make_colossus, 'moth': make_moth, 'crab': make_crab,
}
