# Multi-Pack H2 Studio: Bản Đồ Sử, Terrain và loại dự án mới

## Nguyên tắc

BanDoSuVideo **không chép đè hoặc hợp nhất** các bản renderer khác nhau. Mỗi bộ H2 có thể mang một phiên bản `render.mjs`, `engine/`, `tools/`, `player.html` và dependency riêng. Do đó mỗi phiên bản được cài vào một thư mục độc lập và dự án mới được gắn ID engine.

```text
BanDoSuStudio/
  BanDoSu.Desktop.exe
  studio/                     # H2 engine mặc định đã dùng từ trước (bando)
    render.mjs
    packs/bando/pack.js
    engines/
      h2-terrain-studio-<timestamp>/
        render.mjs
        player.html
        packs/terrain/pack.js
        skills/kich-ban-terrain/
        tools/
  Projects/
    bando-project/config.json
    terrain-project/config.json
    terrain-project/.bandosu-engine.json
  Exports/
```

Không có engine trong bản ZIP release mặc định của ứng dụng. Cần đưa engine H2 đang chạy vào `studio/` như trước; cài Terrain hoặc H2 phiên bản khác bằng **Dự án → Nhập ZIP H2 / Terrain**.

## Hướng dẫn thêm Terrain Studio từ ZIP người dùng

1. Tải hoặc chọn `h2-terrain-studio.zip`, mở ứng dụng tại tab **Dự án**.
2. Bấm **Nhập ZIP H2 / Terrain**, chọn file. Ứng dụng tìm đúng một `kit/` có `render.mjs`, `player.html`, `tools/` và `packs/`.
3. Xác nhận tên engine mới. Renderer và skill `kich-ban-terrain` được cài riêng; thư mục dự án mẫu, `node_modules`, `.venv`, cache và file build của source không được chép theo.
4. Vào **Quản lý môi trường**, chọn **Terrain engine** trong ComboBox rồi cài npm, Playwright Chromium, Python packages (gồm **Pillow** cho DEM). Chạy Doctor.
5. Về **Dự án**, chọn `terrain | h2-terrain-studio-...`, mã ngôn ngữ `en`, style `flat`, nhập tiêu đề và tạo dự án.
6. Nếu cần ví dụ đi kèm ZIP, giải nén ZIP thủ công, chọn **Nhập dự án H2 có sẵn**, rồi duyệt đến `skills/lam-video-terrain/kit/projects/terrain-mau` và nhập vào Workspace.
7. Trong **Biên tập cảnh bản đồ**, dùng template `tw`/`map2d` cho Terrain, sửa `args` (world/cam/camFrom/do/tags…) bằng JSON, chỉnh các `worlds` bằng nút riêng. Có thể dùng **Tải dữ liệu độ cao DEM**, cần Internet hoặc tile được cache. Không nên dùng template `map` Bản Đồ Sử cho Terrain.
8. Trong **AI Kịch bản**, ứng dụng dùng `kich-ban-terrain` nếu có, viết lời đọc tiếng Anh và lưu kế hoạch `03-HINH-<slug>.md` (Bản Đồ Sử dùng `03-BAN-DO-<slug>.md`). Cần xác minh nguồn nghiên cứu, tọa độ và trích dẫn trước khi đưa vào render.

**Lưu ý**: để xuất một video Terrain hoàn chỉnh, `story/shots.json` phải có dữ liệu `worlds` và các template `tw`/`map2d` hợp lệ. `draft_shots.py` chỉ tạo khung cảnh, chưa tự viết bố cục 3D theo kế hoạch Markdown. Các cảnh/DEM cần xem trước và kiểm tra; việc thực sự chạy end-to-end trên máy người dùng chưa được kiểm chứng bởi CI.

## Thêm loại dự án hoặc cập nhật engine mới

1. Chuẩn bị engine H2 có layout cơ bản (renderer, player, tools, `packs/<ten>/pack.js`), trong ZIP hoặc thư mục engine.
2. Nhập qua **Nhập ZIP H2 / Terrain** hoặc **Nhập thư mục engine**, ứng dụng tự quét tất cả thư mục `packs/*/pack.js` và hiện các Pack mới trong danh sách, không cần thay đổi C# nếu pack sử dụng hợp đồng CLI H2.
3. Nếu ZIP có `skills/kich-ban-<pack>/SKILL.md` và `references/cau-truc.md`, giao diện AI có thể tải skill tương ứng. Một pack không có skill vẫn dùng được để tạo/render dự án, nhưng AI Kịch bản sẽ báo thiếu skill chứ không sử dụng nhầm skill Bản Đồ Sử.
4. **Cập nhật bản mới** bằng cách nhập ZIP mới với ID engine mới (ứng dụng tự gắn timestamp). Không cập nhật tại chỗ và không xóa engine cũ. Dự án hiện có tiếp tục dùng phiên bản đã gắn trong `.bandosu-engine.json`; có thể đổi engine có xác nhận, chỉ trong cùng Pack.
5. Cài dependencies và chạy Doctor riêng cho mỗi engine.

### Giới hạn tương thích

- Phần C# hỗ trợ động hợp đồng CLI H2 hiện có: `tools/new_project.py`, `tools/prep_voice.py`, `tools/draft_shots.py`, `render.mjs`, `tools/mix.py`. Các Pack không hỗ trợ các CLI này sẽ cần adapter riêng.
- Terrain có thể sửa `worlds`, template và args JSON, nhưng **chưa có trình chỉnh sửa 3D kéo thả**, chưa có converter tự động Markdown → `shots.json`.
- Giọng TTS vẫn là UI chờ API; không tự tạo MP3.
- Nếu engine dùng packages hoặc Python modules ngoài hệ thống mặc định, quản trị viên cần cài dependency theo tài liệu của pack. Các nguồn DEM từ Internet có thể không truy cập được.
- Luôn sao lưu Projects và engine cũ trước khi thử nâng cấp.

## AI Terrain Scene Builder — tự điền worlds, template, camera và actions

Tính năng mới: trong tab **Biên tập cảnh bản đồ**, nút **AI tạo toàn bộ cảnh Terrain 3D** dùng cùng nhà cung cấp/model/API key đã chọn ở tab **AI Kịch bản**. Có nút **Hủy AI Terrain** và dòng trạng thái theo tiến độ.

### Chuẩn bị

1. Chọn dự án có `config.json` chứa `pack=terrain`. Engine phải có bộ tham khảo `skills/lam-video-terrain/references/mau-canh.md` và `phong-cach.md`.
2. **Quan trọng với engine Terrain đã nhập từ phiên bản cũ:** hãy nhập lại ZIP `h2-terrain-studio.zip` bằng chức năng Multi-Pack phiên bản này để tạo engine phiên bản mới **không ghi đè engine cũ**. Bản nhập mới giữ tài liệu `lam-video-terrain/references` (bản cũ chỉ nhập `kich-ban-terrain`). Sau đó chọn/gắn dự án Terrain với engine mới.
3. Dùng tab **AI Kịch bản** tạo 4 file; phải có `03-HINH-<slug>.md` trong thư mục dự án.
4. Tạo `story/shots.json` nháp qua `Tạo timeline lời đọc → Tạo cảnh nháp`, hoặc dùng `shots.json` có sẵn từ dự án mẫu. File phải chứa ít nhất một cảnh có ID, start, end và lời đọc. Builder **không tự tạo timeline** và chưa tạo MP3.
5. Chọn AI Provider **OpenAI / Gemini / Custom** trong tab AI Kịch bản, nhập Model ID và API key nếu cần. Builder gọi API thật theo số lô và có thể phát sinh phí.

### Cách dùng

1. Vào **Biên tập cảnh bản đồ**, chọn **AI tạo toàn bộ cảnh Terrain 3D**.
2. Xác nhận gửi `03-HINH`, mô tả cảnh và tài liệu mẫu đến nhà cung cấp AI. Đối với endpoint Custom HTTP ngoài loopback, ứng dụng cảnh báo kết nối không mã hóa.
3. AI đọc bộ ví dụ `mau-canh.md` + `phong-cach.md` của **đúng phiên bản engine được gắn**, đề xuất `worlds` (địa hình, các unit có ID) và các `shots` theo lô tối đa 8 cảnh.
4. Bộ kiểm tra từ chối output không hợp lệ: thiếu worlds, thiếu kích thước, ID khác, số cảnh khác, `tpl` ngoài `tw/map2d`, `args.world` tham chiếu world sai, camera 3D không hợp lệ hoặc action `show/move/fire/kill` gọi unit không có.
5. Sau khi AI xử lý, ứng dụng hiển thị số cảnh và worlds cho bạn xác nhận. Nếu đồng ý, ứng dụng sao lưu `shots.json.backup-<timestamp>` rồi ghi `shots.json`. Nếu chọn Không hoặc hủy/lỗi, file cũ không thay đổi.
6. Dùng **Kiểm tra cảnh → Bảng ảnh tổng hợp → Xem trước 30 giây đầu**, rà soát hình ảnh và lịch sử trước khi render hoàn chỉnh.

**Cam kết:** không sửa ID, start, end, line của các cảnh từ timeline; giữ metadata cũ không liên quan; nếu `03-HINH` không có nguồn tọa độ đáng tin, AI được yêu cầu sử dụng `hills` mô phỏng thay vì đoán `dem.c`.

**Giới hạn:** đây là AI sinh bản nháp JSON theo tài liệu engine, không phải bằng chứng nội dung đúng hoặc chắc chắn render được. CI dùng mô phỏng API, không thử kết nối/DEM/render 3D thật. Việc tải DEM và xác minh lịch sử vẫn cần thực hiện riêng. Chưa hỗ trợ tạo lại riêng một cảnh hay vòng tự động sửa lỗi từ ảnh screenshot.
