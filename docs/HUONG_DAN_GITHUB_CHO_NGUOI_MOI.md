# HƯỚNG DẪN GIT & GITHUB TỪ A–Z CHO NGƯỜI MỚI BẮT ĐẦU

> **Dành cho dự án:** `Stickman game` (**MULTI-DIMENSIONS STICKMAN**)  
> **Link GitHub của nhóm:** <https://github.com/tienanh2701/Stickman-game-project>  
> **Lưu ý quan trọng:** Tài liệu này viết riêng cho người **chưa từng dùng Git/GitHub bao giờ**. Bạn chỉ cần làm đúng từng bước từ trên xuống dưới, copy-paste đúng các khối lệnh là xong, không cần nhớ máy móc.

---

## MỤC LỤC NHANH

1. [Câu hỏi 1: Tôi cần đứng ở thư mục nào?](#1-tôi-cần-đứng-ở-thư-mục-nào)
2. [Câu hỏi 2: Giờ cần chạy những câu lệnh nào (từng bước từ A–Z)?](#2-giờ-cần-chạy-những-câu-lệnh-nào-từng-bước-từ-az)
3. [Câu hỏi 3: Sau khi đã lên GitHub rồi, hằng ngày 2 người làm tiếp thế nào?](#3-sau-khi-đã-lên-github-rồi-hằng-ngày-2-người-làm-tiếp-thế-nào)
4. [4 lỗi người mới hay gặp nhất & cách tự cứu](#4-4-lỗi-người-mới-hay-gặp-nhất--cách-tự-cứu)

---

## 1. Tôi cần đứng ở thư mục nào?

Bạn **BẮT BUỘC** phải đứng ở thư mục **bên trong** tên là `Stickman game`, tại đúng đường dẫn:

```text
c:\Users\Admin\Documents\Tài liệu của TAnh\2026.1\VibeApp\OOP Project\Stickman game
```

> **TUYỆT ĐỐI KHÔNG đứng ở thư mục cha `OOP Project`!**
> - **Vì sao?** Trong máy tính, thư mục `OOP Project` chỉ là cái "hộp ngoài" bình thường, bên trong nó không hề có Git. Toàn bộ dữ liệu theo dõi code của Git (thư mục ẩn `.git`) nằm bên trong `Stickman game`.
> - Nếu đứng nhầm ở `OOP Project` mà gõ lệnh Git, máy sẽ báo lỗi đỏ: `fatal: not a git repository`.

### Cách kiểm tra xem mình đã đứng đúng chỗ chưa

Mở **PowerShell**, chạy 2 lệnh sau:

```powershell
cd "c:\Users\Admin\Documents\Tài liệu của TAnh\2026.1\VibeApp\OOP Project\Stickman game"
pwd
git rev-parse --show-toplevel
```

**Dấu hiệu bạn đã đứng ĐÚNG chỗ:**
- Lệnh `pwd` in ra đường dẫn có đuôi kết thúc là `\Stickman game`.
- Lệnh `git rev-parse --show-toplevel` in ra `c:/Users/Admin/Documents/Tài liệu của TAnh/2026.1/VibeApp/OOP Project/Stickman game` (không báo lỗi đỏ).

---

## 2. Giờ cần chạy những câu lệnh nào (từng bước từ A–Z)?

> **Giải thích nhanh hiện trạng hiện tại của máy bạn:**
> - Trên máy bạn đang có sẵn 1 mốc lưu cũ (`Backup truoc khi sua animation`) + **275 file mới/đang sửa** chưa lưu lên Git.
> - Trên GitHub của bạn hiện tại mới chỉ có 1 file `README.md` trống trơn (`first commit`).
> - Vì bản trên máy và bản trên GitHub được tạo tách rời nhau, chúng ta sẽ làm theo **5 bước (Bước 0 → Bước 4)** dưới đây để dọn sạch rác tạm, gộp 2 đầu lại cho khớp, đóng gói code gọn gàng thành 8 phần rồi đẩy lên GitHub.

---

### Bước 0: Đóng Unity Editor & mở PowerShell

1. **Đóng hẳn Unity Editor** lại (bấm `Ctrl + S` nếu đang mở dở Scene nào đó rồi tắt Unity).
   - *Vì sao phải tắt Unity?* Để Unity không giữ khóa file và không tự động sửa đổi ngầm các file trong lúc bạn đang gõ lệnh Git.
2. Mở **PowerShell**: bấm phím `Windows` trên bàn phím, gõ `PowerShell`, rồi mở ứng dụng **Windows PowerShell** (hoặc **Terminal**).

---

### Bước 1: Vào đúng thư mục, kiểm tra kết nối, dọn rác tạm & xử lý `Assets/Animations/`

Copy-paste từng cụm lệnh dưới đây vào PowerShell và nhấn `Enter`:

#### 1.1. Di chuyển vào đúng thư mục & kiểm tra cấu hình

```powershell
cd "c:\Users\Admin\Documents\Tài liệu của TAnh\2026.1\VibeApp\OOP Project\Stickman game"
git config user.name
git config user.email
git remote -v
```

**Kết quả đúng cần thấy:**
- `user.name` ra `tienanh2701`
- `user.email` ra `phamtienanh27012k5@gmail.com`
- `git remote -v` ra `origin  https://github.com/tienanh2701/Stickman-game-project.git`

#### 1.2. Kiểm tra file `.gitignore` & dọn thư mục autosave tạm `Assets/_Recovery`

> **`.gitignore` là gì?** Là danh sách "cấm không cho đưa lên GitHub" (như thư mục `Library/`, `Temp/`, `obj/`, `Logs/` nặng hàng GB do Unity tự đẻ ra khi mở máy).

```powershell
# Kiểm tra các thư mục nặng đã bị .gitignore chặn thành công chưa (nó phải in ra đường dẫn file .gitignore)
git check-ignore -v Library/ Temp/ obj/ Logs/ .vs/ UserSettings/

# Xóa thư mục autosave tạm của Unity để không đẩy rác lên GitHub
if (Test-Path Assets/_Recovery) { Remove-Item -Recurse -Force Assets/_Recovery, Assets/_Recovery.meta }
```

#### 1.3. Hiểu đúng & xử lý `131` file trong `Assets/Animations/`

Khi bạn chạy `git status --short`, bạn sẽ thấy có tới **131 dòng `M Assets/Animations/...`** (chữ `M` nghĩa là *Modified — có chỉnh sửa*). **Đừng hoảng!** Qua kiểm tra thực tế trên chính máy bạn:
- `core.autocrlf` trên máy đang là `true` (quy định cách Git xử lý ký tự xuống dòng trên Windows).
- Trong 131 file đó:
  - **120 file `.anim` KHÔNG hề bị sửa nội dung gì cả.** Chỉ vì trước kia chúng dùng kiểu xuống dòng Windows (`CRLF`), còn sau khi Unity mở lại thì lưu bằng kiểu `LF` nên lệch vài byte xuống dòng khiến Git tưởng nhầm là có sửa.
  - Chỉ có đúng **11 file `.controller`** là thực sự có chỉnh sửa nội dung animation (`Diend.controller`, `Eren.controller`, `Goku.controller`, `BaseCreep.controller`, `Broly.controller`, `MechaColossus.controller`, `WarHammer.controller`, `FemaleTitan.controller`, `Nappa.controller`, `ShadowMoon.controller`, `LordKiva.controller`).

Bạn có thể chạy lệnh **chỉ đọc** sau để tự kiểm chứng (nó sẽ chỉ hiện đúng 11 file `.controller`, không có bất kỳ file `.anim` nào):

```powershell
git diff --ignore-all-space --stat Assets/Animations/
```

> **Cách xử lý:**
> - **TUYỆT ĐỐI KHÔNG** chạy lệnh hoàn tác `Assets/Animations/`, vì làm vậy sẽ mất sạch sửa đổi của 11 file `.controller` thật.
> - Bạn cứ giữ nguyên đó! Lát nữa ở **Bước 3 (Nhóm 6)**, khi bạn chạy `git add Assets/Animations/`, Git sẽ tự kiểm tra nội dung bên trong, **tự động loại bỏ 120 file `.anim` không đổi**, và chỉ đóng gói đúng **11 file `.controller` thật + thư mục VFX mới**.
> - *(Nếu trong lúc chạy lệnh có hiện chữ vàng `warning: ... LF will be replaced by CRLF the next time Git touches it`, đó chỉ là lời nhắc bình thường của Git trên Windows, **không phải lỗi**, bạn cứ chạy tiếp bình thường.)*

---

### Bước 2: Kéo và gộp với `origin/main` trên GitHub (giữ nguyên `README.md` mới)

> **Tại sao phải có bước `Move-Item` này?**
> Trên GitHub hiện có một file `README.md` trống (0 chữ), còn ở máy bạn lại có file `README.md` xịn bạn mới viết giới thiệu đầy đủ về game. Nếu kéo thẳng từ GitHub về, Git sẽ báo lỗi sợ ghi đè mất file `README.md` xịn của bạn. Vì vậy ta cất tạm nó sang tên `README.local.md`, gộp lịch sử xong rồi đặt tên lại như cũ.

Copy-paste toàn bộ khối lệnh sau:

```powershell
# 1. Cất tạm README.md xịn sang tên khác
Move-Item -Force README.md README.local.md

# 2. Tải thông tin từ GitHub về và gộp 2 lịch sử làm một
git fetch origin
git merge origin/main --allow-unrelated-histories -m "chore(git): unify local main with origin/main"

# 3. Đưa README.md xịn quay lại vị trí cũ (đè lên file trống của GitHub)
Move-Item -Force README.local.md README.md
```

**Dấu hiệu thành công:** Dòng cuối in ra kiểu `Merge made by the 'ort' strategy.` và không báo lỗi đỏ `fatal:` hay `error:`.

---

### Bước 3: Đóng gói code lên mốc (**Commit**) theo từng nhóm

> **Commit là gì?** Hãy tưởng tượng **Commit** giống như **"lưu một mốc Save trong game"** (hoặc đóng gói một thùng hàng gọn gàng rồi dán nhãn ghi bên trong có gì).
> - Thay vì nhét cả 275 file vào 1 thùng khổng lồ rất khó kiểm tra, mình sẽ chia làm **8 mốc Save nhỏ gọn** theo từng phần chức năng của game.
> - **Lưu ý dấu `*` ở Nhóm 2:** Trong Unity, mỗi thư mục mới (như `CorePure`, `Audio`, `StreamingAssets`, `CoreUnit`) luôn có một file `TênThưMục.meta` đi kèm ngay bên ngoài để Unity nhận diện ID. Dùng dấu `*` (ví dụ `Assets/CorePure*`) giúp Git nhặt **cả thư mục lẫn file `.meta`** đó, không bị sót!

Hãy chạy lần lượt **8 nhóm lệnh** dưới đây (copy-paste từng nhóm hoặc chạy nối tiếp nhau):

#### Nhóm 1: Cấu hình dự án, danh sách chặn rác `.gitignore` & `README.md`
```powershell
git add .gitignore .vsconfig README.md Packages/ ProjectSettings/ProjectSettings.asset
git commit -m "chore(repo): add Unity .gitignore, .vsconfig, Packages and project README"
```

#### Nhóm 2: Khung thư mục chuẩn & tài liệu `docs/` (kèm đủ các file `.meta` mới sinh)
```powershell
git add Assets/CorePure* Assets/Audio* Assets/StreamingAssets* Assets/Tests/CoreUnit* docs/
git commit -m "chore(structure): scaffold CorePure, Audio, StreamingAssets, CoreUnit and docs folders"
```

#### Nhóm 3: Hệ thống Đăng ký / Đăng nhập (`Auth`)
```powershell
git add Assets/Scripts/Auth.meta Assets/Scripts/Auth/
git commit -m "feat(auth): add local JSON account registration, login and profile sync"
```

#### Nhóm 4: Logic chiến đấu, Nhân vật, Quái/Boss, Màn chơi & Dữ liệu
```powershell
git add Assets/Scripts/Characters/ Assets/Scripts/Combat/ Assets/Scripts/Enemies/ Assets/Scripts/Environment/ Assets/Scripts/GameModes/ Assets/Scripts/Level/ Assets/Scripts/VFX.meta Assets/Scripts/VFX/ Assets/Data/
git commit -m "feat(combat): update characters, enemies, bosses, GenkiDama, level flow and ScriptableObjects"
```

#### Nhóm 5: Cửa hàng (Shop), Túi đồ (Inventory) & Giao diện HUD
```powershell
git add Assets/Scripts/Economy/ Assets/Scripts/Items/ Assets/Scripts/UI/
git commit -m "feat(economy-ui): add Glassmorphism HUD, Shop, Inventory and UI controllers"
```

#### Nhóm 6: Hình ảnh 12 Maps, Giao diện UI, Animation & Font tiếng Việt
*(Lệnh này cũng sẽ tự động dọn sạch 120 cảnh báo `.anim` ở Bước 1.3 và chỉ lưu 11 file `.controller` thật + VFX mới)*
```powershell
git add Assets/Animations/ Assets/Resources/ Assets/Sprites/ "Assets/TextMesh Pro/"
git commit -m "art(assets): update 12-stage maps, SlicedUI, GlassHUD, VFX sprites and Vietnamese fonts"
```

#### Nhóm 7: Các Scene game, Prefabs & công cụ Editor
```powershell
git add Assets/Editor/ Assets/Prefabs/ Assets/Scenes/
git commit -m "build(scenes): update prefabs, MainMenu/CharacterSelect/Demo scenes and Editor builders"
```

#### Nhóm 8: Bộ kiểm thử tự động (`Tests/E2E`) & công cụ kiểm tra (`Tools/`)
```powershell
git add Assets/Tests.meta Assets/Tests/E2E.meta Assets/Tests/E2E/ Tools/
git commit -m "test(e2e): add Tier1-Tier5 EditMode and PlayMode automated test suites"
```

#### Kiểm tra lại xem đã đóng gói sạch sẽ chưa
```powershell
git status --short
git log --oneline -n 10
```

**Dấu hiệu thành công:**
- `git status --short` **không in ra bất kỳ dòng nào** (trống trơn = mọi file cần thiết đều đã vào mốc Save, không sót file nào!).
- `git log --oneline -n 10` hiện đủ 8 mốc vừa tạo + mốc gộp `unify local main with origin/main` + mốc cũ `Backup truoc khi sua animation`.

---

### Bước 4: Đẩy toàn bộ lên GitHub (`git push`)

Bây giờ máy bạn đã đóng gói xong 8 mốc, chỉ còn 1 lệnh cuối cùng để gửi toàn bộ lên mạng GitHub:

```powershell
git push -u origin main
```

#### Nếu đây là lần đầu tiên máy hỏi đăng nhập GitHub
1. Có thể hiện một cửa sổ nhỏ tên **Git Credential Manager** (hoặc **Sign in to GitHub**) → Bấm nút **Sign in with your browser** (Đăng nhập bằng trình duyệt).
2. Trình duyệt mở ra trang GitHub → đăng nhập bằng tài khoản **`tienanh2701`** → bấm nút xanh **Authorize git-ecosystem / GitCredentialManager** (nếu hỏi mã bảo mật 2 lớp thì nhập vào).
3. Quay lại cửa sổ PowerShell, đợi vài chục giây đến vài phút cho máy tải hết ảnh/code lên (sẽ thấy phần trăm `Writing objects: 100%` rồi kết thúc ở dòng `* [new branch]` hoặc `... -> main`).

#### Cách lên web GitHub kiểm tra đã lên đủ chưa
1. Mở trình duyệt vào link: <https://github.com/tienanh2701/Stickman-game-project> (nhấn `F5` tải lại trang).
2. Kiểm tra nhanh 4 điểm sau:
   - Thấy bài giới thiệu **MULTI-DIMENSIONS STICKMAN** hiện ngay ở trang chủ (tức là `README.md` đã lên chuẩn).
   - Thấy các thư mục `Assets`, `Packages`, `ProjectSettings`, `Tools`, `docs`.
   - **KHÔNG** thấy các thư mục rác nặng `Library`, `Temp`, `obj`, `Logs`.
   - Bấm vào chữ **Commits** (góc trên bên phải khung danh sách file) thấy hiện đầy đủ các mốc lịch sử vừa tạo.

---

## 3. Sau khi đã lên GitHub rồi, hằng ngày 2 người làm tiếp thế nào?

### 3.1. Giải thích 5 khái niệm GitHub theo cách đời thường

Hãy tưởng tượng 2 bạn đang cùng viết chung một cuốn vở dự án game đặt trên kho chung GitHub:

| Thuật ngữ | Nghĩa bình dân dễ hiểu | Dùng để làm gì? |
| :--- | :--- | :--- |
| **`Clone`** *(Sao chép về)* | Tải toàn bộ dự án trên GitHub về máy tính mới lần đầu tiên. | Bạn cùng nhóm chỉ chạy **1 lần duy nhất** lúc mới bắt đầu nhận việc. |
| **`Branch`** *(Nhánh)* | Tạo một **"tờ giấy nháp riêng"** sao y từ bản chính `main` để làm việc. | Bạn làm tính năng nào thì tạo nhánh riêng cho tính năng đó. Code dở dang hay lỡ lỗi cũng **không làm hỏng bản chính `main`** của người kia. |
| **`Commit`** *(Lưu mốc)* | **"Bấm Save"** lại những gì vừa làm xong trên nháp, kèm 1 câu ghi chú ngắn. | Làm xong 1 việc nhỏ (ví dụ viết xong 1 class hay 1 màn UI) là lưu 1 mốc. |
| **`Push`** *(Đẩy lên)* | Gửi tờ nháp từ **máy tính của mình** lên **web GitHub**. | Để sao lưu online và cho bạn cùng nhóm xem được code bạn vừa viết. |
| **`Pull Request (PR)`** | Gửi **"phiếu xin ghép bản nháp vào bản chính `main`"** trên web GitHub. | Khi làm xong một tính năng chạy ngon, bạn mở PR để người kia đọc kiểm tra rồi bấm **Merge** (ghép vào bản chính `main`). |
| **`Pull`** *(Kéo về)* | Tải những thay đổi mới nhất từ **web GitHub** về **máy mình**. | Chạy mỗi khi người kia vừa ghép code mới vào `main`, giúp 2 máy luôn khớp nhau. |

---

### 3.2. Bạn cùng nhóm (TV2) tải dự án về máy lần đầu như thế nào?

Sau khi bạn làm xong Bước 4 ở trên, nhắn bạn cùng nhóm mở PowerShell tại nơi muốn đặt game (ví dụ ổ `D:\Projects` hoặc `Desktop`), chạy đúng 4 lệnh sau:

```powershell
# 1. Tải dự án từ GitHub về máy (tạo ra thư mục "Stickman game")
git clone https://github.com/tienanh2701/Stickman-game-project.git "Stickman game"

# 2. Đi vào đúng thư mục vừa tải
cd "Stickman game"

# 3. Đặt tên và email GitHub RIÊNG của bạn cùng nhóm (để GitHub biết ai code phần nào)
git config user.name "TenTaiKhoanGitHubCuaBanKia"
git config user.email "email_dang_ky_github_cua_ban_kia@gmail.com"
git config core.autocrlf true
```

*(Sau đó bạn ấy mở Unity Hub → **Add project from disk** → chọn thư mục `Stickman game` vừa clone, đợi Unity tự tạo lại thư mục `Library/` lần đầu khoảng 2–5 phút là mở game chạy bình thường.)*

---

### 3.3. Quy trình 6 lệnh dùng đi dùng lại hằng ngày (cho cả 2 người)

> **Quy tắc vàng:** Từ sau khi đẩy lên xong, **KHÔNG BAO GIỜ code thẳng rồi đẩy trực tiếp lên `main` nữa**. Mọi tính năng mới đều đi qua **Nhánh nháp riêng (Branch) → Mở phiếu ghép (Pull Request) → Bạn kia duyệt & Merge**.

Mỗi khi bắt đầu làm một tính năng/phần việc mới, cả 2 bạn cứ lặp lại đúng vòng tròn 5 bước sau:

#### Bước A — Về bản chính & kéo code mới nhất của nhóm về máy
```powershell
git switch main
git pull origin main
```

#### Bước B — Tạo một "nhánh nháp" mới cho phần việc sắp làm
*(Ví dụ bạn tạo `feat/shop-ui` hoặc `feat/core-contracts`; nhớ đặt tên ngắn gọn không dấu, không khoảng trắng)*
```powershell
git switch -c feat/ten-tinh-nang-moi
```

#### Bước C — Code / chỉnh sửa trong Unity, xong rồi kiểm tra xem mình đã sửa gì
*(Nếu tạo file mới trong `Assets/`, nhớ mở Unity 1 lần để Unity tự tạo file `.meta` đi kèm trước khi gõ lệnh Git!)*
```powershell
git status --short
```

#### Bước D — Lưu mốc (**Commit**) & Đẩy nhánh nháp lên GitHub (**Push**)
```powershell
git add Assets/...        # (Hoặc gõ đích danh thư mục/file mình đã sửa, kèm file .meta)
git commit -m "feat(ten-phan-he): mo ta ngan gon viec vua lam"
git push -u origin feat/ten-tinh-nang-moi
```
*(Nếu làm nhiều ngày trên cùng 1 nhánh, cứ lặp lại Bước C và D nhiều lần; từ lần push thứ 2 trở đi trên nhánh đó chỉ cần gõ `git push`.)*

#### Bước E — Mở Pull Request (PR) trên web GitHub để ghép vào `main`
1. Lên web GitHub <https://github.com/tienanh2701/Stickman-game-project>, ngay đầu trang sẽ hiện khung vàng **Compare & pull request** → Bấm vào đó.
2. Ghi vài dòng mô tả: *Đã làm gì, sửa file nào, đã test chạy thử ổn chưa* → Bấm **Create pull request**.
3. Nhắn bạn cùng nhóm vào xem qua code và bấm **Merge pull request** → **Confirm merge**.
4. Khi đã ghép xong vào `main`, cả 2 người quay lại **Bước A** (`git switch main` → `git pull origin main`) để nhận code mới nhất của nhau và làm tiếp!

---

### 3.4. Quy tắc vàng tránh làm hỏng file Unity khi 2 người làm chung

1. **Không 2 người cùng mở và bấm `Ctrl + S` chung một file Scene (`.unity`):**
   - File `.unity` và `.prefab` có cấu trúc rất phức tạp, 2 người cùng sửa một file Scene là rất dễ đụng độ hỏng màn chơi.
   - Hãy chia rành mạch: ví dụ **Bạn (TV1)** giữ `Stickman_DemoScene.unity`, **Bạn cùng nhóm (TV2)** giữ `MainMenu.unity` và `CharacterSelectScene.unity`. Ai cần chạm vào Scene của người kia thì nhắn hỏi trước.
2. **File mới luôn đi có đôi với file `.meta`:**
   - Bất cứ file hay thư mục mới nào nằm trong `Assets/` (`.cs`, `.png`, `.wav`, `.prefab`...) đều có một file `.meta` cùng tên nằm cạnh nó.
   - Luôn `git add` **cả 2 file** (hoặc `git add` cả thư mục cha chứa chúng). Nếu thiếu `.meta`, mở máy người kia lên sẽ bị lỗi mất Script (`Missing Script`)!
3. **Đổi tên hoặc xóa file ở đâu?**
   - Nên đổi tên/xóa file **ngay bên trong cửa sổ Project của Unity Editor** để Unity tự đổi/xóa luôn file `.meta` giúp bạn.

---

## 4. 4 lỗi người mới hay gặp nhất & cách tự cứu

| # | Dấu hiệu lỗi gặp phải | Nguyên nhân | Cách xử lý copy-paste (An toàn 100%) |
| :-: | :--- | :--- | :--- |
| **1** | Gõ lệnh Git bị báo đỏ:<br>`fatal: not a git repository` | Bạn đang đứng nhầm ở thư mục cha (`OOP Project`) hoặc mở PowerShell ở thư mục mặc định `C:\Users\Admin`. | Chạy lệnh di chuyển vào đúng thư mục dự án:<br>`cd "c:\Users\Admin\Documents\Tài liệu của TAnh\2026.1\VibeApp\OOP Project\Stickman game"`<br>Rồi kiểm tra lại bằng `pwd`. |
| **2** | Quên tạo nhánh, lỡ code hoặc lỡ `commit` thẳng vào `main` | Quên chạy `git switch -c ...` ở Bước B trước khi bắt đầu code. | **Trường hợp chưa `commit`:** Chạy `git switch -c feat/ten-nhanh-moi` (toàn bộ code bạn đang sửa sẽ tự chạy sang nhánh mới, `main` không bị ảnh hưởng!).<br>**Trường hợp đã lỡ `commit` trên `main` (chưa push):** Chạy lần lượt 3 lệnh:<br>`git branch feat/cuu-code`<br>`git reset --hard origin/main`<br>`git switch feat/cuu-code` |
| **3** | Lỡ `git add` nhầm file rác hoặc file Scene của người khác | Chạy `git add .` quá tay mà không nhìn `git status --short` trước. | **Rút file khỏi danh sách chờ commit** (chỉ bỏ `add`, không mất code vừa sửa):<br>`git restore --staged <duong-dan-file>`<br>**Nếu muốn xóa luôn thay đổi lỡ tay ở file không phải của mình** (ví dụ Unity tự sửa `ProjectSettings.asset`):<br>`git restore <duong-dan-file>` |
| **4** | Hiện chữ **`CONFLICT`** khi `git pull` hoặc `git merge` | Cả 2 bạn vô tình sửa trúng **cùng một đoạn** trong cùng một file. | **Nếu là file code `.cs`:** Mở file đó trong VS Code/Visual Studio, tìm các đoạn đánh dấu `<<<<<<<`, `=======`, `>>>>>>>`, giữ lại phần code đúng, xóa các dòng dấu `<<< === >>>` đi, lưu file rồi chạy:<br>`git add <duong-dan-file.cs>`<br>`git commit -m "chore: resolve merge conflict"`<br><br>**Nếu là file Scene `.unity` hoặc `.prefab`:** **KHÔNG tự sửa bằng tay trong VS Code!** Chạy một trong 2 lệnh:<br>- Giữ bản của mình: `git checkout --ours <file.unity>`<br>- Lấy bản mới trên `main`: `git checkout --theirs <file.unity>`<br>Sau đó chạy `git add <file.unity>` → `git commit -m "chore: resolve scene conflict"`, rồi mở Unity kiểm tra lại. |
