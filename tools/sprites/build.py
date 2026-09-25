"""Renders every creature's sprite sheet into godot/DaggerCave/Art.

    python3 tools/sprites/build.py                  # all creatures
    python3 tools/sprites/build.py player bat       # just these
    python3 tools/sprites/build.py --contact DIR    # also write per-creature contact sheets

Requires pycairo (pip install pycairo).
"""
import os
import sys
import time
import importlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'godot', 'DaggerCave', 'Art'))

CREATURES = ['swordsman', 'warden', 'bat', 'frog', 'goblin', 'slinger', 'spider', 'magma', 'golem', 'fish',
             'fish2', 'urchin', 'eel', 'colossus', 'moth', 'crab',
             'rat', 'bear', 'scorpion', 'hornet', 'skeleton', 'sporeling', 'wraith', 'shardling', 'dragon']
MONSTERS = ('rat', 'bear', 'scorpion', 'hornet', 'skeleton', 'sporeling', 'wraith', 'shardling', 'dragon')


def main(argv):
    contact = None
    if '--contact' in argv:
        i = argv.index('--contact')
        contact = argv[i + 1]
        argv = argv[:i] + argv[i + 2:]
        os.makedirs(contact, exist_ok=True)
    names = argv or CREATURES
    for name in names:
        t0 = time.time()
        heroes = ('swordsman', 'warden')
        mod = importlib.import_module('player' if name in heroes else 'monsters' if name in MONSTERS else 'creatures')
        sheet = mod.make(name) if name in heroes else mod.MAKERS[name]()
        n, w, h = sheet.save(OUT)
        print(f'{name:10s} {n:4d} frames  {w}x{h}  {time.time() - t0:.1f}s')
        if contact:
            sheet.contact_sheet(os.path.join(contact, name + '_r.png'), [a for a in sheet.anims if a.endswith('_r') or a.startswith('turn')])
            sheet.contact_sheet(os.path.join(contact, name + '_l.png'), [a for a in sheet.anims if a.endswith('_l') or a.startswith('turn')])


if __name__ == '__main__':
    main(sys.argv[1:])
