import math
import os
from collections import deque
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DIR = os.path.join(ROOT, "Assets", "Sprites", "SourceSheets")
UI_DIR = os.path.join(ROOT, "Assets", "Sprites", "UI")
CHAR_DIR = os.path.join(ROOT, "Assets", "Sprites", "Characters")
COMBAT_DIR = os.path.join(ROOT, "Assets", "Sprites", "Combat")


def ensure_dirs():
    for d in [
        UI_DIR,
        os.path.join(CHAR_DIR, "Goku"),
        os.path.join(CHAR_DIR, "Eren"),
        os.path.join(CHAR_DIR, "Diend"),
        COMBAT_DIR,
    ]:
        os.makedirs(d, exist_ok=True)


def remove_white_bg(img: Image.Image) -> Image.Image:
    """Border flood-fill background removal preserving interior highlights."""
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    rgb = arr[:, :, :3].astype(np.int16)
    min_c = rgb.min(axis=2)
    max_c = rgb.max(axis=2)
    is_white = (min_c >= 234) & ((max_c - min_c) <= 24)

    visited = np.zeros((h, w), dtype=bool)
    q = deque()

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

    arr[visited, 3] = 0

    adj = np.zeros((h, w), dtype=bool)
    adj[1:, :] |= visited[:-1, :]
    adj[:-1, :] |= visited[1:, :]
    adj[:, 1:] |= visited[:, :-1]
    adj[:, :-1] |= visited[:, 1:]
    fringe = adj & (~visited) & (min_c > 188) & ((max_c - min_c) < 32)
    if np.any(fringe):
        mean_rgb = rgb.mean(axis=2)
        new_alpha = np.clip((250.0 - mean_rgb) * 4.0, 0, 255).astype(np.uint8)
        arr[fringe, 3] = new_alpha[fringe]
        a_f = np.maximum(new_alpha[fringe].astype(np.float32) / 255.0, 0.08)
        for c in range(3):
            chan = arr[fringe, c].astype(np.float32)
            unpremul = (chan - 255.0 * (1.0 - a_f)) / a_f
            arr[fringe, c] = np.clip(unpremul, 0, 255).astype(np.uint8)

    return Image.fromarray(arr, mode="RGBA")


def trim_alpha(img: Image.Image) -> Image.Image:
    bbox = img.getbbox()
    return img if bbox is None else img.crop(bbox)


def strip_thin_grid_lines(img: Image.Image, edge_margin: int = 24) -> Image.Image:
    """Removes stray 1-3px dark border lines along left/right/top/bottom edges of storyboard crops."""
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    opaque = arr[:, :, 3] > 60
    dark = opaque & (arr[:, :, :3].mean(axis=2) < 215)

    # Vertical lines spanning >= 42% height near left/right edge
    col_sums = dark.sum(axis=0)
    for x in list(range(min(edge_margin, w))) + list(range(max(0, w - edge_margin), w)):
        if col_sums[x] > h * 0.42:
            arr[:, max(0, x - 1) : min(w, x + 2), 3] = 0

    # Horizontal baseline spanning >= 45% width near bottom or top edge
    row_sums = dark.sum(axis=1)
    for y in list(range(min(12, h))) + list(range(max(0, h - edge_margin), h)):
        if row_sums[y] > w * 0.45:
            arr[max(0, y - 1) : min(h, y + 2), :, 3] = 0

    return Image.fromarray(arr, mode="RGBA")


def mask_white(img: Image.Image, rects: list[tuple[int, int, int, int]]) -> Image.Image:
    copy = img.copy()
    draw = ImageDraw.Draw(copy)
    for r in rects:
        draw.rectangle(r, fill=(255, 255, 255, 255))
    return copy


def save_portrait(crop: Image.Image, out_path: str, canvas_size=(512, 768)):
    clean = trim_alpha(remove_white_bg(crop))
    cw, ch = canvas_size
    sw, sh = clean.size
    if sw == 0 or sh == 0:
        clean.save(out_path)
        return
    scale = min(460.0 / sw, 700.0 / sh)
    nw, nh = max(1, int(round(sw * scale))), max(1, int(round(sh * scale)))
    resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    canvas.paste(resized, ((cw - nw) // 2, ch - 24 - nh), resized)
    canvas.save(out_path)


def save_fx_sprite(crop: Image.Image, out_path: str, max_dim: int = 320):
    clean = trim_alpha(remove_white_bg(crop))
    sw, sh = clean.size
    if sw == 0 or sh == 0:
        clean.save(out_path)
        return
    scale = min(max_dim / float(max(sw, sh)), 1.5)
    nw, nh = max(1, int(round(sw * scale))), max(1, int(round(sh * scale)))
    resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)
    cw, ch = max(nw + 16, 64), max(nh + 16, 64)
    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    canvas.paste(resized, ((cw - nw) // 2, (ch - nh) // 2), resized)
    canvas.save(out_path)


def finalize_char_frame(img: Image.Image, target_foot_y: int = 283) -> Image.Image:
    """Enforces strictly 300x300 RGBA, top-left pixel (0, 0) alpha == 0, and grounded foot at target_foot_y."""
    w, h = img.size
    if w == 300 and h == 300:
        arr = np.array(img, dtype=np.uint8)
        arr[0, 0, 3] = 0
        op = arr[:, :, 3] > 60
        if np.any(op):
            max_y = int(np.where(op)[0].max())
            if abs(max_y - target_foot_y) > 2 and max_y < 298:
                dy = target_foot_y - max_y
                shifted = np.zeros_like(arr)
                if dy > 0:
                    shifted[dy:, :, :] = arr[:-dy, :, :]
                else:
                    shifted[:dy, :, :] = arr[-dy:, :, :]
                shifted[0, 0, 3] = 0
                return Image.fromarray(shifted, mode="RGBA")
        return Image.fromarray(arr, mode="RGBA")

    bbox = img.getbbox()
    if bbox is None:
        return Image.new("RGBA", (300, 300), (0, 0, 0, 0))
    clean = img.crop(bbox)
    cw, ch = clean.size
    max_w, max_h = 284, 276
    s = min(1.0, max_w / float(cw), max_h / float(ch))
    if s < 0.999:
        nw = max(1, int(round(cw * s)))
        nh = max(1, int(round(ch * s)))
        clean = clean.resize((nw, nh), Image.Resampling.LANCZOS)
    else:
        nw, nh = cw, ch

    canvas = Image.new("RGBA", (300, 300), (0, 0, 0, 0))
    px = max(2, min(300 - nw - 2, (300 - nw) // 2))
    py = max(2, min(300 - nh, target_foot_y - nh + 1))
    canvas.paste(clean, (px, py), clean)

    arr = np.array(canvas, dtype=np.uint8)
    arr[0, 0, 3] = 0
    return Image.fromarray(arr, mode="RGBA")


def normalize_to_canvas(
    img: Image.Image,
    target_body_h: int = 235,
    max_crop_ref_h: int | None = None,
    canvas_h: int = 300,
    anchor_left_x: int | None = None,
    pad_x: int = 44,
    bottom_pad: int = 16,
) -> Image.Image:
    clean = trim_alpha(img)
    sw, sh = clean.size
    if sw == 0 or sh == 0:
        return Image.new("RGBA", (300, canvas_h), (0, 0, 0, 0))
    ref_h = max_crop_ref_h if (max_crop_ref_h and max_crop_ref_h > 0) else sh
    max_w = 284
    max_h = 276
    scale = min(target_body_h / float(ref_h), max_h / float(sh), max_w / float(sw), 2.5)
    nw, nh = max(1, int(round(sw * scale))), max(1, int(round(sh * scale)))
    resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)
    cw = 300
    if anchor_left_x is not None:
        px = max(2, min(300 - nw - 2, anchor_left_x))
    else:
        px = max(2, min(300 - nw - 2, (300 - nw) // 2))
    py = max(2, canvas_h - bottom_pad - nh)
    canvas = Image.new("RGBA", (300, canvas_h), (0, 0, 0, 0))
    canvas.paste(resized, (px, py), resized)
    return canvas


def finalize_frame(canvas: Image.Image, out_path: str, bottom_pad: int = 16):
    """If additions extended above y=0, keep consistent canvas & foot anchor."""
    final = finalize_char_frame(canvas, target_foot_y=283)
    final.save(out_path)


def detect_head_anchor(img: Image.Image, prefer_side: str = "auto") -> tuple[int, int, int]:
    """
    Finds the dark stickman head (cx, cy, radius) in upper half of frame.
    Works reliably on silhouettes, red/blue FX frames, and tilted poses.
    """
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    a = arr[:, :, 3] > 110
    rgb = arr[:, :, :3].astype(np.int16)
    lum = rgb.mean(axis=2)
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    # Neutral dark stickman body mask (ignores colorful red/blue/gold FX trails)
    dark = a & (lum < 65) & (sat < 55)
    if dark.sum() < 120:
        dark = a & (lum < 120)
    ys, xs = np.where(dark)
    if len(ys) == 0:
        return w // 2, h // 4, 24

    y_min, y_max = int(ys.min()), int(ys.max())
    body_h = max(40, y_max - y_min)
    # Restrict search to top 38% of dark body
    top_cut = y_min + int(body_h * 0.36)
    head_mask = dark & (np.arange(h)[:, None] <= top_cut)
    hy, hx = np.where(head_mask)
    if len(hx) == 0:
        return int(xs.mean()), y_min + 22, 22

    # Estimate head center weighted slightly below topmost pixel
    top_band = dark & (np.arange(h)[:, None] <= y_min + int(body_h * 0.22))
    ty, tx = np.where(top_band)
    if len(tx) > 20:
        # Remove isolated extreme horizontal FX speckles by percentile
        cx = int(np.median(tx))
        sub_x = tx[np.abs(tx - cx) < body_h * 0.24]
        sub_y = ty[np.abs(tx - cx) < body_h * 0.24]
        if len(sub_x) > 15:
            cx = int(np.mean(sub_x))
            cy = int(np.min(sub_y) + body_h * 0.095)
            r = max(16, int(body_h * 0.105))
            return cx, cy, r
    return int(np.median(hx)), int(y_min + body_h * 0.10), max(16, int(body_h * 0.105))


def detect_torso_and_hands(img: Image.Image) -> dict:
    """Detects dark silhouette bounding geometry, shoulders, waist, and extended hands."""
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    a = arr[:, :, 3] > 140
    rgb = arr[:, :, :3].astype(np.int16)
    lum = rgb.mean(axis=2)
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    # Strict neutral dark silhouette -> keep ONLY the largest connected body component (ignores detached slash outlines!)
    dark = a & (lum < 65) & (sat < 55)
    if dark.sum() < 100:
        dark = (arr[:, :, 3] > 90) & (lum < 130)
    from scipy.ndimage import label as cc_label
    lbl, nlbl = cc_label(dark)
    if nlbl > 1:
        counts = np.bincount(lbl.ravel())
        counts[0] = 0
        main_id = int(np.argmax(counts))
        dark = lbl == main_id
    ys, xs = np.where(dark)
    if len(ys) == 0:
        return {
            "head": (w // 2, h // 3, 24),
            "neck": (w // 2, h // 3 + 22),
            "waist": (w // 2, h // 2 + 25),
            "front_hand": (w // 2 + 48, h // 2),
            "back_hand": (w // 2 - 42, h // 2 + 10),
            "body_h": 210,
            "dark_mask": dark,
        }

    cx, cy, hr = detect_head_anchor(img)
    y_min, y_max = int(ys.min()), int(ys.max())
    body_h = max(80, y_max - y_min)
    neck_y = int(cy + hr + 3)
    waist_y = int(cy + body_h * 0.44)

    # Search arm zone strictly above hip/legs (neck_y .. waist_y - 4) so swords never snap to legs!
    arm_y0 = max(0, neck_y - 2)
    arm_y1 = min(h, max(neck_y + 16, waist_y - 4))
    arm_band = dark[arm_y0:arm_y1, :]
    ay, ax = np.where(arm_band)

    if len(ax) > 0:
        right_idx = int(np.argmax(ax))
        left_idx = int(np.argmin(ax))
        front_hand = (int(ax[right_idx]), int(ay[right_idx] + arm_y0))
        back_hand = (int(ax[left_idx]), int(ay[left_idx] + arm_y0))
        # Clamp unrealistic distances (e.g., if beam/FX dark pixel reached far edge)
        if abs(front_hand[0] - cx) > body_h * 0.52:
            near = ax < (cx + body_h * 0.48)
            if np.any(near):
                idx = int(np.argmax(ax[near]))
                front_hand = (int(ax[near][idx]), int(ay[near][idx] + arm_y0))
            else:
                front_hand = (cx + int(body_h * 0.32), neck_y + 14)
        if abs(cx - back_hand[0]) > body_h * 0.48:
            near = ax > (cx - body_h * 0.44)
            if np.any(near):
                idx = int(np.argmin(ax[near]))
                back_hand = (int(ax[near][idx]), int(ay[near][idx] + arm_y0))
            else:
                back_hand = (cx - int(body_h * 0.28), neck_y + 18)
    else:
        front_hand = (cx + int(body_h * 0.30), neck_y + 12)
        back_hand = (cx - int(body_h * 0.25), neck_y + 18)

    # Estimate spine tilt cx at waist
    w_band = dark[max(0, waist_y - 12) : min(h, waist_y + 12), :]
    _, wx = np.where(w_band)
    waist_x = int(np.median(wx)) if len(wx) > 0 else cx

    return {
        "head": (cx, cy, hr),
        "neck": (cx, neck_y),
        "waist": (waist_x, waist_y),
        "front_hand": front_hand,
        "back_hand": back_hand,
        "body_h": body_h,
        "dark_mask": dark,
    }


def expand_canvas_and_paste(base: Image.Image, overlay: Image.Image, dest_xy: tuple[int, int], behind: bool = False) -> Image.Image:
    """Composites overlay onto base at dest_xy without clipping top/left/right margins, keeping bottom foot line locked."""
    bw, bh = base.size
    ow, oh = overlay.size
    dx, dy = dest_xy
    pad_l = max(0, -dx)
    pad_t = max(0, -dy)
    pad_r = max(0, dx + ow - bw)
    # Keep bottom row locked! Only expand top/left/right if strictly needed
    if pad_l or pad_t or pad_r:
        nw, nh = bw + pad_l + pad_r, bh + pad_t
        new_base = Image.new("RGBA", (nw, nh), (0, 0, 0, 0))
        new_base.paste(base, (pad_l, pad_t), base)
        ov_layer = Image.new("RGBA", (nw, nh), (0, 0, 0, 0))
        ov_layer.paste(overlay, (dx + pad_l, dy + pad_t), overlay)
        return Image.alpha_composite(ov_layer, new_base) if behind else Image.alpha_composite(new_base, ov_layer)

    ov_layer = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
    ov_layer.paste(overlay, (dx, dy), overlay)
    return Image.alpha_composite(ov_layer, base) if behind else Image.alpha_composite(base, ov_layer)


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


# ---------------------------------------------------------------------------
# Feature Template Extractors from *_Sprite.png
# ---------------------------------------------------------------------------
class FeatureTemplates:
    def __init__(self):
        # 1. Goku Base Black Hair
        goku_sp = remove_white_bg(Image.open(os.path.join(SRC_DIR, "Goku_Sprite.png")).convert("RGBA"))
        # Base Goku head + spiky hair region: (380, 115, 955, 510) -> head circle centered ~ (682, 412), r ~ 115
        g_head = goku_sp.crop((380, 115, 955, 510))
        self.goku_black_hair = trim_alpha(g_head)
        # Relative head center inside trimmed crop:
        bb = g_head.getbbox()
        self.goku_black_hair_head_rel = ((682 - 380 - bb[0]) / float(self.goku_black_hair.width),
                                         (415 - 115 - bb[1]) / float(self.goku_black_hair.height),
                                         105.0 / float(self.goku_black_hair.height))

        # 2. Goku SSJ Golden Hair -> Build crisp 4x SSAA anime spiky SSJ golden hair (zero background aura box!)
        hw, hh = 320, 260
        hc_x, hc_y = 156, 194
        hi = Image.new("RGBA", (hw * 2, hh * 2), (0, 0, 0, 0))
        hd = ImageDraw.Draw(hi)
        # Main upward spiky golden SSJ locks + side locks + front bangs
        ssj_poly = [
            (182, 396), (138, 360), (96, 376), (132, 314), (66, 268), (144, 268),
            (92, 168), (192, 206), (206, 62), (288, 172), (318, 22), (376, 174),
            (464, 68), (434, 214), (540, 172), (470, 276), (552, 290), (474, 344),
            (510, 398), (436, 376), (404, 422), (366, 370), (312, 428), (272, 376),
            (218, 412),
        ]
        hd.polygon(ssj_poly, fill=(255, 224, 48, 255), outline=(18, 18, 20, 255))
        # Re-draw thick crisp ink outline
        hd.line(ssj_poly + [ssj_poly[0]], fill=(18, 18, 20, 255), width=9, joint="curve")
        # Inner bright highlight spikes
        hi_poly = [
            (210, 318), (168, 208), (236, 240), (244, 128), (298, 210),
            (324, 92), (366, 212), (436, 128), (408, 246), (484, 222),
            (422, 314), (356, 336), (284, 336),
        ]
        hd.polygon(hi_poly, fill=(255, 248, 150, 235))
        ssj_clean = trim_alpha(hi.resize((hw, hh), Image.Resampling.LANCZOS))
        bb_ssj = hi.getbbox()
        self.goku_ssj_hair = ssj_clean
        self.goku_ssj_hair_head_rel = ((hc_x * 2 - bb_ssj[0]) / float(bb_ssj[2] - bb_ssj[0]),
                                       (hc_y * 2 - bb_ssj[1]) / float(bb_ssj[3] - bb_ssj[1]),
                                       92.0 / float(bb_ssj[3] - bb_ssj[1]))

        # 3. Eren Hair + Titan Head
        eren_sp = remove_white_bg(Image.open(os.path.join(SRC_DIR, "Eren_Sprite.png")).convert("RGBA"))
        e_head = eren_sp.crop((535, 200, 835, 515))
        bb_e = e_head.getbbox()
        self.eren_hair = e_head.crop(bb_e)
        self.eren_hair_head_rel = ((678 - 535 - bb_e[0]) / float(self.eren_hair.width),
                                   (412 - 200 - bb_e[1]) / float(self.eren_hair.height),
                                   102.0 / float(self.eren_hair.height))

        et_head = eren_sp.crop((1880, 150, 2300, 520))
        bb_et = et_head.getbbox()
        raw_titan_head = et_head.crop(bb_et)
        self.eren_titan_head = feather_titan_head(raw_titan_head, feather_px=24)
        self.eren_titan_head_rel = ((2095 - 1880 - bb_et[0]) / float(self.eren_titan_head.width),
                                    (365 - 150 - bb_et[1]) / float(self.eren_titan_head.height),
                                    108.0 / float(self.eren_titan_head.height))

        # 4. Diend Barcode Helmet & DiendDriver Gun
        diend_sp = remove_white_bg(Image.open(os.path.join(SRC_DIR, "Diend_Sprite.png")).convert("RGBA"))
        d_head = diend_sp.crop((515, 110, 845, 505))
        bb_dh = d_head.getbbox()
        d_helmet_crop = d_head.crop(bb_dh)
        d_arr = np.array(d_helmet_crop).copy()
        # Overlay vibrant magenta Rider optics behind barcode card slits
        for y in range(248, min(d_arr.shape[0], 275)):
            for x in range(133, min(d_arr.shape[1], 153)):
                if d_arr[y, x, 3] > 60 and (d_arr[y, x, 0] > 15 or d_arr[y, x, 1] > 25):
                    d_arr[y, x, :3] = [232, 26, 124]
            for x in range(172, min(d_arr.shape[1], 192)):
                if d_arr[y, x, 3] > 60 and (d_arr[y, x, 0] > 15 or d_arr[y, x, 1] > 25):
                    d_arr[y, x, :3] = [232, 26, 124]
        self.diend_helmet = Image.fromarray(d_arr, mode="RGBA")
        self.diend_helmet_head_rel = ((677 - 515 - bb_dh[0]) / float(self.diend_helmet.width),
                                      (385 - 110 - bb_dh[1]) / float(self.diend_helmet.height),
                                      98.0 / float(self.diend_helmet.height))

        # DiendDriver Gun oriented horizontally pointing RIGHT (for easy rotation along aiming angle)
        d_gun = diend_sp.crop((200, 710, 540, 1080))
        d_gun_clean = trim_alpha(d_gun)
        # In Diend_Sprite.png, gun muzzle points down-left (~230 deg clockwise -> rotate +135 deg to point +X right!)
        self.diend_gun_right = trim_alpha(d_gun_clean.rotate(138, resample=Image.Resampling.BICUBIC, expand=True))


def apply_head_template(
    canvas: Image.Image,
    tmpl: Image.Image,
    head_rel: tuple[float, float, float],
    head_xyr: tuple[int, int, int],
    scale_mul: float = 1.0,
    angle_deg: float = 0.0,
    offset_xy: tuple[int, int] = (0, 0),
    behind: bool = False,
) -> Image.Image:
    cx, cy, hr = head_xyr
    rx, ry, r_ratio = head_rel
    target_h = max(24, int(round((hr / max(0.1, r_ratio)) * 1.15 * scale_mul)))
    scale = target_h / float(tmpl.height)
    tw = max(12, int(round(tmpl.width * scale)))
    th = max(12, int(round(tmpl.height * scale)))
    scaled = tmpl.resize((tw, th), Image.Resampling.LANCZOS)
    if abs(angle_deg) > 1.0:
        scaled = scaled.rotate(angle_deg, resample=Image.Resampling.BICUBIC, expand=True)
        dx = cx - scaled.width // 2 + offset_xy[0]
        dy = cy - int(scaled.height * ry) + offset_xy[1]
    else:
        dx = cx - int(round(tw * rx)) + offset_xy[0]
        dy = cy - int(round(th * ry)) + offset_xy[1]
    return expand_canvas_and_paste(canvas, scaled, (dx, dy), behind=behind)


def add_aura_glow(
    img: Image.Image,
    aura_type: str = "ssj",
    intensity: float = 1.0,
) -> Image.Image:
    """
    Renders a crisp, dynamic anime energy flame aura (golden SSJ or crimson Kaioken)
    hugging the character silhouette without obscuring the stickman interior.
    """
    base = img.convert("RGBA")
    arr = np.array(base, dtype=np.uint8)
    h, w, _ = arr.shape
    pad = 48
    cw, ch = w + pad * 2, h + pad  # keep bottom locked!

    padded = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    padded.paste(base, (pad, pad), base)
    parr = np.array(padded, dtype=np.uint8)

    body_lum = parr[:, :, :3].mean(axis=2)
    silhouette = (parr[:, :, 3] > 80) & (body_lum < 145)
    if not np.any(silhouette):
        silhouette = parr[:, :, 3] > 80

    sil_img = Image.fromarray((silhouette.astype(np.uint8) * 255), mode="L")
    # Stretch silhouette slightly upward for flame spikes
    from scipy.ndimage import binary_dilation
    outer_mask = binary_dilation(silhouette, iterations=max(6, int(11 * intensity)))
    # Shift flame aura upward by subtracting down-shifted silhouette + adding jagged upward licks
    up_mask = np.zeros_like(outer_mask)
    up_mask[:-8, :] = outer_mask[8:, :]
    aura_shape = outer_mask | up_mask

    aura_layer = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    draw = ImageDraw.Draw(aura_layer)

    ys, xs = np.where(silhouette)
    if len(ys) > 0:
        bx0, bx1 = int(xs.min()), int(xs.max())
        by0, by1 = int(ys.min()), int(ys.max())
        bcx = (bx0 + bx1) // 2

        if aura_type == "kaioken":
            outer_col = (225, 25, 35, 130)
            mid_col = (255, 65, 50, 175)
            core_col = (255, 195, 165, 210)
        else:  # ssj
            outer_col = (255, 175, 15, 135)
            mid_col = (255, 225, 45, 180)
            core_col = (255, 250, 175, 215)

        # Build blurry glow from dilated silhouette
        glow_arr = np.zeros((ch, cw, 4), dtype=np.uint8)
        glow_arr[aura_shape] = outer_col
        mid_mask = binary_dilation(silhouette, iterations=max(3, int(6 * intensity)))
        glow_arr[mid_mask] = mid_col
        inner_mask = binary_dilation(silhouette, iterations=2)
        glow_arr[inner_mask] = core_col

        aura_layer = Image.fromarray(glow_arr, mode="RGBA").filter(ImageFilter.GaussianBlur(radius=4.5))
        # Paint spiky upward flame tongues rising from torso/shoulders without any flat bottom bar
        fd = ImageDraw.Draw(aura_layer)
        flame_col = (255, 80, 60, 140) if aura_type == "kaioken" else (255, 215, 35, 145)
        flame_in = (255, 180, 150, 165) if aura_type == "kaioken" else (255, 248, 170, 175)
        body_span_h = max(60, by1 - by0)
        py_base = by0 + int(body_span_h * 0.58)
        for offset_x, tip_dy, half_w in [(-38, -24, 12), (-16, -38, 14), (0, -48, 16), (18, -36, 14), (38, -22, 12)]:
            px = bcx + offset_x
            fd.polygon([(px - half_w, py_base), (px, by0 + tip_dy), (px + half_w, py_base)], fill=flame_col)
            fd.polygon([(px - half_w // 2, py_base - 8), (px, by0 + tip_dy + 12), (px + half_w // 2, py_base - 8)], fill=flame_in)

        aura_layer = aura_layer.filter(ImageFilter.GaussianBlur(radius=3.5))

    comp = Image.alpha_composite(aura_layer, padded)
    out = comp.crop((pad, pad, pad + w, pad + h))
    arr_out = np.array(out, dtype=np.uint8)
    arr_out[0, 0, 3] = 0
    arr_out[284:, :, 3] = 0
    return Image.fromarray(arr_out, mode="RGBA")


def paint_eren_cloak_and_blades(img: Image.Image, dual_blades: bool = True, add_spear: bool = False) -> Image.Image:
    """
    Overlays Eren's Survey Corps dark-green scout cloak/scarf onto neck/shoulders
    and places crisp dual ODM blades in both hands!
    """
    info = detect_torso_and_hands(img)
    cx, cy, hr = info["head"]
    nx, ny = info["neck"]
    wx, wy = info["waist"]
    fh = info["front_hand"]
    bh = info["back_hand"]
    bh_px = info["body_h"]

    w, h = img.size
    cloak = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(cloak)

    # Survey Corps green mantle (fitted high collar + slim cropped scout lapels + sleek fluttering cloak tail)
    mantle_dark = (22, 54, 34, 245)
    mantle_mid = (38, 88, 54, 248)
    mantle_hi = (66, 128, 86, 240)
    strap_tan = (158, 134, 96, 245)
    cape_w = max(14, int(bh_px * 0.085))
    jacket_h = max(18, int(bh_px * 0.115))

    # Slim fluttering cloak tail trailing slightly behind shoulders/upper back (never a boxy tent)
    cape_pts = [
        (nx - cape_w, ny - 2),
        (nx + cape_w // 2, ny - 1),
        (nx + cape_w // 3, ny + jacket_h + 10),
        (nx - cape_w - 14, ny + jacket_h + 16),
    ]
    d.polygon(cape_pts, fill=mantle_dark, outline=(12, 26, 16, 255))

    # Fitted cropped Survey Corps shoulder mantle & high collar
    sh_pts = [
        (nx - cape_w - 2, ny - 4),
        (nx + cape_w + 2, ny - 3),
        (nx + cape_w, ny + jacket_h),
        (nx - cape_w, ny + jacket_h),
    ]
    d.polygon(sh_pts, fill=mantle_mid, outline=(14, 32, 20, 255))
    d.polygon([
        (nx - cape_w // 2, ny - 3),
        (nx + cape_w // 2, ny - 3),
        (nx + 3, ny + 10),
        (nx - 4, ny + 10),
    ], fill=mantle_hi)
    # Cross-chest ODM harness strap
    d.line([(nx - cape_w + 2, ny + 2), (nx + cape_w - 2, ny + jacket_h - 2)], fill=strap_tan, width=3)

    out = Image.alpha_composite(img.convert("RGBA"), cloak)

    if dual_blades:
        blades = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        bd = ImageDraw.Draw(blades)
        blade_len = max(56, int(bh_px * 0.34))

        def draw_odm_blade(hand_xy, dir_angle_deg: float):
            hx, hy = hand_xy
            rad = math.radians(dir_angle_deg)
            ux, uy = math.cos(rad), math.sin(rad)
            px, py = -uy, ux

            # Trigger/grip behind hand, steel blade forward
            p0 = (hx - ux * 12, hy - uy * 12)
            p1 = (hx + ux * blade_len, hy + uy * blade_len)
            p2 = (hx + ux * (blade_len - 10) + px * 5, hy + uy * (blade_len - 10) + py * 5)
            p3 = (hx + ux * 6 + px * 5, hy + uy * 6 + py * 5)
            p4 = (hx + ux * 6 - px * 2, hy + uy * 6 - py * 2)

            # Dark steel blade + metallic sheen edge
            bd.polygon([p4, p3, p2, p1], fill=(38, 44, 52, 255), outline=(12, 14, 18, 255))
            bd.line([(hx + ux * 8 + px * 3, hy + uy * 8 + py * 3), (hx + ux * (blade_len - 6) + px * 3, hy + uy * (blade_len - 6) + py * 3)],
                    fill=(195, 212, 226, 255), width=2)
            # Hilt & handguard
            bd.line([p0, (hx + ux * 7, hy + uy * 7)], fill=(75, 80, 88, 255), width=5)
            bd.line([(hx + ux * 5 - px * 7, hy + uy * 5 - py * 7), (hx + ux * 5 + px * 9, hy + uy * 5 + py * 9)],
                    fill=(120, 128, 138, 255), width=4)

        # Front hand sword pointing forward/upward, back hand sword reverse/angled ready
        dx_f = fh[0] - cx
        dy_f = fh[1] - ny
        ang_f = math.degrees(math.atan2(dy_f * 0.35 - 12, max(18, dx_f)))
        draw_odm_blade(fh, ang_f)
        draw_odm_blade(bh, 145 if bh[0] <= cx else 25)
        out = Image.alpha_composite(out, blades)

    if add_spear:
        sp = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        sd = ImageDraw.Draw(sp)
        fx, fy = fh
        # Olive/grey Thunder Spear cylinder with pointed red/silver warhead tip mounted on front forearm
        sd.rectangle([fx - 12, fy - 7, fx + 32, fy + 5], fill=(82, 92, 74, 255), outline=(20, 24, 20, 255), width=2)
        sd.polygon([(fx + 32, fy - 7), (fx + 52, fy - 1), (fx + 32, fy + 5)], fill=(210, 220, 230, 255), outline=(25, 30, 35, 255))
        sd.ellipse([fx + 44, fy - 8, fx + 58, fy + 6], fill=(110, 205, 255, 190))
        out = Image.alpha_composite(out, sp)

    return out


def paint_titan_muscles(img: Image.Image) -> Image.Image:
    """Adds crimson-brown anatomical Titan muscle striations on chest, abs, and shoulders."""
    info = detect_torso_and_hands(img)
    nx, ny = info["neck"]
    wx, wy = info["waist"]
    bh = info["body_h"]
    dark = info["dark_mask"]

    w, h = img.size
    muscles = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(muscles)

    mid_x = (nx + wx) // 2
    sh_y = ny + int(bh * 0.08)
    chest_y = ny + int(bh * 0.16)
    ab_step = max(8, int(bh * 0.055))

    fiber_hi = (155, 58, 52, 205)
    fiber_lo = (96, 34, 30, 190)

    # Pectoral plates & collarbone lines
    d.arc([mid_x - 24, sh_y - 4, mid_x - 2, chest_y + 10], 10, 175, fill=fiber_hi, width=3)
    d.arc([mid_x + 2, sh_y - 4, mid_x + 24, chest_y + 10], 5, 170, fill=fiber_hi, width=3)
    # Center sternum & abdominal 6-pack ribs
    d.line([(mid_x, sh_y), (wx, wy - 4)], fill=fiber_lo, width=2)
    for k in range(3):
        ry = chest_y + 10 + k * ab_step
        rx = int(mid_x + (wx - mid_x) * (k + 1) / 4.0)
        d.line([(rx - 13, ry), (rx - 3, ry + 3)], fill=fiber_hi, width=2)
        d.line([(rx + 3, ry + 3), (rx + 13, ry)], fill=fiber_hi, width=2)
    # Shoulder/deltoid muscle bands
    d.arc([nx - 28, ny, nx - 8, sh_y + 18], 80, 260, fill=fiber_hi, width=2)
    d.arc([nx + 8, ny, nx + 28, sh_y + 18], 280, 100, fill=fiber_hi, width=2)

    # Mask striations strictly inside the dark Titan body silhouette!
    m_arr = np.array(muscles, dtype=np.uint8)
    m_arr[~dark, 3] = 0
    return Image.alpha_composite(img.convert("RGBA"), Image.fromarray(m_arr, mode="RGBA"))


def recolor_red_body_to_black(img: Image.Image) -> Image.Image:
    """
    In Eren_SwordSlash_0..5, Eren himself was drawn in bright crimson while the slash FX is light pink/white-red.
    Converts opaque solid red stickman body pixels to deep charcoal-black while preserving translucent slash trails!
    """
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    r = arr[:, :, 0].astype(np.int16)
    g = arr[:, :, 1].astype(np.int16)
    b = arr[:, :, 2].astype(np.int16)
    a = arr[:, :, 3]
    # Solid crimson body pixels have low G/B and high alpha
    is_red_body = (a > 210) & (r > 120) & (g < 70) & (b < 70)
    arr[is_red_body, 0] = 14
    arr[is_red_body, 1] = 14
    arr[is_red_body, 2] = 16
    return Image.fromarray(arr, mode="RGBA")


def overlay_diend_gun(img: Image.Image, gun_tmpl: Image.Image, aim_angle_deg: float = 0.0, hand_key: str = "front_hand") -> Image.Image:
    info = detect_torso_and_hands(img)
    hx, hy = info[hand_key]
    bh = info["body_h"]
    target_w = max(32, int(round(bh * 0.24)))
    scale = target_w / float(gun_tmpl.width)
    gun = gun_tmpl.resize((target_w, max(16, int(round(gun_tmpl.height * scale)))), Image.Resampling.LANCZOS)
    if abs(aim_angle_deg) > 1.0:
        gun = trim_alpha(gun.rotate(aim_angle_deg, resample=Image.Resampling.BICUBIC, expand=True))
    dx = hx - int(gun.width * 0.24)
    dy = hy - int(gun.height * 0.42)
    return expand_canvas_and_paste(img, gun, (dx, dy), behind=False)


# ---------------------------------------------------------------------------
# Frame Sequence Generators (6-frame clips with fixed foot anchor)
# ---------------------------------------------------------------------------
def build_6_frames(
    crops_or_imgs: list[Image.Image],
    out_dir: str,
    prefix: str,
    transform_fn,
    target_body_h: int = 235,
    max_crop_ref_h: int | None = None,
    canvas_h: int = 300,
    anchor_left_x: int | None = None,
    already_rgba: bool = False,
):
    """Normalizes crops, applies character feature transform_fn(norm_img, idx), and writes 6 frames _0.._5."""
    n = len(crops_or_imgs)
    # Expand/interpolate cleanly to 6 frames if source strip had < 6 poses
    seq = []
    for k in range(6):
        src_idx = min(n - 1, int(round(k * (n - 1) / 5.0))) if n > 1 else 0
        seq.append(crops_or_imgs[src_idx])

    for idx, src in enumerate(seq):
        clean = src if already_rgba else remove_white_bg(src)
        clean = strip_thin_grid_lines(clean)
        norm = normalize_to_canvas(
            clean,
            target_body_h=target_body_h,
            max_crop_ref_h=max_crop_ref_h,
            canvas_h=canvas_h,
            anchor_left_x=anchor_left_x,
        )
        edited = transform_fn(norm, idx)
        final = finalize_char_frame(edited, target_foot_y=283)
        final.save(os.path.join(out_dir, f"{prefix}_{idx}.png"))


def save_single_enhanced(src: Image.Image, out_path: str, transform_fn, target_body_h: int = 235, canvas_h: int = 300, already_rgba: bool = False):
    clean = src if already_rgba else remove_white_bg(src)
    clean = strip_thin_grid_lines(clean)
    norm = normalize_to_canvas(clean, target_body_h=target_body_h, canvas_h=canvas_h)
    edited = transform_fn(norm, 0)
    final = finalize_char_frame(edited, target_foot_y=283)
    final.save(out_path)


def save_idle_pair(crop: Image.Image, dir_path: str, prefix: str, target_body_h: int = 235, post_fn=None):
    clean = trim_alpha(remove_white_bg(crop))
    sw, sh = clean.size
    scale = min(target_body_h / float(sh), 270.0 / float(sh), 280.0 / float(sw))
    nw, nh = max(1, int(round(sw * scale))), max(1, int(round(sh * scale)))
    cw, ch, bottom_pad = 300, 300, 16

    for idx, (sx, sy) in enumerate([(1.0, 1.0), (1.01, 0.98)]):
        rw, rh = max(1, int(round(nw * sx))), max(1, int(round(nh * sy)))
        r = clean.resize((rw, rh), Image.Resampling.LANCZOS)
        c = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        c.paste(r, ((cw - rw) // 2, ch - bottom_pad - rh), r)
        if post_fn is not None:
            c = post_fn(c, idx)
        final = finalize_char_frame(c, target_foot_y=283)
        final.save(os.path.join(dir_path, f"{prefix}_{idx}.png"))


def measure_max_trimmed_h(sheet: Image.Image, boxes: list[tuple[int, int, int, int]]) -> int:
    max_h = 1
    for b in boxes:
        c = trim_alpha(strip_thin_grid_lines(remove_white_bg(sheet.crop(b))))
        if c.size[1] > max_h:
            max_h = c.size[1]
    return max_h


def process_portraits(tmpl: FeatureTemplates):
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
    save_idle_pair(
        e_norm,
        os.path.join(CHAR_DIR, "Eren"),
        "Eren_Idle",
        target_body_h=235,
        post_fn=lambda img, _: paint_eren_cloak_and_blades(img, dual_blades=False),
    )
    save_idle_pair(
        e_titan,
        os.path.join(CHAR_DIR, "Eren"),
        "Eren_Titan_Idle",
        target_body_h=245,
        post_fn=lambda img, _: paint_titan_muscles(img),
    )

    # Diend
    diend_sp = Image.open(os.path.join(SRC_DIR, "Diend_Sprite.png")).convert("RGBA")
    d_norm = diend_sp.crop((200, 105, 980, 1380))
    d_kiva = diend_sp.crop((1715, 45, 2410, 1380))
    save_portrait(d_norm, os.path.join(UI_DIR, "Diend_Normal_Select.png"))
    save_portrait(d_kiva, os.path.join(UI_DIR, "Diend_Kiva_Select.png"))
    save_idle_pair(
        d_norm,
        os.path.join(CHAR_DIR, "Diend"),
        "Diend_Idle",
        target_body_h=240,
        post_fn=lambda img, _: apply_head_template(img, tmpl.diend_helmet, tmpl.diend_helmet_head_rel, detect_head_anchor(img), scale_mul=0.92, offset_xy=(0, 2)),
    )
    save_idle_pair(d_kiva, os.path.join(CHAR_DIR, "Diend"), "LordKiva_Idle", target_body_h=245)


def regenerate_goku(tmpl: FeatureTemplates):
    out_dir = os.path.join(CHAR_DIR, "Goku")
    sheet = Image.open(os.path.join(SRC_DIR, "GokuA.png")).convert("RGBA")
    sheet = mask_white(
        sheet,
        [
            (2, 2, 260, 34),
            (1378, 2, 1630, 34),
            (2, 128, 72, 212),
            (2, 228, 260, 260),
            (1378, 228, 1590, 260),
            (2, 506, 265, 536),
            (1378, 506, 1650, 536),
            (2, 770, 265, 804),
            (1378, 770, 1645, 804),
            (2, 960, 255, 992),
            (1378, 960, 1630, 992),
            (2, 1158, 265, 1190),
            (460, 1158, 660, 1190),
            (920, 1158, 1120, 1190),
            (1378, 1158, 1640, 1190),
            (1834, 1158, 2050, 1190),
        ],
    )

    def add_black_hair(im: Image.Image, _idx: int) -> Image.Image:
        return apply_head_template(im, tmpl.goku_black_hair, tmpl.goku_black_hair_head_rel, detect_head_anchor(im), scale_mul=1.02)

    def add_ssj_features(im: Image.Image, idx: int, scale_mul: float = 1.18, offset_xy: tuple[int, int] = (0, 22)) -> Image.Image:
        with_hair = apply_head_template(im, tmpl.goku_ssj_hair, tmpl.goku_ssj_hair_head_rel, detect_head_anchor(im), scale_mul=scale_mul, offset_xy=offset_xy)
        return add_aura_glow(with_hair, aura_type="ssj", intensity=0.95 + 0.1 * (idx % 2))

    # 1. Normal Punch (J) -> Add black Saiyan hair cleanly to all frames 0..5
    punch_boxes = [(83, 38, 168, 213), (242, 38, 436, 213), (475, 38, 664, 213), (698, 38, 903, 213), (941, 38, 1112, 213), (1133, 38, 1362, 213)]
    ref_h = measure_max_trimmed_h(sheet, punch_boxes)
    build_6_frames(
        [sheet.crop(b) for b in punch_boxes],
        out_dir,
        "Goku_Punch",
        lambda im, i: add_black_hair(im, i),
        228,
        ref_h,
    )

    # 2. SSJ Punch (J) -> Golden SSJ hair + golden aura on all 6 frames
    ssj_punch_boxes = [(1430, 38, 1560, 213), (1644, 38, 1800, 213), (1871, 38, 2031, 213), (2086, 38, 2270, 213), (2326, 38, 2501, 213), (2529, 38, 2713, 213)]
    ref_h = measure_max_trimmed_h(sheet, ssj_punch_boxes)
    build_6_frames([sheet.crop(b) for b in ssj_punch_boxes], out_dir, "Goku_SSJ_Punch", add_ssj_features, 225, ref_h)

    # 3. Normal Ki Blast (U) -> Add black Saiyan hair onto all 6 frames!
    ki_boxes = [(56, 260, 208, 499), (240, 260, 410, 499), (462, 260, 634, 499), (686, 260, 870, 499)]
    ref_h = measure_max_trimmed_h(sheet, ki_boxes)
    build_6_frames([sheet.crop(b) for b in ki_boxes], out_dir, "Goku_KiBlast", add_black_hair, 226, ref_h)

    # 4. Kaioken (I Normal) -> Black spiky Saiyan hair + crimson Kaioken outline/aura on all 6 frames
    kaio_boxes = [(929, 260, 1071, 499), (1164, 260, 1295, 499), (1449, 260, 1573, 499), (1673, 260, 1828, 499), (1960, 234, 2188, 499), (2408, 234, 2634, 499)]
    ref_h = measure_max_trimmed_h(sheet, kaio_boxes[:4])
    for i, b in enumerate(kaio_boxes):
        raw = strip_thin_grid_lines(remove_white_bg(sheet.crop(b)))
        norm = normalize_to_canvas(raw, 228 if i < 4 else 250, ref_h if i < 4 else None)
        with_hair = add_black_hair(norm, i)
        if i < 4:
            with_hair = add_aura_glow(with_hair, aura_type="kaioken", intensity=0.85 + i * 0.1)
        final = finalize_char_frame(with_hair, target_foot_y=283)
        final.save(os.path.join(out_dir, f"Goku_Kaioken_{i}.png"))

    # 5. Dash + Run + Dash+J (Normal Base) -> All get spiky black Saiyan hair!
    dashj_boxes = [(8, 536, 452, 764), (462, 536, 912, 764), (922, 536, 1371, 764)]
    save_single_enhanced(sheet.crop(dashj_boxes[0]), os.path.join(out_dir, "Goku_Dash_0.png"), add_black_hair, 210)
    run_srcs = [sheet.crop(punch_boxes[0]), sheet.crop(dashj_boxes[1]), sheet.crop(punch_boxes[0]), sheet.crop(dashj_boxes[1]), sheet.crop(punch_boxes[0]), sheet.crop(dashj_boxes[1])]
    build_6_frames(run_srcs, out_dir, "Goku_Run", add_black_hair, 224)
    dashj_seq = [sheet.crop(dashj_boxes[0]), sheet.crop(dashj_boxes[1]), sheet.crop(kaio_boxes[3]), sheet.crop(dashj_boxes[2]), sheet.crop(kaio_boxes[4]), sheet.crop(dashj_boxes[2])]
    build_6_frames(dashj_seq, out_dir, "Goku_DashJ", add_black_hair, 232)

    # 6. SSJ Dash + Run + Dash+J -> All get golden SSJ hair + golden aura!
    ssj_dashj_boxes = [(1380, 536, 1828, 764), (1836, 536, 2281, 764), (2290, 536, 2746, 764)]
    save_single_enhanced(sheet.crop(ssj_dashj_boxes[0]), os.path.join(out_dir, "Goku_SSJ_Dash_0.png"), lambda im, i: add_ssj_features(im, i, 1.15, (0, 20)), 210)
    ssj_run_srcs = [sheet.crop(ssj_punch_boxes[0]), sheet.crop(ssj_dashj_boxes[1]), sheet.crop(ssj_punch_boxes[0]), sheet.crop(ssj_dashj_boxes[1]), sheet.crop(ssj_punch_boxes[0]), sheet.crop(ssj_dashj_boxes[1])]
    build_6_frames(ssj_run_srcs, out_dir, "Goku_SSJ_Run", add_ssj_features, 224)
    ssj_dashj_seq = [sheet.crop(ssj_dashj_boxes[0]), sheet.crop(ssj_dashj_boxes[1]), sheet.crop(ssj_dashj_boxes[1]), sheet.crop(ssj_dashj_boxes[2]), sheet.crop(ssj_dashj_boxes[2]), sheet.crop(ssj_dashj_boxes[2])]
    build_6_frames(ssj_dashj_seq, out_dir, "Goku_SSJ_DashJ", add_ssj_features, 232)

    # 7. SSJ Multi-Hit (U) -> Crisp single-fighter rapid flurry poses with anime speed-streak lines, SSJ hair & golden aura (NO bald afterimages!)
    def add_ssj_flurry(im: Image.Image, idx: int) -> Image.Image:
        # Subtle dynamic speed-streak impact flares ahead of the fist
        fl = Image.new("RGBA", im.size, (0, 0, 0, 0))
        fd = ImageDraw.Draw(fl)
        info = detect_torso_and_hands(im)
        fx, fy = info["front_hand"]
        for k, (dy_off, length) in enumerate([(-16, 56), (0, 72), (16, 48)]):
            y_pos = fy + dy_off + ((idx * 5) % 9) - 4
            fd.line([(fx - 10, y_pos), (fx + length, y_pos)], fill=(255, 240, 120, 195), width=4)
        base = Image.alpha_composite(im.convert("RGBA"), fl)
        return add_ssj_features(base, idx)

    mh_flurry_srcs = [
        sheet.crop(ssj_punch_boxes[1]),
        sheet.crop(ssj_punch_boxes[3]),
        sheet.crop(ssj_dashj_boxes[1]),
        sheet.crop(ssj_punch_boxes[4]),
        sheet.crop(ssj_dashj_boxes[2]),
        sheet.crop(ssj_punch_boxes[5]),
    ]
    ref_mh = measure_max_trimmed_h(sheet, ssj_punch_boxes[1:])
    build_6_frames(mh_flurry_srcs, out_dir, "Goku_SSJ_MultiHit", add_ssj_flurry, 225, ref_mh)

    # 8. Kamehameha (I SSJ) -> Keep Goku + blue orb charge on left (beam comes from KamehamehaBeam.prefab), expand cleanly to 6 frames with golden aura!
    kame_boxes = [
        (6, 992, 164, 1152),
        (170, 992, 338, 1152),
        (461, 992, 650, 1152),
        (921, 992, 1105, 1152),
        (1380, 992, 1565, 1152),
        (921, 992, 1105, 1152),
    ]
    ref_kame = measure_max_trimmed_h(sheet, kame_boxes[:3])
    build_6_frames([sheet.crop(b) for b in kame_boxes], out_dir, "Goku_Kamehameha", add_ssj_features, 225, ref_kame)

    # 9. Genki Dama (Dash+I SSJ) -> Split into 6 clean SINGLE-Goku frames + golden SSJ hair + golden aura!
    gd_boxes = [
        (115, 1192, 405, 1532),   # Dash skyward
        (530, 1270, 632, 1525),   # Jump start
        (685, 1192, 818, 1465),   # Raise hand charging orb
        (1055, 1192, 1272, 1532), # Full Genki Dama overhead!
        (1055, 1192, 1272, 1532), # Hold glow
        (685, 1192, 818, 1465),   # Hurl Genki Dama!
    ]
    for i, b in enumerate(gd_boxes):
        raw = strip_thin_grid_lines(remove_white_bg(sheet.crop(b)))
        norm = normalize_to_canvas(raw, 235 if i in (3, 4) else 225, canvas_h=300)
        edited = add_ssj_features(norm, i, scale_mul=1.08, offset_xy=(0, 2))
        final = finalize_char_frame(edited, target_foot_y=283)
        final.save(os.path.join(out_dir, f"Goku_GenkiDama_{i}.png"))

    # Combat FX from GokuA
    save_fx_sprite(sheet.crop((1440, 1192, 1702, 1532)), os.path.join(COMBAT_DIR, "FX_GenkiDamaOrb.png"), 320)
    save_fx_sprite(sheet.crop((1836, 1192, 2282, 1532)), os.path.join(COMBAT_DIR, "FX_Explosion_0.png"), 360)
    save_fx_sprite(sheet.crop((2290, 1192, 2746, 1532)), os.path.join(COMBAT_DIR, "FX_Explosion_1.png"), 380)


def regenerate_eren(tmpl: FeatureTemplates):
    out_dir = os.path.join(CHAR_DIR, "Eren")
    sheet = Image.open(os.path.join(SRC_DIR, "ErenA.png")).convert("RGBA")

    def add_eren_normal(im: Image.Image, _idx: int, recolor: bool = False, spear: bool = False) -> Image.Image:
        work = recolor_red_body_to_black(im) if recolor else im
        # Paint Survey Corps cloak & dual blades BEFORE hair so neck anchor uses bare round head and hair overlaps collar!
        work = paint_eren_cloak_and_blades(work, dual_blades=True, add_spear=spear)
        work = apply_head_template(work, tmpl.eren_hair, tmpl.eren_hair_head_rel, detect_head_anchor(work), scale_mul=1.02)
        return work

    def add_eren_titan(im: Image.Image, _idx: int, add_head: bool = True) -> Image.Image:
        work = im
        if add_head:
            work = apply_head_template(work, tmpl.eren_titan_head, tmpl.eren_titan_head_rel, detect_head_anchor(work), scale_mul=1.04)
        return paint_titan_muscles(work)

    # Row 1 Left: SwordSlash 0..5 (fix red body -> black + Survey Corps cloak + 2 dual blades!)
    r1_x = [37, 258, 477, 691, 893, 1099, 1302]
    r1_boxes = [(r1_x[i] + 5, 185, r1_x[i + 1] - 5, 400) for i in range(6)]
    ref_r1 = measure_max_trimmed_h(sheet, r1_boxes)
    build_6_frames([sheet.crop(b) for b in r1_boxes], out_dir, "Eren_SwordSlash", lambda im, i: add_eren_normal(im, i, recolor=True), 225, ref_r1)

    # Row 2 Left: DashSlash 0..5 + Run 0..5 + Dash 0..1 (Survey Corps cloak + 2 dual blades!)
    r2_x = [37, 259, 492, 703, 899, 1100, 1302]
    r2_boxes = [(r2_x[i] + 5, 504, r2_x[i + 1] - 5, 730) for i in range(6)]
    ref_r2 = measure_max_trimmed_h(sheet, r2_boxes)
    build_6_frames([sheet.crop(b) for b in r2_boxes], out_dir, "Eren_DashSlash", lambda im, i: add_eren_normal(im, i), 225, ref_r2)
    run_boxes = [r2_boxes[1], r2_boxes[3], r2_boxes[0], r2_boxes[4], r2_boxes[2], r2_boxes[5]]
    build_6_frames([sheet.crop(b) for b in run_boxes], out_dir, "Eren_Run", lambda im, i: add_eren_normal(im, i), 225, ref_r2)
    save_single_enhanced(sheet.crop(r2_boxes[0]), os.path.join(out_dir, "Eren_Dash_0.png"), lambda im, i: add_eren_normal(im, i), 225)
    save_single_enhanced(sheet.crop(r2_boxes[2]), os.path.join(out_dir, "Eren_Dash_1.png"), lambda im, i: add_eren_normal(im, i), 225)

    # Row 3 Left: ThunderSpear 0..5 (Keep Eren hair + Survey Corps cloak + 2 dual blades + forearm spear!)
    r3_boxes = [(42, 836, 200, 1058), (214, 836, 353, 1058), (367, 836, 521, 1058), (535, 836, 696, 1058), (710, 836, 866, 1058)]
    ref_r3 = measure_max_trimmed_h(sheet, r3_boxes)
    build_6_frames([sheet.crop(b) for b in r3_boxes], out_dir, "Eren_ThunderSpear", lambda im, i: add_eren_normal(im, i, spear=(i >= 2)), 225, ref_r3)
    save_fx_sprite(sheet.crop((885, 836, 1296, 1058)), os.path.join(COMBAT_DIR, "FX_ThunderSpear.png"), 260)

    # Row 4 Left: SpinSlash 0..5 (Hair + Survey Corps cloak + 2 dual blades + 360 trails!)
    r4_x = [37, 195, 360, 581, 847, 1071, 1302]
    r4_boxes = [(r4_x[i] + 5, 1170, r4_x[i + 1] - 5, 1386) for i in range(6)]
    ref_r4 = measure_max_trimmed_h(sheet, r4_boxes)
    build_6_frames([sheet.crop(b) for b in r4_boxes], out_dir, "Eren_SpinSlash", lambda im, i: add_eren_normal(im, i), 225, ref_r4)

    # Right Row 1: Titan GroundSlam 0..5 (Muscular Titan striations + long hair!)
    tr1_boxes = [(1437, 184, 1653, 425), (1672, 184, 1863, 425), (1881, 184, 2071, 425), (2089, 184, 2280, 425), (2297, 184, 2489, 425), (2507, 184, 2703, 425)]
    ref_t1 = measure_max_trimmed_h(sheet, tr1_boxes)
    build_6_frames([sheet.crop(b) for b in tr1_boxes], out_dir, "Eren_Titan_GroundSlam", lambda im, i: add_eren_titan(im, i, add_head=False), 245, ref_t1)

    # Right Row 2: Titan RockThrow 0..5 (Expand to 6 frames + Muscular Titan torso!)
    tr2_boxes = [(1437, 598, 1653, 841), (1672, 598, 1863, 841), (1881, 598, 2071, 841), (2089, 598, 2283, 841), (2293, 598, 2489, 841)]
    ref_t2 = measure_max_trimmed_h(sheet, tr2_boxes)
    build_6_frames([sheet.crop(b) for b in tr2_boxes], out_dir, "Eren_Titan_RockThrow", lambda im, i: add_eren_titan(im, i, add_head=False), 245, ref_t2)
    save_fx_sprite(sheet.crop((2505, 598, 2703, 792)), os.path.join(COMBAT_DIR, "FX_RockImpact.png"), 240)

    # Right Row 3: Titan Run & Stomp 0..5 (Notice old TR3 had border line x=2085..2086 making TR3_3 appear taller; crop tightly inside cell!)
    tr3_boxes = [(1440, 1120, 1650, 1352), (1673, 1120, 1862, 1352), (1884, 1120, 2074, 1352), (2095, 1120, 2280, 1352)]
    ref_t3 = measure_max_trimmed_h(sheet, tr3_boxes)
    build_6_frames([sheet.crop(b) for b in tr3_boxes], out_dir, "Eren_Titan_Run", lambda im, i: add_eren_titan(im, i, add_head=False), 245, ref_t3)
    build_6_frames([sheet.crop(b) for b in tr3_boxes], out_dir, "Eren_Titan_Stomp", lambda im, i: add_eren_titan(im, i, add_head=False), 245, ref_t3)
    save_fx_sprite(sheet.crop((2295, 1034, 2712, 1355)), os.path.join(COMBAT_DIR, "FX_TitanFoot.png"), 420)


def regenerate_diend(tmpl: FeatureTemplates):
    out_dir = os.path.join(CHAR_DIR, "Diend")
    sheet = Image.open(os.path.join(SRC_DIR, "DiendA.png")).convert("RGBA")
    sheet = mask_white(
        sheet,
        [
            (480, 105, 890, 178),
            (340, 1300, 560, 1365),
            (1910, 232, 2240, 308),
        ],
    )

    def add_diend_features(im: Image.Image, _idx: int, add_gun: bool = True, aim_angle: float = 0.0, hand_key: str = "front_hand") -> Image.Image:
        work = im
        if add_gun:
            work = overlay_diend_gun(work, tmpl.diend_gun_right, aim_angle_deg=aim_angle, hand_key=hand_key)
        work = apply_head_template(work, tmpl.diend_helmet, tmpl.diend_helmet_head_rel, detect_head_anchor(work), scale_mul=0.92, offset_xy=(0, 2))
        return work

    # 1. GunShoot 0..5 (Already has helmet & gun -> reinforce helmet & gun + expand cleanly to 6 frames!)
    gun_boxes = [(88, 178, 280, 492), (325, 178, 528, 492), (573, 178, 818, 492), (832, 178, 1085, 492)]
    ref_gun = measure_max_trimmed_h(sheet, gun_boxes)
    build_6_frames([sheet.crop(b) for b in gun_boxes], out_dir, "Diend_GunShoot", lambda im, i: add_diend_features(im, i, add_gun=False), 232, ref_gun)

    # 2. Run 0..5 -> Build from Diend's aiming/dashing poses (NO bald blurred victim!), WITH striped helmet + DiendDriver gun!
    dash_pose = (1466, 758, 1818, 1170)
    ph0_pose = (178, 615, 348, 942)
    run_srcs = [sheet.crop(gun_boxes[0]), sheet.crop(dash_pose), sheet.crop(gun_boxes[1]), sheet.crop(dash_pose), sheet.crop(gun_boxes[0]), sheet.crop(dash_pose)]
    build_6_frames(run_srcs, out_dir, "Diend_Run", lambda im, i: add_diend_features(im, i, add_gun=(i % 2 == 0)), 228)

    # 3. Dash_0 -> Dash pose + Diend striped helmet + DiendDriver!
    save_single_enhanced(sheet.crop(dash_pose), os.path.join(out_dir, "Diend_Dash_0.png"), lambda im, i: add_diend_features(im, i, add_gun=False), 215)

    # 4. PhantomSlash 0..5 (U): Diend raises gun/card & summons Kiva phantom (helmet + DiendDriver in EVERY frame!)
    ph_kiva_crop = sheet.crop((490, 615, 850, 942))
    save_fx_sprite(ph_kiva_crop, os.path.join(COMBAT_DIR, "FX_PhantomKivaSlash.png"), 300)

    def paint_phantom_summon(im: Image.Image, idx: int) -> Image.Image:
        base = add_diend_features(im, idx, add_gun=True, aim_angle=18.0 if idx < 2 else 0.0)
        if idx >= 2:
            # Add cyan summon ring flare ahead of DiendDriver muzzle
            info = detect_torso_and_hands(base)
            fx, fy = info["front_hand"]
            ring = Image.new("RGBA", base.size, (0, 0, 0, 0))
            rd = ImageDraw.Draw(ring)
            r_sz = 18 + (idx - 2) * 8
            rd.ellipse([fx + 20 - r_sz, fy - 4 - r_sz, fx + 20 + r_sz, fy - 4 + r_sz], outline=(40, 225, 255, 210), width=4)
            base = Image.alpha_composite(base, ring)
        return base

    ph_seq = [sheet.crop(ph0_pose), sheet.crop(ph0_pose), sheet.crop(gun_boxes[1]), sheet.crop(gun_boxes[2]), sheet.crop(gun_boxes[3]), sheet.crop(gun_boxes[1])]
    build_6_frames(ph_seq, out_dir, "Diend_PhantomSlash", paint_phantom_summon, 232)

    # 5. CardShot 0..5 (I): Diend holds DiendDriver + fires piercing Rider card + helmet!
    card_box = (56, 1106, 330, 1442)
    save_fx_sprite(sheet.crop((355, 1175, 615, 1298)), os.path.join(COMBAT_DIR, "FX_DiendCard.png"), 180)
    card_seq = [sheet.crop(gun_boxes[0]), sheet.crop(card_box), sheet.crop(card_box), sheet.crop(gun_boxes[2]), sheet.crop(card_box), sheet.crop(gun_boxes[1])]
    build_6_frames(card_seq, out_dir, "Diend_CardShot", lambda im, i: add_diend_features(im, i, add_gun=(i in (1, 2, 4)), aim_angle=-20.0, hand_key="back_hand"), 232)

    # 6. FanShot 0..5 (Dash+J): 3-beam cone blast WITH Diend striped helmet + DiendDriver gun!
    fan_box = (688, 1106, 1335, 1442)
    fan_seq = [sheet.crop(dash_pose), sheet.crop(gun_boxes[1]), sheet.crop(fan_box), sheet.crop(fan_box), sheet.crop(fan_box), sheet.crop(gun_boxes[2])]
    for i, src in enumerate(fan_seq):
        raw = strip_thin_grid_lines(remove_white_bg(src))
        norm = normalize_to_canvas(raw, 232, anchor_left_x=60 if 2 <= i <= 4 else None)
        edited = add_diend_features(norm, i, add_gun=False)
        final = finalize_char_frame(edited, target_foot_y=283)
        final.save(os.path.join(out_dir, f"Diend_FanShot_{i}.png"))

    # Right: LordKiva sprites & Freeze VFX
    save_single_enhanced(sheet.crop((1778, 290, 2220, 482)), os.path.join(out_dir, "LordKiva_Attack_0.png"), lambda im, _: im, 230, canvas_h=300)
    save_single_enhanced(sheet.crop((1888, 758, 2322, 1170)), os.path.join(out_dir, "LordKiva_WingFreeze_0.png"), lambda im, _: im, 250, canvas_h=300)
    save_fx_sprite(sheet.crop((1888, 758, 2322, 1170)), os.path.join(COMBAT_DIR, "FX_DarkKivaFreezeBurst.png"), 360)


if __name__ == "__main__":
    ensure_dirs()
    templates = FeatureTemplates()
    process_portraits(templates)
    regenerate_goku(templates)
    regenerate_eren(templates)
    regenerate_diend(templates)
    import regenerate_run_sprites
    regenerate_run_sprites.generate_all_run_cycles()
    print("MainChar sprite regeneration & 6-frame expansion complete!")
