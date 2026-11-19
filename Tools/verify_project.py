import os
import re
import sys
import glob
import tempfile
import subprocess
import xml.etree.ElementTree as ET
from PIL import Image

PROJECT_ROOT = r"c:\Users\Admin\Documents\Tài liệu của TAnh\2026.1\VibeApp\OOP Project\Stickman game"
CSC_DLL = r"C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll"

def parse_csproj(csproj_path):
    tree = ET.parse(csproj_path)
    root = tree.getroot()
    ns = {"ms": "http://schemas.microsoft.com/developer/msbuild/2003"}

    defines = []
    lang_version = "9.0"
    refs = []

    for dc in root.findall(".//ms:DefineConstants", ns):
        if dc.text:
            defines.extend([d.strip() for d in dc.text.split(";") if d.strip()])
    for lv in root.findall(".//ms:LangVersion", ns):
        if lv.text:
            lang_version = lv.text.strip()
    for ref in root.findall(".//ms:Reference", ns):
        hp = ref.find("ms:HintPath", ns)
        if hp is not None and hp.text:
            p = hp.text.strip()
            if not os.path.isabs(p):
                p = os.path.normpath(os.path.join(PROJECT_ROOT, p))
            if os.path.exists(p):
                refs.append(p)

    # Fallback to mscorlib/System from Unity MonoBleedingEdge / Managed if needed
    seen = set()
    unique_refs = []
    for r in refs:
        k = r.lower()
        if k not in seen:
            seen.add(k)
            unique_refs.append(r)
    return defines, lang_version, unique_refs

def compile_roslyn(name, csproj_path, source_glob_dir, extra_refs=None):
    defines, lang_ver, refs = parse_csproj(csproj_path)
    if extra_refs:
        refs = [r for r in refs if os.path.basename(r).lower() != "assembly-csharp.dll"] + list(extra_refs)

    cs_files = sorted(glob.glob(os.path.join(source_glob_dir, "**", "*.cs"), recursive=True))
    out_dll = os.path.join(tempfile.gettempdir(), f"verify_{name}.dll")

    rsp_lines = [
        f"-out:\"{out_dll}\"",
        "-target:library",
        f"-langversion:{lang_ver}",
        "-nostdlib+",
        "-nologo",
        "-warn:4",
        "-nowarn:0649",
        "-warnaserror+",
    ]
    if defines:
        rsp_lines.append("-define:" + ";".join(sorted(set(defines))))
    for r in refs:
        rsp_lines.append(f"-r:\"{r}\"")
    for f in cs_files:
        rsp_lines.append(f"\"{f}\"")

    rsp_path = os.path.join(tempfile.gettempdir(), f"verify_{name}.rsp")
    with open(rsp_path, "w", encoding="utf-8") as fp:
        fp.write("\n".join(rsp_lines))

    proc = subprocess.run(
        ["dotnet", CSC_DLL, f"@{rsp_path}"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    print(f"[Roslyn] {name}: {len(cs_files)} files -> exit={proc.returncode}")
    if proc.stdout.strip():
        print(proc.stdout.strip())
    if proc.stderr.strip():
        print(proc.stderr.strip())
    return proc.returncode == 0, out_dll

def check_class_filenames():
    issues = []
    pattern = re.compile(r"\bpublic\s+(?:abstract\s+)?class\s+(\w+)\s*:\s*(?:MonoBehaviour|ScriptableObject|BaseCharacter|BaseEnemy|EnemyBase|MeleeCreep|EliteEnemy|BossBase|StageDataSO)")
    for folder in ["Scripts", "Editor"]:
        root_dir = os.path.join(PROJECT_ROOT, "Assets", folder)
        for path in glob.glob(os.path.join(root_dir, "**", "*.cs"), recursive=True):
            fname = os.path.splitext(os.path.basename(path))[0]
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                text = f.read()
            matches = pattern.findall(text)
            for cls in matches:
                if cls != fname:
                    issues.append(f"{path}: class {cls} != filename {fname}.cs")
    print(f"[ClassNames] issues={len(issues)}")
    for i in issues:
        print("  ISSUE:", i)
    return len(issues) == 0

def check_sprites():
    sprite_dirs = ["UI", "Characters", "Combat", "Enemies"]
    total = 0
    bad = []
    for sub in sprite_dirs:
        root_dir = os.path.join(PROJECT_ROOT, "Assets", "Sprites", sub)
        if not os.path.isdir(root_dir):
            bad.append(f"Missing folder: {root_dir}")
            continue
        for p in sorted(glob.glob(os.path.join(root_dir, "**", "*.png"), recursive=True)):
            total += 1
            with Image.open(p) as im:
                im_rgba = im.convert("RGBA")
                w, h = im_rgba.size
                a0 = im_rgba.getpixel((0, 0))[3]
                if w <= 0 or h <= 0 or a0 != 0:
                    bad.append(f"{p}: size={w}x{h} corner_alpha={a0}")

    map_bad = []
    expected_maps = [
        "Map_World1_AoT.png", "Map_W1_S2.png", "Map_W1_S3.png", "Map_W1_S4.png",
        "Map_World2_DB.png", "Map_W2_S2.png", "Map_W2_S3.png", "Map_W2_S4.png",
        "Map_World3_Diend.png", "Map_W3_S2.png", "Map_W3_S3.png", "Map_W3_S4.png",
    ]
    for map_name in expected_maps:
        mp = os.path.join(PROJECT_ROOT, "Assets", "Sprites", "Maps", map_name)
        if not os.path.exists(mp):
            map_bad.append(f"Missing map: {mp}")
        else:
            with Image.open(mp) as m:
                if m.size[0] < 1024:
                    map_bad.append(f"Map too narrow ({m.size}): {mp}")

    print(f"[Sprites] checked={total}, bad={len(bad)}, map_issues={len(map_bad)}")
    for b in bad + map_bad:
        print("  SPRITE ISSUE:", b)
    return total >= 200 and len(bad) == 0 and len(map_bad) == 0


def check_world_exclusivity():
    pb_path = os.path.join(PROJECT_ROOT, "Assets", "Editor", "PrefabBuilder.cs")
    with open(pb_path, "r", encoding="utf-8", errors="ignore") as f:
        src = f.read()
    expected_calls = [
        "Create4WorldLevels(1, WorldType.World1_RuinedCity, \"Attack on Titan\", mapW1, new Color(0.22f, 0.21f, 0.19f, 1f), w1PureTitan, w1BeastTitan, null, w1FemaleTitan, w1WarHammerBoss)",
        "Create4WorldLevels(2, WorldType.World2_Wasteland, \"Dragon Ball Wasteland\", mapW2, new Color(0.27f, 0.20f, 0.14f, 1f), w2Saibamen, w2FriezaSoldier, w2SaibamanKamikaze, w2Nappa, w2BrolyBoss)",
        "Create4WorldLevels(3, WorldType.World3_NeoCity, \"Diend Neo Ruins\", mapW3, new Color(0.16f, 0.16f, 0.24f, 1f), w3Shocker, w3Kasshin, null, w3ShadowMoon, w3MechaBoss)",
    ]
    missing = [c for c in expected_calls if c not in src]
    print(f"[WorldExclusivity] verified_worlds={len(expected_calls) - len(missing)}/3")
    for m in missing:
        print("  EXCLUSIVITY ISSUE: Missing call:", m)
    return len(missing) == 0

def check_metas_and_unity_logs():
    cs_all = glob.glob(os.path.join(PROJECT_ROOT, "Assets", "Scripts", "**", "*.cs"), recursive=True) + \
             glob.glob(os.path.join(PROJECT_ROOT, "Assets", "Editor", "**", "*.cs"), recursive=True)
    missing_metas = [p for p in cs_all if not os.path.exists(p + ".meta")]
    print(f"[Metas] scripts={len(cs_all)}, pending_unity_meta={len(missing_metas)}")

    auto_result = os.path.join(PROJECT_ROOT, "Logs", "AutoSetupResult.txt")
    if os.path.exists(auto_result):
        with open(auto_result, "r", encoding="utf-8", errors="replace") as f:
            print("[AutoSetupResult.txt]\n" + f.read().strip())
    else:
        print("[AutoSetupResult.txt] NOT YET WRITTEN (waiting for Unity Editor focus/compile refresh)")

def main():
    ok_runtime, runtime_dll = compile_roslyn(
        "Assembly-CSharp",
        os.path.join(PROJECT_ROOT, "Assembly-CSharp.csproj"),
        os.path.join(PROJECT_ROOT, "Assets", "Scripts"),
    )
    ok_editor = False
    if ok_runtime:
        ok_editor, _ = compile_roslyn(
            "Assembly-CSharp-Editor",
            os.path.join(PROJECT_ROOT, "Assembly-CSharp-Editor.csproj"),
            os.path.join(PROJECT_ROOT, "Assets", "Editor"),
            extra_refs=[runtime_dll],
        )
    ok_names = check_class_filenames()
    ok_sprites = check_sprites()
    ok_exclusivity = check_world_exclusivity()
    check_metas_and_unity_logs()

    all_ok = ok_runtime and ok_editor and ok_names and ok_sprites and ok_exclusivity
    print(f"\nVERIFY SUMMARY: runtime={ok_runtime}, editor={ok_editor}, names={ok_names}, sprites={ok_sprites}, exclusivity={ok_exclusivity}")
    sys.exit(0 if all_ok else 1)

if __name__ == "__main__":
    main()
