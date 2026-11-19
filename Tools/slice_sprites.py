import os
from collections import deque
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DIR = os.path.join(ROOT, "Assets", "Sprites", "SourceSheets")
UI_DIR = os.path.join(ROOT, "Assets", "Sprites", "UI")
CHAR_DIR = os.path.join(ROOT, "Assets", "Sprites", "Characters")
COMBAT_DIR = os.path.join(ROOT, "Assets", "Sprites", "Combat")
ENEMY_DIR = os.path.join(ROOT, "Assets", "Sprites", "Enemies")


def ensure_dirs():
    for d in [
        UI_DIR,
        os.path.join(CHAR_DIR, "Goku"),
        os.path.join(CHAR_DIR, "Eren"),
        os.path.join(CHAR_DIR, "Diend"),
        COMBAT_DIR,
        ENEMY_DIR,
    ]:
        os.makedirs(d, exist_ok=True)


def remove_white_bg(img: Image.Image) -> Image.Image:
    """
    Border flood-fill background removal:
    Removes exterior near-white pixels connected to image borders while preserving
    interior white features (Titan eyes/teeth, Diend cards, Kamehameha core).
    Also smooths 1px anti-aliased fringe against white.
    """
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    rgb = arr[:, :, :3].astype(np.int16)
    min_c = rgb.min(axis=2)
    max_c = rgb.max(axis=2)
    is_white = (min_c >= 235) & ((max_c - min_c) <= 22)

    visited = np.zeros((h, w), dtype=bool)
    q = deque()

    # Seed from all 4 borders
    for x in range(w):
        if is_white[0, x]:
            visited[0, x] = True
            q.append((0, x))
        if is_white[h - 1, x] and not visited[h - 1, x]:
            visited[h - 1, x] = True
            q.append((h - 1, x))
    for y in range(h):
        if is_white[y, 0] and not visited[y, 0]:
            visited[y, 0] = True
            q.append((y, 0))
        if is_white[y, w - 1] and not visited[y, w - 1]:
            visited[y, w - 1] = True
            q.append((y, w - 1))

    # 8-connected BFS
    while q:
        cy, cx = q.popleft()
        for dy in (-1, 0, 1):
            ny = cy + dy
            if ny < 0 or ny >= h:
                continue
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                nx = cx + dx
                if 0 <= nx < w and not visited[ny, nx] and is_white[ny, nx]:
                    visited[ny, nx] = True
                    q.append((ny, nx))

    # Clear exterior background alpha
    arr[visited, 3] = 0

    # Smooth 1-pixel anti-aliased fringe 4-adjacent to exterior background
    adj = np.zeros((h, w), dtype=bool)
    adj[1:, :] |= visited[:-1, :]
    adj[:-1, :] |= visited[1:, :]
    adj[:, 1:] |= visited[:, :-1]
    adj[:, :-1] |= visited[:, 1:]
    fringe = adj & (~visited) & (min_c > 190) & ((max_c - min_c) < 30)

    if np.any(fringe):
        mean_rgb = rgb.mean(axis=2)
        new_alpha = np.clip((250.0 - mean_rgb) * 4.0, 0, 255).astype(np.uint8)
        arr[fringe, 3] = new_alpha[fringe]
        # Un-premultiply against white to eliminate white halo
        a_f = np.maximum(new_alpha[fringe].astype(np.float32) / 255.0, 0.08)
        for c in range(3):
            chan = arr[fringe, c].astype(np.float32)
            unpremul = (chan - 255.0 * (1.0 - a_f)) / a_f
            arr[fringe, c] = np.clip(unpremul, 0, 255).astype(np.uint8)

    return Image.fromarray(arr, mode="RGBA")


def trim_alpha(img: Image.Image) -> Image.Image:
    bbox = img.getbbox()
    if bbox is None:
        return img
    return img.crop(bbox)


def save_portrait(crop: Image.Image, out_path: str, canvas_size=(512, 768)):
    clean = trim_alpha(remove_white_bg(crop))
    cw, ch = canvas_size
    max_w, max_h = 460, 700
    sw, sh = clean.size
    if sw == 0 or sh == 0:
        clean.save(out_path)
        return
    scale = min(max_w / sw, max_h / sh)
    nw = max(1, int(round(sw * scale)))
    nh = max(1, int(round(sh * scale)))
    resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    px = (cw - nw) // 2
    py = ch - 24 - nh
    canvas.paste(resized, (px, py), resized)
    canvas.save(out_path)


def save_action_frame(
    crop: Image.Image,
    out_path: str,
    target_body_h: int = 240,
    max_crop_ref_h: int | None = None,
    canvas_h: int = 300,
    anchor_left_x: int | None = None,
    is_cleaned: bool = False,
):
    clean = crop if is_cleaned else remove_white_bg(crop)
    clean = trim_alpha(clean)
    sw, sh = clean.size
    if sw == 0 or sh == 0:
        Image.new("RGBA", (300, canvas_h), (0, 0, 0, 0)).save(out_path)
        return

    ref_h = max_crop_ref_h if (max_crop_ref_h and max_crop_ref_h > 0) else sh
    scale = target_body_h / float(ref_h)
    # Prevent extreme over-scaling if a crop is much wider/shorter
    max_allowed_h = canvas_h - 24
    if sh * scale > max_allowed_h:
        scale = max_allowed_h / float(sh)
    scale = min(scale, 2.5)

    nw = max(1, int(round(sw * scale)))
    nh = max(1, int(round(sh * scale)))
    resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)

    if anchor_left_x is not None:
        cw = max(300, anchor_left_x + nw + 24)
        px = anchor_left_x
    else:
        cw = max(300, nw + 40)
        px = (cw - nw) // 2

    bottom_pad = 16
    py = max(4, canvas_h - bottom_pad - nh)
    canvas = Image.new("RGBA", (cw, canvas_h), (0, 0, 0, 0))
    canvas.paste(resized, (px, py), resized)
    canvas.save(out_path)


def save_idle_pair(crop: Image.Image, dir_path: str, prefix: str, target_body_h: int = 240):
    clean = trim_alpha(remove_white_bg(crop))
    sw, sh = clean.size
    scale = target_body_h / float(sh)
    nw = max(1, int(round(sw * scale)))
    nh = max(1, int(round(sh * scale)))

    cw = max(300, nw + 40)
    ch = 300
    bottom_pad = 16

    # Frame 0: 1.00x height
    r0 = clean.resize((nw, nh), Image.Resampling.LANCZOS)
    c0 = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    c0.paste(r0, ((cw - nw) // 2, ch - bottom_pad - nh), r0)
    c0.save(os.path.join(dir_path, f"{prefix}_0.png"))

    # Frame 1: 0.98x vertical squash, feet locked
    nh1 = max(1, int(round(nh * 0.98)))
    nw1 = max(1, int(round(nw * 1.01)))
    r1 = clean.resize((nw1, nh1), Image.Resampling.LANCZOS)
    c1 = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    c1.paste(r1, ((cw - nw1) // 2, ch - bottom_pad - nh1), r1)
    c1.save(os.path.join(dir_path, f"{prefix}_1.png"))


def save_fx_sprite(crop: Image.Image, out_path: str, max_dim: int = 320):
    clean = trim_alpha(remove_white_bg(crop))
    sw, sh = clean.size
    if sw == 0 or sh == 0:
        clean.save(out_path)
        return
    scale = min(max_dim / float(max(sw, sh)), 1.5)
    nw = max(1, int(round(sw * scale)))
    nh = max(1, int(round(sh * scale)))
    resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)
    cw = max(nw + 16, 64)
    ch = max(nh + 16, 64)
    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    canvas.paste(resized, ((cw - nw) // 2, (ch - nh) // 2), resized)
    canvas.save(out_path)


def mask_white(img: Image.Image, rects: list[tuple[int, int, int, int]]) -> Image.Image:
    copy = img.copy()
    draw = ImageDraw.Draw(copy)
    for r in rects:
        draw.rectangle(r, fill=(255, 255, 255, 255))
    return copy


def measure_max_trimmed_h(sheet: Image.Image, boxes: list[tuple[int, int, int, int]]) -> int:
    max_h = 1
    for b in boxes:
        c = trim_alpha(remove_white_bg(sheet.crop(b)))
        if c.size[1] > max_h:
            max_h = c.size[1]
    return max_h


def process_portraits():
    # Goku
    goku_sp = Image.open(os.path.join(SRC_DIR, "Goku_Sprite.png")).convert("RGBA")
    g_norm = goku_sp.crop((360, 110, 990, 1385))
    g_ssj = goku_sp.crop((1545, 5, 2620, 1435))
    save_portrait(g_norm, os.path.join(UI_DIR, "Goku_Normal_Select.png"))
    save_portrait(g_ssj, os.path.join(UI_DIR, "Goku_SSJ_Select.png"))
    save_idle_pair(g_norm, os.path.join(CHAR_DIR, "Goku"), "Goku_Idle", target_body_h=235)
    save_idle_pair(g_ssj, os.path.join(CHAR_DIR, "Goku"), "Goku_SSJ_Idle", target_body_h=255)

    # Eren
    eren_sp = Image.open(os.path.join(SRC_DIR, "Eren_Sprite.png")).convert("RGBA")
    e_norm = eren_sp.crop((5, 195, 1370, 1380))
    e_titan = eren_sp.crop((1665, 145, 2515, 1378))
    save_portrait(e_norm, os.path.join(UI_DIR, "Eren_Normal_Select.png"))
    save_portrait(e_titan, os.path.join(UI_DIR, "Eren_Titan_Select.png"))
    save_idle_pair(e_norm, os.path.join(CHAR_DIR, "Eren"), "Eren_Idle", target_body_h=235)
    save_idle_pair(e_titan, os.path.join(CHAR_DIR, "Eren"), "Eren_Titan_Idle", target_body_h=245)

    # Diend
    diend_sp = Image.open(os.path.join(SRC_DIR, "Diend_Sprite.png")).convert("RGBA")
    d_norm = diend_sp.crop((200, 105, 980, 1380))
    d_kiva = diend_sp.crop((1715, 45, 2410, 1380))
    save_portrait(d_norm, os.path.join(UI_DIR, "Diend_Normal_Select.png"))
    save_portrait(d_kiva, os.path.join(UI_DIR, "Diend_Kiva_Select.png"))
    save_idle_pair(d_norm, os.path.join(CHAR_DIR, "Diend"), "Diend_Idle", target_body_h=240)
    save_idle_pair(d_kiva, os.path.join(CHAR_DIR, "Diend"), "LordKiva_Idle", target_body_h=245)


def process_goku_sheet():
    out_dir = os.path.join(CHAR_DIR, "Goku")
    sheet = Image.open(os.path.join(SRC_DIR, "GokuA.png")).convert("RGBA")
    # Mask out text labels and tiny grid box in bottom-left of Row 1 cell 0
    sheet = mask_white(
        sheet,
        [
            (2, 2, 260, 34),        # J-Punch Base left
            (1378, 2, 1630, 34),    # J-Punch Base right
            (2, 128, 72, 212),      # tiny grid in R1 cell 0
            (2, 228, 260, 260),     # U-Ki Small
            (1378, 228, 1590, 260), # I-Kaioken
            (2, 506, 265, 536),     # Dash+J-AOE Blast left
            (1378, 506, 1650, 536), # Dash+J-AOE Blast right
            (2, 770, 265, 804),     # U-Multi-hit SSJ left
            (1378, 770, 1645, 804), # U-Multi-hit SSJ right
            (2, 960, 255, 992),     # I-Kamehameha left
            (1378, 960, 1630, 992), # I-Kamehameha right
            (2, 1158, 265, 1190),   # Dash+I-Genki Dama
            (460, 1158, 660, 1190), # Huy diet 1
            (920, 1158, 1120, 1190),# Huy diet 2
            (1378, 1158, 1640, 1190),# Dash+I-Genki Dama 2
            (1834, 1158, 2050, 1190),# Explosion
        ],
    )

    # Row 1 Left: Goku_Punch_0..5
    punch_boxes = [
        (83, 38, 168, 213),
        (242, 38, 436, 213),
        (475, 38, 664, 213),
        (698, 38, 903, 213),
        (941, 38, 1112, 213),
        (1133, 38, 1362, 213),
    ]
    ref_h = measure_max_trimmed_h(sheet, punch_boxes)
    for i, b in enumerate(punch_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_Punch_{i}.png"), 235, ref_h)

    # Row 1 Right: Goku_SSJ_Punch_0..5
    ssj_punch_boxes = [
        (1430, 38, 1560, 213),
        (1644, 38, 1800, 213),
        (1871, 38, 2031, 213),
        (2086, 38, 2270, 213),
        (2326, 38, 2501, 213),
        (2529, 38, 2713, 213),
    ]
    ref_h = measure_max_trimmed_h(sheet, ssj_punch_boxes)
    for i, b in enumerate(ssj_punch_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_SSJ_Punch_{i}.png"), 235, ref_h)

    # Row 2 Left: Goku_KiBlast_0..3
    ki_boxes = [
        (56, 260, 208, 499),
        (240, 260, 410, 499),
        (462, 260, 634, 499),
        (686, 260, 870, 499),
    ]
    ref_h = measure_max_trimmed_h(sheet, ki_boxes)
    for i, b in enumerate(ki_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_KiBlast_{i}.png"), 235, ref_h)

    # Row 2 Right: Goku_Kaioken_0..5
    kaio_boxes = [
        (929, 260, 1071, 499),
        (1164, 260, 1295, 499),
        (1449, 260, 1573, 499),
        (1673, 260, 1828, 499),
        (1960, 234, 2188, 499),
        (2408, 234, 2634, 499),
    ]
    ref_h = measure_max_trimmed_h(sheet, kaio_boxes[:4])
    for i, b in enumerate(kaio_boxes):
        # frames 4 & 5 have tall aura flame above head, scale by normal body ref_h * 1.1
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_Kaioken_{i}.png"), 235 if i < 4 else 270, ref_h if i < 4 else None)

    # Row 3 Left: Dash, Run, DashJ
    dashj_boxes = [(8, 536, 452, 764), (462, 536, 912, 764), (922, 536, 1371, 764)]
    save_action_frame(sheet.crop(dashj_boxes[0]), os.path.join(out_dir, "Goku_Dash_0.png"), 210)
    save_action_frame(sheet.crop(dashj_boxes[1]), os.path.join(out_dir, "Goku_Run_0.png"), 225)
    save_action_frame(sheet.crop(punch_boxes[0]), os.path.join(out_dir, "Goku_Run_1.png"), 225)
    for i, b in enumerate(dashj_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_DashJ_{i}.png"), 235 if i < 2 else 280)

    # Row 3 Right: SSJ Dash, Run, DashJ
    ssj_dashj_boxes = [(1380, 536, 1828, 764), (1836, 536, 2281, 764), (2290, 536, 2746, 764)]
    save_action_frame(sheet.crop(ssj_dashj_boxes[0]), os.path.join(out_dir, "Goku_SSJ_Dash_0.png"), 215)
    save_action_frame(sheet.crop(ssj_dashj_boxes[1]), os.path.join(out_dir, "Goku_SSJ_Run_0.png"), 230)
    save_action_frame(sheet.crop(ssj_punch_boxes[0]), os.path.join(out_dir, "Goku_SSJ_Run_1.png"), 230)
    for i, b in enumerate(ssj_dashj_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_SSJ_DashJ_{i}.png"), 235 if i < 2 else 280)

    # Row 4: Goku_SSJ_MultiHit_0..5
    mh_boxes = [
        (6, 804, 453, 953),
        (461, 804, 913, 953),
        (921, 804, 1371, 953),
        (1380, 804, 1827, 953),
        (1835, 804, 2281, 953),
        (2290, 804, 2746, 953),
    ]
    ref_h = measure_max_trimmed_h(sheet, mh_boxes)
    for i, b in enumerate(mh_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_SSJ_MultiHit_{i}.png"), 235, ref_h)

    # Row 5: Goku_Kamehameha_0..4
    kame_boxes = [
        (6, 992, 164, 1152),
        (170, 992, 338, 1152),
        (461, 992, 913, 1152),
        (921, 992, 1371, 1152),
        (1380, 992, 2746, 1152),
    ]
    ref_h = measure_max_trimmed_h(sheet, kame_boxes[:3])
    for i, b in enumerate(kame_boxes):
        # Anchor character on left for long rightward beam frames (2,3,4)
        ax = 80 if i >= 2 else None
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_Kamehameha_{i}.png"), 235, ref_h, anchor_left_x=ax)

    # Row 6: Goku_GenkiDama_0..3 & Combat VFX
    gd_boxes = [
        (115, 1192, 405, 1532),
        (530, 1192, 820, 1532),
        (1055, 1192, 1272, 1532),
        (1440, 1192, 1702, 1532),
    ]
    for i, b in enumerate(gd_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Goku_GenkiDama_{i}.png"), 300 if i == 2 else 250, canvas_h=360)

    save_fx_sprite(sheet.crop((1440, 1192, 1702, 1532)), os.path.join(COMBAT_DIR, "FX_GenkiDamaOrb.png"), 320)
    save_fx_sprite(sheet.crop((1836, 1192, 2282, 1532)), os.path.join(COMBAT_DIR, "FX_Explosion_0.png"), 360)
    save_fx_sprite(sheet.crop((2290, 1192, 2746, 1532)), os.path.join(COMBAT_DIR, "FX_Explosion_1.png"), 380)


def process_eren_sheet():
    out_dir = os.path.join(CHAR_DIR, "Eren")
    sheet = Image.open(os.path.join(SRC_DIR, "ErenA.png")).convert("RGBA")

    # Left Row 1: SwordSlash 0..5
    r1_x = [37, 258, 477, 691, 893, 1099, 1302]
    r1_boxes = [(r1_x[i] + 5, 185, r1_x[i + 1] - 5, 400) for i in range(6)]
    ref_h = measure_max_trimmed_h(sheet, r1_boxes)
    for i, b in enumerate(r1_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Eren_SwordSlash_{i}.png"), 235, ref_h)

    # Left Row 2: DashSlash 0..5 + Run + Dash
    r2_x = [37, 259, 492, 703, 899, 1100, 1302]
    r2_boxes = [(r2_x[i] + 5, 504, r2_x[i + 1] - 5, 730) for i in range(6)]
    ref_h = measure_max_trimmed_h(sheet, r2_boxes)
    for i, b in enumerate(r2_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Eren_DashSlash_{i}.png"), 235, ref_h)
    for idx, src_idx in enumerate([1, 3, 0, 4]):
        save_action_frame(sheet.crop(r2_boxes[src_idx]), os.path.join(out_dir, f"Eren_Run_{idx}.png"), 235, ref_h)
    save_action_frame(sheet.crop(r2_boxes[0]), os.path.join(out_dir, "Eren_Dash_0.png"), 235, ref_h)
    save_action_frame(sheet.crop(r2_boxes[2]), os.path.join(out_dir, "Eren_Dash_1.png"), 235, ref_h)

    # Left Row 3: ThunderSpear 0..4 + FX_ThunderSpear
    r3_boxes = [(42, 836, 202, 1058), (212, 836, 355, 1058), (365, 836, 523, 1058), (533, 836, 698, 1058), (708, 836, 868, 1058)]
    ref_h = measure_max_trimmed_h(sheet, r3_boxes)
    for i, b in enumerate(r3_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Eren_ThunderSpear_{i}.png"), 235, ref_h)
    save_fx_sprite(sheet.crop((885, 836, 1296, 1058)), os.path.join(COMBAT_DIR, "FX_ThunderSpear.png"), 260)

    # Left Row 4: SpinSlash 0..5
    r4_x = [37, 195, 360, 581, 847, 1071, 1302]
    r4_boxes = [(r4_x[i] + 5, 1170, r4_x[i + 1] - 5, 1386) for i in range(6)]
    ref_h = measure_max_trimmed_h(sheet, r4_boxes)
    for i, b in enumerate(r4_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Eren_SpinSlash_{i}.png"), 235, ref_h)

    # Right Row 1: Titan GroundSlam 0..5
    tr1_boxes = [(1437, 184, 1653, 425), (1672, 184, 1863, 425), (1881, 184, 2071, 425), (2089, 184, 2280, 425), (2297, 184, 2489, 425), (2507, 184, 2703, 425)]
    ref_h = measure_max_trimmed_h(sheet, tr1_boxes)
    for i, b in enumerate(tr1_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Eren_Titan_GroundSlam_{i}.png"), 255, ref_h)

    # Right Row 2: Titan RockThrow 0..4 + FX_RockImpact
    tr2_boxes = [(1437, 598, 1653, 841), (1672, 598, 1863, 841), (1881, 598, 2071, 841), (2089, 598, 2283, 841), (2293, 598, 2489, 841)]
    ref_h = measure_max_trimmed_h(sheet, tr2_boxes)
    for i, b in enumerate(tr2_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Eren_Titan_RockThrow_{i}.png"), 255, ref_h)
    save_fx_sprite(sheet.crop((2505, 598, 2703, 792)), os.path.join(COMBAT_DIR, "FX_RockImpact.png"), 240)

    # Right Row 3: Titan Run & Stomp 0..3 + FX_TitanFoot
    tr3_boxes = [(1434, 1034, 1653, 1355), (1672, 1034, 1863, 1355), (1881, 1034, 2075, 1355), (2085, 1034, 2283, 1355)]
    ref_h = measure_max_trimmed_h(sheet, tr3_boxes)
    for i, b in enumerate(tr3_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Eren_Titan_Run_{i}.png"), 255, ref_h)
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Eren_Titan_Stomp_{i}.png"), 255, ref_h)
    save_fx_sprite(sheet.crop((2293, 1034, 2712, 1355)), os.path.join(COMBAT_DIR, "FX_TitanFoot.png"), 420)


def process_diend_sheet():
    out_dir = os.path.join(CHAR_DIR, "Diend")
    sheet = Image.open(os.path.join(SRC_DIR, "DiendA.png")).convert("RGBA")
    sheet = mask_white(
        sheet,
        [
            (480, 105, 890, 178),     # "Ban sung J"
            (340, 1300, 560, 1365),   # "Xuyen thau"
            (1910, 232, 2240, 308),   # "LordKivaAI.cs" tag
        ],
    )

    # Left Row 1: GunShoot 0..3 (crop only Diend without target stickman on far right)
    gun_boxes = [(88, 178, 280, 492), (325, 178, 528, 492), (573, 178, 818, 492), (832, 178, 1085, 492)]
    ref_h = measure_max_trimmed_h(sheet, gun_boxes)
    for i, b in enumerate(gun_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Diend_GunShoot_{i}.png"), 240, ref_h)

    # Left Row 2: PhantomSlash 0..2 + Run
    ph_boxes = [(178, 615, 348, 942), (490, 615, 850, 942), (948, 615, 1198, 942)]
    ref_h = measure_max_trimmed_h(sheet, ph_boxes)
    for i, b in enumerate(ph_boxes):
        save_action_frame(sheet.crop(b), os.path.join(out_dir, f"Diend_PhantomSlash_{i}.png"), 240, ref_h)
    save_fx_sprite(sheet.crop(ph_boxes[1]), os.path.join(COMBAT_DIR, "FX_PhantomKivaSlash.png"), 300)
    save_action_frame(sheet.crop(ph_boxes[2]), os.path.join(out_dir, "Diend_Run_0.png"), 240, ref_h)
    save_action_frame(sheet.crop(ph_boxes[0]), os.path.join(out_dir, "Diend_Run_1.png"), 240, ref_h)

    # Left Row 3: CardShot + FanShot + FX_DiendCard
    save_action_frame(sheet.crop((56, 1106, 330, 1442)), os.path.join(out_dir, "Diend_CardShot_0.png"), 240)
    save_fx_sprite(sheet.crop((355, 1175, 615, 1298)), os.path.join(COMBAT_DIR, "FX_DiendCard.png"), 180)
    save_action_frame(sheet.crop((688, 1106, 1335, 1442)), os.path.join(out_dir, "Diend_FanShot_0.png"), 240, anchor_left_x=60)

    # Right: LordKiva_Attack_0, Diend_Dash_0, LordKiva_WingFreeze_0 + FX
    save_action_frame(sheet.crop((1778, 290, 2220, 482)), os.path.join(out_dir, "LordKiva_Attack_0.png"), 230, anchor_left_x=80)
    save_action_frame(sheet.crop((1466, 758, 1818, 1170)), os.path.join(out_dir, "Diend_Dash_0.png"), 215)
    save_action_frame(sheet.crop((1888, 758, 2322, 1170)), os.path.join(out_dir, "LordKiva_WingFreeze_0.png"), 270, canvas_h=340)
    save_fx_sprite(sheet.crop((1888, 758, 2322, 1170)), os.path.join(COMBAT_DIR, "FX_DarkKivaFreezeBurst.png"), 360)


def draw_stickman_enemy(
    out_path: str,
    body_color: tuple[int, int, int, int],
    accent_color: tuple[int, int, int, int],
    scale: float = 1.0,
    style: str = "grunt",
):
    w, h = int(256 * scale), int(300 * scale)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = w // 2
    s = scale
    lw = max(6, int(14 * s))

    # Ground shadow
    d.ellipse([cx - int(44 * s), h - int(22 * s), cx + int(44 * s), h - int(6 * s)], fill=(0, 0, 0, 90))

    # Head
    hr = int(26 * s)
    hy = int(72 * s)
    d.ellipse([cx - hr, hy - hr, cx + hr, hy + hr], fill=body_color)

    # Torso
    ty0, ty1 = hy + hr - 2, int(185 * s)
    d.line([(cx, ty0), (cx, ty1)], fill=body_color, width=lw)

    # Legs (fighting stance, facing left toward player by default or symmetrical; BaseEnemy flips scale.x)
    d.line([(cx, ty1), (cx - int(36 * s), h - int(18 * s))], fill=body_color, width=lw)
    d.line([(cx, ty1), (cx + int(38 * s), h - int(18 * s))], fill=body_color, width=lw)

    # Arms
    sh_y = ty0 + int(18 * s)
    if style == "ranger":
        # Aiming rifle to the right (facingDirection=1 uses scale.x>0, BaseEnemy flips -1 for left)
        d.line([(cx, sh_y), (cx + int(52 * s), sh_y + int(6 * s))], fill=body_color, width=lw)
        d.line([(cx, sh_y + int(10 * s)), (cx + int(35 * s), sh_y + int(12 * s))], fill=body_color, width=max(5, lw - 2))
        d.rectangle([cx + int(22 * s), sh_y - int(4 * s), cx + int(82 * s), sh_y + int(10 * s)], fill=accent_color)
        # Visor
        d.rectangle([cx + int(4 * s), hy - int(8 * s), cx + hr + int(2 * s), hy + int(4 * s)], fill=accent_color)
    elif style == "kamikaze":
        # Raised arms + glowing bomb core on chest
        d.line([(cx, sh_y), (cx - int(42 * s), sh_y - int(28 * s))], fill=body_color, width=lw)
        d.line([(cx, sh_y), (cx + int(42 * s), sh_y - int(28 * s))], fill=body_color, width=lw)
        br = int(20 * s)
        d.ellipse([cx - br, sh_y + int(16 * s) - br, cx + br, sh_y + int(16 * s) + br], fill=accent_color, outline=(255, 255, 160, 255), width=3)
    elif style == "elite":
        # Heavy shoulder pads + menacing eyes + big fists
        d.ellipse([cx - int(42 * s), sh_y - int(14 * s), cx + int(42 * s), sh_y + int(18 * s)], fill=accent_color)
        d.line([(cx - int(28 * s), sh_y + int(8 * s)), (cx - int(55 * s), sh_y + int(45 * s))], fill=body_color, width=lw + 4)
        d.line([(cx + int(28 * s), sh_y + int(8 * s)), (cx + int(58 * s), sh_y + int(38 * s))], fill=body_color, width=lw + 4)
        d.ellipse([cx + int(6 * s), hy - int(6 * s), cx + int(18 * s), hy + int(4 * s)], fill=(255, 230, 80, 255))
    elif style == "boss":
        # Giant horns + flowing dark cape + glowing crimson eyes
        d.polygon([(cx - int(24 * s), ty0), (cx - int(72 * s), ty1 + int(25 * s)), (cx + int(10 * s), ty1 + int(10 * s))], fill=(110, 10, 20, 220))
        d.polygon([(cx - int(14 * s), hy - hr + 4), (cx - int(40 * s), hy - hr - int(36 * s)), (cx - int(4 * s), hy - hr + 6)], fill=accent_color)
        d.polygon([(cx + int(14 * s), hy - hr + 4), (cx + int(40 * s), hy - hr - int(36 * s)), (cx + int(4 * s), hy - hr + 6)], fill=accent_color)
        d.line([(cx, sh_y), (cx - int(52 * s), sh_y + int(34 * s))], fill=body_color, width=lw + 4)
        d.line([(cx, sh_y), (cx + int(58 * s), sh_y + int(20 * s))], fill=body_color, width=lw + 4)
        d.ellipse([cx + int(6 * s), hy - int(8 * s), cx + int(20 * s), hy + int(4 * s)], fill=(255, 60, 60, 255))
    else:
        # Grunt brawler with headband
        d.line([(cx, sh_y), (cx - int(38 * s), sh_y + int(28 * s))], fill=body_color, width=lw)
        d.line([(cx, sh_y), (cx + int(48 * s), sh_y + int(10 * s))], fill=body_color, width=lw)
        d.rectangle([cx - hr - 2, hy - int(14 * s), cx + hr + 2, hy - int(4 * s)], fill=accent_color)

    img.save(out_path)


def generate_procedural_enemies_and_fx():
    draw_stickman_enemy(os.path.join(ENEMY_DIR, "Enemy_Grunt.png"), (30, 20, 20, 255), (220, 50, 50, 255), 1.0, "grunt")
    draw_stickman_enemy(os.path.join(ENEMY_DIR, "Enemy_Ranger.png"), (35, 35, 45, 255), (245, 195, 40, 255), 1.0, "ranger")
    draw_stickman_enemy(os.path.join(ENEMY_DIR, "Enemy_Kamikaze.png"), (40, 25, 15, 255), (255, 120, 20, 255), 0.95, "kamikaze")
    draw_stickman_enemy(os.path.join(ENEMY_DIR, "Enemy_Elite.png"), (35, 20, 50, 255), (150, 60, 220, 255), 1.2, "elite")
    draw_stickman_enemy(os.path.join(ENEMY_DIR, "Enemy_Boss.png"), (20, 10, 15, 255), (235, 30, 50, 255), 1.35, "boss")

    # FX_KiOrb.png
    orb = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    od = ImageDraw.Draw(orb)
    od.ellipse([8, 8, 120, 120], fill=(40, 200, 255, 110))
    od.ellipse([22, 22, 106, 106], fill=(120, 235, 255, 210))
    od.ellipse([38, 38, 90, 90], fill=(245, 255, 255, 255))
    orb.save(os.path.join(COMBAT_DIR, "FX_KiOrb.png"))

    # FX_Shockwave.png
    sw = Image.new("RGBA", (192, 192), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sw)
    sd.polygon([(16, 176), (60, 56), (92, 120), (144, 24), (176, 176)], fill=(255, 165, 50, 235), outline=(255, 240, 160, 255))
    sw.save(os.path.join(COMBAT_DIR, "FX_Shockwave.png"))

    # FX_WarningCircle.png
    wc = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    wd = ImageDraw.Draw(wc)
    wd.ellipse([12, 12, 244, 244], fill=(255, 30, 30, 75), outline=(255, 50, 50, 230), width=10)
    wd.ellipse([70, 70, 186, 186], outline=(255, 140, 140, 200), width=5)
    wc.save(os.path.join(COMBAT_DIR, "FX_WarningCircle.png"))


if __name__ == "__main__":
    ensure_dirs()
    process_portraits()
    process_goku_sheet()
    process_eren_sheet()
    process_diend_sheet()
    generate_procedural_enemies_and_fx()
    print("Sprite slicing & normalization complete!")
