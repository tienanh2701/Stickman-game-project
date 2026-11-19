# MULTI-DIMENSIONS STICKMAN

> **Học phần:** MI4090 – Lập trình hướng đối tượng (Nhóm 02 người)  
> **Thể loại:** Action Beat 'em up 2D (Real-time Side-Scrolling)  
> **Repository:** <https://github.com/tienanh2701/Stickman-game-project.git>

---

## 1. Giới thiệu nhanh

**MULTI-DIMENSIONS STICKMAN** là trò chơi hành động 2D góc nhìn ngang kết hợp 3 thế giới đa vũ trụ với 3 nhân vật điều khiển (`Goku`, `Eren`, `Diend`), hệ thống chiến đấu combo/tích Nộ (Rage)/biến hình (Transform), vật phẩm tiêu hao (`Q`/`E`), cửa hàng nâng cấp-mua sắm, lưu/tải dữ liệu tài khoản và 3 chế độ chơi:

- **Campaign:** 3 Thế giới × 4 Màn (12 màn chơi: *World 1 – Attack on Titan*, *World 2 – Dragon Ball Wasteland*, *World 3 – Diend Neo Ruins*, màn 4 mỗi thế giới có World Boss).
- **Endless:** Sinh tồn theo wave vô tận với độ khó tăng tiến và Boss định kỳ mỗi 5 wave.
- **Training:** Phòng tập sandbox cho phép thử nghiệm nhân vật, triệu hồi quái/Boss theo danh sách và bật/tắt bất tử, hồi Nộ tự động, AI địch.

---

## 2. Yêu cầu môi trường (Clean-room)

- **Unity Editor:** `6000.5.4f1` (xem `ProjectSettings/ProjectVersion.txt`)
- **IDE / Compiler:** Visual Studio 2022 (workload `Microsoft.VisualStudio.Workload.ManagedGame` theo `.vsconfig`) hoặc JetBrains Rider
- **.NET / Roslyn SDK:** Đi kèm Unity `6000.5.4f1` (`Editor/Data/DotNetSdk`)
- **Python 3 + Pillow (`PIL`):** Tùy chọn để chạy bộ kiểm tra tĩnh `Tools/verify_project.py`

---

## 3. Cấu trúc thư mục chuẩn của dự án

```text
Stickman game/
├── .gitignore                        # Quy tắc ignore Unity / VS / Rider / macOS / Windows / Python
├── .vsconfig                         # Khai báo workload Visual Studio cho Unity
├── README.md                         # Hồ sơ hướng dẫn cài đặt, vận hành và kiểm thử
├── Assets/
│   ├── CorePure/                     # Tầng Core thuần C# tách Unity Engine (Entity, Rule, AI, Pattern)
│   │   ├── Entities/                 # Thực thể miền & State thuần C#
│   │   ├── Rules/                    # Luật chơi, công thức sát thương, cân bằng, điều kiện thắng/thua
│   │   ├── AI/                       # Logic AI & thuật toán chiến đấu/sinh quái thuần C#
│   │   ├── Patterns/                 # Triển khai các mẫu thiết kế GoF thuần C#
│   │   ├── Interfaces/               # Hợp đồng giao tiếp (DIP) giữa các phân hệ & tầng Engine
│   │   └── Exceptions/               # Ngoại lệ miền & xử lý lỗi cấu hình/dữ liệu
│   ├── Scripts/                      # Tầng Engine/Adapter (MonoBehaviour, ScriptableObject, UI, Input)
│   │   ├── Auth/                     # Đăng ký, đăng nhập & lưu tài khoản JSON
│   │   ├── Characters/               # Điều khiển Goku, Eren, Diend & CharacterStatsSO
│   │   ├── Combat/                   # Kỹ năng, chưởng, vụ nổ, sóng xung kích
│   │   ├── Core/                     # InputHandler, Hitbox, Projectile, RageManager, IDamageable
│   │   ├── Economy/                  # CurrencyManager & ShopManager
│   │   ├── Enemies/                  # Quái thường, Elite, Boss & EnemyDataSO
│   │   ├── Environment/              # Bẫy môi trường, nền tảng một chiều & vật thể tương tác
│   │   ├── GameModes/                # EndlessModeManager & TrainingModeManager
│   │   ├── Items/                    # Vật phẩm tiêu hao (SenzuBean, SpinalFluid, UltraDivineWater)
│   │   ├── Level/                    # LevelManager, StageManager, LevelDataSO, CameraFollowBeatEmUp
│   │   ├── UI/                       # HUD, MainMenu, CharacterSelect, Pause, GameOver, Instructions
│   │   └── VFX/                      # HitSparkManager & hiệu ứng chiến đấu
│   ├── Audio/                        # Tài nguyên âm thanh cho AudioManager
│   │   ├── BGM/                      # Nhạc nền menu & các thế giới
│   │   └── SFX/                      # Hiệu ứng âm thanh đòn đánh, kỹ năng, UI
│   ├── StreamingAssets/
│   │   └── Config/                   # Tệp cấu hình JSON ngoài build (chỉnh sửa không cần compile lại)
│   ├── Data/                         # ScriptableObject dữ liệu nhân vật, quái & 12 màn Campaign
│   ├── Prefabs/                      # Prefab nhân vật, kẻ địch, đòn đánh & kỹ năng
│   ├── Scenes/                       # MainMenu.unity, CharacterSelectScene.unity, Stickman_DemoScene.unity
│   ├── Sprites/ & Fonts/             # Đồ họa 2D (SlicedUI, Characters, Enemies, Maps, VFX) & Font VN
│   ├── Editor/                       # Script dựng Scene/Prefab/Animation trong Unity Editor
│   └── Tests/
│       ├── CoreUnit/                 # Unit Test thuần C# tách Unity Engine
│       │   ├── TV1/                  # Unit Test phân hệ TV1
│       │   └── TV2/                  # Unit Test phân hệ TV2
│       └── E2E/                      # Bộ kiểm thử tự động Unity Test Framework
│           ├── EditMode/             # Kiểm tra cấu trúc asset, 9-slice, layout & hygiene (F11)
│           └── PlayMode/             # Kiểm thử tích hợp & E2E trong runtime
├── Packages/                         # Khai báo Unity Packages (manifest.json, packages-lock.json)
├── ProjectSettings/                  # Thiết lập dự án, Build Settings, Input System & Layers
├── Tools/                            # Script Python xử lý asset & verify_project.py
└── docs/                             # Bộ hồ sơ đồ án MI4090
    ├── 01_Survey_GDD/                # Khảo sát, GDD, 4W1H, Luật chơi
    ├── 02_Analysis_UML/              # Use Case, Thực thể, Giả mã, UML Class, Package, State/Sequence
    ├── 03_Design_Patterns/           # Bảng GoF Roles, Đánh đổi W, Live OCP
    ├── 04_Testing_QA/                # Ma trận Truy vết A↔TC↔B, Playtest Matrix, Bug Log
    └── 05_Checklist_Slides/          # Checklist tự đánh giá, Slides báo cáo & Link Video Demo
```

> **Lưu ý kiến trúc Unity:** Không đặt tệp `*.asmdef` bên trong `Assets/Scripts/` hoặc `Assets/Editor/`, không đặt thư mục `Releases/` hay `__pycache__/` bên trong thư mục dự án `Stickman game/` (được kiểm tra bắt buộc bởi bài test `F11_T1_WorkspaceHygiene_NoAsmdefInScriptsOrEditor_NoPycache_NoReleases`).

---

## 4. Hướng dẫn cài đặt & chạy game

### Cách 1: Chạy trực tiếp từ Unity Editor

1. Clone kho mã nguồn:
   ```bash
   git clone https://github.com/tienanh2701/Stickman-game-project.git
   ```
2. Mở **Unity Hub** -> **Add** -> **Add project from disk** và trỏ tới thư mục `Stickman game/` (chọn phiên bản Unity **`6000.5.4f1`**).
3. Trong cửa sổ **Project**, mở scene khởi đầu `Assets/Scenes/MainMenu.unity`.
4. Nhấn nút **Play** (`Ctrl + P`) để trải nghiệm từ màn hình chính (*Main Menu -> Đăng nhập / Chơi ngay -> Chọn chế độ & nhân vật -> Vào trận*).

### Cách 2: Chạy bản phát hành Clean-room (không cần cài Unity)

1. Bản build thực thi Windows được đóng gói tách riêng **ngoài** kho Git tại thư mục `../Releases/` (ví dụ `../Releases/Version_latest/MULTI-DIMENSIONS STICKMAN.exe` hoặc bản build hiện có tại `../Releases/Version_2.0/MULTI-DIMENSIONS STICKMAN.exe`).
2. Giải nén (nếu ở dạng lưu trữ) và chạy trực tiếp tệp **`MULTI-DIMENSIONS STICKMAN.exe`**.
3. Đảm bảo thư mục `MULTI-DIMENSIONS STICKMAN_Data` nằm cùng cấp với tệp `.exe`.

---

## 5. Bảng phím điều khiển

| Phím | Chức năng | Ghi chú |
| :--- | :--- | :--- |
| `A` / `D` | Di chuyển trái / phải | Giữ để chạy |
| `Double-tap A` / `D` | Lướt nhanh (Dash) | Nhấn đúp phím `A` hoặc `D` trong 0.25s |
| `W` / `Space` | Nhảy | Nhấn `S + Space` để rơi xuống từ nền tảng một chiều |
| `S` | Bật khiên đỡ đòn (Block) | Giảm sát thương nhận vào trong thời gian chắn |
| `J` | Đánh thường (Normal Attack) | `Dash + J`: Đòn lướt đánh phá Super Armor |
| `U` | Kỹ năng 1 (Skill 1) | Không tiêu tốn Rage, tích Rage khi trúng đích |
| `I` | Kỹ năng 2 (Skill 2) | Tiêu tốn Rage; `Dash + I`: Chiêu cuối gây Stun lớn |
| `O` | Biến hình / Tuyệt kỹ (Transform) | Kích hoạt trạng thái biến hình khi đủ Rage |
| `Q` / `E` | Dùng vật phẩm tiêu hao | `Q`: Ô vật phẩm 1 (Slot 0), `E`: Ô vật phẩm 2 (Slot 1) |
| `Esc` | Tạm dừng (Pause Menu) | Mở modal Tiếp tục / Chơi lại / Về Menu |

---

## 6. Lệnh chạy kiểm định & Test tự động

> Chạy các lệnh dưới đây tại thư mục gốc `Stickman game/`.

### 6.1. Kiểm tra biên dịch tĩnh Roslyn, tên lớp & tài nguyên Sprite

Sử dụng cờ `-B` để Python không tạo thư mục `__pycache__/` trong workspace:

```powershell
python -B Tools/verify_project.py
```

Script thực hiện:
- Biên dịch toàn bộ mã nguồn `Assets/Scripts` (`Assembly-CSharp`) và `Assets/Editor` (`Assembly-CSharp-Editor`) qua Roslyn (`-warn:4 -warnaserror+`).
- Kiểm tra quy ước tên file trùng tên lớp (`MonoBehaviour`, `ScriptableObject`, `BaseCharacter`, v.v.).
- Kiểm tra tính toàn vẹn của toàn bộ PNG UI/Nhân vật/Quái/12 Map Stages và file `.meta`.

### 6.2. Chạy Unity Test Runner qua Batchmode

Xuất file kết quả XML và log ra thư mục tạm ngoài repo (ví dụ `$env:TEMP`) để giữ kho Git sạch:

**EditMode Tests:**
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" `
  -batchmode -nographics -quit `
  -projectPath (Resolve-Path ".").Path `
  -runTests -testPlatform EditMode `
  -testResults "$env:TEMP\stickman_editmode_results.xml" `
  -logFile "$env:TEMP\stickman_editmode.log"
```

**PlayMode Tests:**
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" `
  -batchmode -nographics -quit `
  -projectPath (Resolve-Path ".").Path `
  -runTests -testPlatform PlayMode `
  -testResults "$env:TEMP\stickman_playmode_results.xml" `
  -logFile "$env:TEMP\stickman_playmode.log"
```

---

## 7. Phân công tóm tắt 2 thành viên (TV1 & TV2)

| Thành viên | Phân hệ phụ trách | Nội dung kỹ thuật chính |
| :--- | :--- | :--- |
| **TV1** | **Core, Combat, AI, Spawner & Object Pool** | Thực thể chiến đấu (`BaseCharacter`, Goku/Eren/Diend), Hitbox/Combo/Rage/Transform, AI quái-Elite-Boss 2 Phase, `LevelManager` / `EndlessModeManager` / `TrainingModeManager`, `CampaignHazardSpawner`, Object Pooling (VFX/DamagePopup/Projectile) & Unit Tests tại `Assets/Tests/CoreUnit/TV1/`. |
| **TV2** | **Items, Economy, Save-JSON, Audio & UI phụ trợ** | Vật phẩm tiêu hao (`SenzuBean`, `SpinalFluid`, `UltraDivineWater`, `InventoryManager`, `PlayerItemController`), Cửa hàng & Xu (`CurrencyManager`, `ShopManager`), Lưu/Tải tài khoản & cấu hình JSON (`AccountManager`, `StreamingAssets/Config`), Âm thanh (`Assets/Audio/` & `AudioManager`), UI phụ trợ (`MainMenu`, `CharacterSelect`, `Auth`, `Shop`, `InstructionsUI`, `PauseMenuUI`, `GameOverModalUI`) & Unit Tests tại `Assets/Tests/CoreUnit/TV2/`. |
