"""Monsters for the deeper biomes: rat, bear, scorpion, hornet, skeleton, sporeling, frost wraith,
shardling, and the dragon. Same conventions as creatures.py: facing right, origin at the body
centre, y down, world pixels."""
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


def fade_out(parts, u, fall=0.0, spin=0.0):
    return transform(parts, spin * u, (0, 0), offset=(0, fall * u))


# ============================================================================================ RAT
RAT = hexc('5a4c46')
RAT_D = hexc('3a302c')
RAT_BELLY = hexc('8c7b6d')
RAT_TAIL = hexc('b08c82')
RAT_BASE = dict(gait=0.0, stride=0.0, by=0.0, pitch=0.0, lunge=0.0, jaw=0.0, blink=0.0, tailw=0.0, flip=0.0, hunch=0.0)
rk = K(RAT_BASE)


def rat(p):
    p = P_(RAT_BASE, p)
    parts = []
    by = p['by']
    body = (-0.5 + p['lunge'] * 0.4, 1.0 + by - p['hunch'])
    head = add(body, (6.2 + p['lunge'], -0.8 + p['hunch'] * 0.5))
    # tail: a long whip with a lazy wave
    tail = [add(body, (-5.5, 1.0))]
    for k in range(1, 7):
        tail.append(add(body, (-5.5 - k * 2.3, 1.0 - k * 0.3 + math.sin(p['tailw'] + k * 0.9) * 0.35 * k)))
    parts.append(limb(tail, [1.6, 1.4, 1.2, 1.0, 0.8, 0.6, 0.5], RAT_TAIL, z=-2.5))
    # legs
    for side, z in ((-1, -2.0), (1, 2.0)):
        for k, hx in enumerate((3.2, -3.0)):
            ph = p['gait'] + k * math.pi + (0 if side > 0 else math.pi * 0.5)
            lift = max(0, math.sin(ph)) * 1.6 * p['stride']
            root = add(body, (hx, 1.5))
            foot = (root[0] + math.cos(ph) * 2.2 * p['stride'] + 0.5, 6.2 - lift)
            parts.append(limb([root, foot], [1.8, 1.1], RAT_D if side < 0 else darken(RAT, 0.1), z=z))
            parts.append(ellipse(add(foot, (0.8, 0)), 1.2, 0.6, RAT_TAIL, z=z + 0.1))
    parts.append(ellipse(body, 6.8, 3.9 + p['hunch'] * 0.6, RAT, z=0, rot=-0.08, thick=0.9))
    parts.append(ellipse(add(body, (1.0, 1.8)), 4.8, 1.8, RAT_BELLY, z=0, bias=0.5, outline=False))
    # mangy fur tufts along the back
    for k in range(4):
        b = add(body, (-4 + k * 2.4, -3.4 - p['hunch'] * 0.4))
        parts.append(poly([add(b, (-1, 0.6)), add(b, (0.3, -1.4)), add(b, (1, 0.6))], RAT_D, z=0, bias=0.3, outline=False))
    # head and snout
    j = p['jaw']
    parts.append(ellipse(head, 3.4, 2.7, RAT, z=0.2, thick=0.9))
    parts.append(poly([add(head, (1.6, -1.8)), add(head, (6.4, 0.2)), add(head, (5.8, 0.9 - j * 0.3)), add(head, (1.8, 1.4))], RAT, z=0.25, bias=0.5, thick=0.7))
    if j > 0.1:
        parts.append(poly([add(head, (1.8, 1.4)), add(head, (5.6, 1.2 + j * 1.8)), add(head, (2.2, 2.6 + j))], darken(RAT, 0.2), z=0.25, bias=0.45))
        for tx in (4.2, 5.0):
            parts.append(poly([add(head, (tx, 1.0)), add(head, (tx + 0.4, 2.2)), add(head, (tx + 0.8, 1.0))], hexc('f0e8d8'), z=0.3, bias=0.6, outline=False))
    parts.append(circle(add(head, (6.4, 0.3)), 0.7, hexc('2a1a1a'), z=0.3, bias=0.7, outline=False))
    for ez in (1.2, -1.2):
        parts.append(ellipse(add(head, (-1.2, -2.6)), 1.6, 1.9, darken(RAT_TAIL, 0.1), z=ez * 0.8, thick=0.4))
        if p['blink'] > 0.5:
            parts.append(line([add(head, (1.2, -0.9)), add(head, (2.6, -0.9))], 0.5, RAT_D, z=ez + 0.3))
        else:
            parts.append(circle(add(head, (1.9, -0.9)), 0.7, hexc('ff3a2a'), z=ez + 0.3, outline=False, shade=False))
    parts.append(glow(add(head, (1.9, -0.9)), 2.0, hexc('ff2010', 0.3), z=2))
    # whiskers
    for dy in (-0.3, 0.6):
        parts.append(line([add(head, (5.4, 0.2)), add(head, (8.2, dy - 0.4))], 0.3, hexc('d8d0c8'), z=0.4))
    transform(parts, p['pitch'], (0, 3))
    if p['flip']:
        transform(parts, math.pi * p['flip'], (0, 2))
    return parts


def make_rat():
    sh = Sheet('rat', 44, 28, origin=(24, 16), scale=2.0, outline_w=0.7)
    sh.add_anim('idle', 16, True, lambda u, i: rat(dict(by=math.sin(u * TAU) * 0.3, tailw=u * TAU, hunch=0.2 + 0.2 * math.sin(u * TAU * 2), blink=1 if i in (11, 12) else 0)))
    sh.add_anim('run', 8, True, lambda u, i: rat(dict(gait=u * TAU, stride=1.0, by=-abs(math.sin(u * TAU)) * 0.8, tailw=u * TAU * 2, pitch=0.05 * math.sin(u * TAU * 2))))
    sh.add_turns(4, lambda u, i: rat(dict(hunch=0.3)))
    sh.add_anim('windup', 5, False, lambda u, i: rat(rk(u, [(0, dict()), (1, dict(hunch=1.2, lunge=-1.5, pitch=0.15, jaw=0.4))], ease_out)))
    sh.add_anim('bite', 6, False, lambda u, i: rat(rk(u, [(0, dict(hunch=1.2, lunge=-1.5, pitch=0.15, jaw=0.4)), (0.3, dict(lunge=3.0, pitch=-0.15, jaw=1.0)), (1, dict(lunge=0.5, jaw=0.2))], ease_out)))
    sh.add_anim('hurt', 5, False, lambda u, i: rat(rk(u, [(0, dict(pitch=-0.5, jaw=0.8, blink=1, hunch=0.8)), (1, dict())], ease_out)))
    sh.add_anim('death', 10, False, lambda u, i: rat(rk(u, [(0, dict(pitch=-0.4, jaw=0.8, blink=1)), (1, dict(flip=1.0, by=-1, jaw=0.6, blink=1, stride=0.6, gait=2))])))
    return sh


# ============================================================================================ BEAR
BEAR = hexc('4c3627')
BEAR_D = hexc('2e2017')
BEAR_MUZ = hexc('8a6a52')
CLAW = hexc('e8e0d0')
BEAR_BASE = dict(gait=0.0, stride=0.0, by=0.0, rear=0.0, paw=0.0, jaw=0.0, pitch=0.0, blink=0.0, dead=0.0, head=0.0)
bek = K(BEAR_BASE)


def bear(p):
    p = P_(BEAR_BASE, p)
    parts = []
    rear = p['rear']
    hip = (-9.0, 4.0 + p['by'])
    # rearing rotates the whole torso up around the hips
    ang = -rear * 0.85

    def R(q):
        return add(rot(q, ang), hip)

    body_c = R((9, -4))
    shoulder = R((17, -6))
    # hind legs
    for side, z in ((-1, -5.0), (1, 5.0)):
        ph = p['gait'] + (0 if side > 0 else math.pi)
        lift = max(0, math.sin(ph)) * 3.5 * p['stride']
        root = add(hip, (0, 2))
        foot = (hip[0] - 1 + math.cos(ph) * 4 * p['stride'], 17.5 - lift)
        knee = ik2(root, foot, 7.5, 7.5, bend=1)
        col = BEAR_D if side < 0 else darken(BEAR, 0.05)
        parts.append(limb([root, knee, foot], [8.0, 6.5, 5.5], col, z=z))
        parts.append(ellipse(add(foot, (1.5, 0.3)), 4.0, 1.8, darken(col, 0.1), z=z + 0.1))
    # far front leg
    def front(side, z, col):
        ph = p['gait'] + math.pi * 0.6 + (0 if side > 0 else math.pi)
        lift = max(0, math.sin(ph)) * 3.5 * p['stride']
        root = shoulder
        if rear > 0.1 and side > 0:
            # the swiping paw: raised and slashing with `paw` (0 up/back, 1 down/forward)
            a = lerp(-2.2, 0.9, p['paw'])
            foot = polar(root, 14, a)
        elif rear > 0.1:
            foot = add(root, (5, 6 - rear * 4))
        else:
            foot = (root[0] + 2 + math.cos(ph) * 4 * p['stride'], 17.5 - lift)
        knee = ik2(root, foot, 7.0, 7.5, bend=-1)
        parts.append(limb([root, knee, foot], [7.0, 6.0, 5.0], col, z=z))
        parts.append(ellipse(add(foot, (1.5, 0.3)), 3.8, 2.0, darken(col, 0.1), z=z + 0.1))
        for k in range(3):
            parts.append(line([add(foot, (3.5, -1 + k)), add(foot, (5.8, -0.4 + k * 1.2))], 0.7, CLAW, z=z + 0.2))
    front(-1, -4.5, BEAR_D)
    # body
    body = [R(q) for q in [(-4, -6), (6, -13), (16, -12), (22, -6), (20, 3), (8, 6), (-4, 5)]]
    parts.append(poly(body, BEAR, z=0, thick=0.9, smooth=True))
    parts.append(poly([R(q) for q in [(0, -9), (8, -14.5), (14, -12.5), (6, -8)]], darken(BEAR, 0.18), z=0, bias=0.3, smooth=True, outline=False))
    # scars
    for k in range(3):
        parts.append(line([R((6 + k * 2.2, -9)), R((8 + k * 2.2, -4))], 0.6, hexc('7a5a48'), z=0, bias=0.4))
    # head
    hd = add(R((24, -8)), (0, p['head'] * 4))
    parts.append(circle(hd, 6.2, BEAR, z=0.3, thick=0.9))
    j = p['jaw']
    parts.append(poly([add(hd, (3, -2.5)), add(hd, (10, -0.5)), add(hd, (10, 2 - j * 0.5)), add(hd, (3, 3))], BEAR_MUZ, z=0.35, bias=0.6, thick=0.7))
    if j > 0.1:
        parts.append(poly([add(hd, (3, 3)), add(hd, (9.5, 2.5 + j * 3.5)), add(hd, (3.5, 5 + j * 2))], BEAR_D, z=0.35, bias=0.5))
        parts.append(poly([add(hd, (4, 3.2)), add(hd, (9, 2.2)), add(hd, (8.5, 2.2 + j * 3))], hexc('5a1818'), z=0.36, bias=0.55, outline=False))
        for tx in (5.0, 8.0):
            parts.append(poly([add(hd, (tx, 2.2)), add(hd, (tx + 0.5, 3.8)), add(hd, (tx + 1.0, 2.2))], CLAW, z=0.4, bias=0.6, outline=False))
    parts.append(circle(add(hd, (10, -0.3)), 1.2, hexc('1a1010'), z=0.4, bias=0.7, outline=False))
    for ez in (2.5, -2.5):
        parts.append(circle(add(hd, (-2.5, -5.2)), 2.1, darken(BEAR, 0.15), z=ez * 0.7))
        if p['blink'] > 0.5 or p['dead'] > 0.5:
            parts.append(line([add(hd, (2.2, -2.4)), add(hd, (4.2, -2.4))], 0.7, BEAR_D, z=ez + 0.4))
        else:
            parts.append(circle(add(hd, (3.2, -2.4)), 0.9, hexc('ffcc40'), z=ez + 0.4, outline=False, shade=False))
    parts.append(glow(add(hd, (3.2, -2.4)), 3.0, hexc('ffb020', 0.3), z=3))
    front(1, 4.5, darken(BEAR, 0.02))
    transform(parts, p['pitch'], (0, 12))
    return parts


def make_bear():
    sh = Sheet('bear', 84, 64, origin=(40, 40), scale=2.0, outline_w=0.9)
    sh.add_anim('idle', 24, True, lambda u, i: bear(dict(by=math.sin(u * TAU) * 0.5, head=0.1 * math.sin(u * TAU), blink=1 if i in (17, 18) else 0)))
    sh.add_anim('walk', 16, True, lambda u, i: bear(dict(gait=u * TAU, stride=1.0, by=-abs(math.sin(u * TAU)) * 0.8, head=0.2 * math.sin(u * TAU * 2))))
    sh.add_anim('run', 10, True, lambda u, i: bear(dict(gait=u * TAU, stride=1.5, by=-abs(math.sin(u * TAU)) * 1.6, pitch=0.06 * math.sin(u * TAU), head=0.5, jaw=0.4)))
    sh.add_turns(6, lambda u, i: bear(dict()))
    sh.add_anim('rear', 8, False, lambda u, i: bear(bek(u, [(0, dict()), (1, dict(rear=1.0, paw=0.0, jaw=0.8, head=-0.3))], ease_out)))
    sh.add_anim('swipe', 6, False, lambda u, i: bear(bek(u, [(0, dict(rear=1.0, paw=0.0, jaw=0.8)), (0.35, dict(rear=0.5, paw=1.0, jaw=1.0, pitch=0.1)), (1, dict(rear=0.0, paw=1.0, jaw=0.3))], ease_out)))
    sh.add_anim('roar', 14, False, lambda u, i: bear(bek(u, [(0, dict()), (0.25, dict(rear=0.5, jaw=1.0, head=-0.6, by=math.sin(u * 50) * 0.5)), (0.8, dict(rear=0.5, jaw=1.0, head=-0.6)), (1, dict())])))
    sh.add_anim('hurt', 5, False, lambda u, i: bear(bek(u, [(0, dict(pitch=-0.12, jaw=0.7, blink=1, head=-0.3)), (1, dict())], ease_out)))
    sh.add_anim('death', 16, False, lambda u, i: bear(bek(u, [(0, dict(jaw=0.8, blink=1, head=-0.3)), (0.5, dict(pitch=0.25, by=3, jaw=0.5, dead=1, head=0.5)), (1, dict(pitch=0.45, by=6, jaw=0.4, dead=1, head=1.0, stride=0.3))], ease_in)))
    return sh


# ============================================================================================ SCORPION
SC = hexc('8a4a28')
SC_D = hexc('5a2c18')
SC_STING = hexc('f0d060')
SC_BASE = dict(gait=0.0, tail=0.0, claw=0.0, by=0.0, pitch=0.0, flip=0.0, lunge=0.0)
sck = K(SC_BASE)


def scorpion(p):
    p = P_(SC_BASE, p)
    parts = []
    by = p['by']
    ceph = (4.0 + p['lunge'], 1.5 + by)
    # legs
    spread = (4.5, 1.5, -1.8, -4.8)
    for side, z, col in ((-1, -2.5, darken(SC_D, 0.2)), (1, 2.5, SC_D)):
        for k in range(4):
            root = add(ceph, (-1.5 - k * 1.8, 1.0))
            ph = p['gait'] + k * math.pi / 2 + (0 if side > 0 else math.pi)
            lift = max(0, math.sin(ph)) * 1.8
            foot = add(root, (spread[k] + (0.8 if side < 0 else 0) + math.cos(ph) * 1.5, 5.0 - by - lift))
            knee = (root[0] + (foot[0] - root[0]) * 0.55, root[1] - 3.2 + lift * 0.3)
            parts.append(limb([root, knee, foot], [1.0, 0.8, 0.5], col, z=z + (k - 1.5) * 0.1))
    # tail: segments from the rear arching over the back; `tail` drives it forward for a strike
    t = p['tail']
    base = add(ceph, (-9.5, -0.5))
    segs = [base]
    for k in range(1, 6):
        # the tail rises from the rump and each segment bends forward over the back; striking
        # (t -> 1) straightens and throws it forward, cocking (t < 0) coils it tighter
        a = lerp(-1.95, -1.0, t) + k * lerp(0.52, 0.2, t)
        segs.append(polar(segs[-1], 3.7 - k * 0.15, a))
    for k in range(len(segs) - 1):
        parts.append(limb([segs[k], segs[k + 1]], [3.0 - k * 0.3, 2.6 - k * 0.3], SC if k % 2 == 0 else darken(SC, 0.08), z=-0.5 + k * 0.05))
    tip = segs[-1]
    d = (tip[0] - segs[-2][0], tip[1] - segs[-2][1])
    ln = math.hypot(*d) or 1
    d = (d[0] / ln, d[1] / ln)
    n = (-d[1], d[0])
    parts.append(poly([add(tip, (n[0] * 1.6, n[1] * 1.6)), add(tip, (d[0] * 4.5, d[1] * 4.5)), add(tip, (-n[0] * 1.6, -n[1] * 1.6))], SC_STING, z=0, thick=0.5))
    parts.append(glow(add(tip, (d[0] * 3, d[1] * 3)), 3.0, hexc('f0e060', 0.4), z=2))
    # abdomen plates
    for k in range(3):
        parts.append(ellipse(add(ceph, (-3.8 - k * 2.6, 0.4)), 3.4, 2.8 - k * 0.2, SC if k % 2 else darken(SC, 0.06), z=0))
    parts.append(ellipse(ceph, 4.6, 3.0, lighten(SC, 0.05), z=0.1))
    # pincers
    o = p['claw']
    for side, z, col in ((-1, -1.5, darken(SC, 0.15)), (1, 1.8, SC)):
        root = add(ceph, (2.5, 0.8))
        wrist = add(root, (4.5, -1.0 + side * 0.3))
        parts.append(limb([root, add(root, (2.5, 1.5)), wrist], [1.6, 1.4, 1.4], col, z=z))
        parts.append(poly([wrist, add(wrist, (4.5, -1.2 - o * 2.2)), add(wrist, (1.5, 0.5))], col, z=z + 0.05))
        parts.append(poly([add(wrist, (0.2, 0.6)), add(wrist, (4.0, 1.4 + o * 1.8)), add(wrist, (1.2, 1.3))], darken(col, 0.1), z=z + 0.05))
    for ez in (0.8, -0.8):
        parts.append(circle(add(ceph, (2.6, -1.4)), 0.55, hexc('ff3030'), z=ez, outline=False, shade=False))
    transform(parts, p['pitch'], (0, 4))
    if p['flip']:
        transform(parts, math.pi * p['flip'], (0, 1))
    return parts


def make_scorpion():
    sh = Sheet('scorpion', 52, 40, origin=(24, 24), scale=2.0, outline_w=0.7)
    sh.add_anim('idle', 16, True, lambda u, i: scorpion(dict(by=math.sin(u * TAU) * 0.3, tail=0.05 + 0.05 * math.sin(u * TAU), claw=0.2 + 0.2 * math.sin(u * TAU * 2))))
    sh.add_anim('walk', 8, True, lambda u, i: scorpion(dict(gait=u * TAU, by=math.sin(u * TAU * 2) * 0.3, tail=0.1 + 0.05 * math.sin(u * TAU))))
    sh.add_turns(4, lambda u, i: scorpion(dict(gait=u)))
    sh.add_anim('sting_windup', 6, False, lambda u, i: scorpion(sck(u, [(0, dict()), (1, dict(tail=-0.25, claw=1.0, by=0.6, pitch=0.12, lunge=-1))], ease_out)))
    sh.add_anim('sting', 6, False, lambda u, i: scorpion(sck(u, [(0, dict(tail=-0.25, claw=1.0, by=0.6, pitch=0.12, lunge=-1)), (0.3, dict(tail=1.0, claw=0.4, pitch=-0.08, lunge=1.5)), (1, dict(tail=0.3, claw=0.2))], ease_out)))
    sh.add_anim('hurt', 5, False, lambda u, i: scorpion(sck(u, [(0, dict(pitch=-0.4, claw=1.0, tail=0.4)), (1, dict())], ease_out)))
    sh.add_anim('death', 12, False, lambda u, i: scorpion(sck(u, [(0, dict(pitch=-0.3, claw=1.0)), (1, dict(flip=1.0, by=-2, tail=0.6, claw=0.8))])))
    return sh


# ============================================================================================ HORNET
HO_Y = hexc('d8a820')
HO_K = hexc('2a2218')
HO_WING = hexc('d8e8f0', 0.55)
HO_BASE = dict(flap=0.0, pitch=0.0, sting=0.0, by=0.0, spin=0.0, legs=0.0)
hok = K(HO_BASE)


def hornet(p):
    p = P_(HO_BASE, p)
    parts = []
    by = p['by']
    thorax = (1.0, by)
    head = add(thorax, (4.5, -1.0))
    abd_a = -0.25 + p['sting'] * 1.2
    abd = add(thorax, polar((0, 0), 5.5, math.pi + abd_a))
    # legs dangling
    for z, dx in ((-1.0, 0.5), (1.0, -0.5)):
        for k in range(3):
            r = add(thorax, (1.2 - k * 1.2 + dx * 0.2, 1.8))
            parts.append(limb([r, add(r, (0.8 - k * 0.6, 2.8 + p['legs'])), add(r, (1.8 - k * 0.8, 4.0 + p['legs']))], [0.6, 0.5, 0.4], HO_K, z=z))
    # far wing
    f = math.sin(p['flap'])
    for z, lag in ((-1.6, 0.4), (1.6, 0.0)):
        ff = math.sin(p['flap'] - lag)
        root = add(thorax, (0, -1.8))
        tip = add(root, (-6.5, -7.0 * ff - 1.5))
        parts.append(poly([root, add(root, (-2.0, -3.5 * ff - 1.2)), tip, add(tip, (1.8, 2.0)), add(root, (-3.0, 0.6))], HO_WING, z=z, thick=0.2, outline=False, shade=False))
        parts.append(line([root, tip], 0.35, hexc('8898a8', 0.7), z=z + 0.05))
    # abdomen with stripes and stinger
    d = (math.cos(math.pi + abd_a), math.sin(math.pi + abd_a))
    parts.append(ellipse(abd, 4.8, 3.0, HO_Y, z=0, rot=abd_a, thick=0.9))
    for k in (-1.5, 0.5, 2.4):
        c = add(abd, (d[0] * k, d[1] * k))
        n = (-d[1] * 2.9, d[0] * 2.9)
        parts.append(line([add(c, n), add(c, (-n[0], -n[1]))], 1.1, HO_K, z=0, bias=0.4))
    st = add(abd, (d[0] * 4.6, d[1] * 4.6))
    parts.append(poly([add(st, (-d[1] * 1.0, d[0] * 1.0)), add(st, (d[0] * 3.2, d[1] * 3.2)), add(st, (d[1] * 1.0, -d[0] * 1.0))], HO_K, z=0))
    parts.append(ellipse(thorax, 3.2, 2.7, darken(HO_Y, 0.25), z=0.1))
    parts.append(circle(head, 2.5, HO_Y, z=0.2))
    for ez in (1.0, -1.0):
        parts.append(ellipse(add(head, (1.0, -0.6)), 1.2, 1.6, hexc('3a0e0e'), z=ez, outline=False))
    parts.append(line([add(head, (1.5, 1.4)), add(head, (2.6, 2.4))], 0.6, HO_K, z=0.4))
    transform(parts, p['pitch'], (0, 0))
    if p['spin']:
        transform(parts, p['spin'], (0, 0))
    return parts


def make_hornet():
    sh = Sheet('hornet', 40, 36, origin=(20, 18), scale=2.0, outline_w=0.6)
    sh.add_anim('fly', 4, True, lambda u, i: hornet(dict(flap=u * TAU, by=math.sin(u * TAU) * 0.4, pitch=0.05)))
    sh.add_anim('dive', 4, True, lambda u, i: hornet(dict(flap=u * TAU, sting=1.0, pitch=0.45, legs=-1)))
    sh.add_anim('aim', 6, False, lambda u, i: hornet(hok(u, [(0, dict()), (1, dict(sting=0.7, pitch=-0.2, flap=4))], ease_out)))
    sh.add_turns(4, lambda u, i: hornet(dict(flap=u * TAU * 2)))
    sh.add_anim('hurt', 5, False, lambda u, i: hornet(hok(u, [(0, dict(pitch=-0.6, sting=0.3, flap=2)), (1, dict(flap=6))], ease_out)))
    sh.add_anim('death', 10, False, lambda u, i: fade_out(hornet(dict(flap=u * 3, spin=u * 5, sting=0.6, legs=1)), u, fall=8))
    return sh


# ============================================================================================ SKELETON
BONE = hexc('e0dccb')
BONE_D = hexc('a8a290')
RUST = hexc('8a7e70')
SK_BASE = dict(bx=0.0, by=0.0, lean=0.05, lfx=-2.2, lfy=11.0, lnx=2.4, lny=11.0, rhx=4.5, rhy=2.0, wa=-0.4,
               lhx=-2.5, lhy=2.5, ht=0.0, jaw=0.0, collapse=0.0, eye=1.0, armz=-2.5)
skk = K(SK_BASE)


def skeleton(p):
    p = P_(SK_BASE, p)
    parts = []
    col = p['collapse']
    hip = (p['bx'], 1.5 + p['by'] + col * 8)

    def B(q):
        return add(rot(q, p['lean'] + col * 0.6), hip)

    def drop(pt, k):
        # collapsing: bones fall and scatter toward the ground
        if col <= 0:
            return pt
        return (pt[0] + math.sin(k * 3.1) * 5 * col, lerp(pt[1], 11.5, col))

    sh = B((0.6, -8.0))
    head = rot(B((1.2, -12.5)), p['ht'], B((0.8, -9.5)))
    head = drop(head, 1)
    # legs: thigh and shin as bones
    for side, z in (('f', -1.5), ('n', 1.5)):
        foot = drop((p['l' + side + 'x'], p['l' + side + 'y']), 2 if side == 'f' else 3)
        knee = ik2(add(hip, (-0.3 if side == 'f' else 0.3, 0)), foot, 5.2, 5.2, bend=1)
        knee = drop(knee, 4)
        c = BONE_D if side == 'f' else BONE
        parts.append(line([hip, knee], 1.3, c, z=z))
        parts.append(line([knee, foot], 1.1, c, z=z))
        parts.append(circle(knee, 1.0, c, z=z + 0.05))
        parts.append(ellipse(add(foot, (1.0, 0.2)), 1.8, 0.8, c, z=z + 0.1))
    # pelvis, spine, ribs
    parts.append(ellipse(hip, 3.0, 1.6, BONE, z=0, thick=0.8))
    spine = [hip, drop(B((0.2, -3.0)), 5), drop(B((0.5, -6.0)), 6), drop(sh, 7)]
    parts.append(limb(spine, [1.1, 1.0, 1.0, 1.0], BONE_D, z=-0.2))
    for k in range(4):
        y = -3.6 - k * 1.3
        a = drop(B((0.4, y)), 8 + k)
        parts.append(line([a, drop(B((3.6 - k * 0.2, y + 1.0)), 12 + k), drop(B((0.6, y + 2.0)), 16 + k)], 0.8, BONE, z=0.2))
        parts.append(line([a, drop(B((-2.8 + k * 0.2, y + 1.0)), 20 + k)], 0.8, BONE_D, z=-0.3))
    # skull
    parts.append(circle(head, 3.6, BONE, z=0.2, thick=1.0))
    parts.append(poly([add(head, (0.8, 1.6)), add(head, (4.2, 1.2)), add(head, (4.0, 3.2 + p['jaw'] * 2)), add(head, (1.0, 3.4 + p['jaw']))], BONE_D, z=0.25, bias=0.4))
    for ez in (1.0, -1.0):
        parts.append(circle(add(head, (1.7, -0.6)), 1.1, hexc('1a1410'), z=ez, outline=False, shade=False))
        if p['eye'] > 0.1:
            parts.append(circle(add(head, (1.9, -0.6)), 0.5, hexc('ff6a40'), z=ez + 0.1, outline=False, shade=False))
    if p['eye'] > 0.1:
        parts.append(glow(add(head, (1.9, -0.6)), 3.0 * p['eye'], hexc('ff5020', 0.45), z=3))
    parts.append(line([add(head, (3.4, 1.2)), add(head, (3.4, 2.2))], 0.4, hexc('3a3228'), z=0.3))

    def arm(s, hand, z, c):
        el = drop(ik2(s, hand, 4.0, 4.0, bend=-1), 30 + int(z))
        hand = drop(hand, 40 + int(z))
        parts.append(line([s, el], 1.0, c, z=z))
        parts.append(line([el, hand], 0.9, c, z=z))
        parts.append(circle(hand, 0.9, c, z=z + 0.05))
        return hand

    rz = p['armz']
    hand = arm(add(sh, (-0.3, 0)), (p['rhx'], p['rhy']), rz, BONE)
    # rusty sword
    d = (math.cos(p['wa']), math.sin(p['wa']))
    n = (-d[1], d[0])
    b0 = add(hand, (d[0] * 1.2, d[1] * 1.2))
    tip = add(b0, (d[0] * 11, d[1] * 11))
    parts.append(line([add(hand, (-d[0] * 1.8, -d[1] * 1.8)), b0], 1.3, hexc('5a4a38'), z=rz + 0.2))
    parts.append(line([add(b0, (n[0] * 1.8, n[1] * 1.8)), add(b0, (-n[0] * 1.8, -n[1] * 1.8))], 1.0, RUST, z=rz + 0.25))
    parts.append(poly([add(b0, (n[0] * 0.9, n[1] * 0.9)), tip, add(b0, (-n[0] * 0.9, -n[1] * 0.9))], RUST, z=rz + 0.3, thick=0.3))
    arm(add(sh, (0.3, 0)), (p['lhx'], p['lhy']), 2.3, BONE_D)
    return parts


def make_skeleton():
    sh = Sheet('skeleton', 56, 52, origin=(28, 30), scale=2.0, outline_w=0.6)
    sh.add_anim('idle', 24, True, lambda u, i: skeleton(dict(by=math.sin(u * TAU) * 0.3, rhy=2 + math.sin(u * TAU) * 0.3, jaw=0.2 + 0.2 * math.sin(u * TAU * 3), eye=0.8 + 0.2 * math.sin(u * TAU * 2))))

    def walk(u, i):
        ph = u * TAU
        c, s = math.cos(ph), math.sin(ph)
        return skeleton(dict(lean=0.12, by=-abs(c) * 0.5, lfx=3.4 * c, lfy=11 - max(0, s) * 2.5, lnx=-3.4 * c, lny=11 - max(0, -s) * 2.5,
                             rhx=3 - 2 * c, rhy=1.5, wa=-0.7, lhx=1 + 2 * c, lhy=2, jaw=0.2))
    sh.add_anim('walk', 12, True, walk)
    sh.add_turns(4, lambda u, i: skeleton(dict()))
    wind = dict(lean=-0.2, bx=-1, rhx=-2.5, rhy=-8, wa=-2.4, armz=2.5, lhx=3, lhy=0, jaw=0.8, lfx=-3.5, lnx=3.5)
    sh.add_anim('windup', 6, False, lambda u, i: skeleton(skk(u, [(0, dict()), (1, wind)], ease_out)))
    hit = dict(lean=0.4, bx=1.8, rhx=7.5, rhy=5, wa=0.9, armz=2.5, lfx=-3.5, lnx=5.5, jaw=1.0)
    sh.add_anim('slash', 6, False, lambda u, i: skeleton(skk(u, [(0, wind), (0.3, hit), (1, dict(hit, wa=1.1, jaw=0.4))], ease_out)))
    sh.add_anim('recover', 5, False, lambda u, i: skeleton(skk(u, [(0, dict(hit, wa=1.1)), (1, dict())])))
    sh.add_anim('hurt', 5, False, lambda u, i: skeleton(skk(u, [(0, dict(lean=-0.5, bx=-1.2, ht=-0.5, jaw=1, eye=0.3)), (1, dict())], ease_out)))
    sh.add_anim('death', 16, False, lambda u, i: skeleton(skk(u, [(0, dict(lean=-0.4, jaw=1, eye=0.5)), (1, dict(collapse=1.0, eye=0.0, jaw=1.2, ht=0.8))], ease_in)))
    return sh


# ============================================================================================ SPORELING
CAP = hexc('8a4a8e')
CAP_SPOT = hexc('ecd4f2')
STEM = hexc('d8c8b0')
GILL = hexc('7dff95')
SPO_BASE = dict(step=0.0, squash=0.0, puff=0.0, lean=0.0, blink=0.0, wilt=0.0, by=0.0)
spk = K(SPO_BASE)


def sporeling(p):
    p = P_(SPO_BASE, p)
    parts = []
    sq = p['squash']
    wilt = p['wilt']
    base = (0.0, 9.0)
    body_c = (0.0, 3.0 + p['by'] + sq * 1.5 + wilt * 3)
    # feet
    for z, ph in ((-1.5, 0.0), (1.5, math.pi)):
        lift = max(0, math.sin(p['step'] + ph)) * 1.5
        fx = math.cos(p['step'] + ph) * 1.8
        parts.append(ellipse((fx, 9.2 - lift), 2.2, 1.3, darken(STEM, 0.25 if z < 0 else 0.15), z=z))
    # stem body
    parts.append(ellipse(body_c, 4.2 + sq * 0.8, 5.0 - sq * 1.0, STEM, z=0, rot=p['lean'], thick=0.9))
    for ez in (1.0, -1.0):
        e = add(body_c, (1.8, -1.0))
        if p['blink'] > 0.5 or wilt > 0.5:
            parts.append(line([add(e, (-0.7, 0)), add(e, (0.7, 0))], 0.5, hexc('3a2a3a'), z=ez))
        else:
            parts.append(circle(e, 0.8, hexc('2a1a2a'), z=ez, outline=False, shade=False))
    parts.append(line([add(body_c, (1.2, 1.6)), add(body_c, (2.6, 1.5 + p['puff'] * 0.8))], 0.5, hexc('5a3a4a'), z=0.5))
    # cap: a dome that swells before a puff and droops when dying
    s = 1 + p['puff'] * 0.25
    cap_c = add(body_c, (0.5 * math.sin(p['lean']), -4.0 - p['puff'] * 1.0))
    pts = []
    for k in range(13):
        a = math.pi + k * math.pi / 12
        pts.append(add(cap_c, (math.cos(a) * 8.0 * s, math.sin(a) * 5.2 * s * (1 - wilt * 0.4))))
    pts.append(add(cap_c, (6.5 * s, 1.2 + wilt * 3)))
    pts.append(add(cap_c, (-6.5 * s, 1.2 + wilt * 3)))
    parts.append(poly([rot(q, p['lean'] + wilt * 0.5, cap_c) for q in pts], CAP, z=0.2, thick=0.9, smooth=True))
    parts.append(line([rot(add(cap_c, (-6.8 * s, 1.0)), p['lean'], cap_c), rot(add(cap_c, (6.8 * s, 1.0)), p['lean'], cap_c)], 1.4, GILL, z=0.25))
    parts.append(glow(add(cap_c, (0, 1.5)), 6 + p['puff'] * 8, hexc('60ff80', 0.25 + p['puff'] * 0.35), z=2))
    for (sx, sy, r) in ((-4, -2.5, 1.2), (0.5, -4.0, 1.4), (4.2, -2.2, 1.0), (-1.5, -1.2, 0.8)):
        parts.append(circle(rot(add(cap_c, (sx * s, sy * s)), p['lean'], cap_c), r * s, CAP_SPOT, z=0.3, bias=0.5, outline=False))
    return parts


def make_sporeling():
    sh = Sheet('sporeling', 40, 40, origin=(20, 24), scale=2.0, outline_w=0.7)
    sh.add_anim('idle', 24, True, lambda u, i: sporeling(dict(by=math.sin(u * TAU) * 0.4, squash=0.1 * math.sin(u * TAU * 2), blink=1 if i in (13, 14) else 0)))
    sh.add_anim('walk', 12, True, lambda u, i: sporeling(dict(step=u * TAU, lean=0.1 * math.sin(u * TAU), by=-abs(math.sin(u * TAU)) * 0.8)))
    sh.add_turns(4, lambda u, i: sporeling(dict()))
    sh.add_anim('puff_windup', 8, False, lambda u, i: sporeling(spk(u, [(0, dict()), (1, dict(squash=1.0, puff=1.0, lean=-0.1))], ease_out)))
    sh.add_anim('puff', 8, False, lambda u, i: sporeling(spk(u, [(0, dict(squash=1.0, puff=1.0)), (0.25, dict(squash=-0.4, puff=-0.3, by=-1.5)), (1, dict())], ease_out)))
    sh.add_anim('hurt', 5, False, lambda u, i: sporeling(spk(u, [(0, dict(lean=-0.4, squash=0.6, blink=1)), (1, dict())], ease_out)))
    sh.add_anim('death', 14, False, lambda u, i: sporeling(spk(u, [(0, dict(lean=-0.3, blink=1)), (1, dict(wilt=1.0, lean=0.6, squash=0.8))])))
    return sh


# ============================================================================================ FROST WRAITH
WR = hexc('9fcbe8', 0.9)
WR_D = hexc('4a78a8', 0.95)
WR_EYE = hexc('eaffff')
WR_BASE = dict(drift=0.0, cast=0.0, by=0.0, fade=0.0, lean=0.0, reach=0.0)
wrk = K(WR_BASE)


def wraith(p):
    p = P_(WR_BASE, p)
    parts = []
    fd = 1 - p['fade']
    by = p['by']
    head = (1.0, -9.0 + by)
    a = 0.9 * fd
    # tattered cloak trailing into wisps
    pts = [add(head, (-4.5, 1.0)), add(head, (4.5, 0.5))]
    for k in range(6):
        t = k / 5
        x = lerp(5.0, -7.0, t) + math.sin(p['drift'] + t * 5) * 1.5 * t
        y = lerp(4.0, 22.0, t) + by
        pts.append((x + (2.5 if k % 2 == 0 else -1.0), y))
    pts.append((-9.0 + math.sin(p['drift'] + 3) * 2.5, 20.0 + by))
    pts.append((-7.0, 6.0 + by))
    parts.append(poly(pts, hexc('9fcbe8', 0.55 * fd + 0.1), z=-0.5, thick=0.6, smooth=True))
    parts.append(poly([add(head, (-4.0, 1.0)), add(head, (4.0, 0.5)), (3.0, 10.0 + by), (-6.0, 11.0 + by)], hexc('4a78a8', 0.85 * fd + 0.1), z=0, thick=0.8, smooth=True))
    # hood
    parts.append(circle(head, 5.0, hexc('4a78a8', a + 0.05), z=0.1, thick=1.0))
    parts.append(circle(add(head, (1.4, 0.5)), 3.4, hexc('0a1420', a + 0.05), z=0.15, bias=0.5, outline=False))
    for ez in (1.0, -1.0):
        parts.append(ellipse(add(head, (2.2, 0.3)), 0.8, 0.5, hexc('eaffff', fd), z=ez, outline=False, shade=False))
    parts.append(glow(add(head, (2.2, 0.3)), 4.0, hexc('b0f0ff', 0.5 * fd), z=3))
    # arms: they raise the ice orb when casting
    c = p['cast']
    for z, side in ((-2.0, -1), (2.0, 1)):
        sh = add(head, (0.5, 5.0))
        hand = add(sh, (lerp(4.0, 6.5, c) + p['reach'] * 3, lerp(4.5, -5.5, c) + side * 1.0))
        parts.append(limb([sh, add(sh, (2.5, 2.5 - c * 3)), hand], [1.6, 1.2, 1.0], hexc('7aa8d0', a + 0.05), z=z))
    orb = add(head, (lerp(8.0, 9.5, c) + p['reach'] * 3, lerp(9.5, -1.0, c)))
    if c > 0.05:
        parts.append(glow(orb, 5 + 7 * c, hexc('c0f4ff', 0.55 * c), z=4))
        for k in range(5):
            ang = k * TAU / 5 + c * 2
            parts.append(poly([add(orb, polar((0, 0), 0.6, ang - 0.5)), add(orb, polar((0, 0), 3.2 * c + 0.8, ang)), add(orb, polar((0, 0), 0.6, ang + 0.5))], hexc('e8ffff'), z=4.1, outline=False))
    transform(parts, p['lean'], (0, 0))
    return parts


def make_wraith():
    sh = Sheet('wraith', 48, 56, origin=(24, 28), scale=2.0, outline_w=0.5)
    sh.add_anim('float', 16, True, lambda u, i: wraith(dict(drift=u * TAU, by=math.sin(u * TAU) * 1.2)))
    sh.add_turns(4, lambda u, i: wraith(dict(drift=u * 3)))
    sh.add_anim('cast_windup', 10, False, lambda u, i: wraith(wrk(u, [(0, dict()), (1, dict(cast=1.0, lean=-0.15, by=-1.5))], ease_out) | dict(drift=u * TAU)))
    sh.add_anim('cast', 6, False, lambda u, i: wraith(wrk(u, [(0, dict(cast=1.0, lean=-0.15, by=-1.5)), (0.3, dict(cast=0.6, reach=1.0, lean=0.2)), (1, dict())], ease_out) | dict(drift=u * TAU)))
    sh.add_anim('hurt', 5, False, lambda u, i: wraith(wrk(u, [(0, dict(lean=-0.4, fade=0.4)), (1, dict())], ease_out)))
    sh.add_anim('death', 14, False, lambda u, i: wraith(dict(fade=u, drift=u * 8, by=-u * 6, lean=-0.3 * u)))
    return sh


# ============================================================================================ SHARDLING
SH = hexc('4ab0c8')
SH_D = hexc('246878')
SH_L = hexc('c8f8ff')
SHR_BASE = dict(gait=0.0, bristle=0.0, by=0.0, pitch=0.0, shatter=0.0, crouch=0.0)
shk = K(SHR_BASE)


def shardling(p):
    p = P_(SHR_BASE, p)
    parts = []
    sht = p['shatter']
    by = p['by'] + p['crouch'] * 1.5
    body = (0.0, 2.5 + by)

    def S(q, k):
        if sht <= 0:
            return q
        ang = k * 2.4
        return add(q, (math.cos(ang) * 9 * sht, math.sin(ang) * 6 * sht - 4 * sht + 10 * sht * sht))

    for side, z in ((-1, -2.0), (1, 2.0)):
        for k in range(3):
            root = add(body, (2.5 - k * 2.5, 1.5))
            ph = p['gait'] + k * 2.1 + (0 if side > 0 else math.pi)
            lift = max(0, math.sin(ph)) * 1.5
            foot = add(root, (1.5 - k * 0.8 + math.cos(ph) * 1.2, 5.0 - by - lift - p['crouch'] * 1.2))
            knee = (root[0] + (foot[0] - root[0]) * 0.5, root[1] - 2.0)
            parts.append(limb([root, knee, S(foot, k + side * 3)], [1.1, 0.9, 0.6], SH_D, z=z))
    parts.append(ellipse(S(body, 9), 7.0, 4.0, SH, z=0, thick=0.9))
    parts.append(ellipse(S(add(body, (1.0, 1.8)), 11), 5.0, 1.6, SH_D, z=0, bias=0.5, outline=False))
    # crystal spikes on the back; `bristle` grows them before a burst
    b = p['bristle']
    for k, (sx, ang) in enumerate(((-4.5, -2.2), (-1.8, -1.8), (1.0, -1.45), (3.8, -1.1), (-0.5, -2.0))):
        root = add(body, (sx, -2.5))
        ln = 4.5 + b * 5 + (k % 2) * 1.5
        tip = polar(root, ln, ang)
        w = 1.4 + b * 0.5
        n = (-math.sin(ang) * w, math.cos(ang) * w)
        parts.append(poly([S(add(root, n), 20 + k), S(tip, 20 + k), S(add(root, (-n[0], -n[1])), 20 + k)], SH_L if k % 2 == 0 else lighten(SH, 0.3), z=0.2 + k * 0.02, thick=0.4))
    parts.append(glow(add(body, (0, -4)), 6 + b * 8, hexc('a0f0ff', 0.25 + b * 0.35), z=3))
    for ez in (1.0, -1.0):
        parts.append(circle(S(add(body, (5.5, -1.0)), 30), 0.8, hexc('ff60c0'), z=ez, outline=False, shade=False))
    transform(parts, p['pitch'], (0, 5))
    return parts


def make_shardling():
    sh = Sheet('shardling', 44, 40, origin=(22, 24), scale=2.0, outline_w=0.6)
    sh.add_anim('idle', 16, True, lambda u, i: shardling(dict(by=math.sin(u * TAU) * 0.3, bristle=0.1 + 0.1 * math.sin(u * TAU))))
    sh.add_anim('walk', 8, True, lambda u, i: shardling(dict(gait=u * TAU, by=math.sin(u * TAU * 2) * 0.3)))
    sh.add_turns(4, lambda u, i: shardling(dict()))
    sh.add_anim('curl', 8, False, lambda u, i: shardling(shk(u, [(0, dict()), (1, dict(bristle=1.0, crouch=1.0, pitch=0.1))], ease_out)))
    sh.add_anim('burst', 6, False, lambda u, i: shardling(shk(u, [(0, dict(bristle=1.0, crouch=1.0)), (0.3, dict(bristle=1.4, crouch=-0.3, by=-1.5)), (1, dict(bristle=0.2))], ease_out)))
    sh.add_anim('hurt', 5, False, lambda u, i: shardling(shk(u, [(0, dict(pitch=-0.4, crouch=0.6)), (1, dict())], ease_out)))
    sh.add_anim('death', 12, False, lambda u, i: shardling(dict(shatter=u, bristle=1.0)))
    return sh


# ============================================================================================ DRAGON
DR = hexc('6e1c1c')
DR_D = hexc('3a0c0c')
DR_BELLY = hexc('d0883c')
DR_HORN = hexc('e8dcc0')
DR_WING = hexc('8a2626')
DR_BASE = dict(flap=0.0, wing=0.3, neck=0.0, jaw=0.0, gait=0.0, stride=0.0, tail=0.0, tailw=0.0, air=0.0, by=0.0, pitch=0.0,
               dead=0.0, glowm=0.0, head=0.0)
drk = K(DR_BASE)


def dragon(p):
    p = P_(DR_BASE, p)
    parts = []
    by = p['by']
    air = p['air']
    body_c = (0.0, 2.0 + by)
    shoulder = add(body_c, (14.0, -6.0))

    # tail: a long whip; `tail` swings it (sweep attack), `tailw` waves it
    tail = [add(body_c, (-20, 2))]
    for k in range(1, 9):
        a = math.pi + 0.15 + p['tail'] * (0.25 + k * 0.08) + math.sin(p['tailw'] + k * 0.8) * 0.08 * k
        tail.append(polar(tail[-1], 6.0 - k * 0.3, a + (k * 0.04)))
    widths = [9.0 - k * 1.0 for k in range(len(tail))]
    parts.append(limb(tail, widths, DR_D, z=-6.0))
    tp = tail[-1]
    parts.append(poly([add(tp, (0, -3.5)), add(tp, (-7, 0)), add(tp, (0, 3.5))], DR_HORN, z=-6.0))

    # far wing
    def wing(z, lag, col):
        f = math.sin(p['flap'] - lag)
        spread = p['wing']
        root = add(body_c, (6, -12))
        elbow = add(root, (-6 - 8 * spread, -10 - 14 * spread * (0.4 + 0.6 * f)))
        tip = add(elbow, (-18 * spread - 4, -6 - 16 * spread * f))
        f1 = add(elbow, (-22 * spread - 2, 10 * spread - 8 * f * spread))
        f2 = add(elbow, (-14 * spread - 2, 18 * spread))
        back = add(body_c, (-12, -4))
        parts.append(poly([root, elbow, tip, f1, f2, back], col, z=z, thick=0.3))
        for q in (tip, f1, f2):
            parts.append(line([elbow, q], 1.2, darken(col, 0.4), z=z + 0.05))
        parts.append(line([root, elbow], 2.4, darken(col, 0.3), z=z + 0.05))
    wing(-8.0, 0.35, darken(DR_WING, 0.2))

    # legs
    def leg(front, side, z, col):
        root = add(body_c, (12 if front else -12, 6))
        ph = p['gait'] + (0.6 * math.pi if front else 0) + (0 if side > 0 else math.pi)
        lift = max(0, math.sin(ph)) * 5 * p['stride']
        if air > 0.5:
            foot = add(root, (-4, 12))
        else:
            foot = (root[0] + math.cos(ph) * 6 * p['stride'], 28 - lift)
        knee = ik2(root, foot, 11, 12, bend=1 if not front else -1)
        parts.append(limb([root, knee, foot], [9 if not front else 7, 7, 5.5], col, z=z))
        parts.append(poly([add(foot, (-3, -1)), add(foot, (6, -1)), add(foot, (7, 2)), add(foot, (-3, 2))], col, z=z + 0.1))
        for k in range(3):
            parts.append(line([add(foot, (5 + k * 0.5, 1.5)), add(foot, (8.5 + k * 0.5, 3))], 1.0, DR_HORN, z=z + 0.2))
    leg(False, -1, -5.0, DR_D)
    leg(True, -1, -4.5, DR_D)

    # body
    body = [add(body_c, q) for q in [(-22, -2), (-12, -12), (4, -15), (16, -11), (22, -2), (16, 8), (0, 11), (-16, 8)]]
    parts.append(poly(body, DR, z=0, thick=0.9, smooth=True))
    parts.append(poly([add(body_c, q) for q in [(-14, 6), (0, 9.5), (14, 6), (4, 3), (-8, 3)]], DR_BELLY, z=0, bias=0.5, smooth=True))
    for k in range(6):
        b = add(body_c, (-16 + k * 6, -12 + abs(k - 3) * 1.0))
        parts.append(poly([add(b, (-2, 1)), add(b, (0, -4)), add(b, (2, 1))], DR_HORN if k % 2 == 0 else darken(DR_HORN, 0.2), z=0, bias=0.2))

    # neck and head: `neck` lowers the head toward the ground (breath aims), `head` raises it
    nd = p['neck']
    hd = add(shoulder, (20 + nd * 4, -14 + nd * 24 - p['head'] * 8))
    mid = add(shoulder, (10 + nd * 4, -12 + nd * 10 - p['head'] * 4))
    parts.append(limb([shoulder, mid, hd], [11, 8, 7], DR, z=0.2))
    for k in range(3):
        c = lerp_pt(shoulder, hd, 0.2 + k * 0.3)
        parts.append(line([add(c, (-3, 3)), add(c, (3, 4))], 1.0, DR_BELLY, z=0.25))
    j = p['jaw']
    jaw_a = 0.25 + nd * 0.5
    up = [add(hd, (-4, -5)), add(hd, rot((14, -2), jaw_a * 0.3)), add(hd, rot((13, 1), jaw_a * 0.3)), add(hd, (-3, 2))]
    lo = [add(hd, (-3, 2)), add(hd, rot((12, 2 + j * 7), jaw_a * 0.3)), add(hd, rot((11, 4 + j * 8), jaw_a * 0.3)), add(hd, (-2, 5))]
    parts.append(poly(lo, DR_D, z=0.4, bias=0.5))
    if j > 0.1:
        parts.append(poly([add(hd, (-2, 2)), add(hd, rot((12, 1), jaw_a * 0.3)), add(hd, rot((11, 2 + j * 6), jaw_a * 0.3))], hexc('ffb040' if p['glowm'] > 0.3 else '5a1010'), z=0.41, bias=0.55, outline=False))
        if p['glowm'] > 0.2:
            parts.append(glow(add(hd, rot((11, 2 + j * 3), jaw_a * 0.3)), 12 * p['glowm'], hexc('ffa020', 0.7 * p['glowm']), z=6))
    parts.append(poly(up, DR, z=0.45, bias=0.6))
    for k in range(3):
        parts.append(poly([add(hd, rot((3 + k * 3, 1.5), jaw_a * 0.3)), add(hd, rot((4 + k * 3, 3.5), jaw_a * 0.3)), add(hd, rot((5 + k * 3, 1.5), jaw_a * 0.3))], DR_HORN, z=0.46, bias=0.6, outline=False))
    for ez, hz in ((2.5, 0.5), (-2.5, 0.4)):
        parts.append(poly([add(hd, (-3, -4)), add(hd, (-12, -12)), add(hd, (-1, -6))], DR_HORN if ez > 0 else darken(DR_HORN, 0.2), z=hz + ez * 0.3))
        eye = add(hd, (3, -3))
        if p['dead'] > 0.5:
            parts.append(line([add(eye, (-1.2, 0)), add(eye, (1.2, 0))], 0.8, DR_D, z=ez + 0.5))
        else:
            parts.append(poly([add(eye, (-1.6, 0.4)), add(eye, (1.8, -0.8)), add(eye, (1.4, 0.8))], hexc('ffe040'), z=ez + 0.5, outline=False))
    parts.append(glow(add(hd, (3, -3)), 5, hexc('ffd040', 0.5 * (1 - p['dead'])), z=5))

    leg(False, 1, 5.0, DR)
    leg(True, 1, 4.5, DR)
    wing(8.0, 0.0, DR_WING)
    transform(parts, p['pitch'], (0, 10))
    return parts


def lerp_pt(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def make_dragon():
    sh = Sheet('dragon', 176, 140, origin=(84, 84), scale=1.25, outline_w=1.0)
    sh.add_anim('idle', 24, True, lambda u, i: dragon(dict(by=math.sin(u * TAU) * 1.0, tailw=u * TAU, wing=0.25 + 0.05 * math.sin(u * TAU), head=0.2 * math.sin(u * TAU))))
    sh.add_anim('walk', 16, True, lambda u, i: dragon(dict(gait=u * TAU, stride=1.0, by=-abs(math.sin(u * TAU)) * 1.5, tailw=u * TAU * 2, wing=0.25)))
    sh.add_anim('fly', 8, True, lambda u, i: dragon(dict(flap=u * TAU, wing=1.0, air=1.0, by=-math.sin(u * TAU) * 3, pitch=-0.08, tailw=u * TAU)))
    sh.add_turns(8, lambda u, i: dragon(dict(wing=0.3)))
    sh.add_anim('breath_windup', 10, False, lambda u, i: dragon(drk(u, [(0, dict()), (1, dict(head=1.0, jaw=0.4, glowm=1.0, wing=0.6, pitch=-0.08))], ease_out)))
    sh.add_anim('breath', 8, True, lambda u, i: dragon(dict(neck=0.7, jaw=1.0, glowm=1.0, wing=0.5, by=math.sin(u * TAU) * 0.5, tailw=u * TAU)))
    sh.add_anim('dive', 6, True, lambda u, i: dragon(dict(flap=1.5, wing=0.7, air=1.0, pitch=0.5, neck=0.6, jaw=0.6)))
    sh.add_anim('tail_windup', 8, False, lambda u, i: dragon(drk(u, [(0, dict()), (1, dict(tail=-1.2, pitch=0.08, by=1))], ease_out)))
    sh.add_anim('tail', 8, False, lambda u, i: dragon(drk(u, [(0, dict(tail=-1.2, pitch=0.08, by=1)), (0.3, dict(tail=1.6, pitch=-0.06)), (1, dict(tail=0.4))], ease_out)))
    sh.add_anim('roar', 18, False, lambda u, i: dragon(drk(u, [(0, dict()), (0.2, dict(head=1.4, jaw=1.0, wing=1.0, flap=2.0, by=math.sin(u * 60) * 0.8)), (0.8, dict(head=1.4, jaw=1.0, wing=1.0, flap=2.4)), (1, dict())])))
    sh.add_anim('hurt', 5, False, lambda u, i: dragon(drk(u, [(0, dict(pitch=-0.1, jaw=0.7, head=0.6)), (1, dict())], ease_out)))
    sh.add_anim('death', 30, False, lambda u, i: dragon(drk(u, [(0, dict(jaw=1.0, head=1.2, wing=0.8)), (0.5, dict(pitch=0.25, by=8, jaw=0.6, neck=1.0, wing=0.4, dead=1)), (1, dict(pitch=0.4, by=14, jaw=0.4, neck=1.4, wing=0.1, dead=1, tail=0.5))], ease_in)))
    return sh


MAKERS = {
    'rat': make_rat, 'bear': make_bear, 'scorpion': make_scorpion, 'hornet': make_hornet, 'skeleton': make_skeleton,
    'sporeling': make_sporeling, 'wraith': make_wraith, 'shardling': make_shardling, 'dragon': make_dragon,
}
