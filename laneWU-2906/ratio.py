# lane WU, #2906: the figure-to-base luma ratio on the stills, as #645 measured it.
# Usage: python -I laneWU-ratio.py <dir with points*.json and PNGs>
# Luma = 0.2126 R + 0.7152 G + 0.0722 B on the PNG's 0-1 values (UnitPlinthTests.Luma).
# Base  = median over 45 points on the base's top (15 angles 205-331 deg x 0.6/0.75/0.88 of its radius, lip height),
#         each point the median of its 3x3 pixels (UnitPlinthTests.LumaAt).
# Chest = median over the figures of the 3x3 median at figure-local (0.10, 1.32, 0.10) (#645's shirt point).
# Whole = mean luma of every visible figure pixel (mask: drawn when only the figures draw, and changed when they are hidden).
import json, sys, glob, os, statistics
from PIL import Image

def luma(p): return (0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]) / 255.0

def at(img, pt):
    if pt is None: return None
    w, h = img.size; x, y = int(pt[0]), int(pt[1])
    if x < 2 or y < 2 or x > w - 3 or y > h - 3: return None
    v = sorted(luma(img.getpixel((x + dx, h - 1 - (y + dy)))) for dx in (-1, 0, 1) for dy in (-1, 0, 1))
    return v[4]

def med(v):
    v = sorted(x for x in v if x is not None)
    return v[len(v) // 2] if v else float("nan")

d = sys.argv[1]
rows = []
for jf in sorted(glob.glob(os.path.join(d, "points*.json"))):
    for s in json.load(open(jf)):
        img = Image.open(os.path.join(d, s["name"] + ".png")).convert("RGB")
        mask = Image.open(os.path.join(d, s["name"] + "-mask.png")).convert("L")
        base = med(at(img, p) for p in s["base"])
        chest = med(at(img, p) for p in s["chest"])
        px = [luma(img.getpixel((x, y))) for (x, y) in ((i % img.width, i // img.width) for i, m in enumerate(mask.getdata()) if m > 127)]
        whole = sum(px) / len(px) if px else float("nan")
        rows.append((s["side"], s["km"], s["token"], base, chest, whole, len(px), s["figurePx"], s["heights"], s["models"]))
print("side | km | token | base | chest | chest/base | whole-figure | whole/base | mask px | figure px | heights | models")
for r in rows:
    side, km, tok, b, c, w, n, fpx, hs, ms = r
    print(f"{side} | {km:g} | {tok} | {b:.3f} | {c:.3f} | {c/b:.2f} | {w:.3f} | {w/b:.2f} | {n} | {fpx:.0f} | {hs} | {sorted(set(ms))}")
