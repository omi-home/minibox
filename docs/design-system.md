# Design system Minibox

Bộ nhận diện **Rừng sâu** (PO chốt OPEN-08 ngày 2026-11-27, task #448). Mã nguồn: `src/frontend/packages/ui` (token ở `src/theme.css`). Xem thật ở route `/design` của shop (cổng 3100) và admin (cổng 3101).

## 1. Nguyên tắc

- **Sản phẩm là trung tâm.** Nền tối giản, chiều sâu tiết chế; màu nhấn chỉ cho hành động chính và trạng thái chọn.
- **Bản sắc hộp nhỏ, vật in 3D:** khối lập phương, góc vát nắp hộp, vân lớp in, chuyển động mở hộp.
- **Một màn hình, một nút chính.** Nút phụ dùng `secondary` hoặc `ghost`.
- **Mobile trước.** Vùng chạm 44px, điều hướng dưới dưới 640px, sheet cho bộ lọc và biến thể.

## 2. Token

Chỉ dùng utility sinh từ token (`bg-surface`, `text-muted`, `border-border`, `rounded-box`, `shadow-box-2`…). Không dùng mã màu thô (`#hex`, `rgb(`, `oklch(`) hay bảng màu mặc định của Tailwind (`bg-gray-100`…): `copy.test.ts` chặn trong `apps/*`.

| Nhóm | Token | Dùng cho |
|---|---|---|
| Nền | `background`, `surface`, `surface-sunken`, `surface-raised` | nền trang, thẻ, ô bị khoá/vùng lõm, lớp nổi |
| Chữ | `foreground`, `muted` | chữ chính (≥ 7:1), chữ phụ (≥ 4.5:1) |
| Viền | `border`, `border-strong` | phân cách, viền control |
| Nhấn | `primary`(+`-hover`, `-foreground`), `accent`(+`-foreground`) | nút chính, link; nền mục đang chọn |
| Điểm nhấn hiếm | `highlight` (hổ phách) | rất ít, tối đa một chỗ mỗi màn hình; không dùng cho chữ nhỏ trên nền sáng |
| Trạng thái | `success`, `warning`, `danger`, `info` + `-soft` | chỉ cho trạng thái, luôn kèm icon hoặc chữ |
| Focus | `focus` | vòng focus (đã đặt sẵn trong `:focus-visible`) |

- **Sáng/tối:** mỗi màu khai `light-dark(sáng, tối)`. Mặc định theo hệ điều hành; ép bằng `data-theme="light|dark"` trên `<html>`.
- **Chữ:** tiêu đề `font-display` (Space Grotesk), chữ thường `font-sans` (Be Vietnam Pro), SKU/kích thước/giá `font-mono` (JetBrains Mono). Cả ba có đủ dấu tiếng Việt.
- **Breakpoint:** mobile < 640 (mặc định), `sm:` tablet 640–1023, `lg:` laptop 1024–1535, `2xl:` desktop lớn ≥ 1536. Khung trang `max-w-page`, đoạn văn `max-w-prose`.
- **Lớp xếp chồng:** `z-sticky` < `z-overlay` < `z-modal` < `z-toast`.
- **Mật độ:** admin bật `data-density="compact"` (control 36px); shop giữ 44px.

Tương phản khoá bằng `contrast.test.ts`, gồm cả chữ đặt trên cảnh nền.

## 3. Hình khối và độ sâu

- Control (nút, ô nhập): `rounded-box`. Khối chứa (thẻ, bảng, sheet): `rounded-lg` + `chamfer` (vát góc trên phải như nắp hộp). Nhãn nhỏ, tem: `rounded-sm` + `chamfer-sm`.
- Độ sâu 3 tầng: `shadow-box-1` thẻ nằm, `shadow-box-2` thẻ nổi/hover, `shadow-box-3` lớp phủ. Ô nhập `shadow-inset`. Ảnh sản phẩm trên bục: `shadow-plinth` (đã có trong `ProductMedia`).
- Không bóng đen thuần, không gradient tím/xanh phát sáng, không lồng thẻ trong thẻ trong thẻ.

## 4. Nhận diện

- **Logo** (`<Logo />`): khối lập phương chữ M có vân lớp in và mặt trên lưới infill. Khi tải, khối được in dựng từ dưới lên theo từng lớp, rồi xoay từng nấc 90°. Bản đầy đủ nối chữ "inibox.vn" thành **Minibox.vn**; bản rút gọn `withWordmark={false}` dùng cho favicon, chỗ hẹp. Cỡ theo `font-size` của khung. Trên nền màu chính, đảo màu bằng `[--cube-face:var(--color-primary-foreground)] [--cube-ink:var(--color-primary)]` trên `.logo-cube`.
- **Cảnh nền** (`<BrandScene />`): rừng thông, buổi sáng ở chế độ sáng, đêm trăng có đom đóm ở chế độ tối. Đặt một lần ở trang shop có nền trống (trang chủ, trang giới thiệu, `/design`). Không dùng trong admin hoặc trang nhiều bảng.
- **Tải:** `CubeLoader` (khối isometric lật 120°) thay cho vòng quay tròn; `Skeleton` cho nội dung đang tải.

## 5. Chọn component

| Cần | Dùng | Ghi chú |
|---|---|---|
| Hành động | `Button` (`primary`, `secondary`, `ghost`, `danger`) | `loading` tự hiện `CubeLoader`, khoá bấm trùng; `asChild` cho link dạng nút |
| Nhập chữ | `Input`, `Textarea` | nhãn ở trên; gợi ý và lỗi ở dưới, lỗi có icon |
| Chọn trong ≤ 7 mục | `RadioGroup`, `Select` | |
| Chọn trong danh sách dài | `Combobox`, `MultiCombobox` | tìm không dấu |
| Bật/tắt | `Switch` (hiệu lực ngay), `Checkbox` (xác nhận khi gửi form) | |
| Số lượng | `QuantityInput` | |
| Ngày giờ | `DatePicker`, `DateRangePicker`, `DateTimePicker`, `TimePicker`, `Calendar` | dd/mm/yyyy, giờ 24h |
| Trạng thái ngắn | `Badge` | |
| Thông báo trong trang | `Alert` | |
| Thông báo sau thao tác | `toast()` + `<Toaster />` một lần ở layout | |
| Trống dữ liệu | `EmptyState` | luôn có một việc tiếp theo |
| Xác nhận việc khó hoàn tác | `ConfirmDialog` / `Dialog` | |
| Bộ lọc, biến thể trên mobile | `Sheet` | |
| Menu thao tác | `DropdownMenu`; gợi ý ngắn `Tooltip` | không giấu thông tin bắt buộc trong tooltip |
| Điều hướng | `Tabs`, `Breadcrumb`, `Pagination`, `Stepper`, `BottomNav` | |
| Bảng | `DataTable` | tự thành danh sách thẻ dưới 640px |
| Giá | `Price` | VND, giá gạch, tiền cọc |
| Ảnh sản phẩm | `ProductMedia` (`fit="plinth"` ảnh tách nền, `fit="cover"` ảnh có nền) | |
| Thông số | `SpecLabel` | SKU, vật liệu, kích thước (dùng `×`), lớp in |
| Khung trang | `ShopShell`, `AdminShell`, `PageHeader` | |

Chưa có (thêm khi có nhu cầu thật): Avatar, Accordion, Slider, Carousel.

## 6. Chuyển động

- Chỉ `transform`/`opacity`. Vào giảm tốc (`ease-out-box`), ra tăng tốc và ngắn hơn (`ease-in-box`). Mọi animation tự tắt khi người dùng bật giảm chuyển động.
- Có sẵn: Dialog/popover lật như nắp hộp (`animate-lid-*`), Sheet trượt (`animate-sheet-*`), Toast rút khỏi khe (`animate-slot-*`), hiện dần khi vào trang (`animate-rise`).
- **Thêm vào giỏ:** gọi `flyToCart(nút)` sau khi thêm thành công. Một hộp chữ M bay theo đường vòng vào nút giỏ (`data-cart-target`, `ShopShell` đã gắn sẵn), giỏ nảy nhẹ. Cập nhật số trong giỏ khi promise xong. Vẫn hiện `toast()` để trình đọc màn hình biết kết quả.

```tsx
const onAdd = async (e: React.MouseEvent<HTMLButtonElement>) => {
  const source = e.currentTarget;
  await addToCart(productId);
  toast({ title: "Đã thêm vào giỏ", description: name });
  await flyToCart(source);
  setCartCount((c) => c + 1);
};
```

## 7. Giọng văn

- Câu ngắn, nói việc cụ thể: "Đã thêm vào giỏ", "Số điện thoại cần đủ 10 chữ số".
- Không dùng gạch dài (`—`, `–`) và từ sáo ("nâng tầm", "liền mạch", "đột phá"): `copy.test.ts` chặn.
- Lỗi nói cách sửa, không đổ lỗi cho người dùng.

## 8. Checklist trước khi merge UI

- [ ] Chỉ dùng token và component trong `packages/ui`; không mã màu thô.
- [ ] Xem đủ 4 breakpoint, cả sáng và tối; mobile có điều hướng dưới, không cuộn ngang.
- [ ] Mỗi màn hình một nút chính; vùng chạm ≥ 44px trên shop.
- [ ] Ảnh có `alt`; control có nhãn; trạng thái không chỉ dựa vào màu.
- [ ] Có trạng thái tải, trống, lỗi.
- [ ] Đổi giao diện `/design` có chủ đích: cập nhật ảnh chuẩn e2e (`pnpm exec playwright test --update-snapshots` trong container Playwright, xem `e2e/design.spec.ts`).
- [ ] `node scripts/test.mjs` và `node scripts/e2e.mjs` xanh (axe không lỗi critical/serious).
