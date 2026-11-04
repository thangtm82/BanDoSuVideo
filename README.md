# Bản Đồ Sử Studio — Windows Portable

Ứng dụng C# WPF (.NET 10) quản lý dự án, kịch bản, voice, bản đồ, xem trước và xuất video bằng engine H2 Node.js/Python.

## Bố cục Portable

Sau lần chạy đầu tiên, ứng dụng tự tạo ba thư mục **cạnh tệp EXE**, bất kể shortcut hay working directory:

```text
BanDoSuStudio/
  BanDoSu.Desktop.exe
  studio/              # engine H2: render.mjs, player.html, tools/, packs/, ...
  Projects/            # các dự án do người dùng tạo
  Exports/             # video/ảnh xuất từ các dự án
  integration/         # server xem trước đi kèm ứng dụng
```

**Lưu ý:** Mã nguồn H2 engine **chưa được đưa vào repository này**, nên artifact CI mặc định không thể tự chứa một engine hoàn chỉnh. BanDoSu tạo thư mục `studio` rỗng trước, sau đó người dùng có thể vào **Cài đặt → Nhập engine H2 vào studio** để sao chép mã nguồn H2 đang có. Bản gốc được giữ nguyên. Ứng dụng cảnh báo nếu thư mục `studio` chưa chứa `render.mjs`, `player.html`, `tools` và `packs`. Việc cài Node.js, Python, Chromium và FFmpeg tiếp tục thực hiện qua tab **Quản lý môi trường**.

Nếu muốn tạo **bản publish kèm đầy đủ engine từ thư mục H2 trên máy mình**, dùng lệnh:

```powershell
dotnet publish src/BanDoSu.Desktop/BanDoSu.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:H2StudioSource="D:\\BanDoSu\\h2-2d-studio" -o "D:\\BanDoSuStudio"
```

Target publish sẽ chạy `tools/package-portable.ps1`, kiểm tra engine nguồn và sao chép nó vào `studio/` trong đầu ra. Các thư mục `projects`, `node_modules`, `.venv`, `build` và `out` của engine nguồn bị loại khỏi bản đóng gói. Chỉ phân phối engine/assets khi bạn có quyền phân phối chúng.

## Cài đặt

- Windows 10/11 x64
- .NET 10 SDK để build; bản self-contained không cần cài riêng .NET Runtime
- Node.js, Python, Playwright Chromium, FFmpeg (có thể thiết lập từ tab **Quản lý môi trường**)
- WebView2 Runtime để xem bản đồ

```powershell
dotnet restore BanDoSu.slnx
dotnet build BanDoSu.slnx -c Release
dotnet run --project src/BanDoSu.Desktop/BanDoSu.Desktop.csproj
```

**Dự án cũ:** Cài đặt `settings.json` có đường dẫn ngoài vẫn được giữ. Nút **Dùng cấu trúc Portable cạnh EXE** chuyển sang bố cục mới khi bạn bấm **Lưu cài đặt**. Ứng dụng ghi nhận engine cũ để có thể hiển thị và sao chép các dự án cũ sang `Projects/` bằng nút tương ứng, không xóa dữ liệu nguồn. Nếu bạn nhập engine bằng nút **Nhập engine H2 vào studio**, ứng dụng chuyển sang chế độ Portable sau khi sao chép thành công.

Xem [docs/INSTALL-WINDOWS.md](docs/INSTALL-WINDOWS.md) để biết các bước kiểm thử và giới hạn hiện tại.
