# Hướng dẫn cài Valheim Việt Hóa

Bản phát hành cung cấp ba cách cài. Hãy thoát hẳn Valheim trước khi thao tác.

- **Thunderstore/r2modman**: dễ cập nhật nhất; dùng BepInEx, không cần Jötunn.
- **Cài mod thủ công**: chỉ cần BepInExPack Valheim và một plugin của dự án, không cần Jötunn.
- **Không mod**: installer backup rồi vá trực tiếp `resources.assets`; không cài BepInEx/Jötunn.

Khuyên dùng bản mod tối giản nếu muốn font tiếng Việt đồng nhất và ít phải cài lại sau mỗi lần game
cập nhật. Chế độ không mod dành cho người muốn chạy Valheim nguyên bản; nó chỉ hoạt động với đúng
build game ghi trong release.

## 1. Dùng installer tự động

Tải file đúng hệ điều hành từ
[GitHub Releases](https://github.com/monokaijs/valheim-viet-hoa/releases):

| Hệ điều hành | File |
|---|---|
| Windows 64-bit | `ValheimVietHoa-Installer-Windows-x64.zip` |
| macOS Apple Silicon | `ValheimVietHoa-Installer-macOS-arm64.tar.gz` |
| macOS Intel | `ValheimVietHoa-Installer-macOS-x64.tar.gz` |
| Linux/Steam Deck | `ValheimVietHoa-Installer-Linux-x64.tar.gz` |

Giải nén rồi chạy file. Installer tự đọc các Steam Library, hiển thị đường dẫn Valheim tìm được và
cho phép dán đường dẫn khác. Sau đó chọn:

1. **Mod tối giản**: nếu thiếu, installer tải đúng BepInExPack từ Thunderstore, kiểm tra SHA-256,
   rồi cài plugin và font. Jötunn không được cài.
2. **Không mod**: installer kiểm tra hash của `resources.assets`, tạo file
   `resources.assets.valheim-vn.backup`, áp dụng patch và kiểm tra lại hash đầu ra.
3. **Gỡ bản mod**: chỉ xóa các file trong `BepInEx/plugins/ValheimVietHoa`.
4. **Khôi phục resources.assets**: chỉ phục hồi khi cả file hiện tại và backup đều đúng hash.

Có thể chạy không tương tác từ terminal:

```text
ValheimVietHoa-Installer --game-dir "/duong/dan/Valheim" --method mod --yes
ValheimVietHoa-Installer --game-dir "/duong/dan/Valheim" --method vanilla --yes
ValheimVietHoa-Installer --game-dir "/duong/dan/Valheim" --method uninstall-mod --yes
ValheimVietHoa-Installer --game-dir "/duong/dan/Valheim" --method uninstall-vanilla --yes
```

Trên macOS/Linux, nếu hệ điều hành chặn quyền chạy:

```bash
chmod u+x ValheimVietHoa-Installer-*
./ValheimVietHoa-Installer-*
```

Trên macOS, nếu Gatekeeper vẫn chặn file chưa ký, nhấp phải file và chọn **Open**, hoặc vào
**System Settings → Privacy & Security → Open Anyway**. Trên Windows, nếu thư mục game nằm trong
`Program Files` và báo từ chối quyền ghi, nhấp phải installer và chọn **Run as administrator**.

## 2. Cài bằng Thunderstore/r2modman

1. Mở r2modman hoặc Thunderstore Mod Manager và chọn Valheim.
2. Tìm **Valheim Viet Hoa** rồi chọn **Download with dependencies**.
3. Chạy game bằng **Start modded**.
4. Vào **Settings → Misc → Language**, chọn **Vietnamese**.

Dependency duy nhất là `denikson-BepInExPack_Valheim-5.4.2333`. Bản `0.3.0` trở lên tự nạp JSON
dịch bằng plugin riêng và không dùng Jötunn.

## 3. Cài mod thủ công, không Jötunn

### Tìm thư mục game

Trong Steam chọn **Library → Valheim → Manage → Browse local files**. Thư mục vừa mở là
`<VALHEIM>`. Đường dẫn thường gặp:

| Hệ điều hành | Vị trí thường gặp |
|---|---|
| Windows | `C:\Program Files (x86)\Steam\steamapps\common\Valheim` |
| macOS | `~/Library/Application Support/Steam/steamapps/common/Valheim` |
| Linux/Steam Deck | `~/.local/share/Steam/steamapps/common/Valheim` |

### Cài BepInExPack

Tải [BepInExPack Valheim 5.4.2333](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/5.4.2333/).
Trong ZIP, mở `BepInExPack_Valheim` rồi chép **nội dung bên trong** vào `<VALHEIM>`. Kết quả phải
có `<VALHEIM>/BepInEx/core/BepInEx.dll`; không tạo thêm cấp
`<VALHEIM>/BepInExPack_Valheim/BepInEx`.

Chạy game một lần rồi thoát. Nếu BepInEx hoạt động, file `<VALHEIM>/BepInEx/LogOutput.log` sẽ được
tạo.

Trên Linux/Steam Deck, cấp quyền và đặt Steam Launch Options:

```bash
chmod u+x start_game_bepinex.sh
```

```text
./start_game_bepinex.sh %command%
```

Trên macOS Apple Silicon, BepInEx 5 cần chạy qua Rosetta; đặt Launch Options:

```text
/usr/bin/arch -x86_64 /bin/bash ./start_game_bepinex.sh %command%
```

### Chép bản Việt hóa

Tải `ValheimVietHoa-Mod-x.y.z.zip` từ GitHub Releases và giải nén thẳng vào `<VALHEIM>`. Cấu trúc
đúng là:

```text
<VALHEIM>/
└── BepInEx/
    └── plugins/
        └── ValheimVietHoa/
            ├── ValheimVietnameseFont.dll
            ├── SVN-Norse Regular.otf
            ├── SVN-Norse Bold.otf
            ├── ValheimVN-Sans-Regular.ttf
            ├── ValheimVN-Serif-Regular.ttf
            ├── OFL-Averia.txt
            ├── OFL-Noto.txt
            └── Translations/
                └── Vietnamese/
                    └── ValheimVietHoa.json
```

Không cài Jötunn. Mở game qua BepInEx, vào **Settings → Misc → Language** và chọn
**Vietnamese**. Trong log phải có dòng `Loaded 6031 Vietnamese translations without Jötunn` và
`Vietnamese font fallback ready`.

## 4. Cài thủ công không mod

Đây là cách “chép file vào game” dành cho người không muốn BepInEx/Jötunn. Không phát hành nguyên
file `resources.assets` của Valheim; installer nhỏ tạo file đã Việt hóa từ chính bản game hợp pháp
trên máy người chơi, rồi thay nó một cách an toàn.

1. Tải installer và chọn **Không mod**, hoặc chạy với `--method vanilla`.
2. Installer chỉ tiếp tục nếu file game khớp `source_sha256` trong release.
3. File gốc được chép thành `resources.assets.valheim-vn.backup` cùng thư mục.
4. File mới được tạo tạm, kiểm tra `output_sha256`, sau đó mới thay `resources.assets`.
5. Mở Valheim bình thường từ Steam, không dùng launch option mod, rồi chọn **Vietnamese**.

Vị trí file được thay:

```text
Windows/Linux: <VALHEIM>/valheim_Data/resources.assets
macOS:         <VALHEIM>/valheim.app/Contents/Resources/Data/resources.assets
```

Sau khi Valheim cập nhật, Steam có thể thay file này và bản dịch biến mất. Tải release tương ứng
build mới rồi chạy lại installer. Nếu installer báo **build không tương thích**, không ép cài; hãy
dùng **Steam → Properties → Installed Files → Verify integrity** hoặc chọn bản mod tối giản.

## 5. Cập nhật và gỡ cài đặt

Với bản mod, chạy installer mới hoặc giải nén ZIP mới đè đúng thư mục `ValheimVietHoa`. Gỡ bằng lựa
chọn **Gỡ bản mod**; BepInEx và mod khác được giữ nguyên.

Với bản không mod, chọn **Khôi phục resources.assets gốc**. Installer từ chối phục hồi nếu file hiện
tại hoặc backup đã bị phần mềm khác sửa. Khi đó dùng Steam Verify integrity. Thế giới và nhân vật
không nằm trong các file này nên không bị xóa.

## 6. Xử lý lỗi

### Không thấy Vietnamese

- Bản mod: kiểm tra `BepInEx/LogOutput.log`, đúng đường dẫn DLL/JSON, và chắc chắn game được mở qua
  BepInEx. Không cần tìm hoặc cài Jötunn.
- Bản không mod: kiểm tra installer đã báo cài thành công và hash đúng; sau update game hãy cài lại.

### Chữ có dấu thành ô vuông

- Bản mod: chép lại đủ bốn file font và tìm `Vietnamese font fallback ready` trong log; tắt thử các
  mod thay UI/font khác.
- Bản không mod dựa vào tài nguyên font/fallback sẵn có của build Valheim. Nếu một màn hình cụ thể
  vẫn thiếu glyph, dùng bản mod tối giản để plugin bổ sung glyph động; gửi ảnh và vị trí màn hình
  trong GitHub Issues để dự án bổ sung bản vá vanilla phù hợp.

### Game không mở sau khi cài mod

Đổi tên tạm `BepInEx/plugins/ValheimVietHoa`, kiểm tra BepInEx trước, sau đó đọc các dòng `Error` và
`Exception` trong `BepInEx/LogOutput.log`. Trên Apple Silicon phải chạy script bằng Rosetta như trên.

### Báo sai hash/build

Đây là khóa an toàn, không phải lỗi ngẫu nhiên. Không thay file của build khác. Verify game trên
Steam, kiểm tra GitHub Releases có bản mới, hoặc dùng phương pháp BepInEx-only vốn không sửa asset
gốc.
