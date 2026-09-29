"""The five heroes, drawn by one rig with a style switch:
 * swordsman    -- a hooded rogue with a red scarf and a medium-length sword;
 * warden       -- a blue-tabarded shield-bearer with a gold sash, a shortsword and a buckler;
 * vitalist     -- a green-robed caster in a bone mask, with a crystal-headed staff;
 * elementalist -- a violet-robed caster with an ember sash, a staff crowned with a burning orb;
 * rogue        -- a masked cutthroat in charcoal and plum, with a dagger in each hand to throw.
Right-handed (the weapon hand is the far arm when facing right, the near arm when facing left)."""
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
BLADE_LEN = 8.5      # set per style by make()
BLADE_W = 1.1
GUARD_W = 2.2
BUCKLER = False
BUCKLER_WOOD = hexc('7a5530')
BUCKLER_RIM = hexc('b8c4cc')
STAFF = False
THROW = False
STAFF_WOOD = hexc('5a3d22')
STAFF_GEM = hexc('8dff7a')
MASK = hexc('e2dac8')

STYLES = {
    'swordsman': dict(CLOAK='2b6272', CLOAK_D='183d49', SCARF='cc3a2e', PANTS='2e2c38', BELT='70502c',
                      BLADE_LEN=13.0, BLADE_W=1.3, GUARD_W=2.8, BUCKLER=False),
    'warden': dict(CLOAK='34457e', CLOAK_D='1f2a52', SCARF='d4a93a', PANTS='3a3530', BELT='5a3c22',
                   BLADE_LEN=9.5, BLADE_W=1.25, GUARD_W=2.5, BUCKLER=True),
    'vitalist': dict(CLOAK='24503a', CLOAK_D='122a1e', SCARF='8e1f24', PANTS='2a2a26', BELT='4a3020',
                     BLADE_LEN=0.0, BLADE_W=1.0, GUARD_W=1.0, BUCKLER=False, STAFF=True),
    'elementalist': dict(CLOAK='4b3a7c', CLOAK_D='281d48', SCARF='e0762a', PANTS='2a2634', BELT='4a3a30',
                         BLADE_LEN=0.0, BLADE_W=1.0, GUARD_W=1.0, BUCKLER=False, STAFF=True,
                         STAFF_WOOD='cfc6b4', STAFF_GEM='ff9a3a', MASK='c9b39a'),
    'rogue': dict(CLOAK='3a3346', CLOAK_D='1f1b28', SCARF='2a2430', PANTS='262230', BELT='4a3322',
                  BLADE_LEN=5.5, BLADE_W=1.0, GUARD_W=1.6, BUCKLER=False, THROW=True),
}
# (every style starts from these, so one built after another never inherits its looks)
STYLE_DEFAULTS = dict(BUCKLER=False, STAFF=False, THROW=False, STAFF_WOOD='5a3d22', STAFF_GEM='8dff7a', MASK='e2dac8')

BASE = dict(sa=0.0, bx=0, by=0, lean=0.06, lfx=-2.4, lfy=13, lnx=2.6, lny=13,
            dhx=5.5, dhy=2.5, da=1.0, fhx=-2.5, fhy=3.0, ht=0.0, sw=0.0, sl=0.0, cf=0.0,
            armz=-3.0, sq=1.0, rot=0.0, kneeb=1.0, blade=1.0, scarf=1.0, blink=0.0, st=-1.45, gem=0.0)


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
    parts.append(ellipse(add(head, rot((1.9, 0.9), P['ht'])), 3.3, 3.6, MASK if STAFF else SKIN, z=0.0, thick=0.9, rot=P['ht'], bias=0.5))
    blink = P['blink']
    for ez in (1.6, -1.6):
        ec = add(head, rot((3.4, 0.3), P['ht']))
        if STAFF:
            # the mask's eye holes, lit from within
            parts.append(ellipse(ec, 0.9, 1.1, EYE, z=ez + 0.4, outline=False, shade=False))
            parts.append(circle(ec, 0.45, STAFF_GEM, z=ez + 0.45, outline=False, shade=False))
        elif blink > 0.5:
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
    if STAFF:
        # a staff through the fist: the crystal end leads (st = its angle), the heel trails
        d = (math.cos(P['st']), math.sin(P['st']))
        top = add(dhand, (d[0] * 11.0, d[1] * 11.0))
        heel = add(dhand, (-d[0] * 9.0, -d[1] * 9.0))
        parts.append(line([heel, top], 1.3, STAFF_WOOD, z=dz + 0.25))
        n = (-d[1], d[0])
        for k in (-1, 1):
            parts.append(line([add(top, (-d[0] * 1.2, -d[1] * 1.2)), add(top, (d[0] * 1.6 + n[0] * k * 1.5, d[1] * 1.6 + n[1] * k * 1.5))],
                              0.7, STAFF_WOOD, z=dz + 0.3))
        gem = add(top, (d[0] * 1.4, d[1] * 1.4))
        if P['gem'] > 0.05:
            parts.append(glow(gem, 3.0 + 3.0 * P['gem'], STAFF_GEM[:3] + (0.5 * P['gem'],), z=dz + 0.2))
        parts.append(circle(gem, 1.3, STAFF_GEM, z=dz + 0.35))
    # dagger in the right hand
    elif P['blade'] > 0.05:
        d = (math.cos(P['da']), math.sin(P['da']))
        n = (-d[1], d[0])
        L = BLADE_LEN * P['blade']
        grip = 2.2 if BLADE_LEN < 12 else 3.0
        parts.append(line([add(dhand, (-d[0] * grip, -d[1] * grip)), add(dhand, (d[0] * 1.0, d[1] * 1.0))], 1.7, HILT, z=dz + 0.25))
        parts.append(line([add(add(dhand, (d[0] * 1.1, d[1] * 1.1)), (n[0] * GUARD_W, n[1] * GUARD_W)),
                           add(add(dhand, (d[0] * 1.1, d[1] * 1.1)), (-n[0] * GUARD_W, -n[1] * GUARD_W))], 1.2, GUARD, z=dz + 0.3))
        b0 = add(dhand, (d[0] * 1.6, d[1] * 1.6))
        if BLADE_LEN < 12:
            parts.append(poly([add(b0, (n[0] * BLADE_W, n[1] * BLADE_W)), add(b0, (d[0] * L, d[1] * L)),
                               add(b0, (-n[0] * BLADE_W, -n[1] * BLADE_W))], STEEL, z=dz + 0.3, thick=0.3))
        else:
            # a straight double-edged blade that tapers only near the tip
            sh = add(b0, (d[0] * L * 0.82, d[1] * L * 0.82))
            parts.append(poly([add(b0, (n[0] * BLADE_W, n[1] * BLADE_W)), add(sh, (n[0] * BLADE_W * 0.85, n[1] * BLADE_W * 0.85)),
                               add(b0, (d[0] * L, d[1] * L)),
                               add(sh, (-n[0] * BLADE_W * 0.85, -n[1] * BLADE_W * 0.85)), add(b0, (-n[0] * BLADE_W, -n[1] * BLADE_W))],
                              STEEL, z=dz + 0.3, thick=0.3))
    arm(add(shoulder, (0.3, 0.1)), (P['fhx'], P['fhy']), 2.6, -1)
    if BUCKLER:
        fh = (P['fhx'], P['fhy'])
        parts.append(circle(fh, 3.6, BUCKLER_RIM, z=2.95))
        parts.append(circle(fh, 2.8, BUCKLER_WOOD, z=3.0, outline=False))
        parts.append(circle(fh, 0.9, BUCKLER_RIM, z=3.05, outline=False))

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


WOOSH = (0.88, 0.95, 1.0)


def woosh_arc(sh, r0, r1, s, e, z):
    """A translucent motion smear between radii r0..r1 from angle s to e around the shoulder:
    a faint full-width band, a brighter outer band, and a bright leading edge."""
    parts = []
    n = 10
    angs = [s + (e - s) * k / (n - 1) for k in range(n)]
    rm = r0 + (r1 - r0) * 0.55
    inner = [polar(sh, r0, a) for a in angs]
    mid = [polar(sh, rm, a) for a in angs]
    outer = [polar(sh, r1, a) for a in angs]
    parts.append(poly(outer + inner[::-1], WOOSH + (0.16,), z=z, outline=False, shade=False))
    parts.append(poly(outer + mid[::-1], WOOSH + (0.3,), z=z + 0.01, outline=False, shade=False))
    parts.append(line([polar(sh, r0 + 1, e), polar(sh, r1 + 0.6, e)], 1.2, (1, 1, 1, 0.95), z=z + 0.02))
    parts.append(line(outer[n // 2:], 0.8, (1, 1, 1, 0.6), z=z + 0.02))
    return parts


def slash(kind, alpha):
    """Traditional three-beat swing, aimed along alpha (facing right):
      wind-up  (frames 1-2, three for the finisher): the sword pulled far back, body coiled;
      woosh    (the next two frames): the blade becomes a motion smear, body lunging through;
      follow-through (the rest): the sword past its target, held for a beat, then recovering."""
    if kind == 'a':
        a0, a1 = alpha - 1.75, alpha + 1.3
    elif kind == 'b':
        a0, a1 = alpha + 1.75, alpha - 1.3
    else:
        a0, a1 = alpha - 2.8, alpha + 1.6
    heavy = kind == 'c'
    wind = 3 if heavy else 2
    lean_to = 0.32 * math.cos(alpha) + (0.25 if alpha > 0.5 else 0) - (0.3 if alpha < -0.5 else 0)
    step = 1.0 if abs(alpha) < 1.2 else 0.0
    span = a1 - a0

    def f(u, i):
        common = dict(sl=0.5, scarf=1.2, fhx=-3.5 - 1.5 * math.cos(alpha), fhy=1.5 - 0.6 * math.sin(alpha), armz=3.6)
        extra = {}
        if i < wind:
            # coil: pull the sword back past the start of the arc, crouch and lean away
            w = (i + 1) / wind
            sa = a0 - span * 0.16 * w
            body = dict(lean=-0.32 * math.cos(alpha) * w, bx=-1.3 * w, by=0.9 * w, sq=1 - 0.1 * w, cf=0.2, lnx=2.6, lfx=-3.2)
            reach = 7.2
            blade = 1.0
            smear = None
        elif i < wind + 2:
            # the woosh: blade replaced by a smear, body thrown forward and stretched
            j = i - wind
            sa = a0 + span * (0.62 if j == 0 else 1.0)
            body = dict(lean=lean_to + (0.2 if heavy else 0.12), bx=1.8 * step + (1 if heavy else 0), by=-0.3, sq=1.07,
                        cf=1.4, lnx=2.6 + 4.2 * step, lfx=-3.8)
            reach = 9.2
            blade = 0.0
            smear = (a0 - span * 0.05, a0 + span * 0.62) if j == 0 else (a0 + span * 0.3, a1)
        else:
            # follow-through: overshoot, hold (the dramatic beat), then settle back
            j = i - wind - 2
            n_follow = (9 if heavy else 7) - wind - 2
            over = [0.14, 0.12, 0.03, -0.06][min(j, 3)]
            settle = 0 if j < 2 else (j - 1) / max(1, n_follow - 1)
            sa = a1 + span * over
            body = dict(lean=lean_to * (1 - settle) + 0.06 * settle, bx=1.4 * step * (1 - settle), by=0.2 * (1 - settle),
                        sq=1.0 - 0.03 * (1 - settle), cf=1.0 - 0.7 * settle, lnx=2.6 + 3.5 * step * (1 - settle) + 1.0 * settle,
                        lfx=-3.4)
            reach = 8.6 - 0.8 * settle
            blade = 1.0
            smear = None
        sh = (1.2 + body['bx'], -4 + body.get('by', 0) * 0.5)
        hand = polar(sh, reach, sa)
        extra = dict(body, dhx=hand[0], dhy=hand[1], da=sa + 0.25 * (1 if a1 > a0 else -1), blade=blade, sw=u * 6, **common)
        if alpha > 1.0:  # downward strike: tuck the legs (usually airborne)
            extra.update(lfx=-2.5, lfy=10.5, lnx=3.5, lny=9.5)
        if alpha < -1.0:
            extra.update(ht=-0.25)
        parts = build(extra)
        if smear is not None:
            r1 = reach + 1.6 + BLADE_LEN
            parts += woosh_arc(sh, reach + 1.2, r1, smear[0], smear[1], 4.5)
        return parts
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


def bash(u, i):
    """The Warden's shield dash: shield driven forward, low and leaning into it, sword held back."""
    p = keys(u, [(0, dict(lean=0.2, bx=0.5, fhx=2, fhy=0, dhx=-1, dhy=3, da=2.0, lnx=4, lfx=-3, cf=0.8, sl=0.6)),
                 (0.3, dict(lean=0.45, bx=1.4, by=1.0, fhx=6.5, fhy=-1.5, dhx=-2.5, dhy=2.5, da=2.4, lnx=6.5, lfx=-4.5, cf=1.5, sl=1.0, scarf=1.4)),
                 (1, dict(lean=0.4, bx=1.2, by=0.8, fhx=6, fhy=-1, dhx=-2, dhy=2.5, da=2.3, lnx=6, lfx=-4, cf=1.3, sl=0.9, scarf=1.3))], ease=ease_out)
    return build(p)


def cast(u, i):
    """The Vitalist's drain bolt: the staff drawn back, then thrust out crystal-first."""
    p = keys(u, [(0, dict(st=-1.9, dhx=2.5, dhy=0.5, lean=-0.1, fhx=-2, fhy=2)),
                 (0.35, dict(st=-2.15, dhx=1.2, dhy=-1.0, lean=-0.16, bx=-0.6, fhx=-3, fhy=1)),
                 (0.55, dict(st=-0.3, dhx=8.0, dhy=-1.5, lean=0.25, bx=0.8, fhx=6.0, fhy=-2.0, gem=1.0, lnx=4.5, cf=1.0)),
                 (1, dict(st=-1.25, dhx=5.5, dhy=1.0, lean=0.08, fhx=0, fhy=2.5, gem=0.3))], ease=smooth)
    return build(p)


def hex_(u, i):
    """The Vitalist's hex: staff raised overhead in both hands, then driven down into the ground."""
    p = keys(u, [(0, dict()),
                 (0.4, dict(st=-1.57, dhx=3.0, dhy=-9.0, fhx=-0.5, fhy=-8.0, lean=-0.2, by=-0.5, gem=0.6, ht=-0.2)),
                 (0.6, dict(st=-1.35, dhx=6.0, dhy=4.0, fhx=4.0, fhy=2.0, lean=0.38, by=2.6, sq=0.9, gem=1.0, cf=1.2, lnx=4.5, lfx=-3.5)),
                 (0.8, dict(st=-1.4, dhx=6.0, dhy=3.5, fhx=4.0, fhy=2.0, lean=0.34, by=2.2, sq=0.93, gem=0.8, cf=1.0, lnx=4.5, lfx=-3.5)),
                 (1, dict())], ease=smooth)
    return build(p)


def heal(u, i):
    """The Vitalist's heal: the staff lifted high, the free hand opened, face turned upward."""
    p = keys(u, [(0, dict()),
                 (0.3, dict(st=-1.57, dhx=2.5, dhy=-10.0, fhx=-5.5, fhy=-5.5, lean=-0.12, ht=-0.3, gem=1.0, by=-0.4)),
                 (0.75, dict(st=-1.57, dhx=2.5, dhy=-10.5, fhx=-6.0, fhy=-6.0, lean=-0.14, ht=-0.35, gem=0.8, by=-0.6)),
                 (1, dict())], ease=smooth)
    return build(p)


def heave(u, i):
    """The Swordsman's heaving swing (16 frames): both hands on the hilt, the sword hauled up over
    the head and far behind (seven frames, rooted), brought over and down in one great arc (two
    frames of smear), then a deep lunge held a moment before recovering."""
    wind, sweep = 7, 2
    a0, a1 = -2.75, 1.05
    if i < wind:
        w = smooth((i + 1) / wind)
        sa = lerp(-0.9, a0, w)
        body = dict(lean=-0.28 * w, bx=-1.0 * w, by=0.5 * w, sq=1 - 0.05 * w, cf=0.2, lnx=3.0, lfx=-3.4, ht=-0.25 * w)
        reach, blade, smear = 6.4, 1.0, None
    elif i < wind + sweep:
        j = i - wind
        sa = a1 if j else lerp(a0, a1, 0.55)
        body = dict(lean=0.5, bx=2.2, by=1.8, sq=0.94, cf=1.6, lnx=7.0, lfx=-4.6, ht=0.1)
        reach, blade = 8.6, 0.0
        smear = (a0 - 0.1, lerp(a0, a1, 0.6)) if j == 0 else (lerp(a0, a1, 0.35), a1)
    else:
        j = i - wind - sweep
        settle = smooth(j / (16 - wind - sweep - 1))
        sa = a1 + 0.1 - 0.35 * settle
        body = dict(lean=0.5 * (1 - settle) + 0.06 * settle, bx=2.2 * (1 - settle), by=1.8 * (1 - settle), sq=0.94 + 0.06 * settle,
                    cf=1.6 - 1.3 * settle, lnx=7.0 - 4.4 * settle, lfx=-4.6 + 2.2 * settle)
        reach, blade, smear = 8.2 - 1.5 * settle, 1.0, None
    sh = (1.2 + body['bx'], -4 + body.get('by', 0) * 0.5)
    hand = polar(sh, reach, sa)
    # the off hand on the hilt, just below the sword hand
    off = polar(sh, reach - 1.8, sa)
    parts = build(dict(body, dhx=hand[0], dhy=hand[1], da=sa + 0.2, blade=blade, fhx=off[0], fhy=off[1], armz=3.6,
                       sl=0.6, scarf=1.3, sw=u * 7))
    if smear is not None:
        parts += woosh_arc(sh, reach + 1.2, reach + 2.0 + BLADE_LEN, smear[0], smear[1], 4.5)
    return parts


def shove(u, i):
    """The Warden's shield bash (8 frames): a short step and the buckler punched straight out."""
    p = keys(u, [(0, dict(lean=0.1, fhx=0.5, fhy=1.5, dhx=-1.0, dhy=3.0, da=2.2, lnx=3.0, lfx=-2.6)),
                 (0.25, dict(lean=0.42, bx=1.6, by=0.8, fhx=8.0, fhy=-1.0, dhx=-2.5, dhy=2.0, da=2.5, lnx=7.0, lfx=-4.2, cf=1.5, sl=1.0, scarf=1.4)),
                 (0.55, dict(lean=0.38, bx=1.4, by=0.7, fhx=7.4, fhy=-0.8, dhx=-2.2, dhy=2.2, da=2.4, lnx=6.5, lfx=-4.0, cf=1.3, sl=0.9, scarf=1.3)),
                 (1, dict())], ease=ease_out)
    return build(p)


def rupture(u, i):
    """The Vitalist's rupture (12 frames): the staff raised behind, the free hand thrust out open
    at the creature... then clenched and torn back to the chest as it bursts."""
    p = keys(u, [(0, dict()),
                 (0.35, dict(st=-2.0, dhx=0.5, dhy=-6.5, fhx=8.5, fhy=-3.0, lean=0.22, bx=0.6, gem=0.8, lnx=5.0, lfx=-3.2, cf=0.8, ht=-0.1)),
                 (0.46, dict(st=-2.0, dhx=0.5, dhy=-6.5, fhx=8.8, fhy=-3.2, lean=0.25, bx=0.7, gem=1.0, lnx=5.0, lfx=-3.2, cf=0.9, ht=-0.1)),
                 (0.56, dict(st=-1.9, dhx=0.0, dhy=-6.0, fhx=2.5, fhy=-1.0, lean=-0.18, bx=-0.8, gem=1.0, lnx=3.4, lfx=-2.8, cf=-0.4, sl=0.8)),
                 (0.8, dict(st=-1.7, dhx=2.0, dhy=-2.0, fhx=2.0, fhy=0.0, lean=-0.08, gem=0.5)),
                 (1, dict())], ease=smooth)
    return build(p)


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


def make(style='swordsman'):
    g = globals()
    for k, v in list(STYLE_DEFAULTS.items()) + list(STYLES[style].items()):
        g[k] = hexc(v) if isinstance(v, str) else v
    long_blade = BLADE_LEN > 12 or STAFF
    sh = Sheet(style, 72 if long_blade else 64, 66 if long_blade else 60,
               origin=(36, 36) if long_blade else (30, 34), scale=2.0, outline_w=0.9)
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
    if STAFF:
        # the Vitalist fights with spells, not a blade
        sh.add_anim('cast', 6, False, cast)
        sh.add_anim('hex', 10, False, hex_)
        sh.add_anim('heal', 12, False, heal)
        sh.add_anim('rupture', 12, False, rupture)
    else:
        for kind in ('a', 'b', 'c'):
            for dname, alpha in DIRS.items():
                sh.add_anim(f'slash_{kind}_{dname}', 7 if kind != 'c' else 9, False, slash(kind, alpha))
        if THROW:
            # the Rogue throws its daggers (and recalls them) instead of heaving
            sh.add_anim('throw', 8, False, throw)
        elif not BUCKLER:
            sh.add_anim('heave', 16, False, heave)
    if BUCKLER:
        sh.add_anim('bash', 6, False, bash)
        sh.add_anim('shove', 8, False, shove)
    sh.add_anim('hurt', 6, False, hurt)
    sh.add_anim('death', 18, False, death)
    return sh
