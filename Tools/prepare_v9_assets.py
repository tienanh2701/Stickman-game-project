import os
import pathlib
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = pathlib.Path(r"c:\Users\Admin\Documents\Tài liệu của TAnh\2026.1\VibeApp\OOP Project")
SPRITES = ROOT / "Stickman game" / "Assets" / "Sprites"


def force_transparent_border(arr, pad=2):
    arr[:pad, :, :] = 0
    arr[-pad:, :, :] = 0
    arr[:, :pad, :] = 0
    arr[:, -pad:, :] = 0
    return arr


def remove_white_bg(im_rgba, lum_thresh=240, soft_band=30):
    arr = np.array(im_rgba).astype(np.float32)
    r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    sat = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)

    # Pure near-white, low-saturation pixels become transparent smoothly
    neutral = np.clip((lum - (lum_thresh - soft_band)) / float(soft_band), 0.0, 1.0)
    low_sat = np.clip(1.0 - (sat - 12.0) / 30.0, 0.0, 1.0)
    bg_factor = neutral * low_sat
    new_a = a * (1.0 - bg_factor)
    arr[:, :, 3] = np.clip(new_a, 0, 255)
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def build_eren_block():
    out_dir = SPRITES / "Characters" / "Eren"
    out_dir.mkdir(parents=True, exist_ok=True)
    src = Image.open(ROOT / "Protect frame" / "eren protect.jfif").convert("RGBA")
    arr = np.array(src).astype(np.float32)

    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    sat = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)

    # Remove white background aggressively while keeping black character and orange/cyan sparks
    bg = (lum > 236) & (sat < 26)
    soft = (lum > 205) & (sat < 26) & (~bg)
    alpha = np.ones_like(lum) * 255.0
    alpha[bg] = 0.0
    alpha[soft] = (236.0 - lum[soft]) / 31.0 * 255.0
    arr[:, :, 3] = np.clip(alpha, 0, 255)

    clean = Image.fromarray(arr.astype(np.uint8), "RGBA")
    bbox = clean.getbbox()
    cropped = clean.crop(bbox)

    # Fit into 300x300 matching Eren_Idle_0 (height ~235px, bottom pad = 16px)
    cw, ch = cropped.size
    target_h = 236
    target_w = max(1, int(round(cw * (target_h / float(ch)))))
    if target_w > 276:
        scale = 276.0 / cw
        target_w = 276
        target_h = max(1, int(round(ch * scale)))

    resized0 = cropped.resize((target_w, target_h), Image.Resampling.LANCZOS)
    for idx, (w_mult, h_mult) in enumerate([(1.0, 1.0), (1.03, 0.97)]):
        fw = max(1, int(round(target_w * w_mult)))
        fh = max(1, int(round(target_h * h_mult)))
        fr = resized0.resize((fw, fh), Image.Resampling.LANCZOS) if idx > 0 else resized0
        canvas = Image.new("RGBA", (300, 300), (0, 0, 0, 0))
        x0 = (300 - fw) // 2
        y0 = 300 - 16 - fh  # bottom_pad = 16 matches Eren_Idle_0.png
        canvas.paste(fr, (x0, max(2, y0)), fr)
        out_arr = force_transparent_border(np.array(canvas))
        Image.fromarray(out_arr, "RGBA").save(out_dir / f"Eren_Block_{idx}.png")


def build_cyan_shield():
    out_dir = SPRITES / "Combat"
    out_dir.mkdir(parents=True, exist_ok=True)
    # Reconstruct translucent cyan energy barrier dome inspired by shield.jfif
    # Use radial distance + angular energy crackle extracted from shield.jfif rim
    src = Image.open(ROOT / "Protect frame" / "shield.jfif").convert("L").resize((256, 256), Image.Resampling.BILINEAR)
    src_arr = np.array(src).astype(np.float32)

    size = 256
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    cx, cy = 128.0, 128.0
    dx, dy = (xx - cx) / 104.0, (yy - cy) / 112.0
    dist = np.sqrt(dx * dx + dy * dy)
    ang = np.arctan2(dy, dx)

    # Energy ripple modulated by source rim detail & harmonics
    wave = 0.032 * np.sin(ang * 9.0) + 0.018 * np.cos(ang * 17.0) + (1.0 - src_arr / 255.0) * 0.025
    d = dist + wave

    rgba = np.zeros((size, size, 4), dtype=np.float32)
    # Interior shield dome (translucent cyan-blue)
    inside = d <= 1.0
    rim = np.exp(-((d - 0.94) ** 2) / 0.0045)
    inner_rim = np.exp(-((d - 0.84) ** 2) / 0.009)
    crackle = rim + 0.45 * inner_rim

    # Highlight arc top-left
    shine = np.clip(1.0 - np.sqrt(((xx - 92.0) / 52.0) ** 2 + ((yy - 82.0) / 48.0) ** 2), 0.0, 1.0)

    rgba[:, :, 0] = np.clip(45 + 170 * crackle + 110 * shine, 0, 255)
    rgba[:, :, 1] = np.clip(195 + 60 * crackle + 55 * shine, 0, 255)
    rgba[:, :, 2] = 255.0

    alpha = np.zeros((size, size), dtype=np.float32)
    alpha[inside] = 78.0 + 48.0 * (d[inside] ** 2) + 45.0 * shine[inside]
    alpha += 165.0 * crackle
    alpha[d > 1.05] *= np.clip(1.0 - (d[d > 1.05] - 1.05) / 0.08, 0.0, 1.0)
    alpha[d > 1.13] = 0.0
    rgba[:, :, 3] = np.clip(alpha, 0, 240)

    rgba = force_transparent_border(rgba.astype(np.uint8))
    Image.fromarray(rgba, "RGBA").save(out_dir / "FX_BlockShield_Cyan.png")


def build_item_icons():
    out_dir = SPRITES / "UI" / "Items"
    out_dir.mkdir(parents=True, exist_ok=True)

    # 1. Senzu Bean (remove purple starburst background)
    senzu_src = Image.open(ROOT / "Items" / "Đậu thần Senzu.png").convert("RGBA")
    s_arr = np.array(senzu_src).astype(np.float32)
    r, g, b = s_arr[:, :, 0], s_arr[:, :, 1], s_arr[:, :, 2]
    # Purple bg has b > g + 15 and r > g - 5; bean has g > b + 20 or dark outline near bean
    is_purple = (b > g + 10) & (r > g - 10)
    is_bean_core = (g > b + 12) & (g > r * 0.85)
    # Dilate bean core to preserve its crisp dark outline
    bean_mask = Image.fromarray((is_bean_core * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(5))
    keep = (np.array(bean_mask) > 128) & (~is_purple)
    s_arr[:, :, 3] = np.where(keep, 255.0, 0.0)
    s_clean = Image.fromarray(s_arr.astype(np.uint8), "RGBA")
    bbox = s_clean.getbbox()
    if bbox:
        s_clean = s_clean.crop(bbox)
    canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    scaled = s_clean.resize((208, int(round(208 * s_clean.height / max(1, s_clean.width)))), Image.Resampling.LANCZOS)
    canvas.paste(scaled, ((256 - scaled.width) // 2, (256 - scaled.height) // 2), scaled)
    Image.fromarray(force_transparent_border(np.array(canvas)), "RGBA").save(out_dir / "Icon_SenzuBean.png")

    # 2. Spinal Fluid Syringe & Ultra Divine Water
    for src_name, dst_name, target_box in [
        ("Dịch tủy titan.jfif", "Icon_SpinalFluid.png", 228),
        ("Siêu thánh thủy.jfif", "Icon_UltraDivineWater.png", 220),
    ]:
        im = Image.open(ROOT / "Items" / src_name).convert("RGBA")
        clean = remove_white_bg(im, lum_thresh=244, soft_band=26)
        bbox = clean.getbbox()
        if bbox:
            clean = clean.crop(bbox)
        scale = min(target_box / float(clean.width), target_box / float(clean.height))
        nw, nh = max(1, int(round(clean.width * scale))), max(1, int(round(clean.height * scale)))
        resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        canvas.paste(resized, ((256 - nw) // 2, (256 - nh) // 2), resized)
        Image.fromarray(force_transparent_border(np.array(canvas)), "RGBA").save(out_dir / dst_name)

    # 3. Golden Coin Icon (Icon_CoinXu.png)
    coin = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    draw = ImageDraw.Draw(coin)
    draw.ellipse((8, 8, 120, 120), fill=(255, 200, 35, 255), outline=(170, 105, 10, 255), width=6)
    draw.ellipse((20, 20, 108, 108), fill=(255, 224, 80, 255), outline=(215, 145, 20, 255), width=3)
    draw.rectangle((56, 36, 72, 92), fill=(255, 248, 185, 255))
    draw.ellipse((28, 24, 54, 48), fill=(255, 255, 235, 190))
    Image.fromarray(force_transparent_border(np.array(coin)), "RGBA").save(out_dir / "Icon_CoinXu.png")

    # 4. Locked Slot Padlock Silhouette (Icon_LockedSlot.png)
    lock = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lock)
    ld.arc((40, 20, 88, 68), 180, 360, fill=(180, 190, 210, 220), width=10)
    ld.rounded_rectangle((32, 54, 96, 106), radius=10, fill=(130, 142, 165, 220), outline=(210, 220, 235, 230), width=3)
    ld.ellipse((58, 70, 70, 82), fill=(35, 40, 52, 240))
    ld.polygon([(60, 80), (68, 80), (71, 95), (57, 95)], fill=(35, 40, 52, 240))
    Image.fromarray(force_transparent_border(np.array(lock)), "RGBA").save(out_dir / "Icon_LockedSlot.png")


def build_item_vfx():
    out_dir = SPRITES / "Combat" / "Items"
    out_dir.mkdir(parents=True, exist_ok=True)

    # 1. Senzu Heal (3 pulsing green flame frames)
    heal_im = remove_white_bg(Image.open(ROOT / "Items animation" / "Đậu senzu - heal.jfif").convert("RGBA"), lum_thresh=242, soft_band=35)
    hb = heal_im.getbbox()
    if hb:
        heal_im = heal_im.crop(hb)
    for i, s in enumerate([0.92, 1.0, 1.08]):
        w, h = int(260 * s), int(260 * s)
        r = heal_im.resize((w, h), Image.Resampling.LANCZOS)
        c = Image.new("RGBA", (320, 320), (0, 0, 0, 0))
        c.paste(r, ((320 - w) // 2, (320 - h) // 2), r)
        Image.fromarray(force_transparent_border(np.array(c)), "RGBA").save(out_dir / f"VFX_SenzuHeal_{i}.png")

    # 2. Spinal Fluid Ribcage (remove top-right viewer icons, tiny black stickman, and white bg)
    rib_raw = Image.open(ROOT / "Items animation" / "Dịch tủy titan.png").convert("RGBA")
    r_arr = np.array(rib_raw).astype(np.float32)
    # Remove top-right viewer buttons (x > 540, y < 70)
    r_arr[:72, 535:, 3] = 0.0
    # Mask tiny stickman figure around bottom-right of chest (x in [380..480], y in [312..470]) plus edge halo
    yy, xx = np.mgrid[0:r_arr.shape[0], 0:r_arr.shape[1]]
    lum = 0.299 * r_arr[:, :, 0] + 0.587 * r_arr[:, :, 1] + 0.114 * r_arr[:, :, 2]
    sat = np.maximum(np.maximum(r_arr[:, :, 0], r_arr[:, :, 1]), r_arr[:, :, 2]) - np.minimum(np.minimum(r_arr[:, :, 0], r_arr[:, :, 1]), r_arr[:, :, 2])
    stickman_roi = (xx >= 380) & (xx <= 480) & (yy >= 312) & (yy <= 470) & (lum < 95) & (sat < 45)
    halo_img = Image.fromarray((stickman_roi.astype(np.uint8) * 255), "L").filter(ImageFilter.MaxFilter(11))
    halo_mask = (np.array(halo_img) > 0) & (xx >= 374) & (xx <= 486) & (yy >= 306) & (yy <= 476)
    r_arr[stickman_roi | (halo_mask & (sat < 60)), 3] = 0.0
    rib_clean = remove_white_bg(Image.fromarray(r_arr.astype(np.uint8), "RGBA"), lum_thresh=238, soft_band=35)
    # Semi-transparent armor overlay (~78% alpha)
    rc_arr = np.array(rib_clean).astype(np.float32)
    rc_arr[:, :, 3] *= 0.80
    rib_clean = Image.fromarray(rc_arr.astype(np.uint8), "RGBA")
    rb = rib_clean.getbbox()
    if rb:
        rib_clean = rib_clean.crop(rb)
    for i, s in enumerate([0.96, 1.02]):
        scale = min(330.0 / max(1, rib_clean.width), 290.0 / max(1, rib_clean.height)) * s
        nw, nh = int(rib_clean.width * scale), int(rib_clean.height * scale)
        r = rib_clean.resize((nw, nh), Image.Resampling.LANCZOS)
        c = Image.new("RGBA", (380, 340), (0, 0, 0, 0))
        c.paste(r, ((380 - nw) // 2, (340 - nh) // 2), r)
        Image.fromarray(force_transparent_border(np.array(c)), "RGBA").save(out_dir / f"VFX_SpinalRibcage_{i}.png")

    # 3. Ultra Divine Water Aura (vibrant electric cyan-blue & fiery crimson-red energy frames)
    rage_raw = remove_white_bg(Image.open(ROOT / "Items animation" / "Siêu thánh thủy - rage.jfif").convert("RGBA"), lum_thresh=242, soft_band=35)
    rb = rage_raw.getbbox()
    if rb:
        rage_raw = rage_raw.crop(rb)
    base_arr = np.array(rage_raw).astype(np.float32)
    for i, (scale_f, r_shift, g_shift, b_shift) in enumerate([
        (0.94, 0.75, 0.95, 1.35),
        (1.00, 0.25, 1.20, 1.60),
        (1.06, 1.60, 0.40, 0.45),
    ]):
        t_arr = base_arr.copy()
        t_arr[:, :, 0] = np.clip(t_arr[:, :, 0] * r_shift, 0, 255)
        t_arr[:, :, 1] = np.clip(t_arr[:, :, 1] * g_shift, 0, 255)
        t_arr[:, :, 2] = np.clip(t_arr[:, :, 2] * b_shift, 0, 255)
        t_arr[:, :, 3] *= 0.84
        fr = Image.fromarray(t_arr.astype(np.uint8), "RGBA")
        w, h = int(280 * scale_f), int(280 * scale_f)
        fr = fr.resize((w, h), Image.Resampling.LANCZOS)
        c = Image.new("RGBA", (340, 340), (0, 0, 0, 0))
        c.paste(fr, ((340 - w) // 2, (340 - h) // 2), fr)
        Image.fromarray(force_transparent_border(np.array(c)), "RGBA").save(out_dir / f"VFX_UltraWaterAura_{i}.png")


def build_hazard_sprites():
    out_dir = SPRITES / "Combat" / "Hazards"
    out_dir.mkdir(parents=True, exist_ok=True)

    def save(im, name):
        Image.fromarray(force_transparent_border(np.array(im)), "RGBA").save(out_dir / name)

    # 1. Wooden Eaves (AoT roof platform)
    eaves = Image.new("RGBA", (320, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(eaves)
    d.polygon([(8, 16), (312, 16), (300, 48), (20, 48)], fill=(125, 78, 46, 255), outline=(45, 26, 14, 255))
    for x in range(36, 300, 36):
        d.line((x, 16, x - 6, 48), fill=(72, 42, 22, 255), width=3)
    d.rectangle((14, 10, 306, 22), fill=(165, 108, 62, 255), outline=(45, 26, 14, 255), width=2)
    save(eaves, "Hazard_WoodenEaves.png")

    # 2. Gunpowder Barrel
    barrel = Image.new("RGBA", (128, 160), (0, 0, 0, 0))
    d = ImageDraw.Draw(barrel)
    d.rounded_rectangle((24, 16, 104, 144), radius=14, fill=(145, 52, 34, 255), outline=(38, 18, 12, 255), width=4)
    d.rectangle((22, 36, 106, 50), fill=(85, 88, 96, 255), outline=(30, 32, 36, 255), width=2)
    d.rectangle((22, 110, 106, 124), fill=(85, 88, 96, 255), outline=(30, 32, 36, 255), width=2)
    d.polygon([(64, 56), (48, 86), (62, 86), (54, 106), (80, 74), (66, 74)], fill=(255, 210, 45, 255))
    save(barrel, "Hazard_GunpowderBarrel.png")

    # 3. Rolling Ammo Cart
    cart = Image.new("RGBA", (192, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(cart)
    d.rectangle((20, 36, 172, 92), fill=(115, 76, 44, 255), outline=(35, 20, 12, 255), width=4)
    for bx in [38, 80, 122]:
        d.rectangle((bx, 20, bx + 32, 40), fill=(155, 140, 70, 255), outline=(45, 38, 18, 255), width=2)
    for wx in [48, 144]:
        d.ellipse((wx - 22, 76, wx + 22, 120), fill=(62, 48, 36, 255), outline=(22, 16, 12, 255), width=4)
        d.ellipse((wx - 7, 91, wx + 7, 105), fill=(160, 160, 170, 255))
    save(cart, "Hazard_AmmoCart.png")

    # 4. Wall Cannon Turret
    cannon = Image.new("RGBA", (192, 144), (0, 0, 0, 0))
    d = ImageDraw.Draw(cannon)
    d.rectangle((36, 86, 146, 126), fill=(95, 64, 38, 255), outline=(30, 20, 12, 255), width=3)
    d.rectangle((56, 38, 174, 78), fill=(52, 58, 68, 255), outline=(22, 25, 30, 255), width=4)
    d.ellipse((28, 30, 86, 88), fill=(68, 74, 84, 255), outline=(22, 25, 30, 255), width=4)
    d.ellipse((54, 92, 96, 134), fill=(48, 42, 36, 255), outline=(18, 15, 12, 255), width=4)
    save(cannon, "Hazard_WallCannon.png")

    # 5. Floating Rock (Dragon Ball)
    rock = Image.new("RGBA", (320, 96), (0, 0, 0, 0))
    d = ImageDraw.Draw(rock)
    d.polygon([(18, 16), (302, 16), (274, 62), (165, 86), (48, 60)], fill=(156, 106, 68, 255), outline=(58, 34, 18, 255))
    d.rectangle((14, 12, 306, 30), fill=(198, 142, 92, 255), outline=(58, 34, 18, 255), width=3)
    save(rock, "Hazard_FloatingRock.png")

    # 6. Ki Geyser
    geyser = Image.new("RGBA", (192, 192), (0, 0, 0, 0))
    d = ImageDraw.Draw(geyser)
    d.polygon([(36, 182), (62, 146), (130, 146), (156, 182)], fill=(96, 72, 56, 255), outline=(40, 28, 20, 255))
    d.polygon([(68, 148), (96, 18), (124, 148)], fill=(65, 220, 255, 185))
    d.polygon([(80, 148), (96, 46), (112, 148)], fill=(200, 250, 255, 215))
    save(geyser, "Hazard_KiGeyser.png")

    # 7. High-Gravity Purple Zone
    grav = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(grav)
    d.ellipse((14, 14, 242, 242), fill=(145, 45, 225, 95), outline=(205, 120, 255, 210), width=6)
    d.ellipse((48, 48, 208, 208), outline=(225, 165, 255, 190), width=4)
    save(grav, "Hazard_GravityZone.png")

    # 8. Ancient Stone Pillar
    pillar = Image.new("RGBA", (128, 320), (0, 0, 0, 0))
    d = ImageDraw.Draw(pillar)
    d.rectangle((16, 16, 112, 42), fill=(158, 148, 134, 255), outline=(52, 46, 40, 255), width=3)
    d.rectangle((26, 42, 102, 278), fill=(134, 124, 110, 255), outline=(48, 42, 36, 255), width=3)
    for vx in [46, 64, 82]:
        d.line((vx, 48, vx, 272), fill=(95, 86, 76, 255), width=3)
    d.rectangle((14, 278, 114, 308), fill=(158, 148, 134, 255), outline=(52, 46, 40, 255), width=3)
    save(pillar, "Hazard_StonePillar.png")

    # 9. Cyber / Stone Platform (World 3)
    plat = Image.new("RGBA", (320, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(plat)
    d.polygon([(12, 14), (308, 14), (290, 50), (30, 50)], fill=(54, 66, 94, 255), outline=(18, 24, 38, 255))
    d.rectangle((14, 14, 306, 24), fill=(55, 215, 245, 240))
    save(plat, "Hazard_CyberPlatform.png")

    # 10. Aurora Portal
    portal = Image.new("RGBA", (160, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(portal)
    d.ellipse((14, 12, 146, 244), fill=(35, 195, 245, 145), outline=(145, 245, 255, 240), width=8)
    d.ellipse((34, 38, 126, 218), fill=(155, 85, 255, 165), outline=(225, 245, 255, 220), width=4)
    save(portal, "Hazard_AuroraPortal.png")

    # 11. Low Laser Turret
    turret = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(turret)
    d.polygon([(22, 116), (40, 48), (88, 48), (106, 116)], fill=(65, 72, 86, 255), outline=(22, 24, 30, 255))
    d.rectangle((36, 28, 92, 56), fill=(95, 102, 118, 255), outline=(22, 24, 30, 255), width=3)
    d.ellipse((52, 34, 76, 52), fill=(255, 55, 65, 255))
    save(turret, "Hazard_LaserTurret.png")

    # 12. EMP Terminal
    emp = Image.new("RGBA", (144, 176), (0, 0, 0, 0))
    d = ImageDraw.Draw(emp)
    d.rectangle((26, 24, 118, 164), fill=(48, 58, 76, 255), outline=(18, 24, 34, 255), width=4)
    d.rectangle((36, 36, 108, 86), fill=(30, 190, 245, 255), outline=(180, 245, 255, 255), width=2)
    d.polygon([(72, 42), (58, 62), (72, 62), (66, 80), (88, 56), (74, 56)], fill=(255, 245, 110, 255))
    d.rectangle((56, 6, 88, 24), fill=(90, 225, 255, 255))
    save(emp, "Hazard_EmpTerminal.png")


if __name__ == "__main__":
    build_eren_block()
    build_cyan_shield()
    build_item_icons()
    build_item_vfx()
    build_hazard_sprites()
    print("prepare_v9_assets.py completed successfully!")
