---
name: kich-ban-bandosu
description: Viết kịch bản YouTube tiếng Việt "lịch sử Việt Nam kể trên bản đồ" dạng "Vì sao [nghịch lý trên bản đồ]?" (mở cõi, kinh đô, biên giới, đảo, vương quốc cổ, trận đánh nhìn từ địa hình) dài 10-12 phút hoặc 20-30 phút, giọng thuyết minh "chúng ta - bạn", dữ kiện có nguồn sử; voiceover-ready 1 giọng đọc (file voice tách đoạn bằng |); kèm kế hoạch bản đồ theo năm cho bộ dựng, gói tiêu đề + thumbnail bản đồ + mô tả + tag, gợi ý đề tài có quét kênh đối thủ. Dùng khi gọi /kich-ban-bandosu, nói "viết kịch bản bản đồ sử", "kịch bản lịch sử trên bản đồ", "gợi ý đề tài bản đồ sử", hoặc dán kịch bản đối thủ ngách này để viết bản mới.
---

# Kịch bản "Bản đồ sử" (gói ngách H2 Bản Đồ Sử Studio)

Trao đổi với người dùng bằng **tiếng Việt**. Kịch bản viết bằng tiếng Việt.

Đã có đề tài thì không hỏi lại: chọn mặc định, ghi một dòng giả định ở đầu câu trả lời rồi làm luôn.

Hình do skill `/lam-video-bandosu` dựng **100% bằng code** trên một tấm bản đồ địa hình thật: máy quay bay liền mạch, vùng lãnh thổ loang theo năm, nhãn địa danh, mũi tên, huy hiệu năm. **Không cần ảnh AI.** Vì vậy kịch bản phải "viết cho bản đồ": mỗi đoạn gắn với một nơi chốn, một năm hoặc một thay đổi nhìn thấy được.

Đọc `references/cau-truc.md` trước khi viết. Tên khung nhìn, vùng, địa danh dùng trong file 03 lấy ở `references/dia-danh.md`.

## 1. Đầu vào
- **Gọi lệnh mà không ghi gì:** gợi ý 10 đề tài (mục 6). Chỉ ở chế độ này mới hỏi người dùng chọn số.
- **Một câu hỏi "Vì sao..." hoặc một chủ đề:** viết trọn kịch bản.
- **Kịch bản đối thủ dán vào:** rút khung, nhịp, loại dữ kiện họ dùng, rồi viết **đề tài khác** hoặc **góc khác hẳn**. Không dịch, không viết lại từng đoạn, không dùng lại câu đúc kết hay ẩn dụ của họ.
- **Tuỳ chọn:**
  - Thời lượng: mặc định **10–12 phút (2.000–2.400 chữ)**. Bản dài 20–30 phút (4.000–5.800 chữ) cho đề tài "cả cuộc đời / cả thế kỷ trên bản đồ".
  - Cách tạo voice: mỗi đoạn một file, tách bằng `|` (mặc định), hoặc một file gộp cả bài.
  - Cách viết số: bằng chữ (mặc định, an toàn cho mọi giọng AI) hoặc chữ số (khi tool voice đọc số tiếng Việt chuẩn).

## 2. Nghiên cứu trước khi viết
- Có công cụ tìm web thì tìm và mở nguồn cho **mọi** dữ kiện: năm, tên người, tước hiệu, địa danh xưa và nay, số quân, khoảng cách.
- Ưu tiên: chính sử (Đại Việt Sử Ký Toàn Thư, Khâm Định Việt Sử Thông Giám Cương Mục, Đại Nam Thực Lục, Gia Định Thành Thông Chí, Phủ Biên Tạp Lục, Lịch Triều Hiến Chương Loại Chí, Đại Nam Nhất Thống Chí), sách và bài của nhà sử học, bảo tàng, trang chính phủ, báo lớn.
- Mỗi bài 12–20 dữ kiện kiểm được. **Mỗi chặng phải có ít nhất 1 năm + 1 địa danh định vị được trên bản đồ.**
- Kiểm địa danh: tên xưa ứng với nơi nào hôm nay ("Mang Khảm, sau này là Hà Tiên"). Địa danh chưa có trong `dia-danh.md` thì ghi toạ độ [kinh độ, vĩ độ] vào file 03 kèm nguồn.
- Ghi nguồn vào file 03 (mục Nguồn), **không** đưa vào lời đọc trừ tên sách gốc ("Đại Nam Thực Lục chép...").
- Chuyện dân gian, giai thoại: vẫn kể được nhưng phải nói rõ "tương truyền... sử không chép".

## 3. Đầu ra
Lưu vào `projects/<NN>-<slug>/` trong thư mục đã kết nối. Không có thì gửi qua chat.

1. **`01-SCRIPT-<slug>.txt`:** bản đọc liền mạch, voiceover-ready. Không tiêu đề phần, không markdown, không chú thích. Đoạn văn cách nhau 1 dòng trống. Dòng cuối ghi số chữ và thời lượng ước tính (khoảng **195 chữ mỗi phút**).
2. **`02-VOICE-<slug>.txt`:** chữ trơn cho tool tạo voice. Ngách này **1 giọng đọc**, các đoạn **ngăn nhau bằng `|`**:
   ```
   Hãy nhìn dải đất hẹp này, chỗ hẹp nhất chỉ chừng năm mươi cây số từ biên giới ra tới biển. |
   Vậy mà suốt hai trăm năm, nó là nơi một dòng họ chọn để dựng nghiệp.
   ```
   - **Mỗi đoạn là 1 file voice và thường là 1 "nhịp bản đồ"** (một lần bay, một vùng loang, một nhãn hiện). Mỗi đoạn 1 dòng, cuối đoạn có ` |`, **đoạn cuối không có `|`**.
   - Mỗi đoạn 1–3 câu, tối đa 55 chữ. Ngắt đúng chỗ bản đồ đổi: sang năm mới, sang vùng mới, hiện tên đất mới.
   - Video 10 phút thường có 70–100 đoạn.
   - Trong tool voice bật **Auto Split**, ô ký tự **chỉ để `|`** (xoá `. , ; : ! ?`). Tool xuất `1.mp3`, `2.mp3`... theo thứ tự. Không có Auto Split thì mỗi dòng 1 file.
   - Ký tự tách không bao giờ xuất hiện trong lời đọc. Không `#`, không tiêu đề, không dòng trống. UTF-8, xuống dòng CRLF.
   - Bỏ các `|` rồi ghép lại phải khớp đúng chữ file 01.
   - Đọc gộp cả bài thành 1 file (ElevenLabs, Minimax...): dán file 01 vào là được.
3. **`03-BAN-DO-<slug>.md`:** kế hoạch bản đồ cho bộ dựng (đây là "kịch bản hình"). Gồm:
   - **Thanh chủ đề** (hiện ở phần mở đầu): `Vì sao *<từ khoá>* <phần còn lại>?`, từ khoá trong `*...*` sẽ tô vàng.
   - **Bảng chặng** theo thứ tự thời gian, mỗi dòng: số đoạn (theo file 02) · năm hiện trên huy hiệu (`~1680` khi ước lượng) · khung nhìn (`view`) · vùng thêm/bớt (`r` + màu) · nhãn địa danh · mũi tên / đường đi · chữ lớn hoặc thẻ chương. Dùng tên trong `dia-danh.md`, thiếu thì toạ độ.
   - **Bảng màu vùng** cố định cả bài: nước Việt / Đàng Trong đỏ `#d8383a`, mảnh vừa nhập cam `#e8892b`, thế lực tự trị hay đồng minh xanh ngọc `#1c9c86`, ngoại bang / thực dân tím xanh `#5b5fc7`, Việt Nam hôm nay vàng `#f0b030` có viền.
   - **Khoảnh khắc "Hãy nhìn lại bản đồ"** (2–3 chỗ): đoạn số mấy, bản đồ đổi gì.
   - **Kế hoạch nhạc** theo phần, chọn từ `curious wonder mystery emotional tension triumph`. Mục **Tắt nhạc**: số đoạn (1–3 chỗ, câu chốt đắt nhất).
   - **Nguồn**: mỗi dữ kiện một dòng, kèm link nếu có.
4. (Ngách này không có file 04 ảnh AI.)
5. **`05-PACKAGE-<slug>.md`:**
   - **10 tiêu đề** 10–16 chữ, **viết hoa kiểu câu** (chỉ chữ đầu và tên riêng), số viết bằng chữ số. Đánh dấu 1 tiêu đề nên dùng. Công thức:
     - `Vì sao [nghịch lý trên bản đồ]? | [Nhân vật / địa danh] [năm]` (chủ lực, 6/10 tiêu đề)
     - `[Địa danh / trận] [năm]: vì sao [điều trái lẽ]?`
     - `[Nhân vật / Việt Nam] trên bản đồ: [con số] trong [thời gian]`
   - **3 phương án thumbnail**, vẽ hoàn toàn bằng code (lệnh `thumb` của bộ dựng). Mỗi phương án ghi:
     - **Chữ:** 2 dòng IN HOA, 2–4 từ mỗi dòng, tổng không quá 7 từ. Kiểu: con số gây sốc ("3 LẦN MẤT KINH ĐÔ"), câu hỏi ("AI GIỮ HOÀNG SA?"), biệt danh ("BỨC TƯỜNG HOÀNH SƠN"). Màu: dòng 1 trắng, dòng 2 vàng (hoặc đỏ / xanh ngọc khi muốn khác).
     - **Khung bản đồ:** `view` (tên hoặc `{c, span}`).
     - **Điểm nóng:** vùng tô màu, **vòng tròn đỏ** khoanh đúng chỗ cần nhìn, 1–2 nhãn địa danh, mũi tên nếu có.
     - **Vị trí chữ:** `tl`, `tr`, `bl`, `br` (đặt chữ chỗ không che điểm nóng).
     - **Chọn 1 trong 2 kiểu hình** tuỳ đề tài:
       - **Kiểu khoanh điểm** (mặc định cho đề tài về một nơi chốn: một hòn đảo, một kinh đô, một con đèo): vòng tròn đỏ + 1–2 nhãn, vùng tô nhẹ hoặc không tô.
       - **Kiểu tô vùng đậm + mũi tên lớn** (dùng khi đề tài có lãnh thổ đổi chủ, bị xoá sổ, bị chia đôi, bị xâm chiếm, mở rộng): tô đậm 1–2 vùng màu tương phản (đỏ `#d8383a` cho vùng chính, tím `#7b6be0` hoặc xanh ngọc cho thế lực kia), 1–2 mũi tên dày viền trắng màu tím nhạt `#b9a6ff` chỉ hướng đánh / lấn vào. Chữ dòng 2 đổi màu đỏ cho hợp vùng đỏ. Ví dụ: vương quốc biến mất, một con sông chia đôi đất nước, quân phương Bắc tràn xuống.
       - Không bắt buộc mọi thumbnail đều dùng kiểu tô đậm; trong 3 phương án nên có ít nhất 1 phương án kiểu này nếu đề tài hợp.
   - **Mô tả:** 2–3 câu tóm tắt kết bằng câu hỏi; 6–10 mốc chương (điền giây sau khi dựng); câu hỏi bình luận; lời mời đăng ký; mục "Nguồn tham khảo" 4–8 dòng; dòng "Ranh giới các thời kỳ trên bản đồ là ước lượng"; hashtag `#lichsuvietnam #bandosu #visao` + 2 hashtag riêng.
   - **20 tag** (dưới 450 ký tự) và **bình luận ghim** (một câu hỏi lựa chọn).

## 4. Quy trình viết
1. Chốt **câu hỏi nghịch lý** nhìn thấy được trên bản đồ hôm nay, và **câu trả lời gồm 3 lý do**.
2. Chọn **cảnh mở đầu**: một điểm cụ thể trên bản đồ (một hòn đảo, một dải đất, một con sông, một kinh đô) + một chi tiết lạ (con số, đường ranh, khoảng cách).
3. Lập dàn ý theo `cau-truc.md`: 5–7 chặng, mỗi chặng 1 năm + 1 thay đổi bản đồ + nguồn.
4. Viết file 01 một mạch theo giọng trong `cau-truc.md`.
5. Đọc lại như người nghe: câu khó đọc thì sửa; số và tên nước ngoài phải đọc được.
6. Cắt đoạn ra file 02 theo nhịp bản đồ.
7. Viết 03, rồi 05.

## 5. Tự kiểm trước khi giao
- [ ] Đủ số chữ theo thời lượng. Đủ 7 nhịp. Có "Vì sao" nhắc lại ở mở bài và ở phần chốt. Đủ 3 lý do.
- [ ] Mỗi chặng có năm + địa danh định vị được. 12–20 dữ kiện có nguồn. Không bịa. Giai thoại được gọi là giai thoại.
- [ ] **Chủ quyền:** Hoàng Sa, Trường Sa là của Việt Nam. Dùng "Biển Đông". Không dùng tên nước ngoài đặt cho hai quần đảo.
- [ ] Gọi đúng tên thời đó: quân Nguyên, quân Minh, nhà Thanh, Xiêm La, Chân Lạp... Không miệt thị dân tộc nào. Chuyện còn tranh cãi thì nêu các ý kiến.
- [ ] Không bàn tranh chấp chính trị hiện đại theo kiểu khích bác. Phần nào vượt phạm vi thì nói một câu "vượt ra ngoài phạm vi video này".
- [ ] Số và tên nước ngoài viết để đọc được. Không ký hiệu, không ngoặc đơn.
- [ ] File 01 không tiêu đề, không markdown. File 02 đúng định dạng `|`, ghép lại khớp file 01.
- [ ] File 03 dùng tên trong `dia-danh.md` hoặc toạ độ có nguồn. Mọi khung có Biển Đông sẽ tự hiện Hoàng Sa, Trường Sa.
- [ ] Không dùng lại đề tài, câu đúc kết, câu chữ của kênh đối thủ. Không nhắc tên kênh đối thủ.
- [ ] Cuối câu trả lời nhắc bước tiếp theo:
  1. Đưa file 02 vào tool tạo voice, bật Auto Split ký tự `|`, xuất mỗi đoạn 1 file. Hoặc đọc gộp cả bài thành 1 file.
  2. Gọi `/lam-video-bandosu` (không cần ảnh AI).

## 6. Gợi ý đề tài
Đưa ra 10 đề tài, mỗi đề tài một dòng gồm: tiêu đề "Vì sao...?" · nghịch lý nhìn thấy trên bản đồ · 3 lý do tóm tắt · chặng thời gian (từ năm nào tới năm nào) · chữ thumbnail gợi ý · tiềm năng (1–3 sao).

Chấm sao theo 4 tiêu chí:
- **Nghịch lý trên bản đồ hôm nay** mà người Việt nào cũng từng thấy (hình dạng đất nước, vị trí đảo, kinh đô, thành phố lớn, ranh giới, con sông). Đây là tiêu chí nặng nhất.
- Có **chuyển động lãnh thổ** để vẽ (vùng loang, mũi tên, đường đi).
- Chạm niềm tự hào hoặc gốc gác người Việt, nhưng không khích bác.
- Có sử liệu rõ (năm, sách gốc).

Loại đề tài nên tránh: kể chiến thuật một trận đánh thuần mà không có câu hỏi địa lý; tiểu sử anh hùng không gắn bản đồ; chuyện thế kỷ 20 dễ gây tranh cãi chính trị.

**Tránh trùng đối thủ và trùng bài mình đã làm:**
1. Đọc `DOI-THU-BANDOSU.txt` trong thư mục gốc của người dùng và danh sách `projects/`.
2. Nếu có trình duyệt (built-in browser hoặc Claude in Chrome):
   - Mở trang `/videos` và `/shorts` của kênh đối thủ người dùng đã cung cấp (mặc định `https://www.youtube.com/@bandosuviet08`), chờ khoảng 5 giây.
   - Chạy JavaScript đọc `window.ytInitialData`: video dài lấy từ `lockupViewModel` (`metadata.lockupMetadataViewModel.title.content`, `metadataRows[].metadataParts[].text.content`); Shorts lấy từ `shortsLockupViewModel.overlayMetadata` (`primaryText.content`, `secondaryText.content`).
   - Ghi đè `DOI-THU-BANDOSU.txt` kèm ngày cập nhật.
   - Đề tài nhiều view cho biết **loại câu hỏi** đang ăn khách. Chỉ học loại câu hỏi, không chép đề tài.
3. Không có trình duyệt: báo một dòng rồi gợi ý theo các loại câu hỏi ăn khách:
   - vì sao vùng đất / hòn đảo X thuộc Việt Nam
   - vì sao kinh đô / thành phố lớn nằm ở đó
   - vì sao một con sông, một dãy núi, một con đèo thành ranh giới
   - vương quốc nào từng có trên bản đồ rồi biến mất
   - vì sao đế chế lớn thua ở đúng chỗ địa hình ấy
   - một nhân vật / một triều đại đi bao nhiêu cây số trên bản đồ
   - bản đồ Việt Nam rộng nhất, hẹp nhất, thay đổi nhanh nhất lúc nào