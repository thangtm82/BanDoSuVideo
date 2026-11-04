# Địa danh, khung nhìn, vùng lãnh thổ có sẵn (pack bando)

Dùng đúng tên viết thường không dấu, nối bằng gạch. Tên tiếng Việt có dấu cũng được (pack tự bỏ dấu để so khớp: "Hà Tiên" = "ha-tien"). Chỗ chưa có tên thì ghi toạ độ [kinh độ, vĩ độ], ví dụ [104.48, 10.38]. Kiểm toạ độ trên bản đồ thật trước khi dùng. Tên sai sẽ làm nhãn không hiện.

## Khung nhìn (`view`)

Ghi tên, hoặc [tây, nam, đông, bắc], hoặc { "c": "<địa danh>", "span": <số độ kinh hiện trên chiều ngang> } (span 1 = cận cảnh một huyện, 4 = một vùng, 12 = cả Việt Nam, 40 = Đông Nam Á).

| Tên | Khung [tây, nam, đông, bắc] |
|---|---|
| `the-gioi` | [-170,-50,190,72] |
| `a-au` | [-12,-5,150,62] |
| `chau-a` | [60,-10,150,55] |
| `dong-a` | [95,5,145,45] |
| `dong-nam-a` | [90,-9,128,27] |
| `dong-duong` | [97,7.5,112,24] |
| `viet-nam` | [99.5,7.2,118.5,23.8] |
| `vn-dat-lien` | [101.5,8.2,110.5,23.6] |
| `bien-dong` | [104,3,122,24] |
| `bac-bo` | [102,19.6,108.5,23.5] |
| `dong-bang-song-hong` | [105.2,20,107.2,21.6] |
| `bien-gioi-bac` | [104.8,20.6,108,22.8] |
| `thang-long-lang-son` | [105.3,20.7,107.4,22.2] |
| `bac-trung-bo` | [104,16.8,108,20.6] |
| `trung-bo` | [104.5,10.6,110.8,18.6] |
| `hue-hai-van` | [106.4,15.7,108.6,17.3] |
| `hoanh-son-hai-van` | [105.6,15.6,109,18.4] |
| `nam-trung-bo` | [106.5,10.5,110.3,16.3] |
| `nam-bo` | [103.3,8.3,107.9,11.9] |
| `mekong` | [103.3,8.3,107.9,11.9] |
| `gia-dinh` | [105.8,10,107.6,11.3] |
| `ha-tien-phu-quoc` | [103.1,9,105.6,11] |
| `vinh-thai-lan` | [99,6,106.5,13.8] |
| `campuchia-nam-bo` | [101.8,8.2,108.2,14.8] |
| `trung-quoc` | [73,17,136,54] |
| `hoa-nam` | [104,18,121,28] |
| `quang-dong-quang-tay` | [104.5,19.5,116,26] |
| `xiem-chan-lap` | [97.5,8,109,18] |
| `chau-au` | [-11,35,30,60] |
| `phap` | [-5,41,9,51.5] |

## Vùng lãnh thổ (`r`)

Ranh giới lịch sử là **ước lượng**, cắt theo bờ biển thật. Cộng dồn nước Việt tới một năm: `dai-viet-<năm>` (ví dụ `dai-viet-1757` = mọi mảnh nhập vào tới năm 1757). Tên nước hôm nay ("Campuchia", "Thái Lan", "Lào", "Trung Quốc"...) cũng dùng được. Tự vẽ vùng: { "poly": [[lon,lat]...], "base": "vn" | "land" } (phải có nguồn).

| Tên | Ý nghĩa |
|---|---|
| `goc` | Dai Viet thoi Ly - Tran, toi Hoanh Son / deo Ngang |
| `manh-1069` | Bo Chinh, Dia Ly, Ma Linh (Quang Binh, bac Quang Tri) |
| `manh-1306` | Chau O, chau Ly (Thuan Hoa), toi deo Hai Van |
| `manh-1471` | Le Thanh Tong, toi deo Cu Mong (uoc luong) |
| `manh-1611` | Phu Yen (Nguyen Hoang), toi deo Ca |
| `manh-1653` | Khanh Hoa, toi song Phan Rang |
| `manh-1693` | Thuan Thanh (Ninh Thuan, Binh Thuan) |
| `manh-1698` | Phu Gia Dinh: Tran Bien (Bien Hoa), Phien Tran (Sai Gon) |
| `manh-1708` | Ha Tien cua Mac Cuu: Ha Tien, Rach Gia, Ca Mau va cac dao (Phu Quoc, Tho Chu) |
| `manh-1732` | Dinh Vien (Vinh Long, Ben Tre) |
| `manh-1756` | Tam Bon, Loi Lap (Long An, Tien Giang, Go Cong) |
| `manh-1757` | Tam Phong Long (Chau Doc, Sa Dec) va mien Hau Giang |
| `dong-nam-bo-bac` | Tay Ninh, Binh Phuoc (dan dan, the ky 18-19) |
| `tay-nguyen` | Tay Nguyen (nhap han vao ban do hanh chinh the ky 19-20) |
| `tay-bac` | Tay Bac (nhieu thoi ky, nhieu dan toc) |
| `dang-ngoai` | Dang Ngoai (vua Le - chua Trinh), bac song Gianh |
| `dang-trong-1600` | Dang Trong buoi dau: Thuan Hoa, Quang Nam |
| `champa-1069` | Champa (dai ven bien mien Trung) |
| `champa-1306` | Champa sau 1306 (nam Hai Van) |
| `champa-1471` | Champa sau 1471 (Phu Yen tro vao) |
| `chan-lap-mekong` | Vung chau tho Mekong thuoc Chan Lap (truoc the ky 17) |
| `phu-nam` | Phu Nam (the ky 1-6), uoc luong vung loi |
| `chan-lap` | Chan Lap / Campuchia (lay ranh gioi hom nay) |
| `xiem-la` | Xiem La / Thai Lan (lay ranh gioi hom nay) |
| `ai-lao` | Ai Lao / Lan Xang / Lao (ranh gioi hom nay) |
| `trung-hoa` | Trung Quoc (ranh gioi hom nay) |
| `viet-nam-nay` | Viet Nam hom nay, gom ca Hoang Sa, Truong Sa |

Các mảnh theo năm nhập vào: gốc → `goc`, 1069 → `manh-1069`, 1306 → `manh-1306`, 1471 → `manh-1471`, 1611 → `manh-1611`, 1653 → `manh-1653`, 1693 → `manh-1693`, 1698 → `manh-1698`, 1708 → `manh-1708`, 1732 → `manh-1732`, 1756 → `manh-1756`, 1757 → `manh-1757`, 1840 → `dong-nam-bo-bac`.

## Địa danh (`place`, `from`, `to`, `via`, `pts`, `c`)

`thang-long` = `ha-noi` = `dong-do` [105.85,21.03] · `co-loa` [105.87,21.12] · `hoa-lu` [105.9,20.28] · `me-linh` [105.71,21.18] · `luy-lau` [106.03,21.04] · `phong-chau` = `viet-tri` [105.43,21.32] · `den-hung` [105.32,21.37] · `hai-phong` [106.68,20.86] · `ha-long` [107.08,20.95] · `van-don` [107.42,21.07] · `bach-dang` = `quang-yen` [106.8,20.93] · `van-kiep` = `chi-linh` [106.4,21.13] · `luc-dau-giang` [106.35,21.12] · `nhu-nguyet` = `song-cau` [106.08,21.21] · `xuong-giang` = `bac-giang` [106.2,21.28] · `chi-lang` = `ai-chi-lang` [106.62,21.63] · `lang-son` [106.76,21.85] · `nam-quan` = `huu-nghi` = `ai-nam-quan` [106.72,22] · `cao-bang` [106.25,22.67] · `pac-bo` [106.04,22.98] · `lao-cai` [103.97,22.49] · `ha-giang` [104.98,22.82] · `dien-bien-phu` = `dien-bien` [103.02,21.39] · `son-la` [103.9,21.33] · `hoa-binh` [105.34,20.82] · `ninh-binh` [105.97,20.25] · `nam-dinh` [106.17,20.42] · `thien-truong` [106.17,20.45] · `thai-binh` [106.34,20.45] · `hai-duong` [106.33,20.94] · `bac-ninh` [106.08,21.18] · `thai-nguyen` [105.84,21.59] · `tuyen-quang` [105.21,21.82] · `tan-trao` [105.43,21.78] · `ngoc-hoi` [105.84,20.92] · `dong-da` [105.83,21.01] · `ha-hoi` [105.86,20.86] · `tot-dong` [105.7,20.92] · `chuc-dong` [105.68,20.9] · `mong-cai` [107.97,21.53] · `fansipan` = `hoang-lien-son` [103.78,22.3] · `thanh-hoa` [105.78,19.81] · `lam-kinh` = `lam-son` [105.42,19.92] · `tay-do` = `thanh-nha-ho` [105.61,20.08] · `vinh` = `nghe-an` [105.68,18.67] · `phuong-hoang-trung-do` [105.66,18.68] · `ha-tinh` [105.9,18.34] · `deo-ngang` = `hoanh-son` [106.47,17.96] · `song-gianh` = `cua-gianh` [106.47,17.7] · `dong-hoi` [106.62,17.47] · `luy-thay` [106.6,17.48] · `quang-binh` [106.6,17.47] · `quang-tri` = `thach-han` [107.19,16.75] · `ai-tu` [107.17,16.78] · `tra-bat` [107.12,16.77] · `ben-hai` = `hien-luong` = `vi-tuyen-17` [107.05,17] · `cua-viet` [107.18,16.9] · `phuoc-yen` [107.47,16.55] · `kim-long` [107.55,16.46] · `hue` = `phu-xuan` = `thuan-hoa` [107.58,16.46] · `thuan-an` = `cua-thuan-an` [107.63,16.56] · `deo-hai-van` = `hai-van` [108.13,16.2] · `da-nang` [108.22,16.07] · `hoi-an` [108.33,15.88] · `my-son` [108.12,15.76] · `dong-duong` = `indrapura` [108.25,15.67] · `tra-kieu` [108.23,15.83] · `quang-nam` [108.3,15.6] · `quang-ngai` [108.8,15.12] · `ly-son` [109.12,15.38] · `ba-lang-an` [108.88,15.23] · `an-vinh` [109.11,15.38] · `sa-huynh` [108.88,14.67] · `quy-nhon` [109.22,13.78] · `binh-dinh` [109.1,13.9] · `do-ban` = `vijaya` = `cha-ban` [109.07,13.89] · `tay-son` = `ap-tay-son` = `kien-my` [108.92,13.95] · `an-khe` [108.66,13.95] · `cu-mong` = `deo-cu-mong` [109.18,13.65] · `phu-yen` = `tuy-hoa` [109.3,13.09] · `thach-bi` = `deo-ca` [109.38,12.88] · `nha-trang` = `khanh-hoa` [109.19,12.24] · `dien-khanh` [109.1,12.25] · `cam-ranh` [109.16,11.92] · `phan-rang` = `panduranga` = `ninh-thuan` [108.99,11.57] · `phan-thiet` = `binh-thuan` [108.1,10.93] · `phan-ri` [108.52,11.17] · `kon-tum` [108,14.35] · `pleiku` [108,13.98] · `buon-ma-thuot` [108.05,12.67] · `da-lat` [108.44,11.94] · `tay-nguyen` [108,13.2] · `sai-gon` = `gia-dinh` = `tp-hcm` = `ben-nghe` [106.7,10.78] · `cho-lon` [106.66,10.75] · `bien-hoa` = `dong-nai` [106.82,10.95] · `cu-lao-pho` [106.85,10.94] · `ba-ria` [107.17,10.5] · `vung-tau` [107.08,10.35] · `can-gio` [106.95,10.41] · `tay-ninh` [106.1,11.31] · `my-tho` = `tien-giang` [106.36,10.36] · `rach-gam` = `xoai-mut` [106.27,10.37] · `go-cong` [106.67,10.36] · `ben-tre` [106.38,10.24] · `vinh-long` [105.97,10.25] · `tra-vinh` [106.34,9.93] · `sa-dec` [105.76,10.29] · `can-tho` = `tran-giang` [105.78,10.03] · `long-xuyen` [105.43,10.38] · `chau-doc` [105.12,10.7] · `an-giang` [105.12,10.5] · `nui-sam` [105.08,10.68] · `oc-eo` = `ba-the` [105.14,10.24] · `soc-trang` [105.97,9.6] · `bac-lieu` [105.72,9.29] · `ca-mau` [105.15,9.18] · `mui-ca-mau` = `dat-mui` [104.72,8.62] · `ha-tien` = `mang-kham` [104.48,10.38] · `rach-gia` = `kien-giang` [105.08,10.01] · `phu-quoc` = `duong-dong` [103.96,10.22] · `ham-ninh` [104.05,10.18] · `mui-ong-doi` [104.03,9.95] · `an-thoi` [104.01,10.02] · `tho-chu` [103.47,9.3] · `con-dao` = `con-son` [106.6,8.68] · `nam-du` [104.35,9.68] · `hon-khoai` [104.83,8.43] · `hoang-sa` [111.9,16.45] · `truong-sa` [114,9.8] · `bien-dong` [113.6,14.2] · `vinh-bac-bo` [107.6,20.1] · `vinh-thai-lan` [102.3,10.2] · `co-to` [107.76,20.97] · `bach-long-vi` [107.73,20.13] · `cu-lao-cham` [108.5,15.95] · `phu-quy` [108.94,10.53] · `truong-sa-lon` [111.92,8.64] · `song-tu-tay` [114.33,11.43] · `tri-ton` [111.2,15.78] · `phu-lam` [112.33,16.83] · `dao-hoang-sa` [111.6,16.53] · `phnom-penh` = `nam-vang` [104.92,11.56] · `oudong` = `udong` [104.74,11.81] · `angkor` [103.87,13.41] · `kampot` [104.18,10.61] · `kep` [104.32,10.48] · `sihanoukville` = `kompong-som` [103.52,10.63] · `battambang` [103.2,13.1] · `bien-ho` = `tonle-sap` [104,12.9] · `luang-prabang` [102.13,19.88] · `vieng-chan` = `vientiane` [102.6,17.97] · `champasak` [105.87,14.89] · `savannakhet` [104.75,16.55] · `tran-ninh` [103.2,19.45] · `ayutthaya` [100.57,14.35] · `thonburi` [100.48,13.73] · `bangkok` [100.5,13.75] · `chanthaburi` [102.1,12.6] · `trat` [102.5,12.24] · `sukhothai` [99.82,17] · `chiang-mai` [98.99,18.79] · `pagan` [94.86,21.17] · `ava` [95.98,21.86] · `yangon` [96.16,16.8] · `bac-kinh` = `yen-kinh` = `dai-do` [116.4,39.9] · `nam-kinh` [118.78,32.06] · `truong-an` [108.94,34.34] · `lac-duong` [112.45,34.62] · `khai-phong` [114.31,34.8] · `hang-chau` = `lam-an` [120.16,30.27] · `quang-chau` = `phien-ngung` [113.26,23.13] · `nam-ninh` = `ung-chau` [108.37,22.82] · `kham-chau` [108.62,21.95] · `liem-chau` = `hop-pho` [109.2,21.66] · `bac-hai` [109.12,21.48] · `loi-chau` [110.08,20.91] · `quynh-chau` [110.33,20.03] · `hai-nam` [109.8,19.2] · `hong-kong` [114.17,22.3] · `ma-cao` [113.54,22.2] · `phuc-chau` [119.3,26.08] · `tuyen-chau` [118.59,24.91] · `con-minh` [102.71,25.04] · `dai-ly` [100.23,25.6] · `quang-tay` [108.3,23.6] · `van-nam` [101.5,24.8] · `quang-dong` [113.3,23.4] · `quy-chau` [106.7,26.6] · `ho-nam` [112.9,27.6] · `ung-giang` [108.3,22.8] · `bang-tuong` [106.86,22.1] · `dai-loan` [121,23.7] · `karakorum` = `hoa-lam` [102.83,47.2] · `kinh-thanh-han` = `seoul` [126.98,37.57] · `tokyo` = `edo` [139.69,35.69] · `nagasaki` [129.87,32.75] · `kyoto` [135.77,35.01] · `manila` [120.98,14.6] · `malacca` [102.25,2.2] · `batavia` = `jakarta` [106.85,-6.2] · `singapore` [103.82,1.35] · `paris` [2.35,48.86] · `toulon` [5.93,43.12] · `marseille` [5.37,43.3] · `lisbon` [-9.14,38.72] · `london` [-0.13,51.51] · `madrid` [-3.7,40.42] · `rome` [12.5,41.9] · `moscow` [37.62,55.75] · `washington` [-77.04,38.9] · `delhi` [77.21,28.61] · `goa` [73.83,15.49] · `calcutta` [88.36,22.57] · `ceylon` [80.7,7.9]