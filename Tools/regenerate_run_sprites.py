import math
import os
import shutil
from collections import deque
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.ndimage import binary_dilation

from regenerate_mainchar_sprites import (
    CHAR_DIR,
    FeatureTemplates,
    apply_head_template,
    paint_titan_muscles,
    trim_alpha,
)

# ---------------------------------------------------------------------------
# Kinematic Keyframes & Geometry
# ---------------------------------------------------------------------------
# Angles measured from straight down (0, +1):
#   + swings right/forward (+X), - swings left/backward (-X)
#   shin_deg = thigh_deg - knee_bend_deg
KEYFRAMES = [
    (0, 30.0, 14.0),   # k = 0: Front Contact
    (1, 8.0, 22.0),    # k = 1: Down / Cushion
    (2, -24.0, 16.0),  # k = 2: Push-Off
    (3, -28.0, 60.0),  # k = 3: Rear Recovery Lift
    (4, 12.0, 66.0),   # k = 4: Mid-Swing High Pass
    (5, 36.0, 32.0),   # k = 5: Forward Reach
]

FOOT_ANGLES = {
    0: 0.0,    # Contact foot flat on ground
    1: 0.0,    # Cushion foot planted flat
    2: -25.0,  # Push-off heel raised, toe on ground
    3: -85.0,  # Rear lift trailing along shin
    4: -55.0,  # Mid-swing trailing along shin
    5: 5.0,    # Forward reach ready to strike
}


def feather_titan_head(head_img: Image.Image, feather_px: int = 24) -> Image.Image:
    """Feathers and curves the bottom neck border of Eren Titan head so there is ZERO flat horizontal seam."""
    arr = np.array(head_img, dtype=np.uint8)
    h, w, _ = arr.shape
    ys, xs = np.where(arr[:, :, 3] > 20)
    if len(ys) == 0:
        return head_img
    max_y = int(ys.max())
    mid_x = (int(xs.min()) + int(xs.max())) / 2.0
    half_w = max(1.0, (int(xs.max()) - int(xs.min())) / 2.0)

    for y in range(max(0, max_y - feather_px * 2), h):
        for x in range(w):
            if arr[y, x, 3] == 0:
                continue
            dx_norm = (x - mid_x) / half_w
            curve_drop = (dx_norm ** 2) * (feather_px * 0.6)
            dist_from_cut = max_y - (y + curve_drop)
            if dist_from_cut <= 0:
                arr[y, x, 3] = 0
            elif dist_from_cut < feather_px:
                fade = (dist_from_cut / float(feather_px)) ** 1.3
                arr[y, x, 3] = int(round(arr[y, x, 3] * fade))
    return Image.fromarray(arr, mode="RGBA")


def preserve_eren_titan_dash():
    """Step A1: Preserves current Eren_Titan_Run_0..1.png into Eren_Titan_Dash_0..1.png

    before overwriting Eren_Titan_Run. Keeps the low forward sprint + speed lines for Dash.
    """
    eren_dir = os.path.join(CHAR_DIR, "Eren")
    dash_0 = os.path.join(eren_dir, "Eren_Titan_Dash_0.png")
    dash_1 = os.path.join(eren_dir, "Eren_Titan_Dash_1.png")
    run_0 = os.path.join(eren_dir, "Eren_Titan_Run_0.png")
    run_1 = os.path.join(eren_dir, "Eren_Titan_Run_1.png")

    if not os.path.exists(dash_0) and os.path.exists(run_0):
        shutil.copyfile(run_0, dash_0)
        print(f"[Step A1] Preserved: {run_0} -> {dash_0}")
    if not os.path.exists(dash_1) and os.path.exists(run_1):
        shutil.copyfile(run_1, dash_1)
        print(f"[Step A1] Preserved: {run_1} -> {dash_1}")


def extract_goku_ssj_head() -> Image.Image:
    """Extracts Goku SSJ authentic golden hair + black head from Goku_SSJ_Idle_0.png

    crop (95, 45, 205, 156) using 4-connected border flood-fill outside closed dark ink contour
    (alpha > 180) & (mean_rgb < 95). Sets alpha = 0 on all exterior flood-visited pixels and
    softens 1px outer fringe so ONLY golden spiky SSJ hair + black ink outline + black head remain
    with ZERO orange aura box.
    """
    path = os.path.join(CHAR_DIR, "Goku", "Goku_SSJ_Idle_0.png")
    img = Image.open(path).convert("RGBA")
    crop = img.crop((95, 45, 205, 156))
    arr = np.array(crop, dtype=np.uint8)
    h, w, _ = arr.shape
    rgb = arr[:, :, :3].astype(np.float32)
    mean_rgb = rgb.mean(axis=2)
    alpha = arr[:, :, 3]

    is_barrier = (alpha > 180) & (mean_rgb < 95)
    visited = np.zeros((h, w), dtype=bool)
    q = deque()

    for x in range(w):
        if not is_barrier[0, x]:
            visited[0, x] = True
            q.append((0, x))
        if not is_barrier[h - 1, x] and not visited[h - 1, x]:
            visited[h - 1, x] = True
            q.append((h - 1, x))
    for y in range(h):
        if not is_barrier[y, 0] and not visited[y, 0]:
            visited[y, 0] = True
            q.append((y, 0))
        if not is_barrier[y, w - 1] and not visited[y, w - 1]:
            visited[y, w - 1] = True
            q.append((y, w - 1))

    while q:
        cy, cx = q.popleft()
        for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < h and 0 <= nx < w:
                if not visited[ny, nx] and not is_barrier[ny, nx]:
                    visited[ny, nx] = True
                    q.append((ny, nx))

    arr[visited, 3] = 0

    # Soften 1px outer fringe
    adj = np.zeros((h, w), dtype=bool)
    adj[1:, :] |= visited[:-1, :]
    adj[:-1, :] |= visited[1:, :]
    adj[:, 1:] |= visited[:, :-1]
    adj[:, :-1] |= visited[:, 1:]
    fringe = adj & (~visited) & (arr[:, :, 3] > 180) & (mean_rgb > 95)
    arr[fringe, 3] = (arr[fringe, 3].astype(np.float32) * 0.5).astype(np.uint8)

    # Clear idle neck/shoulder fringe and bottom border outside round head (cx=50, cy=88, r=22)
    arr[103:, :, 3] = 0
    for y in range(92, 103):
        for x in range(w):
            if arr[y, x, 3] > 0 and mean_rgb[y, x] < 70:
                dist_sq = (x - 50) ** 2 + (y - 88) ** 2
                if dist_sq > 22**2:
                    arr[y, x, 3] = 0

    return Image.fromarray(arr, mode="RGBA")


def apply_ssj_aura(base_img: Image.Image, intensity: float = 0.82, torso_cx: float = 149.0) -> Image.Image:
    """Renders a dynamic anime SSJ golden flame aura hugging the character silhouette,

    strictly preserving the 300x300 RGBA canvas, foot contact alignment, and top-left pixel (0, 0) alpha == 0.
    """
    pad = 48
    cw, ch = 300 + pad * 2, 300 + pad
    padded = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    padded.paste(base_img, (pad, pad), base_img)
    parr = np.array(padded, dtype=np.uint8)

    silhouette = parr[:, :, 3] > 80
    outer_mask = binary_dilation(silhouette, iterations=max(6, int(11 * intensity)))
    up_mask = np.zeros_like(outer_mask)
    up_mask[:-8, :] = outer_mask[8:, :]
    aura_shape = outer_mask | up_mask

    outer_col = (255, 175, 15, 135)
    mid_col = (255, 225, 45, 180)
    core_col = (255, 250, 175, 215)

    glow_arr = np.zeros((ch, cw, 4), dtype=np.uint8)
    glow_arr[aura_shape] = outer_col
    mid_mask = binary_dilation(silhouette, iterations=max(3, int(6 * intensity)))
    glow_arr[mid_mask] = mid_col
    inner_mask = binary_dilation(silhouette, iterations=2)
    glow_arr[inner_mask] = core_col
    # Keep grounded foot line locked: flame aura tongues rise upward, never sink below ground plane
    glow_arr[pad + 284:, :, :] = 0

    aura_layer = Image.fromarray(glow_arr, mode="RGBA").filter(ImageFilter.GaussianBlur(radius=4.5))
    comp = Image.alpha_composite(aura_layer, padded)
    out = comp.crop((pad, pad, pad + 300, pad + 300))
    out_arr = np.array(out)
    out_arr[0, 0, 3] = 0
    # Clear any blur spill below ground line y=283 so grounded foot remains locked at y=283
    out_arr[284:, :, 3] = 0
    return Image.fromarray(out_arr, mode="RGBA")


def draw_odm_blade(draw: ImageDraw.ImageDraw, hand_pt: tuple[float, float], angle_deg: float, scale: int = 4):
    """Draws a metallic steel Survey Corps ODM blade (len=66px) with metallic highlight & crossguard hilt."""
    hx, hy = hand_pt
    rad = math.radians(angle_deg)
    ux, uy = math.cos(rad), math.sin(rad)
    px, py = -uy, ux

    blade_len = 66.0 * scale
    blade_w = 4.0 * scale

    p_grip_end = (hx - ux * 12 * scale, hy - uy * 12 * scale)
    p_grip_front = (hx + ux * 6 * scale, hy + uy * 6 * scale)
    p_tip = (hx + ux * blade_len, hy + uy * blade_len)
    p_tip_edge = (hx + ux * (blade_len - 8 * scale) + px * blade_w, hy + uy * (blade_len - 8 * scale) + py * blade_w)
    p_base_edge = (hx + ux * 6 * scale + px * blade_w, hy + uy * 6 * scale + py * blade_w)
    p_base_back = (hx + ux * 6 * scale - px * (blade_w * 0.3), hy + uy * 6 * scale - py * (blade_w * 0.3))

    cg_c = (hx + ux * 5 * scale, hy + uy * 5 * scale)
    cg1 = (cg_c[0] - px * 6 * scale, cg_c[1] - py * 6 * scale)
    cg2 = (cg_c[0] + px * 7 * scale, cg_c[1] + py * 7 * scale)

    # 1. Grip handle & trigger
    draw.line([p_grip_end, p_grip_front], fill=(70, 75, 82, 255), width=int(3.5 * scale))
    draw.line([(hx - ux * 4 * scale, hy - uy * 4 * scale), (hx - ux * 4 * scale + px * 4 * scale, hy - uy * 4 * scale + py * 4 * scale)], fill=(120, 128, 138, 255), width=int(2 * scale))
    draw.line([cg1, cg2], fill=(110, 118, 128, 255), width=int(3 * scale))

    # 2. Steel blade polygon
    draw.polygon([p_base_back, p_base_edge, p_tip_edge, p_tip], fill=(38, 44, 52, 255), outline=(12, 14, 18, 255))
    hl_start = (hx + ux * 7 * scale + px * (blade_w - 1 * scale), hy + uy * 7 * scale + py * (blade_w - 1 * scale))
    hl_end = (hx + ux * (blade_len - 6 * scale) + px * (blade_w - 1 * scale), hy + uy * (blade_len - 6 * scale) + py * (blade_w - 1 * scale))
    draw.line([hl_start, hl_end], fill=(198, 214, 228, 255), width=max(1, int(1.5 * scale)))


def draw_survey_corps_mantle(draw: ImageDraw.ImageDraw, neck_pt: tuple[float, float], frame_idx: int, scale: int = 4):
    """Draws Eren's Survey Corps dark-green scout mantle with high collar and fluttering cloak tail."""
    nx, ny = neck_pt
    cw = 14.0 * scale
    jh = 18.0 * scale
    phase = frame_idx * 2 * math.pi / 6.0
    wave = 4.0 * scale * math.sin(phase)

    # Fluttering cape tail flowing backward (-X)
    cape_pts = [
        (nx - cw, ny - 2 * scale),
        (nx + cw * 0.5, ny - 1 * scale),
        (nx + cw * 0.3, ny + jh + 6 * scale),
        (nx - cw - 18 * scale, ny + jh + 14 * scale + wave),
    ]
    draw.polygon(cape_pts, fill=(22, 54, 34, 255), outline=(12, 26, 16, 255))

    # Fitted collar & shoulder lapels
    sh_pts = [
        (nx - cw - 2 * scale, ny - 4 * scale),
        (nx + cw + 2 * scale, ny - 3 * scale),
        (nx + cw, ny + jh),
        (nx - cw, ny + jh),
    ]
    draw.polygon(sh_pts, fill=(38, 88, 54, 255), outline=(14, 32, 20, 255))
    draw.polygon([
        (nx - cw * 0.5, ny - 3 * scale),
        (nx + cw * 0.5, ny - 3 * scale),
        (nx + 3 * scale, ny + 10 * scale),
        (nx - 4 * scale, neck_y_mantle := ny + 10 * scale),
    ], fill=(66, 128, 86, 255))
    # Cross-chest tan strap
    draw.line([(nx - cw + 2 * scale, ny + 2 * scale), (nx + cw - 2 * scale, ny + jh - 2 * scale)], fill=(158, 134, 96, 255), width=int(2.5 * scale))


def render_run_cycle_for_char(char_key: str, tmpl: FeatureTemplates, ssj_head_tmpl: Image.Image | None = None) -> list[Image.Image]:
    """Renders 6 upright run cycle frames in 4x SSAA (1200x1200) downsampled to 300x300 via Lanczos."""
    scale = 4
    w, h = 300 * scale, 300 * scale

    # Character config
    lean_deg_map = {
        "Goku_Run": 10.5,
        "Goku_SSJ_Run": 12.0,
        "Eren_Run": 12.5,
        "Eren_Titan_Run": 13.0,
        "Diend_Run": 10.0,
    }
    lean_deg = lean_deg_map[char_key]
    is_titan = (char_key == "Eren_Titan_Run")
    thigh_scale = 1.12 if char_key == "Eren_Run" else (0.88 if char_key == "Diend_Run" else 1.0)
    torso_len_1x = 76.0 if is_titan else 72.0

    thigh_len = 42.0 * scale
    shin_len = 42.0 * scale
    torso_len = torso_len_1x * scale
    foot_thick = 6.0 * scale

    limb_w = (16.0 if is_titan else 13.5) * scale
    torso_w = (23.0 if is_titan else 15.0) * scale
    waist_w = (16.0 if is_titan else 15.0) * scale
    fist_r = (8.5 if is_titan else 7.5) * scale
    head_r = (21.5 if is_titan else 19.5) * scale

    col_back = (20, 20, 26, 255)
    col_front = (10, 10, 12, 255)
    col_torso = (10, 10, 12, 255)

    out_frames = []

    for i in range(6):
        im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)

        # Grounded frames: i in {0, 1, 3, 4} -> target_y = 283
        # Flight / push-off frames: i in {2, 5} -> target_y = 279
        base_target_y = 279.0 if i in (2, 5) else 283.0
        target_foot_y_1x = (base_target_y - 2.0) if is_titan else base_target_y
        target_foot_y = target_foot_y_1x * scale
        hip_x = 149.0 * scale

        k_front = i
        k_back = (i + 3) % 6

        def calc_leg(hx, hy, k):
            _, th, kb = KEYFRAMES[k]
            th_r = math.radians(th * thigh_scale)
            sh_r = math.radians(th * thigh_scale - kb)
            kx = hx + thigh_len * math.sin(th_r)
            ky = hy + thigh_len * math.cos(th_r)
            ax = kx + shin_len * math.sin(sh_r)
            ay = ky + shin_len * math.cos(sh_r)
            return (hx, hy), (kx, ky), (ax, ay)

        # Shift hip_y so lowest foot lands exactly on target_foot_y
        _, _, (_, a1y) = calc_leg(hip_x, 0, k_front)
        _, _, (_, a2y) = calc_leg(hip_x, 0, k_back)
        raw_lowest = max(a1y, a2y) + foot_thick
        hip_y = target_foot_y - raw_lowest

        hip_pt = (hip_x, hip_y)
        _, k_b, a_b = calc_leg(hip_x, hip_y, k_back)
        _, k_f, a_f = calc_leg(hip_x, hip_y, k_front)

        # Torso upright lean
        lean_r = math.radians(lean_deg)
        neck_x = hip_x + torso_len * math.sin(lean_r)
        neck_y = hip_y - torso_len * math.cos(lean_r)
        neck_pt = (neck_x, neck_y)

        # Shoulder joint
        sh_dist = 6.0 * scale
        sh_pt = (neck_x - sh_dist * math.sin(lean_r), neck_y + sh_dist * math.cos(lean_r))

        # Arms kinematics
        arm_len1 = 28.0 * scale
        arm_len2 = 26.0 * scale
        phase = i * 2 * math.pi / 6.0

        if char_key in ("Goku_Run", "Goku_SSJ_Run"):
            f_up = [-26.0, -13.0, 14.0, 28.0, 14.0, -13.0][i]
            b_up = [28.0, 14.0, -13.0, -26.0, -13.0, 14.0][i]
            f_kb = 68.0
            b_kb = 68.0
        elif char_key == "Eren_Run":
            f_up = [-22.0, -11.0, 11.0, 22.0, 11.0, -11.0][i]
            b_up = [22.0, 11.0, -11.0, -22.0, -11.0, 11.0][i]
            f_kb = 26.0
            b_kb = 26.0
        elif char_key == "Eren_Titan_Run":
            f_up = [-32.0, -16.0, 16.0, 32.0, 16.0, -16.0][i]
            b_up = [32.0, 16.0, -16.0, -32.0, -16.0, 16.0][i]
            f_kb = 64.0
            b_kb = 64.0
        elif char_key == "Diend_Run":
            b_up = [20.0, 10.0, -10.0, -20.0, -10.0, 10.0][i]
            b_kb = 45.0
            f_up = 9.0 + 3.0 * math.sin(phase)  # steady +6°..+12°
            f_kb = 76.0

        def calc_arm(sh_xy, up_deg, kb_deg):
            up_r = math.radians(up_deg)
            fo_r = math.radians(up_deg + kb_deg)
            ex = sh_xy[0] + arm_len1 * math.sin(up_r)
            ey = sh_xy[1] + arm_len1 * math.cos(up_r)
            wx = ex + arm_len2 * math.sin(fo_r)
            wy = ey + arm_len2 * math.cos(fo_r)
            return (ex, ey), (wx, wy)

        e_b, w_b = calc_arm(sh_pt, b_up, b_kb)
        e_f, w_f = calc_arm(sh_pt, f_up, f_kb)

        def draw_joint_circle(draw, pt, r, col):
            draw.ellipse([pt[0] - r, pt[1] - r, pt[0] + r, pt[1] + r], fill=col)

        def draw_angled_foot(canvas, ankle, k, col):
            ax, ay = ankle
            ang_deg = FOOT_ANGLES[k]
            fw = int(24 * scale)
            fh = int(24 * scale)
            f_im = Image.new("RGBA", (fw * 2, fh * 2), (0, 0, 0, 0))
            fd = ImageDraw.Draw(f_im)
            r0 = (fw - 3.0 * scale, fh - 2.0 * scale, fw + 12.0 * scale, fh + 6.0 * scale)
            fd.rounded_rectangle(r0, radius=3.5 * scale, fill=col)
            rot = f_im.rotate(ang_deg, resample=Image.Resampling.BICUBIC)
            canvas.alpha_composite(rot, (int(round(ax - fw)), int(round(ay - fh))))

        # 1. Back Arm & Back Weapon
        if char_key == "Eren_Run":
            b_blade_ang = 162.0 + 5.0 * math.sin(phase)
            draw_odm_blade(d, w_b, b_blade_ang, scale=scale)
        d.line([sh_pt, e_b], fill=col_back, width=int(limb_w))
        d.line([e_b, w_b], fill=col_back, width=int(limb_w))
        draw_joint_circle(d, sh_pt, limb_w / 2, col_back)
        draw_joint_circle(d, e_b, limb_w / 2, col_back)
        draw_joint_circle(d, w_b, fist_r, col_back)

        # 2. Back Leg
        d.line([hip_pt, k_b], fill=col_back, width=int(limb_w))
        d.line([k_b, a_b], fill=col_back, width=int(limb_w))
        draw_joint_circle(d, hip_pt, limb_w / 2, col_back)
        draw_joint_circle(d, k_b, limb_w / 2, col_back)
        draw_joint_circle(d, a_b, limb_w / 2, col_back)
        draw_angled_foot(im, a_b, k_back, col_back)

        # 3. Torso
        if is_titan:
            tx, ty = math.sin(lean_r), -math.cos(lean_r)
            nx, ny = -ty, tx
            c_w = torso_w / 2
            w_w = waist_w / 2
            t_poly = [
                (neck_x + nx * c_w, neck_y + ny * c_w),
                (neck_x - nx * c_w, neck_y - ny * c_w),
                (hip_x - nx * w_w, hip_y - ny * w_w),
                (hip_x + nx * w_w, hip_y + ny * w_w),
            ]
            d.polygon(t_poly, fill=col_torso)
            draw_joint_circle(d, neck_pt, c_w, col_torso)
            draw_joint_circle(d, hip_pt, w_w, col_torso)
        else:
            d.line([hip_pt, neck_pt], fill=col_torso, width=int(torso_w))
            draw_joint_circle(d, neck_pt, torso_w / 2, col_torso)
            draw_joint_circle(d, hip_pt, torso_w / 2, col_torso)

        # Survey Corps Mantle (Eren)
        if char_key == "Eren_Run":
            draw_survey_corps_mantle(d, neck_pt, i, scale=scale)

        # 4. Front Leg
        d.line([hip_pt, k_f], fill=col_front, width=int(limb_w))
        d.line([k_f, a_f], fill=col_front, width=int(limb_w))
        draw_joint_circle(d, hip_pt, limb_w / 2, col_front)
        draw_joint_circle(d, k_f, limb_w / 2, col_front)
        draw_joint_circle(d, a_f, limb_w / 2, col_front)
        draw_angled_foot(im, a_f, k_front, col_front)

        # 5. Front Arm & Front Weapon
        if char_key == "Eren_Run":
            f_blade_ang = 156.0 - 5.0 * math.sin(phase)
            draw_odm_blade(d, w_f, f_blade_ang, scale=scale)
        d.line([sh_pt, e_f], fill=col_front, width=int(limb_w))
        d.line([e_f, w_f], fill=col_front, width=int(limb_w))
        draw_joint_circle(d, sh_pt, limb_w / 2, col_front)
        draw_joint_circle(d, e_f, limb_w / 2, col_front)
        draw_joint_circle(d, w_f, fist_r, col_front)

        # 6. Head
        head_cx = neck_x + (head_r - 2.0 * scale) * math.sin(lean_r)
        head_cy = neck_y - (head_r - 2.0 * scale) * math.cos(lean_r)
        draw_joint_circle(d, (head_cx, head_cy), head_r, col_front)

        # Downsample from 1200x1200 to 300x300 via Lanczos
        res = im.resize((300, 300), Image.Resampling.LANCZOS)
        head_anchor = (int(round(head_cx / scale)), int(round(head_cy / scale)), int(round(head_r / scale)))

        # Character overlays
        if char_key == "Goku_Run":
            res = apply_head_template(res, tmpl.goku_black_hair, tmpl.goku_black_hair_head_rel, head_anchor, scale_mul=0.92)
        elif char_key == "Goku_SSJ_Run":
            # Scale & paste authentic golden SSJ hair + black head
            if ssj_head_tmpl is not None:
                scale_ssj = (head_anchor[2] * 2.0) / 48.0
                sw = max(1, int(round(ssj_head_tmpl.width * scale_ssj)))
                sh = max(1, int(round(ssj_head_tmpl.height * scale_ssj)))
                ssj_scaled = ssj_head_tmpl.resize((sw, sh), Image.Resampling.LANCZOS)
                # Head center inside crop: cx=50, cy=88 (scaled)
                dx = head_anchor[0] - int(round(50.0 * scale_ssj))
                dy = head_anchor[1] - int(round(88.0 * scale_ssj))
                res.paste(ssj_scaled, (dx, dy), ssj_scaled)
            upper_cx = (neck_x + hip_x) / (2.0 * scale) + 4.0
            res = apply_ssj_aura(res, intensity=0.82 + 0.08 * (i % 2), torso_cx=upper_cx)
        elif char_key == "Eren_Run":
            res = apply_head_template(res, tmpl.eren_hair, tmpl.eren_hair_head_rel, head_anchor, scale_mul=0.92)
        elif char_key == "Eren_Titan_Run":
            res = paint_titan_muscles(res)
            res = apply_head_template(res, tmpl.eren_titan_head, tmpl.eren_titan_head_rel, head_anchor, scale_mul=0.90)
        elif char_key == "Diend_Run":
            res = apply_head_template(res, tmpl.diend_helmet, tmpl.diend_helmet_head_rel, head_anchor, scale_mul=0.92, offset_xy=(0, 2))
            # Overlay 1 single DiendDriver gun in front hand
            gun_w = 46
            gun_h = int(round(tmpl.diend_gun_right.height * gun_w / float(tmpl.diend_gun_right.width)))
            gun_scaled = tmpl.diend_gun_right.resize((gun_w, gun_h), Image.Resampling.LANCZOS)
            gun_rot = trim_alpha(gun_scaled.rotate(-5.0, resample=Image.Resampling.BICUBIC, expand=True))
            hx = int(round(w_f[0] / scale))
            hy = int(round(w_f[1] / scale))
            gx = hx - int(gun_rot.width * 0.24)
            gy = hy - int(gun_rot.height * 0.42)
            res.paste(gun_rot, (gx, gy), gun_rot)

        # Enforce exact 300x300 RGBA & (0, 0)[3] == 0
        if res.size != (300, 300):
            res = res.crop((0, 0, 300, 300))
        arr_final = np.array(res, dtype=np.uint8)
        arr_final[0, 0, 3] = 0
        final_img = Image.fromarray(arr_final, mode="RGBA")

        out_frames.append(final_img)

    return out_frames


def generate_all_run_cycles():
    """Generates and overwrites in-place all 6-frame run cycles for Goku, Goku SSJ, Eren, Eren Titan, and Diend."""
    print("=" * 60)
    print("Starting Track A: Run Cycle Regeneration")
    print("=" * 60)

    # Step A1: Preserve Eren Titan Dash
    preserve_eren_titan_dash()

    # Load templates
    tmpl = FeatureTemplates()
    tmpl.eren_titan_head = feather_titan_head(tmpl.eren_titan_head, feather_px=24)
    ssj_head_tmpl = extract_goku_ssj_head()

    targets = [
        ("Goku", "Goku_Run"),
        ("Goku", "Goku_SSJ_Run"),
        ("Eren", "Eren_Run"),
        ("Eren", "Eren_Titan_Run"),
        ("Diend", "Diend_Run"),
    ]

    for char_folder, anim_prefix in targets:
        char_dir = os.path.join(CHAR_DIR, char_folder)
        frames = render_run_cycle_for_char(anim_prefix, tmpl, ssj_head_tmpl)
        for i, frame in enumerate(frames):
            out_path = os.path.join(char_dir, f"{anim_prefix}_{i}.png")
            # Overwrite in-place (NEVER delete .png or .meta!)
            frame.save(out_path)
            print(f"Saved: {out_path} [Size: {frame.size}, Top-left alpha: {frame.getpixel((0, 0))[3]}]")

    print("=" * 60)
    print("All Run Cycles successfully regenerated and verified!")
    print("=" * 60)


if __name__ == "__main__":
    generate_all_run_cycles()
