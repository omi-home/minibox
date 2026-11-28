# Design system #448 pha 1 Implementation Plan

> **For agentic workers:** thực thi inline (omi-implement: không giao code cho subagent). Bước dùng checkbox `- [ ]`.

**Goal:** Token 3 phương án nhận diện + style mới toàn bộ component + component mới + `/design` có bộ chuyển phương án/sáng tối, để PO chọn.

**Architecture:** Token trong `packages/ui/src/theme.css` (Tailwind v4 `@theme static`), mỗi màu khai `light-dark(sáng, tối)`; phương án B/C ghi đè bằng `[data-brand]`; chế độ bằng `[data-theme]` đặt `color-scheme`. Component ở `packages/ui/src/*.tsx` dùng utility sinh từ token, Radix cho primitive. Font nạp bằng `next/font/google` trong `packages/ui/src/fonts.ts`, app gắn biến font lên `<html>`.

**Tech Stack:** Next 16, React 19, Tailwind 4.3, radix-ui 1.8, @phosphor-icons/react, vitest + Testing Library, Playwright + axe.

**Spec:** `docs/superpowers/specs/2026-11-27-design-system-448-design.md`

## Global Constraints

- Component dạng arrow function; TSDoc tiếng Việt cho mọi export (`pnpm lint` chặn).
- Không thêm thư viện animation; chỉ `transform`/`opacity`; tắt dưới `prefers-reduced-motion`.
- Không dùng `—`/`–` trong chuỗi hiển thị; không mã màu thô trong `apps/*`.
- Vùng chạm ≥ 44px cho thao tác chính trên mobile, ≥ 24px mọi nơi.
- Font: Bricolage Grotesque (A), Space Grotesk (B), Archivo (C) cho tiêu đề; Be Vietnam Pro chữ thường; JetBrains Mono. Subset `latin`, `vietnamese`.
- Test nghiệm thu: tên chứa `ac231_<i>`.
- Giữ tên token class đang dùng: `rounded-box` (control), `rounded-lg` (khối chứa), `rounded-sm`, `shadow-box-1..3`, `shadow-inset`, `p-box`. Bỏ `shadow-key*`, `--color-*-edge`, `--key-edge`.
- Sau mỗi task: `pnpm -C src/frontend -r lint typecheck test` xanh rồi commit `#448: ...`.

## File map

| File | Trách nhiệm |
|---|---|
| `packages/ui/src/theme.css` | Token 3 phương án, chế độ, utility `chamfer`, keyframe |
| `packages/ui/src/contrast.test.ts` | ac231_1 tương phản AA mọi phương án × chế độ |
| `packages/ui/src/fonts.ts` | `fontVariables` (next/font) cho app |
| `packages/ui/src/components.tsx` | Button, Box, Sheet, DataTable, BottomNav (làm lại) |
| `packages/ui/src/field.tsx`, `form.tsx`, `date.tsx` | Control form (làm lại style) |
| `packages/ui/src/brand.tsx` | Logo, CubeLoader |
| `packages/ui/src/feedback.tsx` | Badge, Alert, Toast/Toaster/toast, Skeleton, EmptyState |
| `packages/ui/src/overlay.tsx` | Dialog, Tooltip, DropdownMenu |
| `packages/ui/src/navigation.tsx` | Tabs, Breadcrumb, Pagination, Stepper |
| `packages/ui/src/commerce.tsx` | Price, ProductMedia, SpecLabel |
| `packages/ui/src/layout.tsx` | ShopShell, AdminShell, PageHeader |
| `packages/ui/src/showcase.tsx` (+ `showcase-data.ts`) | Trang `/design` + bộ chuyển |
| `packages/ui/src/copy.test.ts` | Cấm gạch dài + từ sáo trong chuỗi UI |
| `apps/*/app/layout.tsx` | Gắn `fontVariables`, `suppressHydrationWarning` |
| `e2e/design.spec.ts` | ac231_2 đủ nhóm component; ac231_4 axe 4 breakpoint × sáng/tối |

---

### Task 1: Token 3 phương án + font + test tương phản

**Files:** Modify `theme.css`, `theme.test.ts`, `apps/{shop,admin}/app/layout.tsx`, `packages/ui/package.json` (export `./fonts`, peer `next`). Create `contrast.test.ts`, `fonts.ts`.

**Interfaces:** Produces utility: `bg-{background,surface,surface-sunken,surface-raised}`, `text-{foreground,muted}`, `border-{border,border-strong}`, `bg/text-{primary,primary-hover,primary-foreground,accent,accent-foreground,success,warning,danger,info,highlight}`, `bg-{success,warning,danger,info}-soft`, `font-{display,sans,mono}`, `shadow-{box-1,box-2,box-3,inset,plinth}`, `chamfer`, `chamfer-sm`, `animate-{lid-open,lid-close,slot-in,slot-out,cube-spin,shimmer,rise,fade-in,fade-out}`, `z-{sticky,overlay,modal,toast}`, `ease-out-box`, `ease-in-box`. CSS var `--control-h` (đổi theo `[data-density="compact"]`). `fontVariables: string` từ `@minibox/ui/fonts`.

- [ ] **Step 1: Viết test tương phản (fail vì token chưa có `light-dark`).** `contrast.test.ts`: đọc `theme.css`, lấy khối `@theme static {…}` (gốc = A) và các khối `[data-brand="b"] {…}`, `[data-brand="c"] {…}`; với mỗi `--color-x: light-dark(<oklch>, <oklch>)` tách 2 giá trị; chuyển oklch → sRGB tuyến tính (OKLab → LMS → sRGB, kẹp 0..1) → độ chói tương đối WCAG; kiểm các cặp:

```ts
const pairs: [fg: string, bg: string, min: number][] = [
  ["foreground", "background", 7], ["foreground", "surface", 7], ["muted", "surface", 4.5], ["muted", "background", 4.5],
  ["primary-foreground", "primary", 4.5], ["accent-foreground", "accent", 4.5], ["primary", "surface", 3],
  ["success", "success-soft", 4.5], ["warning", "warning-soft", 4.5], ["danger", "danger-soft", 4.5], ["info", "info-soft", 4.5],
  ["danger", "surface", 4.5], ["border-strong", "surface", 1.8], ["focus", "surface", 3],
];
for (const brand of ["a", "b", "c"]) for (const mode of ["light", "dark"]) for (const [fg, bg, min] of pairs)
  test(`ac231_1 tương phản ${brand}/${mode} ${fg} trên ${bg} ≥ ${min}`, () => expect(ratio(get(brand, mode, fg), get(brand, mode, bg))).toBeGreaterThanOrEqual(min));
```

Hàm chuyển (đặt trong test):

```ts
const oklchToLum = (l: number, c: number, h: number) => {
  const a = c * Math.cos((h * Math.PI) / 180), b = c * Math.sin((h * Math.PI) / 180);
  const l_ = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3, m_ = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3, s_ = (l - 0.0894841775 * a - 1.291485548 * b) ** 3;
  const rgb = [4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_, -1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_, -0.0041960863 * l_ - 0.7034186147 * m_ + 1.707614701 * s_].map((v) => Math.min(1, Math.max(0, v)));
  return 0.2126 * rgb[0]! + 0.7152 * rgb[1]! + 0.0722 * rgb[2]!;
};
const ratio = (x: number, y: number) => (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
```

- [ ] **Step 2: Chạy `pnpm -C src/frontend/packages/ui exec vitest run contrast` → FAIL.**
- [ ] **Step 3: Viết lại `theme.css`.** Giá trị khởi điểm (tinh chỉnh tới khi test xanh):

| token | A sáng / tối | B sáng / tối | C sáng / tối |
|---|---|---|---|
| background | .975 .004 255 / .16 .01 262 | .97 .006 160 / .165 .015 165 | .97 .003 60 / .145 .006 50 |
| surface | .995 .002 255 / .2 .012 262 | .993 .003 160 / .205 .018 165 | .993 .002 60 / .185 .007 50 |
| surface-sunken | .95 .005 255 / .135 .01 262 | .945 .008 160 / .14 .014 165 | .94 .004 60 / .12 .005 50 |
| surface-raised | 1 0 0 / .24 .013 262 | 1 0 0 / .245 .02 165 | 1 0 0 / .225 .008 50 |
| foreground | .21 .015 262 / .95 .005 255 | .22 .02 165 / .95 .008 160 | .2 .008 50 / .94 .008 70 |
| muted | .48 .015 262 / .72 .012 262 | .47 .02 165 / .73 .02 160 | .47 .01 50 / .72 .012 65 |
| border | .9 .006 255 / .3 .012 262 | .89 .01 160 / .31 .02 165 | .88 .005 60 / .29 .008 50 |
| border-strong | .8 .01 255 / .42 .014 262 | .79 .014 160 / .43 .024 165 | .78 .008 60 / .41 .01 50 |
| primary | .5 .2 264 / .7 .15 264 | .38 .08 165 / .72 .1 160 | .72 .085 65 / .78 .085 68 |
| primary-hover | .44 .19 264 / .76 .13 264 | .33 .075 165 / .78 .09 160 | .67 .085 65 / .83 .075 68 |
| primary-foreground | .985 .01 264 / .18 .05 264 | .97 .015 160 / .18 .04 165 | .2 .02 50 / .18 .02 50 |
| accent | .94 .04 264 / .3 .07 264 | .93 .03 165 / .3 .05 165 | .93 .025 65 / .28 .03 60 |
| accent-foreground | .42 .17 264 / .86 .08 264 | .36 .07 165 / .86 .07 160 | .43 .07 60 / .85 .06 68 |
| focus | .58 .2 264 / .74 .15 264 | .55 .1 165 / .75 .1 160 | .6 .09 65 / .8 .08 68 |
| highlight | = primary | .78 .14 75 / .8 .13 75 | = primary |

Trạng thái (chung): success .5 .12 150 / .76 .13 150; warning .55 .12 65 / .8 .13 75; danger .53 .19 27 / .72 .16 25; info .52 .13 245 / .76 .11 245; `-soft` sáng `.95 .03 <hue>`, tối `.27 .05 <hue>`. Bóng pha màu `--color-shadow`: A `.3 .04 262`, B `.3 .04 165`, C `.25 .02 50`, tối `0 0 0`. Viền sáng `--rim: light-dark(oklch(1 0 0 / .8), oklch(1 0 0 / .06))`.

Khung file:

```css
@source "./";
@theme static {
  --color-*: initial;
  --color-background: light-dark(oklch(.975 .004 255), oklch(.16 .01 262));
  /* … mọi màu như bảng, A là mặc định … */
  --font-display: var(--font-brand-a), var(--font-body), system-ui, sans-serif;
  --font-sans: var(--font-body), system-ui, -apple-system, "Segoe UI", Roboto, Arial, sans-serif;
  --font-mono: var(--font-code), ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  --radius-sm: .1875rem; --radius-box: .375rem; --radius-lg: .5rem; --chamfer: .875rem; --chamfer-sm: .5rem;
  --shadow-box-1: inset 0 1px 0 var(--rim), 0 1px 2px color-mix(in oklab, var(--color-shadow) 8%, transparent), 0 2px 8px -2px color-mix(in oklab, var(--color-shadow) 8%, transparent);
  --shadow-box-2: inset 0 1px 0 var(--rim), 0 2px 4px color-mix(in oklab, var(--color-shadow) 6%, transparent), 0 12px 28px -8px color-mix(in oklab, var(--color-shadow) 18%, transparent);
  --shadow-box-3: inset 0 1px 0 var(--rim), 0 4px 8px color-mix(in oklab, var(--color-shadow) 8%, transparent), 0 32px 64px -16px color-mix(in oklab, var(--color-shadow) 32%, transparent);
  --shadow-inset: inset 0 1px 2px color-mix(in oklab, var(--color-shadow) 8%, transparent);
  --shadow-plinth: 0 18px 24px -14px color-mix(in oklab, var(--color-shadow) 45%, transparent);
  --z-sticky: 20; --z-overlay: 40; --z-modal: 50; --z-toast: 60;
  --ease-out-box: cubic-bezier(.32,.72,0,1); --ease-in-box: cubic-bezier(.4,0,1,1);
  --animate-lid-open: lid 260ms var(--ease-out-box); --animate-lid-close: lid 160ms var(--ease-in-box) reverse;
  --animate-slot-in: slot 260ms var(--ease-out-box); --animate-slot-out: slot 160ms var(--ease-in-box) reverse;
  --animate-cube-spin: cube 1.2s var(--ease-out-box) infinite; --animate-shimmer: shimmer 1.4s linear infinite;
  /* giữ sheet-*, fade-*, rise; giữ breakpoint, container, spacing như cũ */
  @keyframes lid { from { opacity: 0; transform: perspective(60rem) rotateX(-8deg) translateY(-8px); } }
  @keyframes slot { from { opacity: 0; transform: translateY(100%) scale(.96); } }
  @keyframes cube { 0% { transform: rotateX(-30deg) rotateY(45deg); } 100% { transform: rotateX(-30deg) rotateY(405deg); } }
  @keyframes shimmer { to { background-position: -200% 0; } }
}
@utility chamfer { border-top-right-radius: var(--chamfer); corner-top-right-shape: bevel; }
@utility chamfer-sm { border-top-right-radius: var(--chamfer-sm); corner-top-right-shape: bevel; }
@layer base {
  :root { color-scheme: light dark; --rim: light-dark(oklch(1 0 0 / .8), oklch(1 0 0 / .06)); --control-h: 2.75rem; }
  [data-theme="light"] { color-scheme: light; }  [data-theme="dark"] { color-scheme: dark; }
  [data-density="compact"] { --control-h: 2.25rem; }
  [data-brand="b"] { --font-display: var(--font-brand-b), var(--font-body), system-ui, sans-serif; --color-primary: light-dark(…); … }
  [data-brand="c"] { --font-display: var(--font-brand-c), var(--font-body), system-ui, sans-serif; … }
  body { background-color: var(--color-background); color: var(--color-foreground); font-family: var(--font-sans); line-height: 1.5; -webkit-font-smoothing: antialiased; -webkit-tap-highlight-color: transparent; }
  h1, h2, h3 { font-family: var(--font-display); letter-spacing: -0.015em; }
  /* giữ cursor, :focus-visible, reduced-motion như cũ */
}
```

- [ ] **Step 4: `fonts.ts`.**

```ts
import { Archivo, Be_Vietnam_Pro, Bricolage_Grotesque, JetBrains_Mono, Space_Grotesk } from "next/font/google";
const subsets: ("latin" | "vietnamese")[] = ["latin", "vietnamese"];
const body = Be_Vietnam_Pro({ subsets, weight: ["400", "500", "600", "700"], variable: "--font-body", display: "swap" });
const code = JetBrains_Mono({ subsets, variable: "--font-code", display: "swap" });
const a = Bricolage_Grotesque({ subsets, variable: "--font-brand-a", display: "swap" });
const b = Space_Grotesk({ subsets, variable: "--font-brand-b", display: "swap" });
const c = Archivo({ subsets, variable: "--font-brand-c", display: "swap", axes: ["wdth"] });
/** Class gắn lên `<html>` để khai biến font cho theme.css (pha 1 nạp cả 3 font tiêu đề). */
export const fontVariables = [body, code, a, b, c].map((f) => f.variable).join(" ");
```

Nếu next/font từ chối chạy trong package (lỗi loader), chuyển nguyên file sang `apps/*/app/fonts.ts` mỗi app (2 bản giống nhau) và ghi chú.

- [ ] **Step 5: Layout 2 app** dùng `<html lang="vi" className={fontVariables} suppressHydrationWarning>`; shop bỏ `Be_Vietnam_Pro` cục bộ.
- [ ] **Step 6: Cập nhật `theme.test.ts`** (danh sách biến: thêm `--font-display`, `--font-mono`, `--shadow-plinth`, `--color-surface-raised`; thêm test `chamfer` sinh `corner-top-right-shape: bevel`; test không còn `--color-edge`, `shadow-key`).
- [ ] **Step 7: Chạy test ui; chỉnh giá trị tới khi contrast xanh.** Component còn dùng `shadow-key` sẽ hết style nhưng build không lỗi (Task 2 sửa).
- [ ] **Step 8: `next build` shop** để chắc `light-dark()`/`corner-top-right-shape` không bị lightningcss xoá (grep CSS output). Commit.

### Task 2: Làm lại component hiện có

**Files:** `components.tsx`, `field.tsx`, `form.tsx`, `date.tsx`, `components.test.tsx`; Create `brand.tsx` (CubeLoader trước, Logo ở Task 3).

**Interfaces:** Consumes token Task 1. Produces `CubeLoader({ size?: "sm" | "md" | "lg"; label?: string })` (role="status", `label` mặc định "Đang tải"; sm dùng trong Button). Button giữ API; Box: bỏ ý nghĩa cạnh đáy, giữ `depth`, `interactive`, `asChild`, thêm `chamfer?: boolean` (mặc định true).

- [ ] **Step 1: Test fail** trong `components.test.tsx`:

```tsx
test("ac231_2 nút asChild khi loading/disabled: aria-disabled, chặn click, aria-busy", async () => {
  const onClick = vi.fn();
  render(<Button asChild loading><a href="#x" onClick={onClick}>Lưu</a></Button>);
  const a = screen.getByRole("link", { name: "Lưu" });
  expect(a).toHaveAttribute("aria-disabled", "true");
  expect(a).toHaveAttribute("aria-busy", "true");
  await userEvent.click(a);
  expect(onClick).not.toHaveBeenCalled();
});
test("ac231_2 nút loading hiện CubeLoader có trạng thái", () => {
  render(<Button loading>Lưu</Button>);
  expect(screen.getByRole("button")).toHaveAttribute("aria-busy", "true");
});
test("ac231_2 CubeLoader có role status và nhãn", () => {
  render(<CubeLoader label="Đang tải sản phẩm" />);
  expect(screen.getByRole("status")).toHaveAccessibleName("Đang tải sản phẩm");
});
```

(Kiểm `userEvent` có trong devDeps; nếu không dùng `fireEvent.click`.)

- [ ] **Step 2: FAIL.** **Step 3: Viết.**
  - Button: cao `h-[var(--control-h)]`, `rounded-box`, primary `bg-primary text-primary-foreground shadow-box-1 hover:bg-primary-hover`, secondary `bg-surface-raised border border-border-strong shadow-box-1 hover:bg-surface-sunken`, ghost, danger `bg-danger text-white`→`text-[var(--color-danger-foreground)]` (thêm token `danger-foreground` light-dark trắng/đậm). Nhấn: `active:scale-[.98]`; transition `duration-150`. asChild + (loading||disabled): truyền `aria-disabled`, `aria-busy`, `tabIndex={-1}`, `onClick` chặn `preventDefault`, class `pointer-events-none opacity-50`.
  - CubeLoader: khối isometric 3 mặt bằng 3 `span` (mặt trên/trái/phải với `primary`, `primary-hover`, `accent-foreground`), `transform-style: preserve-3d`, `animate-cube-spin`; reduced-motion thì đứng yên (đã có quy tắc chung).
  - Box: `chamfer rounded-lg border border-border bg-surface p-box shadow-box-{depth}`; interactive: `transition-[translate,box-shadow] duration-300 ease-out-box hover:-translate-y-0.5 hover:shadow-box-2`.
  - field `surface`: `h-[var(--control-h)]` cho input/trigger; `bg-surface-raised`, `border-border-strong`, `shadow-inset`, focus `border-primary ring-4 ring-focus/20`.
  - Sheet: overlay `bg-[color-mix(in_oklab,var(--color-shadow)_45%,transparent)]` thay `bg-black/40`; `z-overlay`/`z-modal`; mobile giữ trượt lên, tablet panel phải; header dùng `font-display`.
  - DataTable: bảng `rounded-lg chamfer`, head `bg-surface-sunken text-xs font-medium uppercase` → không uppercase (giữ chữ thường, `text-muted`), dòng `h-12` (compact: `h-10` qua `[data-density=compact]_&`), số `font-mono tabular-nums`.
  - BottomNav: nền `bg-surface/85 backdrop-blur-md`, mục hiện tại có vạch nhấn 2px phía trên + icon `weight="fill"` do app truyền.
  - form.tsx/date.tsx: thay `rounded-full` toggle giữ; checkbox/radio dùng `rounded-sm`; popover `Panel` dùng `shadow-box-3 rounded-lg` + `animate-lid-open`.
- [ ] **Step 4: Test xanh (cả test cũ).** **Step 5: Commit.**

### Task 3: Logo

**Files:** `brand.tsx`, `brand.test.tsx`. **Produces:** `Logo({ withWordmark?: boolean (mặc định true); className?: string; title?: string (mặc định "Minibox") })` → `<svg role="img">` có `<title>`; ba mark trong một SVG, hiện theo `[data-brand]` (class `brand-a|b|c` + CSS trong theme.css: `.brand-b, .brand-c { display: none }`, `[data-brand=b] .brand-a {display:none}` …). Mark isometric 30° trên lưới 32×32: A khối đặc 3 mặt (fill primary/primary-hover/accent-foreground), B hộp mở nắp (nét `currentColor` 2px), C khung cạnh (nét + mặt trên `primary`). Wordmark chữ `minibox` bằng `<text>`? Không: dùng `<span className="font-display">` cạnh SVG để font thật; component trả `<span>` bọc. Animation lắp khối khi tải: mặt trên `animate-[rise_.9s_var(--ease-out-box)_both]`; hover xoay nhẹ `group-hover:rotate-y`.

- [ ] Test: `render(<Logo />)` → `getByRole("img", { name: "Minibox" })`; `withWordmark={false}` không có chữ "minibox". FAIL → viết → xanh → commit.

### Task 4: Phản hồi

**Files:** `feedback.tsx`, `feedback.test.tsx`; dep `radix-ui` đã có Toast.

**Produces:**
- `Badge({ tone?: "neutral"|"primary"|"success"|"warning"|"danger"|"info"; icon?: ReactNode; children })` → `chamfer-sm rounded-sm px-2 h-6 text-xs font-medium bg-{tone}-soft text-{tone}`.
- `Alert({ tone?: "info"|"success"|"warning"|"danger"; title: ReactNode; children?; action?: ReactNode })` → role `alert` cho danger/warning, `status` cho còn lại; icon Phosphor theo tone.
- `Toaster()` (đặt một lần trong layout) + `toast({ title: string; description?: string; tone?: Alert tone; action?: { label: string; onClick(): void } })`: store module-level + `useSyncExternalStore`; Radix `Toast.Provider duration={4000}`, viewport `fixed bottom-4 right-4 z-toast` (mobile: trên BottomNav `bottom-[calc(5rem+env(safe-area-inset-bottom))]`), `animate-slot-in/out`.
- `Skeleton({ className })` → `aria-hidden` khối `bg-surface-sunken` có shimmer gradient.
- `EmptyState({ title; description?; action?; illustration?: "box" })` → minh hoạ hộp mở nắp isometric (SVG nhỏ, nắp có animation lật một lần).

- [ ] Test: Badge hiện chữ + class tone; Alert danger có role alert; `toast({title:"Đã lưu"})` sau render `<Toaster/>` hiện "Đã lưu" (act); EmptyState có heading + action. FAIL → viết → xanh → commit.

### Task 5: Lớp phủ

**Files:** `overlay.tsx`, `overlay.test.tsx`.

**Produces:**
- `Dialog`, `DialogTrigger`, `DialogClose` (= Radix), `DialogContent({ title; description?; children?; footer?; tone?: "default"|"danger" })` → giữa màn hình, `max-w-md`, `rounded-lg chamfer shadow-box-3`, `animate-lid-open`/`lid-close` (lật từ cạnh trên, `origin-top`).
- `ConfirmDialog({ trigger: ReactNode; title; description?; confirmLabel: string; onConfirm(): void | Promise<void>; tone?: "danger"|"default" })` → nút xác nhận `loading` khi promise chạy.
- `Tooltip({ content: ReactNode; children: ReactElement })` (Radix Tooltip, Provider bên trong, delay 300ms).
- `DropdownMenu({ trigger: ReactNode; items: { label: string; icon?: ReactNode; onSelect(): void; tone?: "danger"; disabled?: boolean }[] ; label: string })`.

- [ ] Test: mở Dialog qua trigger thấy title, Esc đóng; ConfirmDialog gọi onConfirm; DropdownMenu mở bằng bàn phím (Enter) có menuitem. Tooltip: render không lỗi + `aria-describedby` khi focus (Radix) hoặc kiểm hiện khi focus. FAIL → viết → xanh → commit.

### Task 6: Điều hướng

**Files:** `navigation.tsx`, `navigation.test.tsx`.

**Produces:**
- `Tabs({ items: { value: string; label: string; content: ReactNode; count?: number }[]; defaultValue?: string; label: string })` → Radix Tabs; danh sách cuộn ngang trên mobile; mục chọn có vạch dưới `primary`.
- `Breadcrumb({ items: { label: string; href?: string }[]; link?: ElementType })` → `nav aria-label="Đường dẫn"`, mục cuối `aria-current="page"`; mobile chỉ hiện mục cha gần nhất kèm mũi tên lùi.
- `Pagination({ page: number; pageCount: number; onPageChange(p: number): void; label?: string })` → nút trước/sau + số trang rút gọn (1 … p-1 p p+1 … n), nút hiện tại `aria-current="page"`.
- `Stepper({ steps: string[]; current: number })` → `ol`, bước xong có icon check, bước hiện tại `aria-current="step"`; mobile hiện "Bước i/n · tên".

- [ ] Test: Tabs đổi nội dung khi click; Breadcrumb mục cuối aria-current; Pagination dải trang cho (5, 10) = [1,"…",4,5,6,"…",10] (export hàm `pageRange(page, count)`), click sau gọi onPageChange(6); Stepper aria-current step. FAIL → viết → xanh → commit.

### Task 7: Thương mại

**Files:** `commerce.tsx`, `commerce.test.tsx`.

**Produces:**
- `formatVnd(n: number): string` → `"4.300 ₫"` (`Intl.NumberFormat("vi-VN")` + " ₫", khoảng trắng hẹp không ngắt ` `? dùng khoảng trắng thường để test ổn định).
- `Price({ value: number; compareAt?: number; deposit?: number; size?: "sm"|"md"|"lg" })` → giá `font-display font-semibold tabular-nums`; giá gạch `line-through text-muted` + `aria-label` "Giá cũ …"; cọc → dòng "Cọc …".
- `ProductMedia({ src?: string; alt: string; aspect?: "square"|"portrait" })` → khung `chamfer rounded-lg` nền gradient sàn (`bg-[radial-gradient(...)]` từ `surface-raised` tới `surface-sunken`), ảnh `object-contain` trên bục với `shadow-plinth` (lớp ellipse dưới ảnh); không có `src` → placeholder khối isometric; hover (shop) nghiêng `perspective rotateX(4deg)` nhẹ.
- `SpecLabel({ items: { label: string; value: string }[] })` → `dl` font mono, viền nét đứt `border-dashed`, `chamfer-sm`, nhãn `text-muted uppercase`? Không uppercase: nhãn chữ thường cỡ 11px.

- [ ] Test: `formatVnd(14500) === "14.500 ₫"`; Price có giá cũ khi compareAt; ProductMedia không src có `role="img"` + alt; SpecLabel hiện cặp dt/dd. FAIL → viết → xanh → commit.

### Task 8: Bố cục

**Files:** `layout.tsx`, `layout.test.tsx`.

**Produces:**
- `ShopShell({ nav: { href; label; icon?; current? }[]; cartCount?: number; children; link?: ElementType; search?: ReactNode })` → skip link "Bỏ qua tới nội dung", header sticky `h-16` một dòng (Logo, nav từ 1024px, search, nút giỏ có badge số), `main#noi-dung`, footer gọn, BottomNav dưới 640px từ `nav`.
- `AdminShell({ nav: …; children; user?: ReactNode; link? })` → `data-density="compact"`, sidebar `w-60` từ 1024px (Logo + nav dọc), top bar trên mobile/tablet, BottomNav dưới 640px (tối đa 5 mục đầu).
- `PageHeader({ title: ReactNode; description?: ReactNode; breadcrumb?: BreadcrumbProps["items"]; action?: ReactNode })`.

- [ ] Test: ShopShell có link skip tới `#noi-dung`, banner, main; AdminShell có `data-density="compact"`; PageHeader h1. FAIL → viết → xanh → commit.

### Task 9: Trang `/design` + bộ chuyển + test e2e/copy

**Files:** `showcase.tsx`, `showcase-data.ts` (Create), `copy.test.ts` (Create), `e2e/design.spec.ts`, `packages/ui/src/index.ts` (export mọi module mới).

**Nội dung trang:** thanh công cụ dính trên cùng: chọn phương án A/B/C (radio group dạng nút, nhãn "Đá & Cobalt", "Rừng sâu", "Tủ kính đêm") + chế độ Sáng/Tối/Hệ thống; đặt `document.documentElement.dataset.brand/theme`, nhớ bằng `localStorage` (try/catch). Mục: Nhận diện (Logo lớn + mark 3 cỡ, màu, chữ theo thang, hình khối & góc vát, độ sâu, chuyển động có nút "Phát lại"), Nút, Ô nhập, Chọn & đánh dấu, Phản hồi (Badge, Alert, Toast, Skeleton, EmptyState), Lớp phủ, Điều hướng, Thương mại (lưới 4 thẻ sản phẩm: ProductMedia + tên + Price + Badge vật liệu; chi tiết rút gọn với SpecLabel), Bảng ↔ thẻ (đơn sản xuất in 3D), Bố cục (ShopShell/AdminShell thu nhỏ trong khung `aspect-video` có `inert`). Dữ liệu mẫu: figure, chậu cây, đèn ngủ, bình hoa, nhựa PLA; ghi "Dữ liệu mẫu". Giữ: `Điều hướng chính` (BottomNav), bảng caption "Tồn kho hộp carton" đổi thành "Tồn kho mẫu in" và cập nhật `e2e/design.spec.ts` theo.

- [ ] **Step 1: e2e fail trước:** `ac231_2 <app> /design hiển thị đủ nhóm component` kiểm heading level 2: Nhận diện, Nút, Ô nhập, Chọn và đánh dấu, Phản hồi, Lớp phủ, Điều hướng, Thương mại, Bảng và thẻ, Bố cục; `ac231_4` axe chạy 4 breakpoint × `data-theme` light/dark × phương án a/b/c (đặt bằng `page.evaluate` sau khi tải).
- [ ] **Step 2: `copy.test.ts`** (`ac231_2 chuỗi giao diện không có gạch dài và từ sáo`): đọc mọi `.tsx` trong `packages/ui/src` và `apps/*/app`, bỏ comment (`/\/\*[\s\S]*?\*\/|\/\/.*$/gm`), kiểm không chứa `—`, `–`, và không khớp `/nâng tầm|liền mạch|đột phá|trải nghiệm tuyệt vời/i`. Thêm kiểm `apps/*/app/**/*.tsx` không có `#[0-9a-f]{3,8}\b`, `rgb(`, `oklch(`.
- [ ] **Step 3: Viết showcase**, export index, sửa chuỗi trong code hiện có vi phạm `—` (comment không tính).
- [ ] **Step 4:** `pnpm exec playwright test` xanh; `check` PASS. Commit.

### Task 10: Điểm dừng pha 1

- [ ] `OMI_CLI handoff note --wip "#448 pha 1 xong, chờ PO chọn phương án"`.
- [ ] Chạy dev shop (3100) + admin (3101), mở `/design` trên trình duyệt; báo người dùng cách so sánh; DỪNG chờ chọn.
