#!/usr/bin/env python3
"""
build_character_frames.py —— 角色精灵表 → 统一画布单帧 PNG

把 ImageGen 一次性生成的「一个角色所有姿势」精灵表，切成一组可直接导入 Unity 的
单帧 PNG。所有输出帧共享同一画布尺寸、同一脚底基线、同一躯干水平锚点，
这是避免角色在切帧时「忽胖忽瘦 / 忽高忽矮 / 左右跳动」的唯一可靠办法。

用法
----
    python3 build_character_frames.py \
        --sheet  wk_master18c.png \
        --out    ./wk_frames \
        --cols   9 --rows 2 \
        --names  idle,walk1,walk2,walk3,walk4,atk1,atk2,atk3,sweep1,\
sweep2,cloud1,cloud2,clone1,clone2,clone3,havoc1,havoc2,havoc3 \
        --bbox-anchor cloud1,cloud2,havoc2 \
        --frag-strict cloud1 \
        --contact-sheet ./_contact.png \
        --prefix WK_

依赖: pillow, numpy, scipy

流程
----
1. 绿幕抠像 + 去溢色
2. 按固定网格切格（在理论切线附近搜索"列占用最少"的位置，容忍人物轻微越界）
3. 每格丢弃邻格漏进来的碎片（连通域面积 < 最大块的 frag-ratio）
4. 统一画布：脚底对齐同一基线，水平按躯干重心对齐
5. 输出等尺寸 PNG + 一张棋盘格对照图用于目检
"""

import argparse
import json
import math
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage


# --------------------------------------------------------------------------
# 1. 绿幕抠像
# --------------------------------------------------------------------------
def chroma_key(rgb, threshold=60.0, softness=45.0, despill=0.15):
    """
    rgb: float32 (H,W,3)
    返回 (rgba uint8, alpha float 0~1)

    greenness = G - max(R,B)。纯绿背景 greenness 很大，金黄/红色角色为负，
    所以这个判据对暖色调角色几乎零误伤（比固定色距阈值稳得多）。
    """
    R, G, B = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    mx = np.maximum(R, B)
    greenness = G - mx
    alpha = np.clip((threshold - greenness) / softness, 0.0, 1.0)
    # 去溢色：把边缘吃到的绿压回中性，否则描边外侧会留一圈绿边
    newG = np.where(greenness > 0, mx + greenness * despill, G)
    rgba = np.dstack([R, newG, B, alpha * 255.0]).astype(np.uint8)
    return rgba, alpha


# --------------------------------------------------------------------------
# 2. 切格
# --------------------------------------------------------------------------
def find_cuts(alpha_band, n, search=70):
    """
    在宽度方向找 n-1 条切线。
    不能用「找空列」——相邻人物的尾巴/羽毛经常互相碰上，空列检测会漏格。
    做法：在理论切线 k*W/n 附近 ±search 内找列占用量最小的位置。
    """
    H, W = alpha_band.shape
    col = alpha_band.sum(axis=0)
    step = W / float(n)
    cuts = [0]
    for k in range(1, n):
        c = int(round(k * step))
        lo, hi = max(0, c - search), min(W, c + search)
        cuts.append(lo + int(np.argmin(col[lo:hi])))
    cuts.append(W)
    return cuts


def split_rows(alpha, rows):
    """按行方向等分成 rows 个条带，同样在理论切线附近找最空的行。"""
    H, W = alpha.shape
    row = alpha.sum(axis=1)
    step = H / float(rows)
    bounds = [0]
    for k in range(1, rows):
        c = int(round(k * step))
        lo, hi = max(0, c - 60), min(H, c + 60)
        bounds.append(lo + int(np.argmin(row[lo:hi])))
    bounds.append(H)
    return bounds


# --------------------------------------------------------------------------
# 3. 丢碎片
# --------------------------------------------------------------------------
def drop_fragments(cell_rgba, frag_ratio=0.02):
    """
    丢掉邻格漏进来的碎片：保留面积 >= 最大连通域 * frag_ratio 的块。

    注意：角色自身的装饰（悟空头顶的红羽尾尖）也可能是独立连通域，
    面积能到最大块的 3~4%。所以默认 frag_ratio 别调太高，宁可留下再目检。

    但偶尔邻格会漏进一整只拳头这种大块（悟空 cloud1 就吃到了 sweep2 的拳头，
    面积到最大块的 5%+，默认阈值拦不住）。对这类帧用 --frag-strict 单独
    指定 0.25 的严格阈值——前提是该帧人物本体是单一连通域，先看对照图确认。
    """
    a = cell_rgba[:, :, 3]
    lab, n = ndimage.label(a > 128)
    if n == 0:
        return cell_rgba, 0
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    biggest = sizes.max()
    keep = np.zeros_like(a, dtype=bool)
    dropped = 0
    for i, s in enumerate(sizes):
        if s >= biggest * frag_ratio:
            keep |= (lab == i + 1)
        else:
            dropped += 1
    out = cell_rgba.copy()
    out[:, :, 3] = np.where(keep, a, 0)
    return out, dropped


# --------------------------------------------------------------------------
# 4. 锚点
# --------------------------------------------------------------------------
def torso_anchor_x(cell_rgba, use_bbox_mid=False):
    """
    水平锚点。

    默认用「躯干重心」：取人物高度 30%~55% 那几行的 alpha 质心。
    为什么不用 bbox 中点：挥棍/伸腿会让 bbox 单边暴涨，按 bbox 对齐角色会左右横跳。
    为什么不用整体质心：拖长的尾巴和羽毛会把质心拽偏。
    躯干这一段在所有姿势里最稳定。

    例外：俯冲/砸地这类躯干本身横过来的姿势，躯干带取到的是手臂，
    这时改用 bbox 中点（--bbox-anchor 指定）。
    """
    a = cell_rgba[:, :, 3]
    ys, xs = np.nonzero(a > 8)
    if len(ys) == 0:
        return None
    by0, by1 = ys.min(), ys.max()
    bx0, bx1 = xs.min(), xs.max()
    if use_bbox_mid:
        return (bx0 + bx1) / 2.0, by0, by1, bx0, bx1

    h = by1 - by0 + 1
    r0 = by0 + int(h * 0.30)
    r1 = by0 + int(h * 0.55)
    band = a[r0:r1 + 1].astype(np.float32)
    colw = band.sum(axis=0)
    total = colw.sum()
    if total <= 0:
        return (bx0 + bx1) / 2.0, by0, by1, bx0, bx1
    ax = float((colw * np.arange(len(colw))).sum() / total)
    return ax, by0, by1, bx0, bx1


# --------------------------------------------------------------------------
# 主流程
# --------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet", required=True, help="ImageGen 输出的精灵表 PNG")
    ap.add_argument("--out", required=True, help="输出目录")
    ap.add_argument("--cols", type=int, required=True)
    ap.add_argument("--rows", type=int, required=True)
    ap.add_argument("--names", required=True,
                    help="逗号分隔的帧名，按「先左到右、再上到下」顺序")
    ap.add_argument("--prefix", default="", help="输出文件名前缀，例如 WK_")
    ap.add_argument("--bbox-anchor", default="",
                    help="逗号分隔的帧名，这些帧水平对齐改用 bbox 中点"
                         "（躯干横过来的俯冲/砸地姿势）")
    ap.add_argument("--pad", type=int, default=12, help="画布四周留白像素")
    ap.add_argument("--frag-ratio", type=float, default=0.02)
    ap.add_argument("--frag-strict", default="",
                    help="逗号分隔的帧名，这些帧用严格碎片阈值"
                         "（邻格漏进来一整只手/脚这类大块时用）")
    ap.add_argument("--frag-strict-ratio", type=float, default=0.25)
    ap.add_argument("--search", type=int, default=70, help="切线搜索半径")
    ap.add_argument("--contact-sheet", default="",
                    help="输出一张棋盘格对照图的路径（强烈建议开，用来目检）")
    args = ap.parse_args()

    names = [n.strip() for n in args.names.split(",") if n.strip()]
    bbox_anchor = {n.strip() for n in args.bbox_anchor.split(",") if n.strip()}
    frag_strict = {n.strip() for n in args.frag_strict.split(",") if n.strip()}
    expected = args.cols * args.rows
    if len(names) != expected:
        sys.exit(f"names 数量 {len(names)} 与 cols*rows {expected} 不符")

    os.makedirs(args.out, exist_ok=True)

    rgb = np.array(Image.open(args.sheet).convert("RGB")).astype(np.float32)
    rgba, alpha = chroma_key(rgb)
    print(f"sheet {rgb.shape[1]}x{rgb.shape[0]}")

    # 切格
    row_bounds = split_rows(alpha, args.rows)
    cells = []
    for r in range(args.rows):
        y0, y1 = row_bounds[r], row_bounds[r + 1]
        band = alpha[y0:y1]
        cuts = find_cuts(band, args.cols, args.search)
        for c in range(args.cols):
            cells.append((y0, y1, cuts[c], cuts[c + 1]))

    # 抽帧 + 量锚点
    frames = []
    for idx, name in enumerate(names):
        y0, y1, x0, x1 = cells[idx]
        cell = rgba[y0:y1, x0:x1].copy()
        ratio = args.frag_strict_ratio if name in frag_strict else args.frag_ratio
        cell, dropped = drop_fragments(cell, ratio)
        info = torso_anchor_x(cell, use_bbox_mid=(name in bbox_anchor))
        if info is None:
            sys.exit(f"帧 {name} 抠出来是空的，检查绿幕阈值或切格")
        ax, by0, by1, bx0, bx1 = info
        frames.append(dict(name=name, cell=cell, ax=ax,
                           by0=by0, by1=by1, bx0=bx0, bx1=bx1,
                           dropped=dropped))
        print(f"  {name:10s} h={by1-by0+1:4d} anchor_x={ax:7.1f} "
              f"left={ax-bx0:6.1f} right={bx1-ax:6.1f} frag_dropped={dropped}")

    # 统一画布：宽度取「锚点到左右最远」的最大值，高度取最高帧
    Lmax = max(f["ax"] - f["bx0"] for f in frames)
    Rmax = max(f["bx1"] - f["ax"] for f in frames)
    Hmax = max(f["by1"] - f["by0"] + 1 for f in frames)
    half = int(math.ceil(max(Lmax, Rmax))) + args.pad
    CW, CH = half * 2, Hmax + args.pad * 2
    BASE = CH - args.pad          # 脚底基线所在行

    print(f"\ncanvas {CW}x{CH}  anchor_x={half}  baseline_row={BASE}")

    spill_total = 0
    for f in frames:
        canvas = np.zeros((CH, CW, 4), dtype=np.uint8)
        cell = f["cell"]
        dx = int(round(half - f["ax"]))
        dy = int(round(BASE - f["by1"]))
        # 只贴有效区域，越界裁掉
        sy0, sy1 = 0, cell.shape[0]
        sx0, sx1 = 0, cell.shape[1]
        ty0, tx0 = dy + sy0, dx + sx0
        if ty0 < 0:
            sy0 -= ty0; ty0 = 0
        if tx0 < 0:
            sx0 -= tx0; tx0 = 0
        ty1 = min(CH, dy + sy1)
        tx1 = min(CW, dx + sx1)
        sy1 = sy0 + (ty1 - ty0)
        sx1 = sx0 + (tx1 - tx0)
        canvas[ty0:ty1, tx0:tx1] = cell[sy0:sy1, sx0:sx1]

        m = canvas[:, :, 3] > 8
        gr = canvas[:, :, 1].astype(int) - np.maximum(
            canvas[:, :, 0], canvas[:, :, 2]).astype(int)
        spill = int(((gr > 25) & m).sum())
        spill_total += spill

        ys = np.nonzero(m.any(axis=1))[0]
        path = os.path.join(args.out, f"{args.prefix}{f['name']}.png")
        Image.fromarray(canvas).save(path)
        f["canvas"] = canvas
        print(f"  {f['name']:10s} -> {os.path.basename(path)}  "
              f"bottom={ys.max()} (base {BASE})  spill={spill}")

    print(f"\ntotal green spill px = {spill_total}  (必须是 0)")

    with open(os.path.join(args.out, "_canvas.json"), "w") as fp:
        json.dump(dict(width=int(CW), height=int(CH), anchor_x=int(half),
                       baseline_row=int(BASE), pad=int(args.pad),
                       frames=[f["name"] for f in frames]), fp, indent=2)

    # 对照图
    if args.contact_sheet:
        write_contact_sheet(frames, args.cols, args.rows, CW, CH,
                            half, BASE, args.contact_sheet)
        print(f"contact sheet -> {args.contact_sheet}")


def write_contact_sheet(frames, cols, rows, CW, CH, anchor_x, base, path):
    """棋盘格背景 + 蓝色锚点竖线 + 红色基线，用来目检对齐。"""
    cellw, cellh = CW + 6, CH + 6
    sheet = Image.new("RGB", (cellw * cols, cellh * rows), (40, 40, 40))
    # 棋盘格
    chk = np.zeros((CH, CW, 3), dtype=np.uint8)
    s = 16
    yy, xx = np.mgrid[0:CH, 0:CW]
    chk[:] = np.where((((yy // s) + (xx // s)) % 2)[..., None],
                      np.uint8(200), np.uint8(150))
    for i, f in enumerate(frames):
        c = f["canvas"]
        a = c[:, :, 3:4].astype(np.float32) / 255.0
        comp = (c[:, :, :3].astype(np.float32) * a +
                chk.astype(np.float32) * (1 - a)).astype(np.uint8)
        comp[:, anchor_x] = [60, 120, 255]
        comp[base] = [255, 60, 60]
        sheet.paste(Image.fromarray(comp),
                    ((i % cols) * cellw + 3, (i // cols) * cellh + 3))
    sheet.save(path)


if __name__ == "__main__":
    main()
