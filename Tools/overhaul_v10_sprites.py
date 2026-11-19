"""
v10 Sprite Overhaul Script:
1. Equalize all 6 Assets/Sprites/UI/*_Select.png onto 700 x 768 RGBA (y=44..744, centered x=350),
   angling Eren_Normal_Select.png twin blades cleanly downward so full 700px height fits within width <= 660px.
2. Regenerate all normal Eren animations (Idle, Run, Dash, SwordSlash, DashSlash, ThunderSpear, SpinSlash)
   WITHOUT any Survey Corps scarf/cloak polygon or misplaced extra floating swords/hair, and verify all
   Eren_Block / Eren_Titan sprites are scarf-free.
3. Regenerate Goku_Punch_0..5.png and Goku_SSJ_Punch_0..5.png cleanly on 300 x 300 RGBA canvas
   (matching Goku_Idle_0.png & Goku_SSJ_Idle_0.png scale, grounded at y=283, pixel(0,0) alpha == 0),
   fixing the narrow-crop grid striping bug, removing double hair wigs, and replacing SSJ punch hair
   with a single golden SSJ hair crown + subtle golden aura.
"""

import glob
import math
import os
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import binary_dilation, binary_erosion, binary_fill_holes, distance_transform_edt, label as cc_label

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
if TOOLS_DIR not in sys.path:
    sys.path.insert(0, TOOLS_DIR)

from regenerate_mainchar_sprites import (  # noqa: E402
    CHAR_DIR,
    SRC_DIR,
    UI_DIR,
    FeatureTemplates,
    apply_head_template,
    finalize_char_frame,
    mask_white,
    normalize_to_canvas,
    remove_white_bg,
    save_idle_pair,
    trim_alpha,
)
import regenerate_run_sprites as run_gen  # noqa: E402


# ---------------------------------------------------------------------------
# 1. Character Select Portraits (700 x 768 RGBA)
# ---------------------------------------------------------------------------

def angle_eren_select_blades(e_norm: Image.Image, rot_deg: float = 33.0) -> Image.Image:
    """
    Angles Eren's left and right horizontal ODM blades downward around his fists
    so his full head-to-toe 700px body fits cleanly within width <= 660px on 700x768.
    Input e_norm is trimmed RGBA from Eren_Sprite.png (1363 x 1170).
    """
    w, h = e_norm.size
    arr = np.array(e_norm.convert("RGBA"), dtype=np.uint8)

    # Measured fist centers in trimmed Eren_Sprite crop (1363 x 1170)
    lx, ly = 422, 644
    rx, ry = w - 422, 644

    # Build coordinate grids
    yy, xx = np.mgrid[0:h, 0:w]

    def extract_and_clear_blade(base_arr: np.ndarray, fx: int, fy: int, is_left: bool) -> Image.Image:
        # Sword consists of outer blade (above legs y<840), lower trigger guard (y<735), and rounded fist core.
        # Clear the horizontal pommel/lever protrusion along the exact linear forearm boundary so forearms stay un-notched.
        if is_left:
            outer = (xx < 398) & (yy < 840)
            guard = (yy > 668) & (yy < 735) & (xx < 475)
            forearm_edge = 492.0 - 0.55 * (yy - 600.0)
            clear_outer = (xx < 388) & (yy < 840)
            clear_guard = (yy > 668) & (yy < 735) & (xx < 475)
            clear_pommel = (yy >= 602) & (yy <= 665) & (xx > forearm_edge) & (xx < 535)
        else:
            outer = (xx > (w - 398)) & (yy < 840)
            guard = (yy > 668) & (yy < 735) & (xx > 870)
            forearm_edge = 853.0 + 0.55 * (yy - 600.0)
            clear_outer = (xx > (w - 388)) & (yy < 840)
            clear_guard = (yy > 668) & (yy < 735) & (xx > 870)
            clear_pommel = (yy >= 602) & (yy <= 665) & (xx < forearm_edge) & (xx > 815)

        fist_core = ((xx - fx) ** 2 + (yy - fy) ** 2) <= (36 ** 2)
        sword_mask = (outer | guard | fist_core) & (base_arr[:, :, 3] > 0)

        sword_arr = np.zeros_like(base_arr)
        sword_arr[sword_mask] = base_arr[sword_mask]

        # Clear sword parts protruding from fist on base body without slicing forearm
        clear_mask = clear_outer | clear_guard | clear_pommel
        base_arr[clear_mask] = 0
        return Image.fromarray(sword_arr, mode="RGBA")

    base_arr = arr.copy()
    left_sword = extract_and_clear_blade(base_arr, lx, ly, is_left=True)
    right_sword = extract_and_clear_blade(base_arr, rx, ry, is_left=False)

    # Restore clean rounded fist caps on base body so hand-to-hilt junction is seamless
    base_img = Image.fromarray(base_arr, mode="RGBA")
    bd = ImageDraw.Draw(base_img)
    r_fist = 35
    bd.ellipse([lx - r_fist, ly - r_fist, lx + r_fist, ly + r_fist], fill=(8, 8, 10, 255))
    bd.ellipse([rx - r_fist, ry - r_fist, rx + r_fist, ry + r_fist], fill=(8, 8, 10, 255))

    # Rotate each sword around its fist pivot (pad so rotation never clips)
    pad = 500
    cw, ch = w + pad * 2, h + pad * 2

    def rotate_around_pivot(sword_img: Image.Image, px: int, py: int, deg: float) -> Image.Image:
        padded = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        padded.paste(sword_img, (pad, pad), sword_img)
        return padded.rotate(
            deg,
            resample=Image.Resampling.BICUBIC,
            center=(px + pad, py + pad),
        )

    # PIL rotate(+deg) is counter-clockwise: left blade rotates down-inward (+rot_deg),
    # right blade rotates clockwise down-inward (-rot_deg).
    left_rot = rotate_around_pivot(left_sword, lx, ly, +rot_deg)
    right_rot = rotate_around_pivot(right_sword, rx, ry, -rot_deg)

    comp = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    comp = Image.alpha_composite(comp, left_rot)
    comp = Image.alpha_composite(comp, right_rot)
    comp.paste(base_img, (pad, pad), base_img)

    # Drop any tiny detached speck (< 80px) near handguard/pommel cut
    carr = np.array(comp, dtype=np.uint8)
    clbl, cn = cc_label(carr[:, :, 3] > 20)
    if cn > 1:
        counts = np.bincount(clbl.ravel())
        keep = counts[clbl] >= 80
        carr[~keep, 3] = 0
        comp = Image.fromarray(carr, mode="RGBA")

    return trim_alpha(comp)


def save_uniform_select_portrait(
    clean_rgba: Image.Image,
    out_path: str,
    canvas_size: tuple[int, int] = (700, 768),
    target_h: int = 700,
    max_w: int = 660,
    top_y: int = 44,
):
    """Places trimmed character RGBA cleanly onto 700 x 768 spanning y=top_y..top_y+target_h centered at x=350."""
    clean = trim_alpha(clean_rgba)
    cw, ch = canvas_size
    sw, sh = clean.size
    if sw == 0 or sh == 0:
        raise ValueError(f"Empty portrait for {out_path}")

    scale = min(target_h / float(sh), max_w / float(sw))
    nw = max(1, int(round(sw * scale)))
    nh = max(1, int(round(sh * scale)))
    resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    px = (cw - nw) // 2
    py = top_y + (target_h - nh)
    canvas.paste(resized, (px, py), resized)

    arr = np.array(canvas, dtype=np.uint8)
    arr[0, 0, 3] = 0
    Image.fromarray(arr, mode="RGBA").save(out_path)
    print(f"[Select] {os.path.basename(out_path)}: size={canvas.size}, drawn={nw}x{nh}, pos=({px},{py})..({px+nw},{py+nh})")


def rebuild_all_select_portraits():
    goku_sp = Image.open(os.path.join(SRC_DIR, "Goku_Sprite.png")).convert("RGBA")
    eren_sp = Image.open(os.path.join(SRC_DIR, "Eren_Sprite.png")).convert("RGBA")
    diend_sp = Image.open(os.path.join(SRC_DIR, "Diend_Sprite.png")).convert("RGBA")

    g_norm = trim_alpha(remove_white_bg(goku_sp.crop((360, 110, 990, 1385))))
    g_ssj = trim_alpha(remove_white_bg(goku_sp.crop((1545, 5, 2620, 1435))))
    e_norm_raw = trim_alpha(remove_white_bg(eren_sp.crop((5, 195, 1370, 1380))))
    e_norm = angle_eren_select_blades(e_norm_raw, rot_deg=33.0)
    e_titan = trim_alpha(remove_white_bg(eren_sp.crop((1665, 145, 2515, 1378))))
    d_norm = trim_alpha(remove_white_bg(diend_sp.crop((200, 105, 980, 1380))))
    d_kiva = trim_alpha(remove_white_bg(diend_sp.crop((1715, 45, 2410, 1380))))

    save_uniform_select_portrait(g_norm, os.path.join(UI_DIR, "Goku_Normal_Select.png"))
    save_uniform_select_portrait(g_ssj, os.path.join(UI_DIR, "Goku_SSJ_Select.png"))
    save_uniform_select_portrait(e_norm, os.path.join(UI_DIR, "Eren_Normal_Select.png"))
    save_uniform_select_portrait(e_titan, os.path.join(UI_DIR, "Eren_Titan_Select.png"))
    save_uniform_select_portrait(d_norm, os.path.join(UI_DIR, "Diend_Normal_Select.png"))
    save_uniform_select_portrait(d_kiva, os.path.join(UI_DIR, "Diend_Kiva_Select.png"))


# ---------------------------------------------------------------------------
# 2. Eren Normal Animations (NO Scarf/Cloak, Clean Borders, Grounded y=283)
# ---------------------------------------------------------------------------

def clean_eren_swordslash_red_to_black(img: Image.Image) -> Image.Image:
    """
    Converts Eren's red stickman silhouette in SwordSlash_0..5 to deep black
    while preserving translucent crimson slash arc FX, and fills tiny bright
    interior eye/mouth specks inside the head circle so silhouette is pure black.
    """
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    r = arr[:, :, 0].astype(np.int16)
    g = arr[:, :, 1].astype(np.int16)
    b = arr[:, :, 2].astype(np.int16)
    a = arr[:, :, 3]

    # Solid crimson body pixels (and anti-aliased crimson edge inside body)
    is_red_body = (a > 150) & (r > 70) & (g < 110) & (b < 110)
    arr[is_red_body, 0] = 10
    arr[is_red_body, 1] = 10
    arr[is_red_body, 2] = 12

    # Fill ONLY tiny enclosed facial holes (eyes/mouth <= 220px) inside the dark head/torso silhouette
    # Never paint an oversized circle over the chest/arm negative space.
    dark = (arr[:, :, 3] > 140) & (arr[:, :, :3].mean(axis=2) < 60)
    dys, _ = np.where(dark)
    if len(dys) > 0:
        y_cut = int(dys.min() + (dys.max() - dys.min()) * 0.50)
        holes = binary_fill_holes(dark) & ~dark
        hlbl, hn = cc_label(holes)
        if hn > 0:
            counts = np.bincount(hlbl.ravel())
            counts[0] = 0
            # Small enclosed eye/mouth cutouts <= 220px located in upper silhouette
            small_holes = np.zeros_like(holes)
            for k in range(1, hn + 1):
                if 0 < counts[k] <= 220:
                    comp_mask = hlbl == k
                    cys, _ = np.where(comp_mask)
                    if len(cys) > 0 and cys.mean() <= y_cut:
                        small_holes |= comp_mask
            # Also darken 1px pink AA rim hugging the filled eye/mouth cutout
            rim = binary_dilation(small_holes, iterations=1) & (a > 120) & ((r - g > 12) | (arr[:, :, :3].mean(axis=2) < 140))
            fill_mask = small_holes | rim
            arr[fill_mask, 0] = 10
            arr[fill_mask, 1] = 10
            arr[fill_mask, 2] = 12
            arr[fill_mask, 3] = 255

    return Image.fromarray(arr, mode="RGBA")


def strip_outer_edge_artifacts(img: Image.Image, margin: int = 4) -> Image.Image:
    """Only strips stray 1-2px dark lines flush against the outer margin of a crop (never interior!)."""
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    opaque = arr[:, :, 3] > 60
    dark = opaque & (arr[:, :, :3].mean(axis=2) < 200)

    col_sums = dark.sum(axis=0)
    for x in list(range(min(margin, w))) + list(range(max(0, w - margin), w)):
        if col_sums[x] > h * 0.55:
            arr[:, x, 3] = 0

    row_sums = dark.sum(axis=1)
    for y in list(range(min(margin, h))) + list(range(max(0, h - margin), h)):
        if row_sums[y] > w * 0.55:
            arr[y, :, 3] = 0

    return Image.fromarray(arr, mode="RGBA")


def build_clean_6_frames(
    crops: list[Image.Image],
    out_dir: str,
    prefix: str,
    post_fn=None,
    target_body_h: int = 230,
    ref_h: int | None = None,
):
    n = len(crops)
    seq = [crops[min(n - 1, int(round(k * (n - 1) / 5.0)))] if n > 1 else crops[0] for k in range(6)]
    if ref_h is None:
        ref_h = max(
            1,
            max((trim_alpha(strip_outer_edge_artifacts(remove_white_bg(c))).size[1] for c in crops), default=1),
        )
    for idx, src in enumerate(seq):
        clean = strip_outer_edge_artifacts(remove_white_bg(src))
        if post_fn is not None:
            clean = post_fn(clean, idx)
        norm = normalize_to_canvas(clean, target_body_h=target_body_h, max_crop_ref_h=ref_h, canvas_h=300)
        final = finalize_char_frame(norm, target_foot_y=283)
        final.save(os.path.join(out_dir, f"{prefix}_{idx}.png"))


def render_eren_run_no_mantle(tmpl: FeatureTemplates) -> list[Image.Image]:
    """
    Replicates regenerate_run_sprites.py's upright Eren_Run kinematics (12.5° lean, dual ODM blades,
    Eren spiky hair) WITHOUT drawing any Survey Corps mantle/scarf polygon.
    """
    scale = 4
    w, h = 300 * scale, 300 * scale
    lean_deg = 12.5
    thigh_scale = 1.12
    torso_len = 72.0 * scale
    thigh_len = 42.0 * scale
    shin_len = 42.0 * scale
    foot_thick = 6.0 * scale
    limb_w = 13.5 * scale
    torso_w = 15.0 * scale
    fist_r = 7.5 * scale
    head_r = 19.5 * scale

    col_back = (20, 20, 26, 255)
    col_front = (10, 10, 12, 255)
    col_torso = (10, 10, 12, 255)
    out_frames = []

    for i in range(6):
        im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)

        target_foot_y = (279.0 if i in (2, 5) else 283.0) * scale
        hip_x = 149.0 * scale
        k_front = i
        k_back = (i + 3) % 6

        def calc_leg(hx, hy, k):
            _, th, kb = run_gen.KEYFRAMES[k]
            th_r = math.radians(th * thigh_scale)
            sh_r = math.radians(th * thigh_scale - kb)
            kx = hx + thigh_len * math.sin(th_r)
            ky = hy + thigh_len * math.cos(th_r)
            ax = kx + shin_len * math.sin(sh_r)
            ay = ky + shin_len * math.cos(sh_r)
            return (hx, hy), (kx, ky), (ax, ay)

        _, _, (_, a1y) = calc_leg(hip_x, 0, k_front)
        _, _, (_, a2y) = calc_leg(hip_x, 0, k_back)
        hip_y = target_foot_y - (max(a1y, a2y) + foot_thick)

        hip_pt = (hip_x, hip_y)
        _, k_b, a_b = calc_leg(hip_x, hip_y, k_back)
        _, k_f, a_f = calc_leg(hip_x, hip_y, k_front)

        lean_r = math.radians(lean_deg)
        neck_x = hip_x + torso_len * math.sin(lean_r)
        neck_y = hip_y - torso_len * math.cos(lean_r)
        neck_pt = (neck_x, neck_y)

        sh_dist = 6.0 * scale
        sh_pt = (neck_x - sh_dist * math.sin(lean_r), neck_y + sh_dist * math.cos(lean_r))

        arm_len1 = 28.0 * scale
        arm_len2 = 26.0 * scale
        phase = i * 2 * math.pi / 6.0
        f_up = [-22.0, -11.0, 11.0, 22.0, 11.0, -11.0][i]
        b_up = [22.0, 11.0, -11.0, -22.0, -11.0, 11.0][i]

        def calc_arm(sh_xy, up_deg, kb_deg=26.0):
            up_r = math.radians(up_deg)
            fo_r = math.radians(up_deg + kb_deg)
            ex = sh_xy[0] + arm_len1 * math.sin(up_r)
            ey = sh_xy[1] + arm_len1 * math.cos(up_r)
            wx = ex + arm_len2 * math.sin(fo_r)
            wy = ey + arm_len2 * math.cos(fo_r)
            return (ex, ey), (wx, wy)

        e_b, w_b = calc_arm(sh_pt, b_up)
        e_f, w_f = calc_arm(sh_pt, f_up)

        def draw_joint_circle(pt, r, col):
            d.ellipse([pt[0] - r, pt[1] - r, pt[0] + r, pt[1] + r], fill=col)

        def draw_angled_foot(canvas, ankle, k, col):
            ax, ay = ankle
            ang_deg = run_gen.FOOT_ANGLES[k]
            fw, fh = int(24 * scale), int(24 * scale)
            f_im = Image.new("RGBA", (fw * 2, fh * 2), (0, 0, 0, 0))
            fd = ImageDraw.Draw(f_im)
            r0 = (fw - 3.0 * scale, fh - 2.0 * scale, fw + 12.0 * scale, fh + 6.0 * scale)
            fd.rounded_rectangle(r0, radius=3.5 * scale, fill=col)
            rot = f_im.rotate(ang_deg, resample=Image.Resampling.BICUBIC)
            canvas.alpha_composite(rot, (int(round(ax - fw)), int(round(ay - fh))))

        # Back arm + back ODM blade
        run_gen.draw_odm_blade(d, w_b, 162.0 + 5.0 * math.sin(phase), scale=scale)
        d.line([sh_pt, e_b], fill=col_back, width=int(limb_w))
        d.line([e_b, w_b], fill=col_back, width=int(limb_w))
        draw_joint_circle(sh_pt, limb_w / 2, col_back)
        draw_joint_circle(e_b, limb_w / 2, col_back)
        draw_joint_circle(w_b, fist_r, col_back)

        # Back leg
        d.line([hip_pt, k_b], fill=col_back, width=int(limb_w))
        d.line([k_b, a_b], fill=col_back, width=int(limb_w))
        draw_joint_circle(hip_pt, limb_w / 2, col_back)
        draw_joint_circle(k_b, limb_w / 2, col_back)
        draw_joint_circle(a_b, limb_w / 2, col_back)
        draw_angled_foot(im, a_b, k_back, col_back)

        # Torso (NO Survey Corps mantle!)
        d.line([hip_pt, neck_pt], fill=col_torso, width=int(torso_w))
        draw_joint_circle(neck_pt, torso_w / 2, col_torso)
        draw_joint_circle(hip_pt, torso_w / 2, col_torso)

        # Front leg
        d.line([hip_pt, k_f], fill=col_front, width=int(limb_w))
        d.line([k_f, a_f], fill=col_front, width=int(limb_w))
        draw_joint_circle(hip_pt, limb_w / 2, col_front)
        draw_joint_circle(k_f, limb_w / 2, col_front)
        draw_joint_circle(a_f, limb_w / 2, col_front)
        draw_angled_foot(im, a_f, k_front, col_front)

        # Front arm + front ODM blade
        run_gen.draw_odm_blade(d, w_f, 156.0 - 5.0 * math.sin(phase), scale=scale)
        d.line([sh_pt, e_f], fill=col_front, width=int(limb_w))
        d.line([e_f, w_f], fill=col_front, width=int(limb_w))
        draw_joint_circle(sh_pt, limb_w / 2, col_front)
        draw_joint_circle(e_f, limb_w / 2, col_front)
        draw_joint_circle(w_f, fist_r, col_front)

        # Head + Eren spiky hair
        head_cx = neck_x + (head_r - 2.0 * scale) * math.sin(lean_r)
        head_cy = neck_y - (head_r - 2.0 * scale) * math.cos(lean_r)
        draw_joint_circle((head_cx, head_cy), head_r, col_front)

        res = im.resize((300, 300), Image.Resampling.LANCZOS)
        head_anchor = (int(round(head_cx / scale)), int(round(head_cy / scale)), int(round(head_r / scale)))
        res = apply_head_template(res, tmpl.eren_hair, tmpl.eren_hair_head_rel, head_anchor, scale_mul=0.92)

        if res.size != (300, 300):
            res = res.crop((0, 0, 300, 300))
        arr_final = np.array(res, dtype=np.uint8)
        arr_final[0, 0, 3] = 0
        out_frames.append(Image.fromarray(arr_final, mode="RGBA"))

    return out_frames


def regenerate_eren_no_scarf(tmpl: FeatureTemplates):
    out_dir = os.path.join(CHAR_DIR, "Eren")
    eren_sp = Image.open(os.path.join(SRC_DIR, "Eren_Sprite.png")).convert("RGBA")
    e_norm = eren_sp.crop((5, 195, 1370, 1380))

    # 1. Eren_Idle_0..1 (from Eren_Sprite.png directly, zero scarf/cloak)
    save_idle_pair(e_norm, out_dir, "Eren_Idle", target_body_h=235, post_fn=None)

    # 2. Eren_Run_0..5 (upright run cycle without Survey Corps mantle)
    for idx, frame in enumerate(render_eren_run_no_mantle(tmpl)):
        frame.save(os.path.join(out_dir, f"Eren_Run_{idx}.png"))

    # 3. ErenA.png action sets with strict interior cell crops (no cell dividers, no cloak, no double hair/swords)
    sheet = Image.open(os.path.join(SRC_DIR, "ErenA.png")).convert("RGBA")

    # R1: SwordSlash_0..5
    r1_boxes = [(42, 184, 250, 402), (266, 184, 472, 402), (486, 184, 684, 402), (698, 184, 888, 402), (902, 184, 1093, 402), (1107, 184, 1298, 402)]
    build_clean_6_frames(
        [sheet.crop(b) for b in r1_boxes],
        out_dir,
        "Eren_SwordSlash",
        post_fn=lambda im, _: clean_eren_swordslash_red_to_black(im),
        target_body_h=228,
    )

    # R2: DashSlash_0..5 + Dash_0..1
    r2_boxes = [(42, 504, 252, 731), (266, 504, 485, 731), (499, 504, 696, 731), (710, 504, 892, 731), (906, 504, 1093, 731), (1107, 504, 1298, 731)]
    build_clean_6_frames([sheet.crop(b) for b in r2_boxes], out_dir, "Eren_DashSlash", target_body_h=228)
    for idx, box in enumerate([r2_boxes[0], r2_boxes[2]]):
        clean = strip_outer_edge_artifacts(remove_white_bg(sheet.crop(box)))
        norm = normalize_to_canvas(clean, target_body_h=228, canvas_h=300)
        finalize_char_frame(norm, target_foot_y=283).save(os.path.join(out_dir, f"Eren_Dash_{idx}.png"))

    # R3: ThunderSpear_0..5 (includes full firing fist x=875)
    r3_boxes = [(42, 836, 200, 1058), (214, 836, 353, 1058), (367, 836, 521, 1058), (535, 836, 696, 1058), (710, 836, 875, 1058)]
    build_clean_6_frames([sheet.crop(b) for b in r3_boxes], out_dir, "Eren_ThunderSpear", target_body_h=228)

    # R4: SpinSlash_0..5
    r4_boxes = [(42, 1170, 188, 1387), (202, 1170, 353, 1387), (367, 1170, 575, 1387), (589, 1170, 840, 1387), (854, 1170, 1065, 1387), (1078, 1170, 1298, 1387)]
    build_clean_6_frames([sheet.crop(b) for b in r4_boxes], out_dir, "Eren_SpinSlash", target_body_h=228)

    # Verify ALL Eren PNGs (including Block and Titan) have zero green scarf pixels
    for p in sorted(glob.glob(os.path.join(out_dir, "*.png"))):
        arr = np.array(Image.open(p).convert("RGBA"), dtype=np.uint8)
        r, g, b, a = arr[:, :, 0].astype(int), arr[:, :, 1].astype(int), arr[:, :, 2].astype(int), arr[:, :, 3]
        green = (a > 100) & (g > r + 10) & (g > b + 5) & (g < 180)
        if int(green.sum()) > 0:
            raise RuntimeError(f"Unexpected green scarf pixels ({int(green.sum())}) in {p}")
    print("[Eren] Regenerated Idle/Run/Dash/SwordSlash/DashSlash/ThunderSpear/SpinSlash cleanly & verified 0 scarf pixels across all Eren PNGs.")


# ---------------------------------------------------------------------------
# 3. Goku Normal Punch & SSJ Punch (300 x 300 RGBA matching Goku_Idle_0)
# ---------------------------------------------------------------------------

def clean_goku_punch_cell0(cell_crop: Image.Image, tmpl: FeatureTemplates) -> Image.Image:
    """
    Removes the faint thin background grid from GokuA top-left cell (0) via morphological opening
    while keeping Goku's full solid black stickman silhouette (including his back leg), then adds
    ONE properly scaled black Saiyan hair crown matching Goku_Idle_0.png proportions.
    """
    rgba = remove_white_bg(cell_crop)
    arr = np.array(rgba, dtype=np.uint8)
    rgb = arr[:, :, :3].astype(np.int16)
    lum = rgb.mean(axis=2)

    # Goku's thick stickman limbs are ~12px wide & pure dark (lum < 70); grid lines are 1-2px thin & lighter
    disk5 = np.ones((5, 5), dtype=bool)
    disk5[0, 0] = disk5[0, 4] = disk5[4, 0] = disk5[4, 4] = False
    dark = (arr[:, :, 3] > 140) & (lum < 72)
    eroded = binary_erosion(dark, structure=disk5)
    thick = binary_dilation(eroded, structure=disk5)
    # Keep largest thick connected body component (drops all thin grid strokes cleanly)
    lbl, nlbl = cc_label(thick)
    if nlbl > 0:
        counts = np.bincount(lbl.ravel())
        counts[0] = 0
        main_thick = lbl == int(np.argmax(counts))
        # Allow only 1px jet-black AA fringe immediately bordering main_thick (excludes gray grid junctions)
        aa_rim = binary_dilation(main_thick, iterations=1) & (arr[:, :, 3] > 125) & (lum < 65)
        keep = main_thick | aa_rim
        arr[~keep, 3] = 0

    cleaned = trim_alpha(Image.fromarray(arr, mode="RGBA"))

    # Locate exact bald round cranium via distance transform
    carr = np.array(cleaned, dtype=np.uint8)
    cdark = (carr[:, :, 3] > 120) & (carr[:, :, :3].mean(axis=2) < 85)
    cys, _ = np.where(cdark)
    y0, y1 = int(cys.min()), int(cys.max())
    top_only = cdark.copy()
    top_only[y0 + int((y1 - y0) * 0.36):, :] = False
    dt = distance_transform_edt(cdark)
    dt[~top_only] = 0
    cy, cx = np.unravel_index(np.argmax(dt), dt.shape)
    hr = max(16, int(round(float(dt[cy, cx]))))

    # Apply ONE black Goku hair crown matching Goku_Idle_0 head-to-hair ratio
    with_hair = apply_head_template(
        cleaned,
        tmpl.goku_black_hair,
        tmpl.goku_black_hair_head_rel,
        (int(cx), int(cy), hr),
        scale_mul=0.88,
        angle_deg=-6.0,
    )
    return trim_alpha(with_hair)


def detect_cranium_in_punch(img: Image.Image) -> tuple[int, int, float]:
    """Detects Goku's circular cranium center (cx, cy) and radius in raw GokuA punch crop."""
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    dark = (arr[:, :, 3] > 100) & (arr[:, :, :3].mean(axis=2) < 90)
    lbl, nlbl = cc_label(dark)
    if nlbl > 0:
        counts = np.bincount(lbl.ravel())
        counts[0] = 0
        body = lbl == int(np.argmax(counts))
    else:
        body = dark
    ys, _ = np.where(body)
    y_min, y_max = int(ys.min()), int(ys.max())
    top_mask = body.copy()
    top_mask[y_min + int((y_max - y_min) * 0.42):, :] = False
    dt = distance_transform_edt(body)
    dt[~top_mask] = 0
    cy, cx = np.unravel_index(np.argmax(dt), dt.shape)
    return int(cx), int(cy), float(dt[cy, cx])


def erase_black_hair_spikes(img: Image.Image, cx: int, cy: int, cranium_r: float = 17.2) -> Image.Image:
    """
    Erases Goku's black spiky hair protruding outside his round cranium (cx, cy, cranium_r)
    in raw GokuA SSJ punch crops while keeping his round cranium, neck/torso, raised fists,
    and disconnected punch starbursts untouched.
    """
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    dark = (arr[:, :, 3] > 60) & (arr[:, :, :3].mean(axis=2) < 120)
    lbl, nlbl = cc_label(dark)
    if nlbl > 0:
        counts = np.bincount(lbl.ravel())
        counts[0] = 0
        main_body = lbl == int(np.argmax(counts))
    else:
        main_body = dark

    yy, xx = np.mgrid[0:h, 0:w]
    outside_cranium = ((xx - cx) ** 2 + (yy - cy) ** 2) > (cranium_r ** 2)
    neck_protect = (yy > cy + 10) & (np.abs(xx - cx) <= 10)
    head_zone = (yy <= cy + 18) & (xx >= cx - 68) & (xx <= cx + 48) & (~neck_protect)

    # Erase hair spikes connected to main body (plus any detached stray spike-tip specks near head left/top)
    spikes = main_body & outside_cranium & head_zone
    stray_specks = (~main_body) & (arr[:, :, 3] > 20) & (yy <= cy + 16) & (xx >= cx - 68) & (xx <= cx + 28) & outside_cranium
    spikes_clean = (binary_dilation(spikes, iterations=2) | stray_specks) & outside_cranium & head_zone
    arr[spikes_clean, 3] = 0

    out = Image.fromarray(arr, mode="RGBA")
    # Ensure a crisp solid round black cranium remains
    d = ImageDraw.Draw(out)
    r_draw = cranium_r - 0.5
    d.ellipse([cx - r_draw, cy - r_draw, cx + r_draw, cy + r_draw], fill=(8, 8, 10, 255))
    return out


def clean_ssj_head_chin_fringe(ssj_head: Image.Image) -> Image.Image:
    """
    Isolates ONLY the golden spiky SSJ hair crown + its immediate dark ink outline from extract_goku_ssj_head(),
    stripping the lower black face/chin/neck and glowing chin rim so it composites cleanly onto any head pose.
    """
    arr = np.array(ssj_head.convert("RGBA"), dtype=np.uint8)
    rgb = arr[:, :, :3].astype(np.float32)
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    yellow_hair = (arr[:, :, 3] > 30) & (sat >= 35) & (rgb[:, :, 0] > 110)
    hair_plus_ink = binary_dilation(yellow_hair, iterations=3) & (arr[:, :, 3] > 15)
    arr[~hair_plus_ink, 3] = 0
    return Image.fromarray(arr, mode="RGBA")


def regenerate_goku_punches(tmpl: FeatureTemplates):
    out_dir = os.path.join(CHAR_DIR, "Goku")
    sheet = Image.open(os.path.join(SRC_DIR, "GokuA.png")).convert("RGBA")
    sheet = mask_white(
        sheet,
        [
            (2, 2, 260, 34),
            (1378, 2, 1630, 34),
            (2, 228, 260, 260),
            (1378, 228, 1590, 260),
        ],
    )

    # --- A. Normal Goku_Punch_0..5 ---
    # Frame 0: Full cell 0 (x=4..168) cleaned of grid lines + single black hair
    cell0_cleaned = clean_goku_punch_cell0(sheet.crop((4, 38, 168, 213)), tmpl)

    # Frames 1..5: Already have Goku's natural Saiyan hair! Trim far-left speed lines on frame 5 so
    # Goku stays full ~230px height without width compression.
    punch_boxes_1_to_5 = [
        (242, 38, 436, 213),
        (475, 38, 664, 213),
        (698, 38, 903, 213),
        (941, 38, 1112, 213),
        (1158, 38, 1362, 213),
    ]
    punch_imgs = [cell0_cleaned] + [
        trim_alpha(strip_outer_edge_artifacts(remove_white_bg(sheet.crop(b)), margin=2))
        for b in punch_boxes_1_to_5
    ]

    # Reference height from natural punch poses 1..4 (~159px) -> scales cleanly to ~228px body height
    ref_h_norm = max(im.size[1] for im in punch_imgs[1:5])
    for idx, im in enumerate(punch_imgs):
        norm = normalize_to_canvas(im, target_body_h=228, max_crop_ref_h=ref_h_norm, canvas_h=300)
        final = finalize_char_frame(norm, target_foot_y=283)
        final.save(os.path.join(out_dir, f"Goku_Punch_{idx}.png"))
        print(f"[Goku_Punch_{idx}] size={final.size}, bbox={final.getbbox()}")

    # --- B. SSJ Goku_SSJ_Punch_0..5 ---
    ssj_punch_boxes = [
        (1430, 38, 1560, 213),
        (1644, 38, 1800, 213),
        (1871, 38, 2031, 213),
        (2086, 38, 2270, 213),
        (2326, 38, 2501, 213),
        (2529, 38, 2713, 213),
    ]
    ssj_head_tmpl = clean_ssj_head_chin_fringe(run_gen.extract_goku_ssj_head())

    # Step 1: Erase black hair spikes on raw crops and store cranium anchors
    bald_ssj_raw = []
    for idx, b in enumerate(ssj_punch_boxes):
        raw = trim_alpha(strip_outer_edge_artifacts(remove_white_bg(sheet.crop(b)), margin=2))
        cx, cy, _ = detect_cranium_in_punch(raw)
        bald = erase_black_hair_spikes(raw, cx, cy, cranium_r=17.2)
        bald_ssj_raw.append(bald)

    # Measure bald body height (~142px from cranium top to foot) -> matches Goku_SSJ_Idle_0 height cleanly
    ref_bald_h = max(trim_alpha(im).size[1] for im in bald_ssj_raw)
    target_dark_h = 205  # Leaves ample headroom above tall SSJ spikes + aura so y=0 never clips

    for idx, bald in enumerate(bald_ssj_raw):
        norm = normalize_to_canvas(bald, target_body_h=target_dark_h, max_crop_ref_h=ref_bald_h, canvas_h=300)
        # Re-detect scaled cranium center on 300x300 canvas
        cx, cy, dt_r = detect_cranium_in_punch(norm)
        head_diam = max(44.0, dt_r * 2.0)

        # Tilt golden SSJ hair subtly forward with punch lean (-4° windup, -10° punch)
        tilt_deg = -4.0 if idx == 0 else -10.0
        scale_ssj = head_diam / 54.0
        sw = max(1, int(round(ssj_head_tmpl.width * scale_ssj)))
        sh = max(1, int(round(ssj_head_tmpl.height * scale_ssj)))
        ssj_scaled = ssj_head_tmpl.resize((sw, sh), Image.Resampling.LANCZOS)

        # Anchor pivot (50, 88) inside 110x111 ssj_head_tmpl crop
        piv_x = 50.0 * scale_ssj
        piv_y = 88.0 * scale_ssj
        if abs(tilt_deg) > 0.5:
            # Rotate around image center and track transformed pivot offset
            ox, oy = piv_x - sw / 2.0, piv_y - sh / 2.0
            rad = math.radians(-tilt_deg)  # PIL rotates counter-clockwise -> screen y down flips sign
            rx = ox * math.cos(rad) - oy * math.sin(rad)
            ry = ox * math.sin(rad) + oy * math.cos(rad)
            ssj_rot = ssj_scaled.rotate(tilt_deg, resample=Image.Resampling.BICUBIC, expand=True)
            dx = int(round(cx - (ssj_rot.width / 2.0 + rx)))
            dy = int(round(cy - (ssj_rot.height / 2.0 + ry)))
            ssj_piece = ssj_rot
        else:
            dx = int(round(cx - piv_x))
            dy = int(round(cy - piv_y))
            ssj_piece = ssj_scaled

        with_hair = norm.copy()
        with_hair.paste(ssj_piece, (dx, dy), ssj_piece)

        # Apply subtle golden SSJ aura matching Goku_SSJ_Run / Goku_SSJ_Idle (leave headroom at y>=2)
        with_aura = run_gen.apply_ssj_aura(with_hair, intensity=0.62 + 0.04 * (idx % 2), torso_cx=float(cx))
        final = finalize_char_frame(with_aura, target_foot_y=283)
        final.save(os.path.join(out_dir, f"Goku_SSJ_Punch_{idx}.png"))
        print(f"[Goku_SSJ_Punch_{idx}] size={final.size}, bbox={final.getbbox()}")


# ---------------------------------------------------------------------------
# 4. Global Verification
# ---------------------------------------------------------------------------

def verify_all_sprite_corners():
    sprite_dirs = ["UI", "Characters", "Combat", "Enemies"]
    sprites_root = os.path.join(os.path.dirname(UI_DIR))
    checked = 0
    bad = []
    for sub in sprite_dirs:
        sub_dir = os.path.join(sprites_root, sub)
        if not os.path.isdir(sub_dir):
            continue
        for p in sorted(glob.glob(os.path.join(sub_dir, "**", "*.png"), recursive=True)):
            checked += 1
            with Image.open(p) as im:
                rgba = im.convert("RGBA")
                a0 = rgba.getpixel((0, 0))[3]
                if rgba.size[0] <= 0 or rgba.size[1] <= 0 or a0 != 0:
                    bad.append((p, rgba.size, a0))
    print(f"[CornerAlphaCheck] checked={checked}, bad={len(bad)}")
    for item in bad:
        print("  BAD:", item)
    if bad:
        raise RuntimeError(f"{len(bad)} PNGs failed pixel(0,0) alpha == 0 check")


def main():
    tmpl = FeatureTemplates()
    rebuild_all_select_portraits()
    regenerate_eren_no_scarf(tmpl)
    regenerate_goku_punches(tmpl)
    verify_all_sprite_corners()
    print("v10 sprite overhaul complete!")


if __name__ == "__main__":
    main()
