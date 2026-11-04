# Cài đặt BanDoSu Studio Portable trên Windows

## Yêu cầu

- Windows 10/11 x64, WebView2 Runtime
- .NET 10 SDK nếu biên dịch source (bản publish self-contained không cần .NET Runtime)
- Engine H2 đầy đủ gồm `render.mjs`, `player.html`, `tools/`, `packs/`
- Node.js, npm, Python, FFmpeg, FFprobe, Playwright Chromium và các package engine (có thể kiểm tra/cài từ tab **Quản lý môi trường**)

## Biên dịch và chạy

```powershell
git clone https://github.com/thangtm82/BanDoSuVideo.git
cd BanDoSuVideo
dotnet restore BanDoSu.slnx
dotnet build BanDoSu.slnx -c Debug
dotnet run --project src/BanDoSu.Desktop/BanDoSu.Desktop.csproj
```

Tại lần khởi động, chương trình tạo ba thư mục cạnh tệp EXE (không phụ thuộc thư mục đang đứng khi chạy):

```text
BanDoSuStudio/
  BanDoSu.Desktop.exe
  studio/                # Mã nguồn H2 Engine và tài nguyên
  Projects/              # Dự án của người dùng
  Exports/               # Bản sao thành phẩm
  integration/           # Script preview-host.mjs từ ứng dụng
```

**Quan trọng:** Nếu dùng artifact GitHub Actions mặc định, `studio/` mới chỉ là thư mục rỗng. Repository này chưa có mã nguồn engine H2. Hãy mở **Cài đặt → Nhập engine H2 vào studio**, chọn thư mục `D:\\BanDoSu\\h2-2d-studio` (hoặc thư mục engine đang dùng). Ứng dụng sẽ kiểm tra các tệp tối thiểu, sao chép engine sang thư mục `studio` và không xóa bản gốc. Các thư mục dữ liệu `projects`, `node_modules`, `.venv` và dữ liệu build không được nhập; sau khi nhập, dùng **Quản lý môi trường** để cài packages tương ứng.

Để tránh ghi đè nhầm, chương trình từ chối nhập nếu `studio/` đã có dữ liệu. Hãy sao lưu dữ liệu trước khi thay thế engine.

## Tạo bản phát hành kèm engine từ máy Windows

```powershell
dotnet publish src/BanDoSu.Desktop/BanDoSu.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:H2StudioSource="D:\\BanDoSu\\h2-2d-studio" -o "D:\\BanDoSuStudio"
```

Khi truyền `H2StudioSource`, MSBuild tự gọi `tools/package-portable.ps1` sau publish để chép engine vào `<output>/studio`. Script xác thực engine và không ghi đè một thư mục `studio` đã chứa dữ liệu. Các thành phần `node_modules`, `.venv`, `projects`, `build`, `out` bị loại khỏi bản build. Việc đóng gói và phân phối mã nguồn/tài nguyên của engine phải tuân thủ giấy phép của chúng.

Không truyền `H2StudioSource` thì chỉ publish WPF app, **không bao gồm engine H2**. Khi chạy EXE lần đầu, ba thư mục được tự tạo.

## Chuyển từ phiên bản cũ

- Nếu `settings.json` đã lưu đường dẫn ngoài, ứng dụng vẫn sử dụng nó; không tự ghi đè cấu hình.
- Vào **Cài đặt → Dùng cấu trúc Portable cạnh EXE → Lưu cài đặt** để chuyển sang đường dẫn mới.
- Đường dẫn engine cũ được ghi nhớ trong cấu hình, các dự án ở đó vẫn được liệt kê và có thể được **Sao chép dự án cũ vào Workspace** từ tab Dự án.
- Dự án gốc không bị xóa khi dùng nút sao chép. Dự án mới được tạo trong `Projects`.
- Khi nhập engine bằng **Nhập engine H2 vào studio**, ứng dụng tự chuyển sang cấu trúc Portable sau khi hoàn tất.

## Thiết lập môi trường

Mở **Quản lý môi trường**, chọn **Kiểm tra tất cả**, rồi chọn **Cài tất cả thành phần thiếu** hoặc **Cài mục đã chọn** và xác nhận. Node.js, Python, FFmpeg và WebView2 được cài qua WinGet; các thư viện npm/Playwright/Python được thiết lập cho `studio`. Python packages được cài trong `studio/.venv`. Có thể cần khởi động lại ứng dụng sau khi WinGet cài runtime để nhận PATH mới.

## Quy trình dựng video

1. Tạo hoặc chọn dự án trong **Dự án**.
2. Nhập lời đọc TXT và các tệp MP3 đã đánh số trong **Kịch bản và giọng đọc**.
3. Chạy kiểm tra MP3 → chuẩn bị voice → tạo cảnh nháp.
4. Chỉnh JSON từng cảnh trong **Biên tập cảnh bản đồ**, lưu tất cả.
5. Kiểm tra **Xem trước bản đồ** và **Dựng và xem video**.
6. Chạy kiểm tra cảnh, contact sheet, preview 30 giây, full render rồi ghép âm thanh.
7. Thành phẩm ban đầu lưu trong `Projects/<name>/out/`, và được sao chép đến `Exports/<name>/<timestamp>/` khi xuất.

## Giới hạn & kiểm thử

- Studio chưa tự tạo giọng đọc AI. Cần chuẩn bị MP3 bên ngoài.
- Chức năng chỉnh shot hiện dùng JSON; chưa có trình chỉnh timeline kéo thả đầy đủ.
- Live Map hỗ trợ seek khung hình, chưa đồng bộ phát video/âm thanh realtime.
- Dự án mới bên ngoài engine dùng đường dẫn tuyệt đối với các lệnh H2; cần kiểm tra end-to-end với bản engine cụ thể trên máy Windows.
- GitHub Actions kiểm tra build/publish, Core smoke tests và script đóng gói với engine **giả lập**; không tải engine bên ngoài, không chạy WinGet hay Playwright render thực tế.
- Bản Portable nên được giải nén vào thư mục có quyền ghi (ví dụ `D:\\BanDoSuStudio`), không nên đặt trong `C:\\Program Files`.
