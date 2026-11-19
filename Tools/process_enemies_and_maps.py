import math
import os
from collections import deque
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WORKSPACE = os.path.dirname(ROOT)
ENEMY_SRC = os.path.join(WORKSPACE, "Enemies")
MAP_SRC = os.path.join(WORKSPACE, "Maps")

ENEMY_OUT = os.path.join(ROOT, "Assets", "Sprites", "Enemies")
MAP_OUT = os.path.join(ROOT, "Assets", "Sprites", "Maps")
COMBAT_OUT = os.path.join(ROOT, "Assets", "Sprites", "Combat")


def ensure_dirs():
    for d in [
        MAP_OUT,
        COMBAT_OUT,
        os.path.join(ENEMY_OUT, "World1_AoT"),
        os.path.join(ENEMY_OUT, "World2_DB"),
        os.path.join(ENEMY_OUT, "World3_Diend"),
    ]:
        os.makedirs(d, exist_ok=True)


def remove_white_bg(img: Image.Image, thresh: int = 232) -> Image.Image:
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    rgb = arr[:, :, :3].astype(np.int16)
    min_c = rgb.min(axis=2)
    max_c = rgb.max(axis=2)
    is_white = (min_c >= thresh) & ((max_c - min_c) <= 28)

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
    fringe = adj & (~visited) & (min_c > 180) & ((max_c - min_c) < 34)
    if np.any(fringe):
        mean_rgb = rgb.mean(axis=2)
        new_alpha = np.clip((248.0 - mean_rgb) * 4.0, 0, 255).astype(np.uint8)
        arr[fringe, 3] = new_alpha[fringe]
        a_f = np.maximum(new_alpha[fringe].astype(np.float32) / 255.0, 0.08)
        for c in range(3):
            chan = arr[fringe, c].astype(np.float32)
            arr[fringe, c] = np.clip((chan - 255.0 * (1.0 - a_f)) / a_f, 0, 255).astype(np.uint8)
    return Image.fromarray(arr, mode="RGBA")


def strip_gray_ui_circles(img: Image.Image, top_frac: float = 0.42, right_frac: float = 0.25) -> Image.Image:
    """Removes semi-transparent grey download/copy UI buttons stamped in the top-right corner of mobile screenshot exports."""
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    h, w, _ = arr.shape
    y_lim = int(h * top_frac)
    x_start = int(w * (1.0 - right_frac))
    sub = arr[:y_lim, x_start:, :3].astype(np.int16)
    sat = sub.max(axis=2) - sub.min(axis=2)
    mean_v = sub.mean(axis=2)
    # Grey overlay buttons + white icon strokes inside them
    is_ui_grey = (sat <= 14) & (mean_v >= 130) & (mean_v <= 245)
    # Only wipe where actual grey circle region exists (connected to non-white grey blob >= 145)
    arr[:y_lim, x_start:][is_ui_grey] = (255, 255, 255, 255)
    return Image.fromarray(arr, mode="RGBA")


def trim_alpha(img: Image.Image) -> Image.Image:
    bb = img.getbbox()
    return img if bb is None else img.crop(bb)


def normalize_enemy_sprite(
    crop: Image.Image,
    out_path: str,
    target_h: int = 235,
    canvas_h: int = 300,
    min_canvas_w: int = 300,
    is_rgba: bool = False,
    flip_x: bool = False,
):
    clean = crop if is_rgba else remove_white_bg(crop)
    clean = trim_alpha(clean)
    if flip_x:
        clean = clean.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    sw, sh = clean.size
    if sw == 0 or sh == 0:
        Image.new("RGBA", (min_canvas_w, canvas_h), (0, 0, 0, 0)).save(out_path)
        return
    scale = min(target_h / float(sh), (canvas_h - 24) / float(sh), 5.0)
    nw, nh = max(1, int(round(sw * scale))), max(1, int(round(sh * scale)))
    resized = clean.resize((nw, nh), Image.Resampling.LANCZOS)
    cw = max(min_canvas_w, nw + 40)
    canvas = Image.new("RGBA", (cw, canvas_h), (0, 0, 0, 0))
    canvas.paste(resized, ((cw - nw) // 2, canvas_h - 16 - nh), resized)
    canvas.save(out_path)


def create_tilable_world_bg(src_path: str, out_path: str, crop_box=None, remove_top_right_ui: bool = False, target_size=(2048, 768)):
    im = Image.open(src_path).convert("RGBA")
    if crop_box is not None:
        im = im.crop(crop_box)
    if remove_top_right_ui:
        # Inpaint top-right aurora/rounded-corner artifact smoothly from left clean aurora columns
        arr = np.array(im, dtype=np.uint8)
        h, w, _ = arr.shape
        # Clean rows/cols: top-right UI occupies y < 58, x > 88
        ref_x0 = max(10, 88 - (w - 88))
        for y in range(min(60, h)):
            left_slice = arr[y, 30:86].astype(np.float32)
            # Tile/mirror horizontally to fill x=86..w cleanly
            mirrored = left_slice[::-1]
            needed = w - 86
            rep = int(math.ceil(needed / float(len(mirrored))))
            patch = np.tile(mirrored, (rep, 1))[:needed]
            # Blend smooth fade at x=86..102 and bottom y=50..60
            arr[y, 86:w] = np.clip(patch, 0, 255).astype(np.uint8)
        # Smooth out top-left rounded corner (y<10, x<10)
        for y in range(min(10, h)):
            arr[y, :10] = arr[y, 12]
        im = Image.fromarray(arr, mode="RGBA")

    # Resize single panel height to target_size[1] keeping crisp aspect ratio
    th = target_size[1]
    tw_pan = max(128, int(round(im.width * (th / float(im.height)))))
    panel = im.resize((tw_pan, th), Image.Resampling.LANCZOS)
    panel_flip = panel.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

    # Tile alternating mirror panels [panel, panel_flip, panel, ...] to fill target_size[0]
    canvas = Image.new("RGBA", target_size, (0, 0, 0, 255))
    x = 0
    idx = 0
    while x < target_size[0]:
        src_p = panel if (idx % 2 == 0) else panel_flip
        canvas.paste(src_p, (x, 0))
        x += tw_pan
        idx += 1

    canvas.convert("RGB").save(out_path, optimize=True)


def split_equal_cols(img: Image.Image, n_cols: int, y0: int, y1: int, x0: int = 0, x1: int | None = None) -> list[Image.Image]:
    if x1 is None:
        x1 = img.width
    step = (x1 - x0) / float(n_cols)
    crops = []
    for i in range(n_cols):
        cx0 = int(round(x0 + i * step))
        cx1 = int(round(x0 + (i + 1) * step))
        crops.append(img.crop((cx0, y0, cx1, y1)))
    return crops


def process_world1_aot():
    out = os.path.join(ENEMY_OUT, "World1_AoT")

    # 1. Pure Titan (Creep.png: 596x90, 6 cols: Rush, Lunge, Grab, Bite, Hit, Vaporize)
    creep = strip_gray_ui_circles(Image.open(os.path.join(ENEMY_SRC, "Attack on titan world", "Creep.png")).convert("RGBA"), 0.45, 0.25)
    # Remove floating "BAM!" above col 2 & top title
    c_cols = split_equal_cols(creep, 6, 13, 66)
    normalize_enemy_sprite(c_cols[0], os.path.join(out, "PureTitan_Move_0.png"), 225)
    normalize_enemy_sprite(c_cols[0].resize((int(c_cols[0].width * 1.03), int(c_cols[0].height * 0.96))), os.path.join(out, "PureTitan_Move_1.png"), 218)
    normalize_enemy_sprite(c_cols[1], os.path.join(out, "PureTitan_Lunge_0.png"), 225)
    normalize_enemy_sprite(c_cols[2], os.path.join(out, "PureTitan_Grab_0.png"), 225)
    normalize_enemy_sprite(c_cols[3], os.path.join(out, "PureTitan_Bite_0.png"), 225)
    normalize_enemy_sprite(c_cols[4], os.path.join(out, "PureTitan_Hit_0.png"), 225)
    normalize_enemy_sprite(c_cols[5], os.path.join(out, "PureTitan_Death_0.png"), 185)

    # 2. Beast Titan (Range creep.png: 600x72, 6 cols: Crouch/PickUp, DrawBack, HeavyThrow, BoulderVolley, Stagger, Death/Vapor)
    beast = Image.open(os.path.join(ENEMY_SRC, "Attack on titan world", "Range creep.png")).convert("RGBA")
    b_cols = split_equal_cols(beast, 6, 8, 56)
    # Derive Move_0/1 from tall upright HeavyThrow/DrawBack silhouette without projectile dust on col 2
    b_draw_clean = remove_white_bg(b_cols[1])
    b_throw_body = remove_white_bg(b_cols[2])
    normalize_enemy_sprite(b_draw_clean, os.path.join(out, "BeastTitan_Move_0.png"), 245, is_rgba=True)
    normalize_enemy_sprite(b_cols[0], os.path.join(out, "BeastTitan_Crouch_0.png"), 205)
    normalize_enemy_sprite(b_cols[1], os.path.join(out, "BeastTitan_DrawBack_0.png"), 245)
    normalize_enemy_sprite(b_cols[2], os.path.join(out, "BeastTitan_HeavyThrow_0.png"), 245)
    normalize_enemy_sprite(b_draw_clean.resize((int(b_draw_clean.width * 1.04), int(b_draw_clean.height * 0.96))), os.path.join(out, "BeastTitan_Move_1.png"), 238, is_rgba=True)
    normalize_enemy_sprite(b_cols[4], os.path.join(out, "BeastTitan_Hit_0.png"), 240)
    normalize_enemy_sprite(b_cols[5], os.path.join(out, "BeastTitan_Death_0.png"), 200)
    # Extract boulder volley FX from col 3
    normalize_enemy_sprite(b_cols[3], os.path.join(COMBAT_OUT, "FX_BeastBoulderVolley.png"), 120, canvas_h=160, min_canvas_w=200)

    # 3. Female Titan (Elite1.png: 594x103, 6 cols: FightStance, DashRush, HighKick, HardenedPunch, Counter/Parry, Collapse)
    female = Image.open(os.path.join(ENEMY_SRC, "Attack on titan world", "Elite1.png")).convert("RGBA")
    f_cols = split_equal_cols(female, 6, 8, 72)
    normalize_enemy_sprite(f_cols[0], os.path.join(out, "FemaleTitan_FightStance_0.png"), 240)
    normalize_enemy_sprite(f_cols[0].resize((int(f_cols[0].width * 1.02), int(f_cols[0].height * 0.97))), os.path.join(out, "FemaleTitan_FightStance_1.png"), 234)
    normalize_enemy_sprite(f_cols[1], os.path.join(out, "FemaleTitan_DashRush_0.png"), 235)
    normalize_enemy_sprite(f_cols[2], os.path.join(out, "FemaleTitan_HighKick_0.png"), 245)
    normalize_enemy_sprite(f_cols[3], os.path.join(out, "FemaleTitan_HardenedPunch_0.png"), 245)
    normalize_enemy_sprite(f_cols[4], os.path.join(out, "FemaleTitan_Parry_0.png"), 240)
    normalize_enemy_sprite(f_cols[5], os.path.join(out, "FemaleTitan_Death_0.png"), 185)

    # 4. Cart Titan Support Turret (Elite2.png: 195x102, 2 cols -> Face Left toward arena center or Right by default)
    cart = Image.open(os.path.join(ENEMY_SRC, "Attack on titan world", "Elite2.png")).convert("RGBA")
    ct_cols = split_equal_cols(cart, 2, 12, 74)
    normalize_enemy_sprite(ct_cols[0], os.path.join(out, "CartTitan_Idle_0.png"), 215)
    normalize_enemy_sprite(ct_cols[1], os.path.join(out, "CartTitan_Fire_0.png"), 215)

    # 5. War Hammer Boss (Boss.png: 397x92, 4 cols: HammerSmash, GroundSpikes, Hit(with blue crystal left!), Destroyed)
    wh = Image.open(os.path.join(ENEMY_SRC, "Attack on titan world", "Boss.png")).convert("RGBA")
    w_cols = split_equal_cols(wh, 4, 4, 65)
    normalize_enemy_sprite(w_cols[0], os.path.join(out, "WarHammer_HammerSmash_0.png"), 255)
    normalize_enemy_sprite(w_cols[1], os.path.join(out, "WarHammer_GroundSpikes_0.png"), 255)
    normalize_enemy_sprite(w_cols[2], os.path.join(out, "WarHammer_Hit_0.png"), 255)
    normalize_enemy_sprite(w_cols[3], os.path.join(out, "WarHammer_Destroyed_0.png"), 225)
    # Clean Idle pose from HammerSmash standing body or Hit without crystal
    # Extract glowing blue CrystalCore from left side of w_cols[2] (pixels x < 34% of w_cols[2])
    wc2 = remove_white_bg(w_cols[2])
    cryst_crop = wc2.crop((0, int(wc2.height * 0.35), int(wc2.width * 0.32), wc2.height))
    normalize_enemy_sprite(cryst_crop, os.path.join(COMBAT_OUT, "FX_CrystalCore.png"), 180, canvas_h=220, min_canvas_w=180, is_rgba=True)


def process_world2_db():
    out = os.path.join(ENEMY_OUT, "World2_DB")

    # 1. Saibamen (Saibaman Kamikaze.png: 993x221, x>=22, y=10..161)
    sai = Image.open(os.path.join(ENEMY_SRC, "Dragon ball world", "Saibaman Kamikaze.png")).convert("RGBA")
    s_cols = split_equal_cols(sai, 6, 10, 161, x0=20, x1=990)
    normalize_enemy_sprite(s_cols[0], os.path.join(out, "Saibamen_Move_0.png"), 215)
    normalize_enemy_sprite(s_cols[0].resize((int(s_cols[0].width * 1.04), int(s_cols[0].height * 0.96))), os.path.join(out, "Saibamen_Move_1.png"), 208)
    normalize_enemy_sprite(s_cols[1], os.path.join(out, "Saibamen_WindUp_0.png"), 225)
    normalize_enemy_sprite(s_cols[2], os.path.join(out, "Saibamen_DashSlash_0.png"), 220)
    normalize_enemy_sprite(s_cols[3], os.path.join(out, "Saibamen_Headbutt_0.png"), 220)
    normalize_enemy_sprite(s_cols[4], os.path.join(out, "Saibamen_Hit_0.png"), 220)
    normalize_enemy_sprite(s_cols[5], os.path.join(out, "Saibamen_Death_0.png"), 180)

    # 2. Frieza Soldier (Range Creep.png: 598x135, y=4..93, strip top-right UI circles)
    frieza = strip_gray_ui_circles(Image.open(os.path.join(ENEMY_SRC, "Dragon ball world", "Range Creep.png")).convert("RGBA"), 0.35, 0.25)
    fr_cols = split_equal_cols(frieza, 6, 3, 93)
    normalize_enemy_sprite(fr_cols[0], os.path.join(out, "FriezaSoldier_Move_0.png"), 225)
    normalize_enemy_sprite(fr_cols[0].resize((int(fr_cols[0].width * 1.03), int(fr_cols[0].height * 0.96))), os.path.join(out, "FriezaSoldier_Move_1.png"), 218)
    normalize_enemy_sprite(fr_cols[1], os.path.join(out, "FriezaSoldier_LockOn_0.png"), 225)
    normalize_enemy_sprite(fr_cols[2], os.path.join(out, "FriezaSoldier_KiBlast_0.png"), 225)
    normalize_enemy_sprite(fr_cols[3], os.path.join(out, "FriezaSoldier_ContFire_0.png"), 225)
    normalize_enemy_sprite(fr_cols[4], os.path.join(out, "FriezaSoldier_Hit_0.png"), 225)
    normalize_enemy_sprite(fr_cols[5], os.path.join(out, "FriezaSoldier_Death_0.png"), 185)

    # 3. Nappa (Elite.png: 615x140, x>=10, y=10..103)
    nappa = Image.open(os.path.join(ENEMY_SRC, "Dragon ball world", "Elite.png")).convert("RGBA")
    n_cols = split_equal_cols(nappa, 6, 10, 103, x0=10, x1=615)
    normalize_enemy_sprite(n_cols[0], os.path.join(out, "Nappa_Advance_0.png"), 240)
    normalize_enemy_sprite(n_cols[0].resize((int(n_cols[0].width * 1.03), int(n_cols[0].height * 0.97))), os.path.join(out, "Nappa_Advance_1.png"), 234)
    normalize_enemy_sprite(n_cols[1], os.path.join(out, "Nappa_ChargeUp_0.png"), 255)
    normalize_enemy_sprite(n_cols[2], os.path.join(out, "Nappa_HeavyHaymaker_0.png"), 240)
    normalize_enemy_sprite(n_cols[3], os.path.join(out, "Nappa_GroundEruption_0.png"), 255)
    normalize_enemy_sprite(n_cols[4], os.path.join(out, "Nappa_Hit_0.png"), 240)
    normalize_enemy_sprite(n_cols[5], os.path.join(out, "Nappa_Death_0.png"), 185)

    # 4. Berserker Broly Boss (Boss Broly.png: 620x146, x>=14, y=3..110, top-right UI circles)
    broly = strip_gray_ui_circles(Image.open(os.path.join(ENEMY_SRC, "Dragon ball world", "Boss Broly.png")).convert("RGBA"), 0.35, 0.25)
    br_cols = split_equal_cols(broly, 6, 3, 110, x0=14, x1=620)
    normalize_enemy_sprite(br_cols[0], os.path.join(out, "Broly_HeavyStomp_0.png"), 265)
    normalize_enemy_sprite(br_cols[0].resize((int(br_cols[0].width * 1.03), int(br_cols[0].height * 0.97))), os.path.join(out, "Broly_HeavyStomp_1.png"), 258)
    normalize_enemy_sprite(br_cols[1], os.path.join(out, "Broly_Roar_0.png"), 265)
    normalize_enemy_sprite(br_cols[2], os.path.join(out, "Broly_LariatCharge_0.png"), 255)
    normalize_enemy_sprite(br_cols[3], os.path.join(out, "Broly_GiganticSlam_0.png"), 265)
    normalize_enemy_sprite(br_cols[4], os.path.join(out, "Broly_Stagger_0.png"), 255)
    normalize_enemy_sprite(br_cols[5], os.path.join(out, "Broly_Defeated_0.png"), 215)


def process_world3_diend():
    out = os.path.join(ENEMY_OUT, "World3_Diend")

    # 1. Shocker Combatman + Kasshin (Normal creep.png: 599x160)
    norm = strip_gray_ui_circles(Image.open(os.path.join(ENEMY_SRC, "Diend world", "Normal creep.png")).convert("RGBA"), 0.22, 0.25)
    sh_cols = split_equal_cols(norm, 6, 1, 52)
    normalize_enemy_sprite(sh_cols[0], os.path.join(out, "Shocker_Move_0.png"), 225)
    normalize_enemy_sprite(sh_cols[0].resize((int(sh_cols[0].width * 1.03), int(sh_cols[0].height * 0.96))), os.path.join(out, "Shocker_Move_1.png"), 218)
    normalize_enemy_sprite(sh_cols[1], os.path.join(out, "Shocker_WindUp_0.png"), 230)
    normalize_enemy_sprite(sh_cols[2], os.path.join(out, "Shocker_RapidStab_0.png"), 225)
    normalize_enemy_sprite(sh_cols[3], os.path.join(out, "Shocker_LungeStrike_0.png"), 225)
    normalize_enemy_sprite(sh_cols[4], os.path.join(out, "Shocker_Hit_0.png"), 225)
    normalize_enemy_sprite(sh_cols[5], os.path.join(out, "Shocker_Death_0.png"), 175)

    ka_cols = split_equal_cols(norm, 6, 76, 129)
    normalize_enemy_sprite(ka_cols[0], os.path.join(out, "Kasshin_Move_0.png"), 230)
    normalize_enemy_sprite(ka_cols[0].resize((int(ka_cols[0].width * 1.03), int(ka_cols[0].height * 0.96))), os.path.join(out, "Kasshin_Move_1.png"), 223)
    normalize_enemy_sprite(ka_cols[1], os.path.join(out, "Kasshin_PlantStaff_0.png"), 230)
    normalize_enemy_sprite(ka_cols[2], os.path.join(out, "Kasshin_LaserSweep_0.png"), 230)
    normalize_enemy_sprite(ka_cols[3], os.path.join(out, "Kasshin_EnergySpear_0.png"), 230)
    normalize_enemy_sprite(ka_cols[4], os.path.join(out, "Kasshin_Hit_0.png"), 230)
    normalize_enemy_sprite(ka_cols[5], os.path.join(out, "Kasshin_Death_0.png"), 185)

    # 2. Shadow Moon (Elite.png: 585x84, mask clipped "xuyen thau" top word at x=290..405,y<14)
    sm = Image.open(os.path.join(ENEMY_SRC, "Diend world", "Elite.png")).convert("RGBA")
    sm_d = ImageDraw.Draw(sm)
    sm_d.rectangle([280, 0, 415, 13], fill=(255, 255, 255, 255))
    sm_cols = split_equal_cols(sm, 6, 4, 67)
    normalize_enemy_sprite(sm_cols[0], os.path.join(out, "ShadowMoon_Walk_0.png"), 240)
    normalize_enemy_sprite(sm_cols[0].resize((int(sm_cols[0].width * 1.03), int(sm_cols[0].height * 0.97))), os.path.join(out, "ShadowMoon_Walk_1.png"), 234)
    normalize_enemy_sprite(sm_cols[1], os.path.join(out, "ShadowMoon_SwordCharge_0.png"), 240)
    normalize_enemy_sprite(sm_cols[2], os.path.join(out, "ShadowMoon_CrescentSlash_0.png"), 245)
    normalize_enemy_sprite(sm_cols[3], os.path.join(out, "ShadowMoon_Teleport_0.png"), 235)
    normalize_enemy_sprite(sm_cols[4], os.path.join(out, "ShadowMoon_Hit_0.png"), 240)
    normalize_enemy_sprite(sm_cols[5], os.path.join(out, "ShadowMoon_Death_0.png"), 190)

    # 3. Dai-Shocker x Zanjack Giant Robot (Boss.jfif: 204x245) -> Extract Clean Hull + LeftArm + RightArm + Glowing ChestCore!
    robot = remove_white_bg(Image.open(os.path.join(ENEMY_SRC, "Diend world", "Boss.jfif")).convert("RGBA"), thresh=226)
    rw, rh = robot.size  # 204 x 245
    normalize_enemy_sprite(robot, os.path.join(out, "Mecha_Full.png"), 276, canvas_h=300, is_rgba=True)

    # Modular parts normalized cleanly around their visual sockets so multi-part boss explodes/separates dynamically!
    # LeftArm (screen-left blaster cannon arm): x=0..64, y=45..185
    left_arm = robot.crop((0, 45, 64, 185))
    normalize_enemy_sprite(left_arm, os.path.join(out, "Mecha_LeftArm.png"), 190, canvas_h=240, min_canvas_w=200, is_rgba=True)
    # RightArm (screen-right claw/cannon arm): x=138..204, y=45..185
    right_arm = robot.crop((138, 45, rw, 185))
    normalize_enemy_sprite(right_arm, os.path.join(out, "Mecha_RightArm.png"), 190, canvas_h=240, min_canvas_w=200, is_rgba=True)
    # Body (center torso + head + legs): x=50..154, y=0..245
    torso = robot.crop((50, 0, 154, rh))
    normalize_enemy_sprite(torso, os.path.join(out, "Mecha_Body.png"), 276, canvas_h=300, is_rgba=True)

    # ChestCore: crisp glowing cyan reactor orb matching Mecha's chest core
    core = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    cd = ImageDraw.Draw(core)
    cd.ellipse([8, 8, 120, 120], fill=(20, 155, 255, 110))
    cd.ellipse([22, 22, 106, 106], fill=(70, 225, 255, 220), outline=(190, 250, 255, 255), width=4)
    cd.ellipse([42, 42, 86, 86], fill=(235, 255, 255, 255))
    core.save(os.path.join(out, "Mecha_ChestCore.png"))


def generate_new_combat_fx():
    # 1. GroundSpikes (AoT War Hammer)
    sp = Image.new("RGBA", (160, 220), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sp)
    sd.polygon([(20, 212), (48, 56), (78, 212)], fill=(195, 200, 208, 245), outline=(40, 42, 46, 255))
    sd.polygon([(58, 212), (90, 14), (124, 212)], fill=(225, 230, 236, 255), outline=(35, 38, 42, 255))
    sd.polygon([(102, 212), (128, 82), (150, 212)], fill=(175, 182, 190, 245), outline=(40, 42, 46, 255))
    sp.save(os.path.join(COMBAT_OUT, "FX_GroundSpikes.png"))

    # 2. Nappa Giant Storm Pillar (golden/amber energy column)
    pil = Image.new("RGBA", (160, 360), (0, 0, 0, 0))
    pd = ImageDraw.Draw(pil)
    pd.rectangle([24, 12, 136, 348], fill=(255, 190, 35, 150))
    pd.rectangle([44, 8, 116, 352], fill=(255, 235, 110, 220))
    pd.rectangle([62, 8, 98, 352], fill=(255, 255, 230, 255))
    pil.filter(ImageFilter.GaussianBlur(radius=3)).save(os.path.join(COMBAT_OUT, "FX_NappaPillar.png"))

    # 3. Broly Green Ki Shield aura dome
    sh = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    shd = ImageDraw.Draw(sh)
    shd.ellipse([10, 10, 246, 246], fill=(55, 235, 95, 80), outline=(110, 255, 140, 235), width=8)
    shd.ellipse([26, 26, 230, 230], outline=(210, 255, 215, 180), width=4)
    sh.save(os.path.join(COMBAT_OUT, "FX_GreenKiShield.png"))

    # 4. Shadow Moon Red Crescent Slash Wave
    cw = Image.new("RGBA", (180, 220), (0, 0, 0, 0))
    cwd = ImageDraw.Draw(cw)
    cwd.polygon([(40, 12), (126, 55), (156, 110), (126, 165), (40, 208), (94, 150), (108, 110), (94, 70)], fill=(235, 25, 35, 235), outline=(255, 180, 180, 255))
    cwd.polygon([(62, 32), (118, 72), (132, 110), (118, 148), (62, 188), (102, 110)], fill=(255, 225, 225, 220))
    cw.save(os.path.join(COMBAT_OUT, "FX_CrescentSlashWave.png"))

    # 5. Kasshin Red Piercing Energy Spear
    ks = Image.new("RGBA", (220, 72), (0, 0, 0, 0))
    ksd = ImageDraw.Draw(ks)
    ksd.polygon([(8, 36), (160, 22), (212, 36), (160, 50)], fill=(255, 50, 50, 235), outline=(255, 200, 200, 255))
    ksd.polygon([(35, 36), (165, 29), (198, 36), (165, 43)], fill=(255, 240, 240, 255))
    ks.save(os.path.join(COMBAT_OUT, "FX_KasshinSpear.png"))


def process_maps():
    create_tilable_world_bg(
        os.path.join(MAP_SRC, "Attack on titan.png"),
        os.path.join(MAP_OUT, "Map_World1_AoT.png"),
        crop_box=None,
        remove_top_right_ui=False,
    )
    create_tilable_world_bg(
        os.path.join(MAP_SRC, "DragonBall.png"),
        os.path.join(MAP_OUT, "Map_World2_DB.png"),
        crop_box=None,
        remove_top_right_ui=False,
    )
    # Diend.png (235x372): crop out bottom text footer y>=354, remove top-right UI overlay buttons
    create_tilable_world_bg(
        os.path.join(MAP_SRC, "Diend.png"),
        os.path.join(MAP_OUT, "Map_World3_Diend.png"),
        crop_box=(0, 0, 235, 354),
        remove_top_right_ui=True,
    )


if __name__ == "__main__":
    ensure_dirs()
    process_world1_aot()
    process_world2_db()
    process_world3_diend()
    generate_new_combat_fx()
    process_maps()
    print("Enemy sprites, Combat FX & 3 World Maps processed cleanly!")
