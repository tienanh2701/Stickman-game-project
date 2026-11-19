import math
import os
import random
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

MAPS_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Assets", "Sprites", "Maps"
)

W, H = 2048, 768
HORIZON_Y = 580


def blend_vertical_gradient(base_img, top_color, mid_color, bot_color, split_y=HORIZON_Y):
    """Apply a smooth atmospheric color overlay onto the base RGBA image."""
    grad = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = grad.load()
    for y in range(H):
        if y < split_y:
            t = y / max(1, split_y)
            r = int(top_color[0] * (1 - t) + mid_color[0] * t)
            g = int(top_color[1] * (1 - t) + mid_color[1] * t)
            b = int(top_color[2] * (1 - t) + mid_color[2] * t)
            a = int(top_color[3] * (1 - t) + mid_color[3] * t)
        else:
            t = (y - split_y) / max(1, H - split_y)
            r = int(mid_color[0] * (1 - t) + bot_color[0] * t)
            g = int(mid_color[1] * (1 - t) + bot_color[1] * t)
            b = int(mid_color[2] * (1 - t) + bot_color[2] * t)
            a = int(mid_color[3] * (1 - t) + bot_color[3] * t)
        row_col = (r, g, b, a)
        for x in range(W):
            px[x, y] = row_col
    return Image.alpha_composite(base_img, grad)


def draw_lightning(draw, start_x, start_y, end_x, end_y, color, glow_color, width=5, seed=1):
    rng = random.Random(seed)
    pts = [(start_x, start_y)]
    steps = 10
    for i in range(1, steps):
        t = i / steps
        jitter = rng.randint(-42, 42)
        pts.append((
            int(start_x + (end_x - start_x) * t + jitter),
            int(start_y + (end_y - start_y) * t)
        ))
    pts.append((end_x, end_y))
    draw.line(pts, fill=glow_color, width=width * 3)
    draw.line(pts, fill=color, width=width)
    return pts


def draw_smoke_plume(layer, cx, cy, scale_x=1.0, scale_y=1.0, color=(40, 35, 35, 130), seed=0):
    rng = random.Random(seed)
    d = ImageDraw.Draw(layer)
    for i in range(9):
        rx = int((36 + i * 16) * scale_x)
        ry = int((22 + i * 11) * scale_y)
        px = cx + int(math.sin(i * 0.9 + seed) * 28 * scale_x) + rng.randint(-10, 10)
        py = cy - int(i * 34 * scale_y)
        alpha = max(25, color[3] - i * 10)
        d.ellipse([px - rx, py - ry, px + rx, py + ry], fill=(color[0], color[1], color[2], alpha))


def apply_bright_stickman_contrast(img, brightness=1.18, mist_alpha=46):
    """Ensure map background stays luminous so black Stickman silhouettes pop clearly."""
    bright = ImageEnhance.Brightness(img).enhance(brightness)
    mist = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    px = mist.load()
    for y in range(H):
        # Stronger soft light lift behind ground combat band (y=260..768)
        t = max(0.0, min(1.0, (y - 180) / float(H - 180)))
        a = int(mist_alpha * (0.45 + 0.55 * t))
        row_col = (255, 255, 255, a)
        for x in range(W):
            px[x, y] = row_col
    return Image.alpha_composite(bright, mist)


def generate_w1_maps():
    base = Image.open(os.path.join(MAPS_DIR, "Map_World1_AoT.png")).convert("RGBA")

    # Stage 1: Brightened Shiganshina Dawn for crisp Stickman contrast
    s1 = apply_bright_stickman_contrast(base, brightness=1.12, mist_alpha=42)
    s1.save(os.path.join(MAPS_DIR, "Map_W1_S1.png"))

    # Stage 2: Sunset Garrison (Warm bright orange haze)
    s2 = ImageEnhance.Color(base).enhance(1.12)
    s2 = ImageEnhance.Brightness(s2).enhance(1.14)
    s2 = blend_vertical_gradient(
        s2,
        top_color=(235, 115, 55, 70),
        mid_color=(255, 185, 105, 68),
        bot_color=(165, 120, 88, 60),
        split_y=520
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    # Glowing setting sun
    sun_x, sun_y = 1420, 195
    d.ellipse([sun_x - 135, sun_y - 135, sun_x + 135, sun_y + 135], fill=(255, 200, 105, 95))
    d.ellipse([sun_x - 78, sun_y - 78, sun_x + 78, sun_y + 78], fill=(255, 240, 175, 220))
    for cx in [310, 890, 1730]:
        draw_smoke_plume(ov, cx, 555, 1.1, 1.2, (110, 86, 76, 85), seed=cx)
    # Wooden/stone cannon barricades & craters
    for bx in [180, 560, 1050, 1520, 1900]:
        d.polygon([(bx - 65, 605), (bx + 65, 605), (bx + 45, 555), (bx - 45, 555)], fill=(125, 92, 68, 170))
        d.rectangle([bx - 30, 536, bx + 30, 560], fill=(145, 106, 78, 175))
        d.line([(bx - 40, 560), (bx + 40, 602)], fill=(75, 52, 36, 175), width=5)
        d.line([(bx + 40, 560), (bx - 40, 602)], fill=(75, 52, 36, 175), width=5)
        d.ellipse([bx + 90, 635, bx + 185, 672], fill=(95, 72, 54, 85))
    ov = ov.filter(ImageFilter.GaussianBlur(0.8))
    s2 = Image.alpha_composite(s2, ov)
    apply_bright_stickman_contrast(s2, brightness=1.08, mist_alpha=44).save(os.path.join(MAPS_DIR, "Map_W1_S2.png"))

    # Stage 3 [ELITE - Female Titan]: Giant Forest Daylight Mist
    s3 = ImageEnhance.Brightness(base).enhance(1.06)
    s3 = blend_vertical_gradient(
        s3,
        top_color=(110, 165, 205, 75),
        mid_color=(125, 195, 180, 65),
        bot_color=(140, 175, 165, 62),
        split_y=540
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    # Giant forest trunks behind rooftops (muted twilight blue-green so black sprites pop)
    for tx in [120, 410, 740, 1120, 1460, 1820]:
        tw = 42 + (tx % 28)
        d.rectangle([tx - tw // 2, 0, tx + tw // 2, 595], fill=(64, 88, 92, 145))
        d.rectangle([tx - tw // 2 + 8, 0, tx - tw // 2 + 16, 595], fill=(98, 132, 134, 120))
        d.ellipse([tx - 165, -40, tx + 165, 195], fill=(56, 108, 96, 155))
    # Scout Regiment smoke signal flares (green & red)
    for fx, fcol in [(540, (90, 245, 140, 165)), (1580, (250, 95, 95, 165))]:
        d.line([(fx, 565), (fx + 18, 180)], fill=(245, 255, 245, 160), width=4)
        draw_smoke_plume(ov, fx + 18, 240, 0.9, 0.9, fcol, seed=fx)
    # ODM grapple cables crossing upper sky
    d.line([(0, 140), (740, 260), (1460, 170), (2048, 230)], fill=(215, 235, 245, 130), width=2)
    d.line([(200, 290), (1120, 190), (1820, 310)], fill=(215, 235, 245, 115), width=2)
    s3 = Image.alpha_composite(s3, ov)
    apply_bright_stickman_contrast(s3, brightness=1.10, mist_alpha=52).save(os.path.join(MAPS_DIR, "Map_W1_S3.png"))

    # Stage 4 [BOSS - War Hammer Titan]: Liberio Bright Crimson Sky
    s4 = ImageEnhance.Contrast(base).enhance(1.08)
    s4 = ImageEnhance.Brightness(s4).enhance(1.08)
    s4 = blend_vertical_gradient(
        s4,
        top_color=(185, 65, 78, 72),
        mid_color=(235, 115, 98, 65),
        bot_color=(165, 110, 105, 58),
        split_y=540
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    # Solar-eclipse corona & golden Titan lightning
    d.ellipse([930, 55, 1118, 243], fill=(255, 95, 95, 145))
    d.ellipse([968, 93, 1080, 205], fill=(65, 28, 35, 215))
    draw_lightning(d, 480, 0, 525, 580, (255, 250, 175, 245), (255, 195, 60, 140), width=6, seed=11)
    draw_lightning(d, 1540, 0, 1495, 585, (255, 250, 175, 245), (255, 195, 60, 140), width=6, seed=27)
    # War Hammer crystal spires along horizon
    for cx, ch in [(230, 190), (650, 240), (1260, 220), (1760, 260), (1950, 160)]:
        d.polygon([(cx - 46, 610), (cx, 610 - ch), (cx + 46, 610)], fill=(95, 225, 255, 185))
        d.polygon([(cx - 22, 610), (cx + 6, 610 - ch + 20), (cx + 28, 610)], fill=(225, 252, 255, 210))
    s4 = Image.alpha_composite(s4, ov)
    apply_bright_stickman_contrast(s4, brightness=1.10, mist_alpha=52).save(os.path.join(MAPS_DIR, "Map_W1_S4.png"))


def generate_w2_maps():
    base = Image.open(os.path.join(MAPS_DIR, "Map_World2_DB.png")).convert("RGBA")

    # Stage 1: Bright Wasteland Morning
    s1 = apply_bright_stickman_contrast(base, brightness=1.12, mist_alpha=40)
    s1.save(os.path.join(MAPS_DIR, "Map_W2_S1.png"))

    # Stage 2: Frieza Landing Zone
    s2 = ImageEnhance.Brightness(base).enhance(1.12)
    s2 = blend_vertical_gradient(
        s2,
        top_color=(165, 115, 215, 65),
        mid_color=(245, 175, 115, 60),
        bot_color=(175, 130, 95, 55),
        split_y=520
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for px, py, pr in [(640, 565, 72), (1490, 550, 88)]:
        d.ellipse([px - pr - 35, py + 15, px + pr + 35, py + 62], fill=(105, 75, 52, 125))
        d.ellipse([px - pr, py - pr, px + pr, py + pr], fill=(225, 232, 240, 230), outline=(95, 108, 125, 230), width=4)
        d.ellipse([px - pr // 2, py - pr // 2, px + pr // 3, py + pr // 5], fill=(235, 80, 110, 215))
    for sx, sy in [(340, 160), (1120, 125), (1760, 185)]:
        d.ellipse([sx - 95, sy - 22, sx + 95, sy + 22], fill=(125, 110, 155, 200))
        d.arc([sx - 45, sy - 38, sx + 45, sy + 6], 180, 360, fill=(195, 235, 255, 215), width=4)
    s2 = Image.alpha_composite(s2, ov)
    apply_bright_stickman_contrast(s2, brightness=1.08, mist_alpha=44).save(os.path.join(MAPS_DIR, "Map_W2_S2.png"))

    # Stage 3 [ELITE - Nappa]: Electrified Badlands
    s3 = ImageEnhance.Contrast(base).enhance(1.08)
    s3 = ImageEnhance.Brightness(s3).enhance(1.08)
    s3 = blend_vertical_gradient(
        s3,
        top_color=(135, 95, 195, 75),
        mid_color=(195, 130, 225, 65),
        bot_color=(165, 125, 150, 58),
        split_y=520
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for gx in [280, 840, 1380, 1840]:
        d.rectangle([gx - 28, 110, gx + 28, 600], fill=(255, 215, 95, 90))
        d.rectangle([gx - 12, 70, gx + 12, 600], fill=(255, 248, 175, 145))
        draw_lightning(d, gx, 40, gx + 14, 590, (255, 250, 195, 220), (215, 145, 255, 115), width=4, seed=gx)
    for rx, ry, rs in [(460, 240, 28), (690, 310, 19), (1120, 220, 32), (1600, 290, 24), (1940, 215, 22)]:
        d.polygon([(rx - rs, ry), (rx - rs // 3, ry - rs), (rx + rs, ry - rs // 4), (rx + rs // 2, ry + rs)], fill=(125, 98, 110, 200))
    s3 = Image.alpha_composite(s3, ov)
    apply_bright_stickman_contrast(s3, brightness=1.08, mist_alpha=48).save(os.path.join(MAPS_DIR, "Map_W2_S3.png"))

    # Stage 4 [BOSS - Broly]: Green Ki Supernova Overcast
    s4 = ImageEnhance.Contrast(base).enhance(1.10)
    s4 = ImageEnhance.Brightness(s4).enhance(1.06)
    s4 = blend_vertical_gradient(
        s4,
        top_color=(85, 175, 115, 78),
        mid_color=(235, 135, 95, 68),
        bot_color=(165, 108, 92, 60),
        split_y=535
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for mx, my in [(240, 110), (760, 165), (1290, 95), (1740, 150)]:
        d.line([(mx - 170, my - 95), (mx, my)], fill=(115, 255, 135, 135), width=16)
        d.line([(mx - 135, my - 75), (mx, my)], fill=(220, 255, 215, 225), width=6)
        d.ellipse([mx - 24, my - 24, mx + 24, my + 24], fill=(185, 255, 175, 235))
    for kx in range(80, W - 80, 260):
        d.line([(kx, 620), (kx + 55, 650), (kx + 115, 635), (kx + 175, 690)], fill=(255, 120, 45, 185), width=6)
        d.line([(kx, 620), (kx + 55, 650), (kx + 115, 635), (kx + 175, 690)], fill=(255, 230, 125, 210), width=2)
    s4 = Image.alpha_composite(s4, ov)
    apply_bright_stickman_contrast(s4, brightness=1.08, mist_alpha=50).save(os.path.join(MAPS_DIR, "Map_W2_S4.png"))


def generate_w3_maps():
    base = Image.open(os.path.join(MAPS_DIR, "Map_World3_Diend.png")).convert("RGBA")

    # Stage 1: Brightened Neo City Atrium
    s1 = apply_bright_stickman_contrast(base, brightness=1.24, mist_alpha=54)
    s1.save(os.path.join(MAPS_DIR, "Map_W3_S1.png"))

    # Stage 2: Dimensional Veil Outpost
    s2 = ImageEnhance.Brightness(base).enhance(1.22)
    s2 = blend_vertical_gradient(
        s2,
        top_color=(95, 165, 235, 78),
        mid_color=(115, 220, 240, 68),
        bot_color=(135, 165, 210, 62),
        split_y=520
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for px in [380, 1024, 1668]:
        d.rectangle([px - 52, 160, px + 52, 580], fill=(175, 240, 255, 85), outline=(215, 250, 255, 210), width=4)
        for gy in range(195, 560, 45):
            d.line([(px - 52, gy), (px + 52, gy)], fill=(230, 255, 255, 140), width=2)
    for tx, th in [(160, 290), (720, 330), (1320, 310), (1890, 270)]:
        d.rectangle([tx - 38, 575 - th, tx + 38, 575], fill=(82, 104, 145, 155))
        d.line([(tx, 575 - th), (tx, 575 - th - 65)], fill=(120, 245, 255, 215), width=4)
    s2 = Image.alpha_composite(s2, ov)
    apply_bright_stickman_contrast(s2, brightness=1.15, mist_alpha=54).save(os.path.join(MAPS_DIR, "Map_W3_S2.png"))

    # Stage 3 [ELITE - Shadow Moon]: Gorgom Aurora Sanctuary
    s3 = ImageEnhance.Brightness(base).enhance(1.18)
    s3 = blend_vertical_gradient(
        s3,
        top_color=(85, 118, 175, 85),
        mid_color=(98, 188, 172, 72),
        bot_color=(125, 148, 170, 66),
        split_y=530
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    ex, ey = 1024, 175
    d.ellipse([ex - 135, ey - 135, ex + 135, ey + 135], fill=(95, 255, 195, 130))
    d.ellipse([ex - 102, ey - 102, ex + 102, ey + 102], fill=(145, 255, 225, 215))
    d.ellipse([ex - 84, ey - 84, ex + 84, ey + 84], fill=(32, 42, 64, 225))
    for gx in [320, 720, 1328, 1728]:
        d.arc([gx - 55, 600, gx + 55, 670], 10, 170, fill=(135, 255, 210, 180), width=4)
    s3 = Image.alpha_composite(s3, ov)
    apply_bright_stickman_contrast(s3, brightness=1.14, mist_alpha=56).save(os.path.join(MAPS_DIR, "Map_W3_S3.png"))

    # Stage 4 [BOSS - Mecha Colossus]: Dai-Shocker Reactor Atrium
    s4 = ImageEnhance.Contrast(base).enhance(1.06)
    s4 = ImageEnhance.Brightness(s4).enhance(1.16)
    s4 = blend_vertical_gradient(
        s4,
        top_color=(175, 78, 135, 80),
        mid_color=(225, 108, 148, 70),
        bot_color=(145, 135, 170, 64),
        split_y=535
    )
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    gx, gy, gr = 1024, 340, 235
    d.ellipse([gx - gr, gy - gr, gx + gr, gy + gr], outline=(245, 95, 125, 135), width=18)
    d.ellipse([gx - gr // 2, gy - gr // 2, gx + gr // 2, gy + gr // 2], fill=(255, 110, 145, 85), outline=(125, 245, 255, 195), width=6)
    for deg in range(0, 360, 30):
        rad = math.radians(deg)
        tx = gx + int(math.cos(rad) * (gr + 22))
        ty = gy + int(math.sin(rad) * (gr + 22))
        d.rectangle([tx - 16, ty - 16, tx + 16, ty + 16], fill=(115, 72, 105, 165))
    d.polygon([(380, 585), (120, 0), (260, 0)], fill=(255, 115, 145, 75))
    d.polygon([(1668, 585), (1788, 0), (1928, 0)], fill=(120, 245, 255, 75))
    s4 = Image.alpha_composite(s4, ov)
    apply_bright_stickman_contrast(s4, brightness=1.14, mist_alpha=56).save(os.path.join(MAPS_DIR, "Map_W3_S4.png"))


def main():
    os.makedirs(MAPS_DIR, exist_ok=True)
    generate_w1_maps()
    generate_w2_maps()
    generate_w3_maps()
    generated = [
        "Map_World1_AoT.png", "Map_W1_S1.png", "Map_W1_S2.png", "Map_W1_S3.png", "Map_W1_S4.png",
        "Map_World2_DB.png", "Map_W2_S1.png", "Map_W2_S2.png", "Map_W2_S3.png", "Map_W2_S4.png",
        "Map_World3_Diend.png", "Map_W3_S1.png", "Map_W3_S2.png", "Map_W3_S3.png", "Map_W3_S4.png",
    ]
    for name in generated:
        p = os.path.join(MAPS_DIR, name)
        with Image.open(p) as im:
            print(f"{name}: size={im.size} mode={im.mode}")


if __name__ == "__main__":
    main()
