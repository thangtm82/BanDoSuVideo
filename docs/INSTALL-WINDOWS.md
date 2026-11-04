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

## AI Kịch bản — skill kich-ban-bandosu

Tab **AI Kịch bản** đọc skill `kich-ban-bandosu` và hai reference `cau-truc.md`, `dia-danh.md`. Khi build/publish, bản skill gốc từ repo được đóng gói cùng EXE tại `skills/kich-ban-bandosu/`. Nếu engine thực tế đã có `studio/skills/kich-ban-bandosu/` đủ ba tệp, tab ưu tiên dùng bộ skill của engine.

Quy trình:

1. Tạo hoặc chọn dự án ở **Dự án** (cần engine H2 đã cấu hình để tạo dự án).
2. Vào **AI Kịch bản**, nhập chủ đề và thời lượng 12 hoặc 25 phút.
3. Chọn **OpenAI** hoặc **Gemini**, nhập tên model mà API key của bạn được phép gọi; nhập API key vào ô mật khẩu. **API key không được lưu vào settings.json và không gửi tới bất kỳ máy chủ nào ngoài API của provider đã chọn.** Mỗi lần gọi API có thể phát sinh phí.
4. Bấm **Tạo kịch bản bằng AI**. Ứng dụng gọi API lần lượt ba bước để tạo lời đọc, kế hoạch bản đồ, gói YouTube; file 02-VOICE được phân đoạn tại chỗ từ chính lời đọc, mỗi đoạn tối đa 55 chữ và ngăn cách bằng `|`. Có thể bấm **Hủy**.
5. Xem và chỉnh sửa các tab **01**, **02**, **03**, **05**. Bấm **Lưu 4 file vào dự án** để tạo:
   - `<Projects>/<slug>/01-SCRIPT-<slug>.txt`
   - `<Projects>/<slug>/02-VOICE-<slug>.txt`
   - `<Projects>/<slug>/03-BAN-DO-<slug>.md`
   - `<Projects>/<slug>/05-PACKAGE-<slug>.md`
   Nếu tồn tại, tệp cũ được sao lưu với hậu tố `.backup-<UTC timestamp>`.
6. Bấm **Đưa voice vào dự án** để sao chép `02-VOICE` vào `input/02-VOICE.txt` và mở tab **Kịch bản và giọng đọc**. Bạn vẫn cần tạo `1.mp3`, `2.mp3`... theo thứ tự trước khi chạy `Chuẩn bị giọng đọc`.

**Giới hạn hiện tại:**
- Không tích hợp web search/grounding trong API AI. AI có thể sinh sai sự kiện, ngày tháng, địa danh, tọa độ hay nguồn; yêu cầu kiểm chứng thủ công trước khi sử dụng video.
- Bản `03-BAN-DO` chỉ là kế hoạch Markdown, **chưa tự động chuyển thành H2 `story/shots.json`**. Bản `05-PACKAGE` cũng chưa tự động xuất thumbnail.
- Không tích hợp TTS; đoạn voice được xuất dưới dạng văn bản phân cách `|`, cần tạo MP3 bằng công cụ ngoài.
- Không lưu API key xuống ổ đĩa; nhập lại khi khởi động lần khác.
- Nếu model trả bản thảo quá ngắn hoặc bị cắt do giới hạn output, hãy đổi model hoặc chỉnh sửa và chạy lại.

### Custom (OpenAI Compatible)

Trong tab **AI Kịch bản → Nhà cung cấp**, chọn **Custom (OpenAI Compatible)**. Nhập:

- **Base URL:** ví dụ `http://127.0.0.1:8080/v1` (local Strata/llama.cpp) hoặc URL HTTPS của proxy tương thích OpenAI. Có thể nhập URL trọn vẹn `.../v1/chat/completions` / `.../v1/responses`, ứng dụng chuẩn hóa phần endpoint.
- **Model:** đúng model ID mà server chấp nhận (ví dụ `my-local-model`).
- **Endpoint:** chọn `/v1/chat/completions` (mặc định) hoặc `/v1/responses`, theo API mà server hỗ trợ.
- **API key:** không bắt buộc cho server local không xác thực; nếu cung cấp, ứng dụng gửi header `Authorization: Bearer <key>`.
- Bấm **Test Connection** để gửi một request nhỏ yêu cầu trả lời `OK`; việc test gọi API thật và có thể phát sinh phí. Kết quả kiểm tra không đảm bảo model sẽ tạo thành công kịch bản dài.

Ứng dụng **chỉ lưu Base URL, Model và loại endpoint** trong `%LOCALAPPDATA%\\BanDoSuVideo\\settings.json`; không lưu API key. Khi đổi nhà cung cấp, ô API key được xóa để tránh vô tình chuyển khóa sang server khác. Đừng đưa API key nhạy cảm cho endpoint HTTP công khai; ưu tiên HTTPS, trừ server `localhost`/loopback đáng tin cậy.

Lựa chọn **OpenAI** vẫn gọi `https://api.openai.com/v1/responses`; **Gemini** vẫn gọi Gemini API. Custom sử dụng `chat/completions` với `messages` + `choices[0].message.content` hoặc `responses` với `input` + `output[].content[].text`. Chưa hỗ trợ streaming/SSE hoặc các biến thể proxy trả dữ liệu không đúng một trong hai schema này.

## Tab AI Kịch bản và khung TTS cho API sắp tích hợp

Các tab chính theo thứ tự: **Dự án → AI Kịch bản → Kịch bản và giọng đọc → Biên tập cảnh bản đồ → Xem trước bản đồ → Dựng và xem video → Cài đặt → Quản lý môi trường**.

Trong tab **Kịch bản và giọng đọc**, mục **TTS · Giọng đọc** có thể:
- Chọn/nhập Voice ID, **Lưu giọng**, **Xóa giọng** khỏi danh sách. Voice ID là định danh tùy API TTS tương lai, không phải tên voice hợp lệ đã được kiểm tra.
- Mở kịch bản được ngăn đoạn bằng dấu |, mỗi đoạn tương ứng 1.mp3, 2.mp3,...; chọn thư mục xuất MP3, mặc định theo dự án là input/voice/.
- Bấm **Tạo MP3** để kiểm tra nội dung, Voice ID và nơi lưu. Khi chưa có API, ứng dụng báo rõ **Chưa cấu hình API TTS** và không ghi MP3 giả.
- Sau khi tích hợp API, nút đó sẽ gọi bộ xử lý TTS theo từng đoạn, có hỗ trợ hủy. Tệp MP3 đã tồn tại không bị ghi đè tự động.
- Sử dụng các nút **Kiểm tra số tệp MP3**, **Tạo timeline lời đọc** với file thật được tạo bởi API sau này hoặc bằng công cụ ngoài.

Trong Core, interface ITtsSynthesisProvider yêu cầu hàm SynthesizeMp3Async(text, voiceId, cancellationToken) trả về bytes MP3. TtsBatch kiểm tra số đoạn, đánh số MP3, kiểm tra chữ ký định dạng, ghi file tạm an toàn và hủy. Khi có API TTS, chỉ cần cài adapter đáp ứng hợp đồng này.

**Giới hạn:** Đây là giao diện và khung xử lý; chưa có provider TTS chạy thật, chưa lấy danh sách voice tự động. Voice ID lưu trong settings.json, không lưu API key. Để tích hợp tiếp, cần endpoint, xác thực, request/response, danh sách Voice IDs, rate limit, và định dạng audio.
