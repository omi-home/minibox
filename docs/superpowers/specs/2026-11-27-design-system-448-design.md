# Design system Minibox: chốt bộ nhận diện và UI (#448)

Ngày: 2026-11-27. Task: #448 (parent #231). Tham chiếu: BRD §5.1, §5.2, OPEN-08; spec kỹ thuật §6.4.

## 1. Mục tiêu

Một bộ design system đầy đủ, có bản sắc riêng, để mọi task UI sau chỉ việc dùng lại: bộ nhận diện (logo, màu, font), token, style toàn bộ component trong `packages/ui`, mẫu bố cục trang, trang `/design` và hướng dẫn design.

Tiêu chí thành công:
- PO chọn một trong 3 phương án sau khi so sánh trên `/design` thật (sáng/tối, 4 breakpoint).
- Nhìn vào không thấy dấu hiệu UI "AI làm chung chung" (xem §8).
- Test khoá: tương phản AA, axe, ảnh chụp so sánh `/design`, cấm màu thô và gạch dài.

Ngoài phạm vi: asset 3D mẫu và cảnh three.js (story M6), trang nghiệp vụ thật (shop/admin) ngoài `/design`.

## 2. Hướng thiết kế

**Design read:** thương mại điện tử sản phẩm in 3D cho khách mua đồ trang trí, figure, quà tặng; ngôn ngữ "phòng trưng bày tinh tế": sản phẩm là trung tâm, nền tối giản, chiều sâu tiết chế, bản sắc khối lập phương/hộp nhỏ.

Dial (taste-skill): VARIANCE 6, MOTION 5, DENSITY 4 cho shop; admin dùng mật độ `compact` (DENSITY ~7), motion chỉ phản hồi và loading.

## 3. Ba phương án (pha 1)

Ba phương án khác nhau ở token màu, font tiêu đề và biến thể logo; dùng chung component. Mọi font có subset `vietnamese` trong next/font (đã kiểm `font-data.json`).

| | A · Đá & Cobalt | B · Rừng sâu | C · Tủ kính đêm |
|---|---|---|---|
| Họ màu | Đơn sắc lạnh (trắng đá, than chì) + một màu nổi | Xanh rừng đậm làm mảng thương hiệu + xương lạnh, điểm hổ phách rất ít | Đen than + nâu da sáng; dark là chế độ chính, có bản sáng |
| primary | Cobalt bão hoà | Xanh rừng | Nâu da (tan) |
| Tiêu đề | Bricolage Grotesque | Space Grotesk | Archivo (trục wdth hẹp, đậm) |
| Chữ thường | Be Vietnam Pro | Be Vietnam Pro | Be Vietnam Pro |
| Mono | JetBrains Mono | JetBrains Mono | JetBrains Mono |
| Logo | Khối isometric đặc 3 mặt, 3 sắc độ | Hộp mở nắp, nét đều | Khung cạnh khối, mặt trên sáng |

Cấm (theo taste-skill): nền kem/ngà + đồng thau/đất nung + chữ nâu-đen; serif tiêu đề; Inter mặc định; tím/xanh phát sáng; bóng đen thuần.

Cơ chế so sánh: `theme.css` khai token mặc định (A) và ghi đè theo `[data-brand="b"]`, `[data-brand="c"]`, mỗi cái có khối dark riêng. Trang `/design` có bộ chuyển phương án và sáng/tối/hệ thống (đặt `data-brand`, `data-theme` trên `<html>`, nhớ bằng `localStorage`, bọc try/catch). Font cả 3 phương án nạp qua `next/font` trong một module dùng chung, chỉ trong pha 1.

## 4. Token

Giữ cấu trúc hiện có (Tailwind v4 `@theme static`), đổi giá trị và bổ sung:
- **Màu ngữ nghĩa:** background, surface, surface-sunken, surface-raised, foreground, muted, border, border-strong, primary(+hover, foreground), accent(+foreground), success, warning, danger(+hover), info, focus. Mỗi màu trạng thái có cặp `-soft` (nền nhạt) để làm badge/alert. Bỏ `edge`, `primary-edge`, `danger-edge`, `shadow-key*`.
- **Chữ:** `--font-display`, `--font-sans`, `--font-mono`; thang cỡ 12/14/16/18/20/24/30/36/48 với line-height đi kèm; số dạng tabular cho giá/tồn.
- **Hình khối:** `--radius-control` (nút, ô nhập, bo nhẹ), `--radius-container` (thẻ, sheet), `--chamfer` (kích thước góc vát). Quy tắc: control bo nhẹ; khối chứa (thẻ, khung ảnh, badge, tem) vát một góc trên phải.
- **Độ sâu:** `shadow-1..3` bóng mềm nhiều lớp pha màu nền + viền sáng mảnh phía trên (`--rim`); `shadow-plinth` cho bóng tiếp xúc dưới sản phẩm.
- **Mật độ:** `[data-density="compact"]` đổi `--control-h` (44px → 36px, vẫn ≥ 24px vùng chạm WCAG 2.2 và giữ 44px cho thao tác chính trên mobile), padding ô bảng, cỡ chữ bảng.
- **Chuyển động:** `--duration-fast/base/slow` (120/220/400ms), `--ease-out-box`, `--ease-in-box`; thoát ngắn hơn vào; mọi animation tắt dưới `prefers-reduced-motion`.
- **z-index:** thang cố định (base, sticky, overlay, modal, toast) ghi trong token.

## 5. Điểm nhấn thương hiệu

1. **Góc vát nắp hộp:** `clip-path` trên khối chứa; focus ring vẽ bằng `box-shadow`/outline của lớp bọc ngoài để không bị cắt.
2. **Bục trưng bày + isometric:** `ProductMedia` đặt ảnh trên mặt sàn sáng dần + `shadow-plinth`; logo, minh hoạ `EmptyState`, placeholder ảnh và `CubeLoader` cùng góc isometric 30°.
3. **Tem hộp:** `SpecLabel` hiển thị SKU, kích thước (dùng `×`), vật liệu (PLA, PETG, resin), độ dày lớp bằng font mono trên nền tem có viền nét đứt nhẹ.
4. **Chuyển động mở hộp:** Dialog/Sheet mở bằng `rotateX` quanh cạnh trên (nắp lật) + fade; Toast trượt ra như thẻ rút khỏi khe; logo lắp khối một lần khi tải (≤ 1,2s), xoay nhẹ khi rê; nút thêm giỏ có hiệu ứng hộp đóng nắp. Chỉ `transform`/`opacity`, CSS thuần, không thêm thư viện.
5. **Giọng văn:** câu ngắn, việc cụ thể; cấm gạch dài/gạch nối dài (`—`, `–`) và từ sáo ("nâng tầm", "liền mạch", "đột phá"); test quét chuỗi hiển thị trong `packages/ui` và `apps/*/app`.

## 6. Component

Làm lại style (API giữ, trừ chỗ ghi): Button (sửa finding 231.4: `asChild` khi loading/disabled đặt `aria-disabled`, chặn click), Input, Textarea, Select, Combobox, MultiCombobox, Checkbox, RadioGroup, Switch, QuantityInput, DatePicker, DateRangePicker, DateTimePicker, TimePicker, Calendar, Box (bỏ prop `depth` cạnh đáy, giữ 3 tầng bóng), Sheet, DataTable, BottomNav. Spinner trong Button đổi thành `CubeLoader` cỡ nhỏ.

Thêm mới:

| Nhóm | Component | Nền |
|---|---|---|
| Nhận diện | `Logo` (mark + wordmark, 3 biến thể pha 1), `CubeLoader` | SVG/CSS |
| Phản hồi | `Badge`, `Alert`, `Toast` (+ `toast()`), `Skeleton`, `EmptyState` | Radix Toast |
| Lớp phủ | `Dialog` (xác nhận), `Tooltip`, `DropdownMenu` | Radix |
| Điều hướng | `Tabs`, `Breadcrumb`, `Pagination`, `Stepper` | Radix Tabs |
| Shop | `Price` (VND, giá gạch, cọc), `ProductMedia`, `SpecLabel` | CSS |

Mẫu bố cục (layout component trong `packages/ui`): `ShopShell` (header một dòng, tìm kiếm, giỏ; BottomNav dưới 640px; footer), `AdminShell` (sidebar từ 1024px, top bar + BottomNav dưới 640px, `data-density="compact"`), `PageHeader` (tiêu đề, breadcrumb, một nút chính).

Chưa làm (YAGNI): Avatar, Accordion, Slider, Carousel.

## 7. Trang `/design` và hướng dẫn

- `/design` (`packages/ui/src/showcase.tsx`, dùng chung shop/admin) trình bày lại: nhận diện (logo, màu, chữ, hình khối, độ sâu, chuyển động), component theo nhóm, mẫu bố cục (ShopShell, AdminShell thu nhỏ), mẫu trang (lưới sản phẩm, chi tiết sản phẩm rút gọn, bảng tác nghiệp admin). Dữ liệu mẫu là sản phẩm in 3D có ghi "dữ liệu mẫu".
- `docs/design-system.md`: nguyên tắc, khi nào dùng component nào, quy tắc hình khối/mật độ/chuyển động, giọng văn, checklist trước khi merge UI. Thêm một dòng trỏ tới file này trong `AGENTS.md` của repo.

## 8. Kiểm thử

- `ac231_1`: test tương phản AA mọi cặp chữ/nền chính cho từng phương án × sáng/tối (tính từ giá trị oklch trong `theme.css`).
- `ac231_2`: Playwright kiểm `/design` hiển thị đủ nhóm component ở shop và admin (gộp IMPROVE từ #234).
- `ac231_3`: giữ test 4 breakpoint; thêm `toHaveScreenshot` cho `/design` (sau khi chốt, một phương án), ảnh chuẩn commit vào repo.
- `ac231_4`: axe đủ 4 breakpoint (IMPROVE từ #234) và cả sáng/tối.
- Unit test component mới (vitest + Testing Library) theo mẫu hiện có.
- Lint/test: cấm màu thô (`#hex`, `rgb(`, `oklch(`) và class màu Tailwind mặc định trong `apps/*`; cấm `—`/`–` trong chuỗi hiển thị.

## 9. Hai pha và điểm dừng

1. **Pha 1:** token + 3 phương án + font + logo 3 biến thể + style toàn bộ component + component mới + `/design` có bộ chuyển. Dừng, mở trình duyệt cho PO so sánh.
2. **PO chọn** (`[manual]` trong #448).
3. **Pha 2:** gộp về phương án đã chọn (xoá token/font/logo còn lại và bộ chuyển), bỏ ghi chú "tạm, OPEN-08", viết `docs/design-system.md`, test tương phản + screenshot, cập nhật spec §6.4 và bảng OPEN-08 (BRD), dừng cho người dùng duyệt UI trước `qa`.

Rủi ro: diff lớn (cả `packages/ui`); giảm bằng commit theo nhóm component và chạy `check` sau mỗi nhóm. `clip-path` cắt focus ring và bóng: xử lý bằng lớp bọc. Nạp 3 bộ font ở pha 1 làm trang nặng: chấp nhận vì chỉ tạm, pha 2 xoá.
