"""The dagger wielder: a hooded rogue with a red scarf. Right-handed (the dagger hand is the far
arm when facing right, the near arm when facing left)."""
import math
from rig import (keys as _keys, hexc, lighten, darken, ellipse, circle, poly, limb, line, glow, rot, add, polar,
                 ik2, keys, lerp, smooth, ease_out, ease_in, transform, Sheet)

CLOAK = hexc('2b6272')
CLOAK_D = hexc('183d49')
PANTS = hexc('2e2c38')
BOOT = hexc('4d3526')
SKIN = hexc('efc49c')
SCARF = hexc('cc3a2e')
BELT = hexc('70502c')
STEEL = hexc('e3ecf2')
HILT = hexc('6e4b22')
GUARD = hexc('c9a54a')
EYE = hexc('1b1b26')

BASE = dict(sa=0.0, bx=0, by=0, lean=0.06, lfx=-2.4, lfy=13, lnx=2.6, lny=13,
            dhx=5.5, dhy=2.5, da=1.0, fhx=-2.5, fhy=3.0, ht=0.0, sw=0.0, sl=0.0, cf=0.0,
            armz=-3.0, sq=1.0, rot=0.0, kneeb=1.0, blade=1.0, scarf=1.0, blink=0.0)


def keys(u, table, ease=smooth):
    return _keys(u, table, ease, base=BASE)


def build(pose):
    P = dict(BASE)
    P.update(pose)
    P.pop('sa', None)
    parts = []
    hip = (P['bx'], 3 + P['by'])
    lean = P['lean']

    def B(pt):
        return add(rot(pt, lean), hip)

    shoulder = B((1.0, -7.0))
    neck = B((1.4, -9.2))
    head = add(B((1.8, -13.6)), rot((0, 0), 0))
    head = rot(head, P['ht'], neck)

    # --- legs (far z=-1.5, near z=+1.5) ---
    for side, z in (('f', -1.6), ('n', 1.6)):
        foot = (P['l' + side + 'x'] + P['bx'] * 0.0, P['l' + side + 'y'])
        h = add(hip, (-0.6 if side == 'f' else 0.6, 0))
        knee = ik2(h, foot, 5.6, 5.6, bend=P['kneeb'])
        col = darken(PANTS, 0.1) if side == 'f' else PANTS
        parts.append(limb([h, knee, foot], [3.6, 3.2, 3.0], col, z=z))
        parts.append(ellipse(add(foot, (1.1, 0.4)), 2.4, 1.5, BOOT, z=z + 0.2, rot=0.0))

    # --- scarf tail (behind everything) ---
    s = P['scarf']
    wave = P['sw']
    lift = P['sl']
    tail = [add(neck, (-1.2, 0.4))]
    for i in range(1, 5):
        tail.append(add(neck, (-3.6 * i * s, 0.6 * i - lift * 1.3 * i + math.sin(wave + i * 1.3) * 0.9 * i * 0.6)))
    parts.append(limb(tail, [2.6, 2.4, 2.1, 1.7, 1.3], SCARF, z=-3.5))

    # --- cloak / torso ---
    cf = P['cf']
    body = [(-3.4, -7.4), (2.8, -7.6), (3.9, -2.5), (4.1 + cf * 0.4, 4.6), (0, 5.8 + cf * 0.3),
            (-4.8 - cf * 2.2, 6.2 - cf * 1.2), (-4.3, -1.5)]
    parts.append(poly([B(p) for p in body], CLOAK, z=0.0, thick=0.75, smooth=True))
    parts.append(line([B((-3.9, -0.6)), B((3.9, -0.6))], 1.5, BELT, z=0.0, bias=0.5))
    parts.append(circle(B((2.6, -0.6)), 0.9, GUARD, z=0.0, outline=False, bias=0.6))

    # --- head ---
    parts.append(poly([add(head, rot(p, P['ht'])) for p in [(-4.4, -1.5), (-2.0, -6.2), (-9.0, -3.2)]],
                      CLOAK_D, z=0.0, thick=0.5, bias=-0.8))
    parts.append(circle(head, 5.4, CLOAK, z=0.0, thick=1.0))
    parts.append(ellipse(add(head, rot((1.9, 0.9), P['ht'])), 3.3, 3.6, SKIN, z=0.0, thick=0.9, rot=P['ht'], bias=0.5))
    blink = P['blink']
    for ez in (1.6, -1.6):
        ec = add(head, rot((3.4, 0.3), P['ht']))
        if blink > 0.5:
            parts.append(line([add(ec, (-0.7, 0)), add(ec, (0.7, 0))], 0.6, EYE, z=ez + 0.4))
        else:
            parts.append(ellipse(ec, 0.75, 1.0, EYE, z=ez + 0.4, outline=False, shade=False))
    # scarf wrap around the neck
    parts.append(ellipse(add(neck, (0.2, 0.3)), 3.4, 1.8, SCARF, z=0.0, thick=1.0, bias=0.9))

    # --- arms ---
    def arm(sh, hand, z, bend):
        el = ik2(sh, hand, 4.4, 4.4, bend=bend)
        parts.append(limb([sh, el, hand], [2.8, 2.5], CLOAK if z > 0 else darken(CLOAK, 0.12), z=z))
        parts.append(circle(hand, 1.35, SKIN, z=z + 0.1))

    dz = P['armz']
    dhand = (P['dhx'], P['dhy'])
    arm(add(shoulder, (-0.3, 0.2)), dhand, dz, -1)
    # dagger in the right hand
    if P['blade'] > 0.05:
        d = (math.cos(P['da']), math.sin(P['da']))
        n = (-d[1], d[0])
        L = 8.5 * P['blade']
        parts.append(line([add(dhand, (-d[0] * 2.2, -d[1] * 2.2)), add(dhand, (d[0] * 1.0, d[1] * 1.0))], 1.7, HILT, z=dz + 0.25))
        parts.append(line([add(add(dhand, (d[0] * 1.1, d[1] * 1.1)), (n[0] * 2.2, n[1] * 2.2)),
                           add(add(dhand, (d[0] * 1.1, d[1] * 1.1)), (-n[0] * 2.2, -n[1] * 2.2))], 1.2, GUARD, z=dz + 0.3))
        b0 = add(dhand, (d[0] * 1.6, d[1] * 1.6))
        parts.append(poly([add(b0, (n[0] * 1.1, n[1] * 1.1)), add(b0, (d[0] * L, d[1] * L)),
                           add(b0, (-n[0] * 1.1, -n[1] * 1.1))], STEEL, z=dz + 0.3, thick=0.3))
    arm(add(shoulder, (0.3, 0.1)), (P['fhx'], P['fhy']), 2.6, -1)

    # whole-body squash (around the feet) and rotation (rolls, swimming)
    if P['sq'] != 1.0:
        transform(parts, 0, (0, 13), sx=1 / math.sqrt(P['sq']), sy=P['sq'])
    if P['rot'] != 0:
        transform(parts, P['rot'], (0, 2))
    return parts


# ------------------------------------------------------------------------------------ poses

def idle(u, i):
    b = math.sin(u * math.tau)
    return build(dict(by=b * 0.35, dhy=2.5 + b * 0.35, fhy=3 + b * 0.3, sw=u * math.tau, sl=0.1,
                      blink=1 if 20 <= i <= 21 else 0))


def run(u, i):
    ph = u * math.tau
    c, s = math.cos(ph), math.sin(ph)
    lift_f = max(0, s) * 4.2
    lift_n = max(0, -s) * 4.2
    return build(dict(lean=0.22, by=-abs(c) * 0.9 + 0.6,
                      lfx=5.2 * c, lfy=13 - lift_f, lnx=-5.2 * c, lny=13 - lift_n,
                      dhx=3 - 3.2 * c, dhy=1.5, da=0.6 - 0.3 * c,
                      fhx=1.5 + 3.2 * c, fhy=1.6,
                      sw=ph * 2, sl=0.5, scarf=1.2, cf=0.8 + 0.3 * math.sin(2 * ph)))


def run_start(u, i):
    p = keys(u, [(0, dict(lean=0.06)), (1, dict(lean=0.35, by=0.8, lfx=-4, lnx=4, lny=11, dhx=1, fhx=4, cf=1.0, sl=0.4))])
    return build(p)


def run_stop(u, i):
    p = keys(u, [(0, dict(lean=-0.25, by=1.4, lfx=-2, lnx=6.5, lny=13, dhx=6, dhy=0, fhx=-5, fhy=0, cf=1.6, sl=0.8, scarf=1.2)),
                 (1, dict(lean=0.06))])
    return build(p)


def turn(u, i):
    return build(dict(by=0.3, lean=0.0, dhx=3.5, dhy=3, fhx=-1, fhy=3.2, sl=0.2, sw=u * 3))


def jump_start(u, i):
    p = keys(u, [(0, dict()), (1, dict(by=2.6, lean=0.25, sq=0.9, dhx=-1, dhy=2, fhx=-5, fhy=2.5, cf=0.4))])
    return build(p)


def jump_rise(u, i):
    w = math.sin(u * math.tau)
    return build(dict(by=-0.5, lean=0.1, lfx=-3.2, lfy=11.8, lnx=3.4, lny=9.5, dhx=5, dhy=-2 + w * 0.3,
                      fhx=-4, fhy=-2.5, sl=-0.6, scarf=0.9, sw=u * math.tau, cf=-0.3, sq=1.04))


def jump_apex(u, i):
    p = keys(u, [(0, dict(by=-0.5, lean=0.1, lfx=-3.2, lfy=11.8, lnx=3.4, lny=9.5, dhx=5, dhy=-2, fhx=-4, fhy=-2.5, sl=-0.6, scarf=0.9)),
                 (1, dict(by=-1.0, lean=0.12, lfx=-1.8, lfy=10.2, lnx=3.6, lny=8.8, dhx=6, dhy=0.5, fhx=-5, fhy=0, sl=0.3, scarf=1.0, cf=0.3))])
    return build(p)


def fall(u, i):
    w = math.sin(u * math.tau)
    return build(dict(by=-0.5, lean=0.0, lfx=-2.4, lfy=13, lnx=3.2, lny=12, dhx=6, dhy=-3.5 + w * 0.5,
                      fhx=-5.5, fhy=-4 - w * 0.5, sl=1.3, scarf=0.8, sw=u * math.tau * 2, cf=-1.0, sq=1.03))


def land(u, i):
    p = keys(u, [(0, dict(by=3.0, lean=0.3, sq=0.86, lfx=-3.6, lnx=3.8, dhx=6.5, dhy=5, fhx=-5, fhy=4, cf=1.3, sl=-0.3)),
                 (0.35, dict(by=2.4, lean=0.25, sq=0.9, lfx=-3.4, lnx=3.6, dhx=6, dhy=4.5, fhx=-4.5, fhy=4, cf=1.0)),
                 (1, dict())], ease=ease_out)
    return build(p)


def wall_slide(u, i):
    w = math.sin(u * math.tau * 2)
    return build(dict(lean=-0.15, lfx=5.2, lfy=11.5, lnx=4.2, lny=13.2, fhx=5.8, fhy=-7 + w * 0.3,
                      dhx=-1.5, dhy=1, da=2.3, sl=1.2, scarf=0.7, sw=u * math.tau * 2, cf=-0.8))


def swim(u, i):
    ph = u * math.tau
    c, s = math.cos(ph), math.sin(ph)
    # breaststroke arms + flutter kick, body roughly horizontal
    return build(dict(rot=1.25, lean=0.05, lfx=-1.5 + 1.5 * s, lfy=13, lnx=1.5 - 1.5 * s, lny=12.5,
                      dhx=6 + 3 * c, dhy=-6 + 3 * s, da=-1.4, fhx=5 + 3 * c, fhy=-4 + 3 * s,
                      sl=-1.0, scarf=1.1, sw=ph * 2, cf=-0.6))


def swim_idle(u, i):
    ph = u * math.tau
    c, s = math.cos(ph), math.sin(ph)
    return build(dict(rot=0.25, by=s * 0.5, lfx=-2 + 2.5 * s, lfy=12.4, lnx=2 - 2.5 * s, lny=12.6,
                      dhx=6 + 1.5 * c, dhy=1 + s, da=0.3, fhx=-4 - 1.5 * c, fhy=1 - s, sl=0.6, sw=ph, scarf=0.8))


def dodge(u, i):
    # tucked forward roll: crouch, full rotation, spring out
    tuck = math.sin(min(1, u * 1.15) * math.pi)
    return build(dict(rot=u * math.tau, by=2.0 * tuck, lean=0.4 * tuck, sq=1 - 0.2 * tuck,
                      lfx=lerp(-2.4, 1.5, tuck), lfy=lerp(13, 8.5, tuck), lnx=lerp(2.6, 3.5, tuck), lny=lerp(13, 8, tuck),
                      dhx=lerp(5.5, 3, tuck), dhy=lerp(2.5, 1, tuck), fhx=lerp(-2.5, 2.5, tuck), fhy=lerp(3, 1, tuck),
                      sl=0.8, scarf=1.2, sw=u * 10, cf=0.5))


def airdash(u, i):
    p = keys(u, [(0, dict(rot=0.2, lean=0.4, lfx=-5, lfy=11, lnx=-3, lny=12, dhx=8, dhy=-2, da=0.0, fhx=-5, fhy=0, sl=0.4, scarf=1.4, cf=1.2)),
                 (1, dict(rot=0.1, lean=0.3, lfx=-4, lfy=12, lnx=-1, lny=12.5, dhx=7, dhy=0, da=0.3, fhx=-4, fhy=1, sl=0.3, scarf=1.2, cf=0.8))])
    return build(p)


DIRS = {'up': -math.pi / 2, 'upfwd': -math.pi / 4, 'fwd': 0.0, 'downfwd': math.pi / 4, 'down': math.pi / 2}


def slash(kind, alpha):
    """Anticipation -> contact -> follow-through -> recover, aimed along alpha (facing right)."""
    if kind == 'a':
        a0, a1, reach = alpha - 1.5, alpha + 1.1, 8.2
    elif kind == 'b':
        a0, a1, reach = alpha + 1.5, alpha - 1.1, 8.2
    else:
        a0, a1, reach = alpha - 2.6, alpha + 1.4, 8.8
    heavy = kind == 'c'
    lean_to = 0.25 * math.cos(alpha) + (0.25 if alpha > 0.5 else 0) - (0.3 if alpha < -0.5 else 0)
    step = 1.0 if abs(alpha) < 1.2 else 0.0

    def f(u, i):
        # the swing angle: small wind-up back, then a fast arc, then settle
        p = keys(u, [
            (0.0, dict(sa=a0 - (a1 - a0) * 0.12, lean=-0.15 * math.cos(alpha), lnx=2.6, bx=0, sq=1.0, cf=0.2)),
            (0.18, dict(sa=a0, lean=-0.2 * math.cos(alpha) - 0.05, lnx=2.6, bx=-0.5, sq=0.96, cf=0.3)),
            (0.42, dict(sa=a1 - (a1 - a0) * 0.15, lean=lean_to + (0.15 if heavy else 0), lnx=2.6 + 3 * step, bx=1.2 * step + (1 if heavy else 0), sq=1.03, cf=1.2)),
            (0.6, dict(sa=a1, lean=lean_to, lnx=2.6 + 3 * step, bx=1.2 * step, sq=1.0, cf=1.0)),
            (1.0, dict(sa=a1 - (a1 - a0) * 0.08, lean=0.06, lnx=2.6 + 1.5 * step, bx=0.4 * step, sq=1.0, cf=0.3)),
        ], ease=smooth)
        sa = p.pop('sa')
        sh = (1.2 + p['bx'], -4 + 0)
        hand = polar(sh, reach * (0.85 + 0.15 * math.sin(min(1, u * 1.6) * math.pi)), sa)
        extra = dict(p, dhx=hand[0], dhy=hand[1], da=sa + 0.25 * (1 if a1 > a0 else -1), armz=3.6,
                     fhx=-3.5 - 1.5 * math.cos(alpha), fhy=1.5 - 2 * math.sin(alpha) * 0.3,
                     sl=0.5, scarf=1.1, sw=u * 6)
        if alpha > 1.0:  # downward strike: tuck the legs (usually airborne)
            extra.update(lfx=-2.5, lfy=10.5, lnx=3.5, lny=9.5)
        if alpha < -1.0:
            extra.update(ht=-0.25)
        return build(extra)
    return f


def throw(u, i):
    p = keys(u, [(0, dict(sa=-2.6, lean=-0.2, lnx=3.5, bx=-0.5, cf=0.3)),
                 (0.35, dict(sa=-2.9, lean=-0.3, lnx=3.5, bx=-1.0, cf=0.3)),
                 (0.55, dict(sa=-0.1, lean=0.3, lnx=5.5, bx=1.2, cf=1.1)),
                 (1.0, dict(sa=0.5, lean=0.1, lnx=3.5, bx=0.3, cf=0.4))], ease=smooth)
    sa = p.pop('sa')
    hand = polar((1.2 + p['bx'], -4), 8.0, sa)
    return build(dict(p, dhx=hand[0], dhy=hand[1], da=sa, armz=3.4, blade=0 if u > 0.55 else 1,
                      fhx=-2 + 2 * u, fhy=2, sl=0.4))


def hurt(u, i):
    p = keys(u, [(0, dict(lean=-0.55, bx=-1.5, by=0.8, ht=-0.4, dhx=6.5, dhy=-3, fhx=-6, fhy=-4, sq=0.92, cf=-1.2, sl=1.0, blink=1)),
                 (0.4, dict(lean=-0.45, bx=-1.2, by=0.6, ht=-0.3, dhx=6, dhy=-1.5, fhx=-5.5, fhy=-2, sq=0.95, cf=-0.8, sl=0.8, blink=1)),
                 (1, dict())], ease=ease_out)
    return build(p)


def death(u, i):
    p = keys(u, [(0, dict(lean=-0.5, bx=-1, ht=-0.4, dhx=6, dhy=-3, fhx=-6, fhy=-4, blink=1)),
                 (0.3, dict(lean=0.5, bx=1, by=4.5, lfx=-4, lnx=4.5, lny=13, ht=0.5, dhx=7, dhy=8, fhx=4, fhy=8, blink=1, sq=0.9)),
                 (0.55, dict(lean=0.9, bx=2, by=6.5, lfx=-5, lfy=13, lnx=3, lny=13, ht=0.7, dhx=9, dhy=11, fhx=7, fhy=11, blink=1, sq=0.85, blade=1)),
                 (1, dict(lean=1.45, bx=3, by=9.5, lfx=-9, lfy=13, lnx=-7, lny=13, ht=0.3, dhx=12, dhy=12.5, fhx=10, fhy=12.5, blink=1, sq=0.8, blade=0.9, cf=-0.5, sl=-0.8, scarf=0.7))],
             ease=ease_in)
    return build(p)


def make():
    sh = Sheet('player', 64, 60, origin=(30, 34), scale=2.0, outline_w=0.9)
    sh.add_anim('idle', 24, True, idle)
    sh.add_anim('run', 16, True, run)
    sh.add_anim('run_start', 3, False, run_start)
    sh.add_anim('run_stop', 5, False, run_stop)
    sh.add_turns(5, turn)
    sh.add_anim('jump_start', 3, False, jump_start)
    sh.add_anim('jump_rise', 4, True, jump_rise)
    sh.add_anim('jump_apex', 4, False, jump_apex)
    sh.add_anim('fall', 4, True, fall)
    sh.add_anim('land', 5, False, land)
    sh.add_anim('wall_slide', 8, True, wall_slide)
    sh.add_anim('swim', 16, True, swim)
    sh.add_anim('swim_idle', 24, True, swim_idle)
    sh.add_anim('dodge', 6, False, dodge)
    sh.add_anim('airdash', 4, False, airdash)
    for kind in ('a', 'b', 'c'):
        for dname, alpha in DIRS.items():
            sh.add_anim(f'slash_{kind}_{dname}', 7 if kind != 'c' else 9, False, slash(kind, alpha))
    sh.add_anim('throw', 6, False, throw)
    sh.add_anim('hurt', 6, False, hurt)
    sh.add_anim('death', 18, False, death)
    return sh
