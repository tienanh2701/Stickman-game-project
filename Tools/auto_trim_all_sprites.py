#!/usr/bin/env python3
"""
Batch sprite cleaner, scale-restorer, tight-cropper (exact 2px transparent safety margin),
and .png.meta configurator for Stickman Beat 'Em Up.

Operates strictly on:
  - Assets/Sprites/Characters/**/*.png (+ .png.meta)
  - Assets/Sprites/Enemies/**/*.png    (+ .png.meta)
"""

import glob
import os
import re
import numpy as np
from PIL import Image
from scipy import ndimage


PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SPRITES_ROOT = os.path.join(PROJECT_ROOT, "Assets", "Sprites")
TARGET_DIRS = [
    os.path.join(SPRITES_ROOT, "Characters"),
    os.path.join(SPRITES_ROOT, "Enemies"),
]
DIENDA_SHEET_PATH = os.path.join(SPRITES_ROOT, "SourceSheets", "DiendA.png")

ALPHA_VISIBLE_THRESHOLD = 13  # > 0.05f (~12.75 / 255)
SAFETY_PADDING = 2
TARGET_PPU = 160

UPRIGHT_REF_TOKENS = ("Idle_0", "Move_0", "FightStance_0", "Walk_0", "Advance_0")
SKIP_UPSCALE_TOKENS = {
    "Death",
    "Defeated",
    "Destroyed",
    "Hit",
    "Hurt",
    "Slide",
    "Crouch",
    "GroundSpikes",
    "Shockwave",
    "Stagger",
}
SKIP_UPSCALE_PREFIXES = (
    "CartTitan_Fire",
    "Mecha_ChestCore",
    "Mecha_LeftArm",
    "Mecha_RightArm",
    "Mecha_Full",
)


def is_skip_upscale(filename):
    stem = os.path.splitext(filename)[0]
    if any(stem.startswith(pref) for pref in SKIP_UPSCALE_PREFIXES):
        return True
    tokens = set(stem.split("_"))
    return bool(tokens & SKIP_UPSCALE_TOKENS)
# Poses that remain standing upright even when wide beams/auras clamp them; scale to ~1.00 * H_ref
STANDING_WIDE_FRAMES = {
    "Diend_FanShot_2.png",
    "Diend_FanShot_3.png",
    "Diend_FanShot_4.png",
    "Goku_GenkiDama_0.png",
    "LordKiva_Attack_0.png",
    "LordKiva_Attack_1.png",
}
# Explicitly recognized crushed action frames even if pre-crop width < 260 (e.g. Eren_SwordSlash_5)
EXPLICIT_CRUSHED_FRAMES = {
    "Goku_Dash_0.png",
    "Goku_DashJ_0.png",
    "Goku_GenkiDama_0.png",
    "Goku_SSJ_DashJ_3.png",
    "Goku_SSJ_DashJ_4.png",
    "Goku_SSJ_DashJ_5.png",
    "Goku_SSJ_MultiHit_4.png",
    "Eren_SpinSlash_3.png",
    "Eren_SwordSlash_5.png",
    "Diend_Dash_0.png",
    "Diend_FanShot_0.png",
    "Diend_FanShot_2.png",
    "Diend_FanShot_3.png",
    "Diend_FanShot_4.png",
    "LordKiva_Attack_0.png",
    "LordKiva_Attack_1.png",
}


def collect_target_pngs():
    files = []
    for d in TARGET_DIRS:
        if os.path.isdir(d):
            files.extend(glob.glob(os.path.join(d, "**", "*.png"), recursive=True))
    return sorted(files)


def get_character_group(filename):
    """Return character/enemy group key, keeping Enemy_* root placeholders distinct."""
    stem = os.path.splitext(filename)[0]
    if stem.startswith("Enemy_"):
        return stem
    for multi in ("Goku_SSJ_", "Eren_Titan_"):
        if stem.startswith(multi):
            return multi[:-1]
    parts = stem.split("_")
    return parts[0] if parts else stem


def compute_metrics(arr):
    r = arr[:, :, 0].astype(np.int16)
    g = arr[:, :, 1].astype(np.int16)
    b = arr[:, :, 2].astype(np.int16)
    a = arr[:, :, 3]
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    diff = mx - mn
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    return r, g, b, a, mx, mn, diff, lum


def restore_lord_kiva_attack_from_sheet():
    """
    Restore LordKiva_Attack_0 from SourceSheets/DiendA.png cleanly:
    keeps Kiva unclipped (~244px tall) + his immediate hand muzzle flash,
    excluding the dark UI header box ('LordKivaAI.cs') and far-right floating projectile streaks.
    """
    if not os.path.isfile(DIENDA_SHEET_PATH):
        return None
    sheet = Image.open(DIENDA_SHEET_PATH).convert("RGBA")
    # Region containing Lord Kiva + immediate fist muzzle flash in DiendA.png (2752x1536)
    x_min, y_min, x_max, y_max = 1770, 224, 2012, 486
    if sheet.width < x_max or sheet.height < y_max:
        return None
    crop = np.array(sheet.crop((x_min, y_min, x_max, y_max)))
    # Clear the 'LordKivaAI.cs' UI box region (global x >= 1922, y <= 318) safely away from wings/muzzle
    rel_x_cut = 1922 - x_min
    rel_y_cut = 318 - y_min
    crop[:rel_y_cut, rel_x_cut:, :] = 0
    return crop


def step_a_clean_background_islands_strays(arr, filename):
    """
    Step A:
    1) Border + transparent floodfill through near-white neutral pixels -> (0,0,0,0)
       + 1px neutral white fringe cleanup.
    2) Enclosed paper-white island detection & removal with AA-extension and bright-glow/armor preservation
       + 1px AA fringe cleanup around removed islands.
    3) Strip tiny detached stray specks (area <= 30, gap >= 3px) and isolated bottom smudges strictly below feet.
    """
    out = arr.copy()
    h, w = out.shape[:2]
    r, g, b, a, mx, mn, diff, lum = compute_metrics(out)

    # 1. Border + existing-transparent flood-fill through near-white neutral pixels
    near_white = (r >= 242) & (g >= 242) & (b >= 242) & (diff <= 12)
    trans = a <= 12
    seed = trans.copy()
    seed[0, :] = True
    seed[-1, :] = True
    seed[:, 0] = True
    seed[:, -1] = True

    bg_region = trans | (near_white & (a > 12))
    s4 = np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]], dtype=bool)
    lbl_bg, _ = ndimage.label(bg_region, structure=s4)
    ext_labels = np.unique(lbl_bg[seed & bg_region])
    ext_labels = ext_labels[ext_labels != 0]
    ext_mask = np.isin(lbl_bg, ext_labels) | trans
    removed_bg_white = ext_mask & (~trans)

    out[ext_mask] = [0, 0, 0, 0]

    # Feather / clean 1px neutral white fringe pixels directly touching newly stripped white background
    if np.any(removed_bg_white):
        _, _, _, a, _, _, diff, lum = compute_metrics(out)
        adj_ext = ndimage.binary_dilation(removed_bg_white, structure=s4) & (~ext_mask) & (a > 0)
        hard_white_fringe = adj_ext & (lum >= 238) & (diff <= 12)
        out[hard_white_fringe] = [0, 0, 0, 0]
        soft_white_fringe = adj_ext & (~hard_white_fringe) & (lum >= 220) & (diff <= 12)
        if np.any(soft_white_fringe):
            scale = np.clip((238.0 - lum[soft_white_fringe]) / 18.0, 0.0, 1.0)
            new_alpha = np.round(out[soft_white_fringe, 3].astype(np.float32) * scale).astype(np.uint8)
            out[soft_white_fringe, 3] = new_alpha
            out[out[:, :, 3] == 0] = [0, 0, 0, 0]

    # 2. Enclosed flat pure-white island removal
    r, g, b, a, mx, mn, diff, lum = compute_metrics(out)
    if filename == "Eren_SwordSlash_5.png":
        # Eren_SwordSlash_5 has a slight warm JPEG bleed inside his torso-arm loop
        pure_w = (a >= ALPHA_VISIBLE_THRESHOLD) & (lum >= 165) & (r >= 190) & (diff <= 55)
        light_aa = (a >= ALPHA_VISIBLE_THRESHOLD) & (lum >= 150) & (diff <= 85)
        lbl_w, n_islands = ndimage.label(pure_w, structure=np.ones((3, 3), dtype=bool))
        min_island_area = 25
    else:
        pure_w = (a >= ALPHA_VISIBLE_THRESHOLD) & (r >= 247) & (g >= 247) & (b >= 247) & (diff <= 6)
        light_aa = (a >= ALPHA_VISIBLE_THRESHOLD) & (lum >= 175) & (diff <= 14)
        lbl_w, n_islands = ndimage.label(pure_w, structure=s4)
        min_island_area = 95

    removed_islands = 0
    kept_islands = 0
    # Warm armor/tan colors for protecting FriezaSoldier's white tank-top vest
    tan_armor = (a >= ALPHA_VISIBLE_THRESHOLD) & (r >= 90) & (r <= 210) & ((r - b) >= 22) & (lum >= 80) & (lum <= 175)

    near_trans = ndimage.binary_dilation(out[:, :, 3] < ALPHA_VISIBLE_THRESHOLD, iterations=2)
    for i in range(1, n_islands + 1):
        comp = lbl_w == i
        area = int(comp.sum())
        if area < min_island_area:
            continue
        if filename == "Eren_SwordSlash_5.png" and np.any(comp & near_trans):
            continue

        # Protect FriezaSoldier chest white tank-top (upright frames touch warm tan belly armor)
        if filename.startswith("FriezaSoldier_") and "Death" not in filename:
            if np.any(ndimage.binary_dilation(comp, iterations=4) & tan_armor):
                kept_islands += 1
                continue

        # Grow outward through neutral light-grey anti-aliased ramp up to 6px to hit actual bounding wall
        cur = comp.copy()
        for _ in range(6):
            grown = ndimage.binary_dilation(cur, structure=s4) & (cur | light_aa)
            if np.array_equal(grown, cur):
                break
            cur = grown

        ring = ndimage.binary_dilation(cur, iterations=2) & (~cur) & (a >= ALPHA_VISIBLE_THRESHOLD)
        ring_wall = ring & (~light_aa) & (~pure_w)
        if int(ring_wall.sum()) < 10:
            ring_wall = ring
        rc = int(ring_wall.sum())
        if rc == 0:
            kept_islands += 1
            continue

        d150 = float((lum[ring_wall] < 150).sum()) / rc
        d175 = float((lum[ring_wall] < 175).sum()) / rc
        bright_glow_ring = float(
            ((mx[ring_wall] >= 165) & (lum[ring_wall] >= 125) & (diff[ring_wall] >= 25)).sum()
        ) / rc

        halo = ndimage.binary_dilation(comp, iterations=6) & (~comp) & (a >= ALPHA_VISIBLE_THRESHOLD)
        hc = int(halo.sum())
        bright_glow_halo = (
            float(((mx[halo] >= 165) & (lum[halo] >= 125) & (diff[halo] >= 24)).sum()) / hc
            if hc > 0
            else 0.0
        )

        # In Eren_SwordSlash_5 (non-glow lunge pose), JPEG red bleed around black arm should not keep trapped chest gap
        if filename == "Eren_SwordSlash_5.png":
            is_glow = False
            is_dark_enclosed = (d175 >= 0.40) and (d150 >= 0.30)
        else:
            is_glow = (bright_glow_ring > 0.20) or (bright_glow_halo >= 0.22)
            is_dark_enclosed = (d150 >= 0.45) or (d150 >= 0.40 and d175 >= 0.90)

        if (not is_glow) and is_dark_enclosed:
            # Clear enclosed white island + its 1-2px neutral/warm AA fringe so no bright rim remains
            fringe_mask = (lum >= 175) if filename == "Eren_SwordSlash_5.png" else ((lum >= 205) & (diff <= 14))
            fringe_1 = ndimage.binary_dilation(comp, structure=s4) & (~comp) & (a >= ALPHA_VISIBLE_THRESHOLD) & fringe_mask
            fringe_2 = ndimage.binary_dilation(comp | fringe_1, structure=s4) & (~(comp | fringe_1)) & (a >= ALPHA_VISIBLE_THRESHOLD) & (lum >= 210)
            wipe = comp | fringe_1 | fringe_2
            out[wipe] = [0, 0, 0, 0]
            # Soften immediate 1px neutral grey transition next to cleared island
            edge_soft = ndimage.binary_dilation(wipe, structure=s4) & (~wipe) & (out[:, :, 3] > 0) & (lum >= 180) & (diff <= 14)
            if np.any(edge_soft):
                scale = np.clip((210.0 - lum[edge_soft]) / 40.0, 0.0, 1.0)
                out[edge_soft, 3] = np.round(out[edge_soft, 3].astype(np.float32) * scale).astype(np.uint8)
                out[out[:, :, 3] == 0] = [0, 0, 0, 0]
            removed_islands += 1
        else:
            kept_islands += 1

    # 3. Strip tiny detached stray specks (area <= 30, separated >= 3px from larger parts)
    #    AND isolated floating bottom smudges strictly below main feet (gap >= 4px)
    vis = out[:, :, 3] >= ALPHA_VISIBLE_THRESHOLD
    if np.any(vis):
        s8 = np.ones((3, 3), dtype=bool)
        lbl_v, n_comp = ndimage.label(vis, structure=s8)
        if n_comp > 1:
            areas = np.bincount(lbl_v.ravel())
            areas[0] = 0
            main_id = int(np.argmax(areas))
            main_area = int(areas[main_id])

            # Main silhouette mask (largest body + significant structural pieces > 15% of main, e.g. Mecha parts / Aura / Thrown limbs)
            # Exclude tiny floating streaks from main_y1 computation
            sig_mask = np.zeros_like(vis)
            for cid in range(1, n_comp + 1):
                if areas[cid] >= max(160, int(0.02 * main_area)):
                    sig_mask |= (lbl_v == cid)
            if not np.any(sig_mask):
                sig_mask = (lbl_v == main_id)

            ys_sig, _ = np.where(sig_mask)
            main_y1 = int(ys_sig.max())

            # Distance transform from components with area > 30
            large_mask = np.zeros_like(vis)
            for cid in range(1, n_comp + 1):
                if areas[cid] > 30:
                    large_mask |= (lbl_v == cid)
            dist_from_large = ndimage.distance_transform_edt(~large_mask) if np.any(large_mask) else None

            removed_strays = 0
            for cid in range(1, n_comp + 1):
                if cid == main_id:
                    continue
                comp_m = lbl_v == cid
                c_area = int(areas[cid])
                ys_c, xs_c = np.where(comp_m)
                cy0, cy1 = int(ys_c.min()), int(ys_c.max())
                ch = cy1 - cy0 + 1

                is_bottom_smudge = (cy0 >= main_y1 + 4) and (c_area <= 200 or ch <= 6)
                is_tiny_speck = False
                if c_area <= 30 and dist_from_large is not None:
                    min_dist = float(dist_from_large[comp_m].min())
                    # Check gap >= 3px (Euclidean distance >= 3.0 from nearest area>30 pixel)
                    if min_dist >= 3.0:
                        # Keep intentional dissolve dots in mid-body of Death/Destroyed frames unless strictly near bottom/edge
                        if ("Death" in filename or "Destroyed" in filename) and cy0 <= main_y1:
                            is_tiny_speck = False
                        else:
                            is_tiny_speck = True

                # LordKiva fallback streak removal if somehow processed without sheet crop
                is_far_right_kiva_streak = (
                    filename.startswith("LordKiva_Attack")
                    and (int(xs_c.min()) > 170)
                    and c_area < 2500
                )

                if is_bottom_smudge or is_tiny_speck or is_far_right_kiva_streak:
                    # Also clear soft AA alpha (< 13) tightly clinging to that stray fragment
                    stray_aa = ndimage.binary_dilation(comp_m, iterations=2) & (out[:, :, 3] > 0) & (~sig_mask)
                    # Only clear AA pixels not belonging to another non-removed visible component
                    other_vis = vis & (~comp_m)
                    stray_aa &= ~ndimage.binary_dilation(other_vis, iterations=1)
                    out[comp_m | stray_aa] = [0, 0, 0, 0]
                    removed_strays += 1
        else:
            removed_strays = 0
    else:
        removed_strays = 0

    return out, removed_islands, kept_islands, removed_strays


def get_tight_bbox(arr, threshold=ALPHA_VISIBLE_THRESHOLD):
    ys, xs = np.where(arr[:, :, 3] >= threshold)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def resize_rgba_lanczos_clean(arr, new_w, new_h):
    """Premultiply RGB by alpha before LANCZOS resampling to eliminate dark-edge bleed on transparent sprites."""
    f = arr.astype(np.float32)
    alpha = f[:, :, 3:4] / 255.0
    premul = np.empty_like(f)
    premul[:, :, :3] = f[:, :, :3] * alpha
    premul[:, :, 3:4] = f[:, :, 3:4]

    im_p = Image.fromarray(np.clip(np.round(premul), 0, 255).astype(np.uint8), "RGBA")
    resized_p = np.array(im_p.resize((new_w, new_h), Image.Resampling.LANCZOS)).astype(np.float32)

    out = np.zeros_like(resized_p, dtype=np.uint8)
    a_out = np.clip(np.round(resized_p[:, :, 3]), 0, 255).astype(np.uint8)
    valid = a_out > 0
    a_f = resized_p[:, :, 3:4]
    rgb = np.zeros_like(resized_p[:, :, :3])
    mask3 = valid[:, :, None]
    rgb = np.where(mask3, (resized_p[:, :, :3] * 255.0) / np.maximum(a_f, 1.0), 0.0)
    out[:, :, :3] = np.clip(np.round(rgb), 0, 255).astype(np.uint8)
    out[:, :, 3] = a_out
    out[out[:, :, 3] == 0] = [0, 0, 0, 0]
    return out


def pad_tight_crop(arr, padding=SAFETY_PADDING):
    """Tight-crop to alpha >= 13 bbox and center inside transparent canvas with exact 2px padding on all 4 sides."""
    bb = get_tight_bbox(arr)
    if bb is None:
        return arr, (0, 0, arr.shape[1] - 1, arr.shape[0] - 1)
    x0, y0, x1, y1 = bb
    crop = arr[y0 : y1 + 1, x0 : x1 + 1].copy()
    # Zero out sub-threshold outer alpha? Leave pixels inside bbox untouched; canvas outside bbox is pure (0,0,0,0)
    ch, cw = crop.shape[:2]
    canvas = np.zeros((ch + 2 * padding, cw + 2 * padding, 4), dtype=np.uint8)
    canvas[padding : padding + ch, padding : padding + cw] = crop
    return canvas, bb


def read_meta_pivot(meta_path):
    """Read spritePivot {x, y} from a .png.meta file."""
    with open(meta_path, "r", encoding="utf-8") as f:
        text = f.read()
    m = re.search(r"spritePivot:\s*\{x:\s*([-0-9.eE]+),\s*y:\s*([-0-9.eE]+)\}", text)
    if not m:
        return 0.5, 0.0
    return float(m.group(1)), float(m.group(2))


def update_meta_file(meta_path, alignment, pivot_x, pivot_y, max_tex_size=4096):
    """Update TextureImporter YAML settings cleanly while preserving GUIDs, fileIDs, and sub-platform settings."""
    with open(meta_path, "r", encoding="utf-8", newline="") as f:
        text = f.read()

    newline = "\r\n" if "\r\n" in text else "\n"
    px_str = f"{pivot_x:.6f}".rstrip("0").rstrip(".") if abs(pivot_x - round(pivot_x, 2)) > 1e-4 else f"{pivot_x:g}"
    py_str = f"{pivot_y:.6f}".rstrip("0").rstrip(".") if abs(pivot_y - round(pivot_y, 2)) > 1e-4 else f"{pivot_y:g}"
    if abs(pivot_x - 0.5) < 1e-6:
        px_str = "0.5"
    if abs(pivot_y) < 1e-6:
        py_str = "0"
    elif abs(pivot_y - 0.5) < 1e-6:
        py_str = "0.5"

    # Update top-level TextureImporter fields
    text = re.sub(r"(?m)^(\s*)enableMipMap:\s*\d+", r"\g<1>enableMipMap: 0", text)
    text = re.sub(r"(?m)^(\s*)filterMode:\s*-?\d+", r"\g<1>filterMode: 1", text)
    text = re.sub(r"(?m)^(\s*)spriteMode:\s*\d+", r"\g<1>spriteMode: 1", text)
    text = re.sub(r"(?m)^(\s*)spriteMeshType:\s*\d+", r"\g<1>spriteMeshType: 1", text)
    text = re.sub(r"(?m)^(\s*)alignment:\s*\d+", rf"\g<1>alignment: {alignment}", text)
    text = re.sub(
        r"(?m)^(\s*)spritePivot:\s*\{x:\s*[^,]+,\s*y:\s*[^}]+\}",
        rf"\g<1>spritePivot: {{x: {px_str}, y: {py_str}}}",
        text,
    )
    text = re.sub(r"(?m)^(\s*)spritePixelsToUnits:\s*[0-9.]+", rf"\g<1>spritePixelsToUnits: {TARGET_PPU}", text)
    text = re.sub(r"(?m)^(\s*)alphaIsTransparency:\s*\d+", r"\g<1>alphaIsTransparency: 1", text)

    # Update DefaultTexturePlatform block specifically (first platform block with name: DefaultTexturePlatform)
    default_plat_pat = re.compile(
        r"(first:\s*\{\s*: DefaultTexturePlatform\s*\}[\s\S]*?maxTextureSize:\s*)\d+([\s\S]*?textureCompression:\s*)\d+",
        re.MULTILINE,
    )
    if default_plat_pat.search(text):
        text = default_plat_pat.sub(rf"\g<1>{max_tex_size}\g<2>0", text, count=1)

    # Normalize line endings cleanly
    text = text.replace("\r\n", "\n").replace("\n", newline)
    with open(meta_path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def main():
    files = collect_target_pngs()
    print(f"Found {len(files)} sprite PNGs under Characters/ and Enemies/.")

    # Pass 1: Step A + LordKiva_Attack_0 restoration + pre-crop Mecha metadata capture
    processed = {}
    mecha_pivots = {}
    total_islands_removed = 0
    total_islands_kept = 0
    total_strays_removed = 0

    for path in files:
        filename = os.path.basename(path)
        meta_path = path + ".meta"
        orig_im = Image.open(path).convert("RGBA")
        orig_w, orig_h = orig_im.size
        orig_arr = np.array(orig_im)

        # Capture pre-crop pivots for Mecha rigged sub-parts BEFORE modifying image/meta
        if filename in ("Mecha_LeftArm.png", "Mecha_RightArm.png", "Mecha_ChestCore.png") and os.path.isfile(meta_path):
            old_px, old_py = read_meta_pivot(meta_path)
            mecha_pivots[filename] = (old_px, old_py, orig_w, orig_h)

        # Special restoration for clipped LordKiva_Attack_0 from SourceSheets/DiendA.png
        working_arr = orig_arr
        restored_from_sheet = False
        if filename == "LordKiva_Attack_0.png":
            restored = restore_lord_kiva_attack_from_sheet()
            if restored is not None:
                working_arr = restored
                restored_from_sheet = True

        cleaned, rem_i, kept_i, rem_s = step_a_clean_background_islands_strays(working_arr, filename)
        total_islands_removed += rem_i
        total_islands_kept += kept_i
        total_strays_removed += rem_s

        bb = get_tight_bbox(cleaned)
        processed[path] = {
            "filename": filename,
            "group": get_character_group(filename),
            "orig_size": (orig_w, orig_h),
            "arr": cleaned,
            "bbox": bb,
            "restored_from_sheet": restored_from_sheet,
            "islands_removed": rem_i,
            "islands_kept": kept_i,
            "strays_removed": rem_s,
        }

    print(
        f"Step A summary: islands_removed={total_islands_removed}, "
        f"islands_kept={total_islands_kept}, strays_removed={total_strays_removed}"
    )

    # Pass 2: Step B — Compute per-group H_ref and upscale crushed wide action frames
    groups = {}
    for path, info in processed.items():
        groups.setdefault(info["group"], []).append(path)

    upscaled_logs = []
    for grp, gpaths in sorted(groups.items()):
        if grp.startswith("Enemy_"):
            continue

        # Measure H_ref from upright stance frames
        ref_heights = []
        for p in gpaths:
            fn = processed[p]["filename"]
            bb = processed[p]["bbox"]
            if bb is None:
                continue
            bh = bb[3] - bb[1] + 1
            if any(tok in fn for tok in UPRIGHT_REF_TOKENS):
                ref_heights.append(bh)

        if not ref_heights:
            # Fallback to max non-Death height in group (ignoring tiny subparts)
            for p in gpaths:
                fn = processed[p]["filename"]
                bb = processed[p]["bbox"]
                if bb is None or is_skip_upscale(fn):
                    continue
                ref_heights.append(bb[3] - bb[1] + 1)

        if not ref_heights:
            continue
        h_ref = max(ref_heights)

        for p in gpaths:
            info = processed[p]
            fn = info["filename"]
            bb = info["bbox"]
            if bb is None:
                continue
            if is_skip_upscale(fn):
                continue

            x0, y0, x1, y1 = bb
            bw = x1 - x0 + 1
            bh = y1 - y0 + 1
            ratio = bh / float(h_ref)
            if ratio >= 0.78:
                continue

            # Ensure we only upscale wide width-clamped poses or explicitly flagged crushed action frames
            is_width_clamped = (bw >= 260) and ((bw / float(max(1, bh))) > 1.25)
            is_explicit = fn in EXPLICIT_CRUSHED_FRAMES
            if not (is_width_clamped or is_explicit):
                continue

            target_factor = 1.00 if fn in STANDING_WIDE_FRAMES else 0.92
            target_h = int(round(h_ref * target_factor))
            scale = target_h / float(bh)
            new_bw = max(1, int(round(bw * scale)))
            new_bh = max(1, int(round(bh * scale)))

            crop_arr = info["arr"][y0 : y1 + 1, x0 : x1 + 1].copy()
            upscaled = resize_rgba_lanczos_clean(crop_arr, new_bw, new_bh)
            info["arr"] = upscaled
            info["bbox"] = get_tight_bbox(upscaled)
            upscaled_logs.append(f"  {fn}: {bw}x{bh} (ratio={ratio:.2f}, H_ref={h_ref}) -> {new_bw}x{new_bh} (target={target_factor:.2f}*H_ref)")

    print(f"Step B upscaled frames ({len(upscaled_logs)}):")
    for log_line in upscaled_logs:
        print(log_line)

    # Pass 3: Step C — Tight crop + exact 2px padding + exact Mecha pivot recomputation + .meta sync
    print("\nStep C Mecha recomputed pivots:")
    for path, info in processed.items():
        fn = info["filename"]
        final_arr, pre_pad_bb = pad_tight_crop(info["arr"], SAFETY_PADDING)
        new_h, new_w = final_arr.shape[:2]

        # Enforce strict (0,0,0,0) outer 2px borders
        final_arr[0:SAFETY_PADDING, :, :] = 0
        final_arr[-SAFETY_PADDING:, :, :] = 0
        final_arr[:, 0:SAFETY_PADDING, :] = 0
        final_arr[:, -SAFETY_PADDING:, :] = 0

        Image.fromarray(final_arr, "RGBA").save(path, format="PNG")
        info["final_size"] = (new_w, new_h)
        info["final_bbox"] = get_tight_bbox(final_arr)

        meta_path = path + ".meta"
        if not os.path.isfile(meta_path):
            continue

        if fn in mecha_pivots:
            old_px, old_py, orig_w, orig_h = mecha_pivots[fn]
            x0, y0, x1, y1 = pre_pad_bb
            old_from_left = old_px * orig_w
            old_from_bottom = old_py * orig_h
            crop_bottom_from_bottom = orig_h - (y1 + 1)
            new_px = (old_from_left - x0 + SAFETY_PADDING) / float(new_w)
            new_py = (old_from_bottom - crop_bottom_from_bottom + SAFETY_PADDING) / float(new_h)

            if fn == "Mecha_ChestCore.png":
                # Preserve Center (0.5, 0.5) alignment for chest core, while printing exact recomputed coordinates
                print(
                    f"  {fn}: orig={orig_w}x{orig_h} oldPivot=({old_px:.4f},{old_py:.4f}) "
                    f"bbox=({x0},{y0}..{x1},{y1}) newSize={new_w}x{new_h} "
                    f"exactJointPivot=({new_px:.6f}, {new_py:.6f}) -> alignment=Center (0.5, 0.5)"
                )
                update_meta_file(meta_path, alignment=0, pivot_x=0.5, pivot_y=0.5)
            else:
                print(
                    f"  {fn}: orig={orig_w}x{orig_h} oldPivot=({old_px:.4f},{old_py:.4f}) "
                    f"bbox=({x0},{y0}..{x1},{y1}) newSize={new_w}x{new_h} "
                    f"recomputedPivot=({new_px:.6f}, {new_py:.6f}) -> alignment=Custom (9)"
                )
                update_meta_file(meta_path, alignment=9, pivot_x=new_px, pivot_y=new_py)
        else:
            update_meta_file(meta_path, alignment=7, pivot_x=0.5, pivot_y=0.0)

    # Final verification across every PNG
    errors = []
    for path in files:
        im = Image.open(path).convert("RGBA")
        arr = np.array(im)
        h, w = arr.shape[:2]
        if int(arr[0, 0, 3]) != 0:
            errors.append(f"{path}: pixel(0,0) alpha={arr[0,0,3]}")
        if np.any(arr[0, :, 3] != 0) or np.any(arr[-1, :, 3] != 0) or np.any(arr[:, 0, 3] != 0) or np.any(arr[:, -1, 3] != 0):
            errors.append(f"{path}: non-zero alpha on outer border")
        bb = get_tight_bbox(arr, ALPHA_VISIBLE_THRESHOLD)
        if bb is None:
            errors.append(f"{path}: completely transparent!")
        else:
            x0, y0, x1, y1 = bb
            pad_l, pad_t, pad_r, pad_b = x0, y0, w - 1 - x1, h - 1 - y1
            if (pad_l, pad_t, pad_r, pad_b) != (SAFETY_PADDING, SAFETY_PADDING, SAFETY_PADDING, SAFETY_PADDING):
                errors.append(f"{path}: wrong padding ({pad_l},{pad_t},{pad_r},{pad_b})")

    if errors:
        print("\nVERIFICATION ERRORS:")
        for e in errors:
            print(" ", e)
        raise SystemExit(1)
    else:
        print(f"\nALL {len(files)} SPRITES VERIFIED CLEAN: exact 2px padding on all 4 sides, border alpha == 0.")


if __name__ == "__main__":
    main()
