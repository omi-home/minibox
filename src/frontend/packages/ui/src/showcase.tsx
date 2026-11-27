"use client";
import {
  ArrowRightIcon,
  CopyIcon,
  DotsThreeIcon,
  FunnelSimpleIcon,
  HouseIcon,
  MagnifyingGlassIcon,
  PackageIcon,
  PencilSimpleIcon,
  PlusIcon,
  PrinterIcon,
  ShoppingBagIcon,
  SquaresFourIcon,
  TrashIcon,
  UserIcon,
} from "@phosphor-icons/react";
import { useEffect, useState, useSyncExternalStore, type ReactNode } from "react";
import {
  AdminShell,
  Alert,
  Badge,
  BottomNav,
  Box,
  Breadcrumb,
  Button,
  Checkbox,
  Combobox,
  ConfirmDialog,
  CubeLoader,
  DataTable,
  DatePicker,
  DateRangePicker,
  DateTimePicker,
  Dialog,
  DialogClose,
  DialogContent,
  DialogTrigger,
  DropdownMenu,
  EmptyState,
  Input,
  Logo,
  MultiCombobox,
  PageHeader,
  Pagination,
  Price,
  ProductMedia,
  QuantityInput,
  RadioGroup,
  Select,
  Sheet,
  SheetClose,
  SheetContent,
  SheetTrigger,
  ShopShell,
  Skeleton,
  SpecLabel,
  Stepper,
  Switch,
  Tabs,
  Textarea,
  TimePicker,
  Toaster,
  Tooltip,
  formatVnd,
  toast,
  type Tone,
} from ".";
import { featured, jobs, products, provinces, swatches } from "./showcase-data";

const brands = [
  ["a", "Đá & Cobalt"],
  ["b", "Rừng sâu"],
  ["c", "Tủ kính đêm"],
] as const;
const themes = [
  ["system", "Theo hệ thống"],
  ["light", "Sáng"],
  ["dark", "Tối"],
] as const;

const sections = [
  ["nhan-dien", "Nhận diện"],
  ["nut", "Nút"],
  ["o-nhap", "Ô nhập"],
  ["chon", "Chọn và đánh dấu"],
  ["phan-hoi", "Phản hồi"],
  ["lop-phu", "Lớp phủ"],
  ["dieu-huong", "Điều hướng"],
  ["thuong-mai", "Thương mại"],
  ["bang", "Bảng và thẻ"],
  ["bo-cuc", "Bố cục"],
] as const;

const KEY = "minibox-design";
const fallback = '{"brand":"a","theme":"system"}';
const listeners = new Set<() => void>();

/** Lựa chọn đã nhớ dạng chuỗi JSON (bộ nhớ trình duyệt có thể bị chặn thì dùng mặc định). */
const read = () => {
  try {
    return localStorage.getItem(KEY) ?? fallback;
  } catch {
    return fallback;
  }
};
const subscribe = (l: () => void) => {
  listeners.add(l);
  return () => listeners.delete(l);
};
const save = (prefs: { brand: string; theme: string }) => {
  try {
    localStorage.setItem(KEY, JSON.stringify(prefs));
  } catch {
    // Bộ nhớ bị chặn: vẫn đổi được trong phiên, chỉ không nhớ.
    memory = JSON.stringify(prefs);
  }
  listeners.forEach((l) => l());
};
let memory: string | null = null;

/**
 * Nhóm nút chọn một (radio native) dạng thanh phân đoạn, dùng cho bộ chuyển phương án/chế độ.
 *
 * @param props - `legend` là tên nhóm; `name` là tên radio; `options` là [giá trị, nhãn]; `value`/`onChange` điều khiển lựa chọn.
 * @returns Nhóm radio.
 */
const Segmented = ({ legend, name, options, value, onChange }: { legend: string; name: string; options: readonly (readonly [string, string])[]; value: string; onChange: (v: string) => void }) => {
  return (
    <fieldset className="flex items-center gap-2">
      <legend className="sr-only">{legend}</legend>
      <span aria-hidden className="hidden text-xs text-muted 2xl:inline">
        {legend}
      </span>
      <div className="flex rounded-box border border-border bg-surface-sunken p-0.5">
        {options.map(([v, label]) => (
          <label
            key={v}
            className="relative flex h-8 cursor-pointer items-center rounded-[calc(var(--radius-box)-2px)] px-2.5 text-xs font-medium whitespace-nowrap text-muted transition-colors has-checked:bg-surface-raised has-checked:text-foreground has-checked:shadow-box-1 has-focus-visible:outline-2 has-focus-visible:outline-focus"
          >
            <input type="radio" name={name} value={v} checked={value === v} onChange={() => onChange(v)} className="sr-only" />
            {label}
          </label>
        ))}
      </div>
    </fieldset>
  );
};

/**
 * Một mục của trang: tiêu đề, mô tả một câu, nội dung.
 *
 * @param props - `id` là neo liên kết; `title`/`desc` là tiêu đề và mô tả; `children` là nội dung xem trước.
 * @returns Khối section.
 */
const Section = ({ id, title, desc, children }: { id: string; title: string; desc: string; children: ReactNode }) => {
  return (
    <section id={id} aria-labelledby={`${id}-h`} className="flex scroll-mt-24 flex-col gap-5">
      <div className="flex flex-col gap-1.5">
        <h2 id={`${id}-h`} className="text-2xl font-semibold">
          {title}
        </h2>
        <p className="max-w-prose text-sm text-muted">{desc}</p>
      </div>
      {children}
    </section>
  );
};

/**
 * Nhãn phụ nhỏ cho một nhóm trong mục.
 *
 * @param props - `children` là chữ nhãn.
 * @returns Tiêu đề cấp 3.
 */
const Sub = ({ children }: { children: ReactNode }) => <h3 className="text-sm font-semibold text-muted">{children}</h3>;

const shopNav = [
  { href: "#trang-chu", label: "Trang chủ", icon: <HouseIcon />, current: true },
  { href: "#danh-muc", label: "Danh mục", icon: <SquaresFourIcon /> },
  { href: "#gio", label: "Giỏ hàng", icon: <ShoppingBagIcon /> },
  { href: "#toi", label: "Tài khoản", icon: <UserIcon /> },
];
const adminNav = [
  { href: "#don", label: "Đơn hàng", icon: <ShoppingBagIcon />, current: true },
  { href: "#san-xuat", label: "Sản xuất", icon: <PrinterIcon /> },
  { href: "#kho", label: "Kho", icon: <PackageIcon /> },
];

const tone = (t: string) => t as Tone;

/**
 * Trang xem trước design system Minibox (token, nhận diện, component, mẫu bố cục), dùng chung cho shop và admin.
 * Pha 1 #448 có bộ chuyển 3 phương án nhận diện và chế độ sáng/tối để PO so sánh.
 *
 * @returns Nội dung trang `/design`.
 */
export const DesignShowcase = () => {
  const prefs = useSyncExternalStore(subscribe, () => memory ?? read(), () => fallback);
  const { brand, theme } = { brand: "a", theme: "system", ...(JSON.parse(prefs) as { brand?: string; theme?: string }) };
  const setBrand = (b: string) => save({ brand: b, theme });
  const setTheme = (t: string) => save({ brand, theme: t });
  const [qty, setQty] = useState(2);
  const [page, setPage] = useState(3);
  const [replay, setReplay] = useState(0);

  // Đồng bộ lựa chọn lên <html> (theme.css đọc data-brand/data-theme).
  useEffect(() => {
    const root = document.documentElement;
    root.dataset.brand = brand;
    if (theme === "system") delete root.dataset.theme;
    else root.dataset.theme = theme;
  }, [brand, theme]);

  return (
    <>
      <div className="sticky top-0 z-sticky border-b border-border bg-background/85 backdrop-blur-md">
        <div className="mx-auto flex max-w-page flex-wrap items-center gap-x-6 gap-y-2 px-4 py-2.5 sm:px-6 lg:px-8">
          <Logo className="text-base" title="Minibox" />
          <div className="ml-auto flex flex-wrap items-center gap-x-4 gap-y-2">
            <Segmented legend="Phương án" name="brand" options={brands} value={brand} onChange={setBrand} />
            <Segmented legend="Chế độ" name="theme" options={themes} value={theme} onChange={setTheme} />
          </div>
        </div>
      </div>

      <div className="mx-auto grid max-w-page gap-12 px-4 pt-10 pb-16 sm:px-6 lg:grid-cols-[11rem_1fr] lg:gap-16 lg:px-8 lg:pt-14">
        <aside className="hidden lg:block">
          <nav aria-label="Mục lục" className="sticky top-24 flex flex-col text-sm">
            {sections.map(([id, label]) => (
              <a key={id} href={`#${id}`} className="rounded-box px-3 py-1.5 text-muted transition-colors hover:bg-surface-sunken hover:text-foreground">
                {label}
              </a>
            ))}
          </nav>
        </aside>

        <main className="flex min-w-0 flex-col gap-20">
          <header className="flex animate-rise flex-col gap-4">
            <h1 className="max-w-3xl text-4xl font-bold tracking-tight text-balance sm:text-5xl">Design system Minibox</h1>
            <p className="max-w-prose text-lg text-muted">Một bộ token và component cho cửa hàng và trang quản trị. Đổi phương án ở thanh trên cùng để so sánh.</p>
          </header>

          <Section id="nhan-dien" title="Nhận diện" desc="Khối lập phương nhìn theo góc isometric 30 độ. Mỗi phương án có màu, font tiêu đề và mark riêng.">
            <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
              <Box className="flex min-h-56 flex-col justify-between gap-8 bg-surface-raised p-6">
                <div key={replay}>
                  <Logo className="text-5xl sm:text-6xl" />
                </div>
                <div className="flex flex-wrap items-end justify-between gap-4">
                  <div className="flex items-end gap-5 text-foreground">
                    <Logo withWordmark={false} className="text-4xl" title="Mark cỡ lớn" />
                    <Logo withWordmark={false} className="text-2xl" title="Mark cỡ vừa" />
                    <Logo withWordmark={false} className="text-base" title="Mark cỡ favicon" />
                  </div>
                  <Button variant="secondary" onClick={() => setReplay((r) => r + 1)}>
                    Phát lại chuyển động
                  </Button>
                </div>
              </Box>
              <div className="chamfer flex flex-col justify-between gap-6 rounded-lg bg-primary p-6 text-primary-foreground shadow-box-2">
                <Logo withWordmark={false} className="text-5xl text-primary-foreground [&_.fill-primary]:fill-primary-foreground [&_.fill-primary-hover]:fill-primary-foreground/70" title="Mark trên nền màu chính" />
                <p className="font-display text-2xl leading-tight font-semibold text-balance">Đồ in 3D tinh tế, đựng trong những chiếc hộp nhỏ.</p>
              </div>
            </div>

            <div className="flex flex-col gap-6">
              {Object.entries(swatches).map(([group, list]) => (
                <div key={group} className="flex flex-col gap-3">
                  <Sub>{group}</Sub>
                  <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 2xl:grid-cols-6">
                    {list.map(([token, label]) => (
                      <li key={token} className="flex flex-col gap-2">
                        <span className="chamfer-sm h-14 rounded-box border border-border" style={{ background: `var(--color-${token})` }} />
                        <span className="flex flex-col text-sm leading-tight">
                          <span className="font-medium">{label}</span>
                          <code className="font-mono text-xs text-muted">{token}</code>
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>

            <div className="grid gap-4 lg:grid-cols-2">
              <Box className="flex flex-col gap-4">
                <Sub>Chữ</Sub>
                <p className="font-display text-4xl leading-tight font-bold tracking-tight">Hộp nhỏ, đồ in tinh tế</p>
                <p className="font-display text-2xl font-semibold">Tiêu đề mục 24px</p>
                <p className="max-w-prose">
                  Chữ thường Be Vietnam Pro 16px, đủ dấu tiếng Việt: Quý khách đặt in theo mẫu sẽ nhận ảnh duyệt trước khi máy chạy, cọc được hoàn nếu mẫu không đạt.
                </p>
                <p className="font-mono text-sm text-muted">MB-FIG-0142 · 8 × 6 × 11 cm · 0,05 mm</p>
              </Box>
              <Box className="flex flex-col gap-4">
                <Sub>Hình khối và độ sâu</Sub>
                <div className="grid grid-cols-3 gap-3">
                  {([1, 2, 3] as const).map((d) => (
                    <Box key={d} depth={d} className="flex aspect-square flex-col justify-end bg-surface-raised p-3">
                      <span className="text-sm font-medium">Tầng {d}</span>
                      <code className="font-mono text-xs text-muted">shadow-box-{d}</code>
                    </Box>
                  ))}
                </div>
                <p className="text-sm text-muted">Khối chứa vát góc trên phải như mép hộp giấy. Nút và ô nhập bo nhẹ để dễ chạm.</p>
              </Box>
            </div>
          </Section>

          <Section id="nut" title="Nút" desc="Cao 44 điểm cho ngón tay, nhấn thì thu nhỏ một chút. Mỗi màn hình chỉ một nút chính.">
            <Box className="flex flex-col gap-5">
              <div className="flex flex-wrap items-center gap-3">
                <Button>
                  Thêm vào giỏ
                  <ArrowRightIcon aria-hidden />
                </Button>
                <Button variant="secondary">
                  <PlusIcon aria-hidden />
                  Tạo đơn
                </Button>
                <Button variant="ghost">Để sau</Button>
                <Button variant="danger" className="sm:ml-auto">
                  <TrashIcon aria-hidden />
                  Xoá
                </Button>
              </div>
              <div className="flex flex-wrap items-center gap-3 border-t border-border pt-5">
                <Button loading>Đang lưu</Button>
                <Button disabled variant="secondary">
                  Không khả dụng
                </Button>
                <Button asChild variant="ghost">
                  <a href="#bang">Link dạng nút</a>
                </Button>
                <Tooltip content="Sao chép mã đơn">
                  <Button variant="secondary" aria-label="Sao chép mã đơn" className="w-11 px-0">
                    <CopyIcon aria-hidden />
                  </Button>
                </Tooltip>
              </div>
            </Box>
          </Section>

          <Section id="o-nhap" title="Ô nhập" desc="Nền sáng khi dùng được, nền chìm chỉ khi bị khoá. Nhãn ở trên, gợi ý và lỗi ở dưới, lỗi có icon.">
            <Box className="grid gap-5 sm:grid-cols-2">
              <Input label="Họ tên người nhận" autoComplete="name" placeholder="Trần Thị Mai" required />
              <Input label="Email" type="email" autoComplete="email" hint="Gửi xác nhận đơn và hoá đơn" />
              <Input label="Số điện thoại" type="tel" autoComplete="tel" defaultValue="0912 34" error="Số điện thoại cần đủ 10 chữ số" />
              <Input label="Mã giảm giá" disabled defaultValue="Không áp dụng cho đơn này" />
            </Box>
          </Section>

          <Section id="chon" title="Chọn và đánh dấu" desc="Combobox tìm không dấu cho danh sách dài. Lịch tiếng Việt dd/mm/yyyy, giờ 24h, khoảng ngày có lựa chọn nhanh.">
            <Box className="grid gap-5 sm:grid-cols-2">
              <Combobox label="Tỉnh/thành" name="tinh" required placeholder="Chọn tỉnh/thành" searchPlaceholder="Gõ tên, có dấu hoặc không" hint="Thử gõ “da nang”" options={provinces} />
              <Combobox
                label="Sản phẩm"
                name="sp"
                placeholder="Tìm sản phẩm"
                searchPlaceholder="Tên hoặc mã sản phẩm"
                options={products.map((p) => ({ value: p.sku, label: p.name, description: `${p.sku} · ${p.material}`, disabled: p.stock === 0 && p.made === "stock" }))}
              />
              <div className="sm:col-span-2">
                <MultiCombobox label="Khu vực giao hàng" name="khuVuc" max={5} defaultValue={["hn", "dn"]} placeholder="Chọn tỉnh/thành" searchPlaceholder="Gõ tên, có dấu hoặc không" hint="Chọn tối đa 5" options={provinces} />
              </div>
              <Select label="Vật liệu (select native)" defaultValue="pla">
                <optgroup label="FDM">
                  <option value="pla">PLA</option>
                  <option value="petg">PETG</option>
                </optgroup>
                <optgroup label="Resin">
                  <option value="resin">Resin tiêu chuẩn</option>
                </optgroup>
              </Select>
              <DatePicker label="Ngày nhận hàng" name="ngayNhan" min={new Date()} hint="Từ hôm nay, giao trong giờ hành chính" />
              <DateTimePicker label="Hẹn lấy hàng lúc" name="henLay" min={new Date()} />
              <TimePicker label="Giờ mở cửa kho" name="gioMo" defaultValue="08:00" minuteStep={30} />
              <DateRangePicker label="Kỳ báo cáo" nameFrom="tu" nameTo="den" max={new Date()} hint="Có khoảng chọn nhanh; 2 tháng cạnh nhau từ tablet" />
              <QuantityInput label="Số lượng" value={qty} onChange={setQty} max={14} hint="Còn 14 sản phẩm" />
              <div className="sm:col-span-2">
                <Textarea label="Ghi chú cho đơn" placeholder="Khắc tên lên đế, giao trước 10 giờ" />
              </div>
              <div className="flex flex-col gap-1 sm:col-span-2">
                <Checkbox label="Xuất hoá đơn VAT" hint="Nhập thông tin công ty ở bước sau" />
                <Checkbox label="Tôi đồng ý với điều khoản mua hàng" error="Cần đồng ý để đặt hàng" />
                <Checkbox label="Gói quà (tạm ngưng)" disabled />
              </div>
            </Box>
            <div className="grid gap-4 lg:grid-cols-2">
              <Box>
                <RadioGroup
                  label="Phương thức giao hàng"
                  variant="card"
                  defaultValue="std"
                  options={[
                    { value: "std", label: "Giao tiêu chuẩn", description: "2 đến 3 ngày làm việc", aside: formatVnd(22000) },
                    { value: "fast", label: "Giao nhanh", description: "Trong 24 giờ, nội thành", aside: formatVnd(35000) },
                    { value: "pickup", label: "Nhận tại kho", description: "Tạm ngưng trong dịp lễ", aside: "Miễn phí", disabled: true },
                  ]}
                />
              </Box>
              <Box className="flex flex-col gap-4">
                <RadioGroup
                  label="Thanh toán"
                  defaultValue="cod"
                  options={[
                    { value: "cod", label: "Thanh toán khi nhận hàng" },
                    { value: "bank", label: "Chuyển khoản VietQR" },
                  ]}
                />
                <div className="flex flex-col border-t border-border pt-2">
                  <Switch label="Thông báo trạng thái đơn" hint="Qua email và Zalo" defaultChecked />
                  <Switch label="Nhận ưu đãi" />
                </div>
              </Box>
            </div>
          </Section>

          <Section id="phan-hoi" title="Phản hồi" desc="Màu trạng thái luôn đi kèm icon hoặc chữ. Thông báo nói rõ chuyện gì xảy ra và cách xử lý.">
            <div className="flex flex-wrap gap-2">
              <Badge tone="success">Còn hàng</Badge>
              <Badge tone="warning">Sắp hết</Badge>
              <Badge tone="danger">Hết hàng</Badge>
              <Badge tone="info">In theo mẫu</Badge>
              <Badge tone="primary">Mới</Badge>
              <Badge>PLA mờ</Badge>
            </div>
            <div className="grid gap-3 lg:grid-cols-2">
              <Alert tone="danger" title="Không lưu được đơn" action={<Button variant="secondary">Thử lại</Button>}>
                Mất kết nối khi gửi. Đơn vẫn nằm trong bản nháp, chưa bị trừ tồn.
              </Alert>
              <Alert tone="info" title="Đơn in theo mẫu cần duyệt ảnh">
                Minibox gửi ảnh mẫu trong 24 giờ; máy chỉ chạy sau khi bạn duyệt.
              </Alert>
            </div>
            <div className="grid gap-4 lg:grid-cols-2">
              <Box className="flex flex-col gap-4">
                <Sub>Thông báo nổi và chờ tải</Sub>
                <div className="flex flex-wrap gap-3">
                  <Button variant="secondary" onClick={() => toast({ title: "Đã thêm vào giỏ", description: "Đèn ngủ khối lập phương", action: { label: "Xem giỏ", onClick: () => {} } })}>
                    Hiện thông báo
                  </Button>
                  <Button variant="secondary" onClick={() => toast({ title: "Không gửi được ảnh mẫu", tone: "danger" })}>
                    Thông báo lỗi
                  </Button>
                </div>
                <div className="flex items-center gap-4 border-t border-border pt-4">
                  <CubeLoader label="Đang tải danh sách" className="text-primary" />
                  <div className="flex flex-1 flex-col gap-2">
                    <Skeleton className="h-4 w-3/4" />
                    <Skeleton className="h-4 w-1/2" />
                  </div>
                </div>
              </Box>
              <EmptyState title="Giỏ hàng đang trống" description="Sản phẩm bạn thêm sẽ nằm ở đây, kể cả hàng in theo mẫu." action={<Button>Xem sản phẩm</Button>} />
            </div>
          </Section>

          <Section id="lop-phu" title="Lớp phủ" desc="Hộp thoại mở như nắp hộp lật xuống. Sheet trượt từ dưới lên trên điện thoại, thành panel bên phải từ tablet.">
            <Box className="flex flex-wrap items-center gap-3">
              <Dialog>
                <DialogTrigger asChild>
                  <Button variant="secondary">
                    <PencilSimpleIcon aria-hidden />
                    Sửa địa chỉ
                  </Button>
                </DialogTrigger>
                <DialogContent
                  title="Địa chỉ giao hàng"
                  description="Áp dụng cho đơn này"
                  footer={
                    <>
                      <DialogClose asChild>
                        <Button variant="secondary">Huỷ</Button>
                      </DialogClose>
                      <DialogClose asChild>
                        <Button>Lưu địa chỉ</Button>
                      </DialogClose>
                    </>
                  }
                >
                  <Input label="Số nhà, đường" defaultValue="18 Nguyễn Văn Linh" />
                  <Combobox label="Tỉnh/thành" name="tinh2" placeholder="Chọn tỉnh/thành" searchPlaceholder="Gõ tên" options={provinces} />
                </DialogContent>
              </Dialog>
              <ConfirmDialog
                trigger={<Button variant="danger">Huỷ đơn</Button>}
                title="Huỷ đơn MB-2041?"
                description="Tiền cọc 130.000 ₫ được hoàn trong 3 ngày làm việc."
                confirmLabel="Huỷ đơn"
                cancelLabel="Giữ đơn"
                tone="danger"
                onConfirm={() => new Promise((r) => setTimeout(r, 900))}
              />
              <Sheet>
                <SheetTrigger asChild>
                  <Button variant="secondary">
                    <FunnelSimpleIcon aria-hidden />
                    Bộ lọc
                  </Button>
                </SheetTrigger>
                <SheetContent
                  title="Lọc sản phẩm"
                  description="Thu hẹp theo vật liệu và giá"
                  footer={
                    <>
                      <SheetClose asChild>
                        <Button variant="secondary">Đặt lại</Button>
                      </SheetClose>
                      <SheetClose asChild>
                        <Button>Áp dụng</Button>
                      </SheetClose>
                    </>
                  }
                >
                  <Checkbox label="Có sẵn trong kho" defaultChecked />
                  <Checkbox label="In theo mẫu" />
                  <Input label="Giá tối đa" inputMode="numeric" placeholder="500.000 ₫" />
                </SheetContent>
              </Sheet>
              <DropdownMenu
                label="Thao tác đơn"
                trigger={
                  <Button variant="ghost" aria-label="Thao tác khác" className="w-11 px-0">
                    <DotsThreeIcon aria-hidden weight="bold" />
                  </Button>
                }
                items={[
                  { label: "In phiếu giao", icon: <PrinterIcon />, onSelect: () => toast({ title: "Đã gửi lệnh in phiếu" }) },
                  { label: "Sao chép mã đơn", icon: <CopyIcon />, onSelect: () => toast({ title: "Đã sao chép MB-2041" }) },
                  { label: "Huỷ đơn", icon: <TrashIcon />, tone: "danger", onSelect: () => {} },
                ]}
              />
            </Box>
          </Section>

          <Section id="dieu-huong" title="Điều hướng" desc="Mục hiện tại luôn có dấu hiệu ngoài màu: vạch, chữ đậm hoặc ô đặc.">
            <Box className="flex flex-col gap-8">
              <Breadcrumb
                items={[
                  { label: "Trang chủ", href: "#trang-chu" },
                  { label: "Đèn ngủ", href: "#den-ngu" },
                  { label: "Đèn ngủ khối lập phương" },
                ]}
              />
              <Tabs
                label="Trạng thái đơn"
                items={[
                  { value: "cho", label: "Chờ xử lý", count: 3, content: <p className="text-sm text-muted">3 đơn chờ xác nhận thanh toán.</p> },
                  { value: "in", label: "Đang in", count: 12, content: <p className="text-sm text-muted">12 đơn đang chạy trên 5 máy.</p> },
                  { value: "giao", label: "Đang giao", content: <p className="text-sm text-muted">Chưa có đơn đang giao.</p> },
                ]}
              />
              <Stepper steps={["Giỏ hàng", "Giao hàng", "Thanh toán", "Xác nhận"]} current={1} />
              <Pagination page={page} pageCount={12} onPageChange={setPage} />
            </Box>
          </Section>

          <Section id="thuong-mai" title="Thương mại" desc="Ảnh phủ khung có góc vát; ảnh tách nền thì đứng trên bục như trong tủ trưng bày. Thông số in hiện như tem dán thùng hàng. Ảnh minh hoạ từ Unsplash.">
            <ul className="grid grid-cols-2 gap-x-4 gap-y-8 lg:grid-cols-4">
              {products.map((p) => (
                <li key={p.sku}>
                  <a href="#thuong-mai" className="group flex flex-col gap-3 rounded-lg">
                    <ProductMedia src={p.image} alt={p.name} fit="cover" interactive />
                    <div className="flex flex-col gap-1.5">
                      <div className="flex flex-wrap gap-1.5">
                        {p.made === "print" ? <Badge tone="info">In theo mẫu</Badge> : p.stock > 0 && p.stock < 5 ? <Badge tone="warning">Còn {p.stock}</Badge> : null}
                        <Badge>{p.material}</Badge>
                      </div>
                      <h3 className="font-sans text-sm leading-snug font-medium tracking-normal group-hover:underline">{p.name}</h3>
                      <Price value={p.price} compareAt={p.compareAt} deposit={p.deposit} size="sm" />
                    </div>
                  </a>
                </li>
              ))}
            </ul>
            <div className="grid grid-cols-2 items-center gap-4 sm:grid-cols-[12rem_1fr]">
              <ProductMedia alt="Sản phẩm chưa có ảnh" interactive />
              <p className="max-w-prose text-sm text-muted">Ảnh đã tách nền hoặc chưa có ảnh: sản phẩm đứng trên bục có bóng tiếp xúc, mọi thẻ đồng bộ dù ảnh chụp khác nhau.</p>
            </div>
            <Box className="grid gap-6 p-5 sm:grid-cols-[minmax(0,5fr)_minmax(0,7fr)] sm:p-6">
              <ProductMedia src={featured.image} alt={featured.name} aspect="portrait" fit="cover" />
              <div className="flex flex-col gap-5">
                <div className="flex flex-col gap-2">
                  <Badge tone="info" className="self-start">
                    In theo mẫu
                  </Badge>
                  <h3 className="text-3xl font-semibold">{featured.name}</h3>
                  <Price value={featured.price} deposit={featured.deposit} size="lg" />
                </div>
                <p className="max-w-prose text-muted">In nhiều màu theo mẫu, chọn màu lông và tư thế khi đặt. Minibox gửi ảnh duyệt trước khi in; làm trong 5 đến 7 ngày sau khi nhận cọc.</p>
                <SpecLabel
                  className="self-start"
                  items={[
                    { label: "SKU", value: featured.sku },
                    { label: "Vật liệu", value: featured.material },
                    { label: "Kích thước", value: featured.size },
                    { label: "Lớp in", value: featured.layer },
                  ]}
                />
                <div className="flex flex-wrap gap-3">
                  <Button onClick={() => toast({ title: "Đã thêm vào giỏ", description: featured.name })}>
                    <ShoppingBagIcon aria-hidden />
                    Đặt cọc và in
                  </Button>
                  <Button variant="secondary">Hỏi về mẫu</Button>
                </div>
              </div>
            </Box>
          </Section>

          <Section id="bang" title="Bảng và thẻ" desc="Bảng từ tablet trở lên; trên điện thoại mỗi dòng thành một thẻ. Cột số canh phải, chữ số mono.">
            <DataTable
              caption="Đơn sản xuất mẫu"
              rows={[...jobs]}
              rowKey={(r) => r.id}
              columns={[
                { header: "Mã", cell: (r) => <span className="font-mono text-[0.8125rem]">{r.id}</span> },
                { header: "Sản phẩm", cell: (r) => r.product },
                { header: "Máy", cell: (r) => r.printer },
                { header: "Trạng thái", cell: (r) => <Badge tone={tone(r.tone)}>{r.status}</Badge> },
                { header: "SL", cell: (r) => r.qty, numeric: true },
                { header: "Tổng", cell: (r) => formatVnd(r.total), numeric: true },
              ]}
            />
            <DataTable caption="Đơn chờ giao" rows={[]} rowKey={() => ""} columns={[]} empty="Không có đơn nào chờ giao" />
          </Section>

          <Section id="bo-cuc" title="Bố cục" desc="Khung trang dựng sẵn cho shop và admin. Xem trước theo cỡ màn hình hiện tại; admin dùng mật độ gọn.">
            <div className="grid gap-6 2xl:grid-cols-2">
              {(
                [
                  [
                    "Khung shop",
                    <ShopShell key="shop" nav={shopNav} cartCount={2} search={<Input label={<span className="sr-only">Tìm sản phẩm</span>} placeholder="Tìm đèn, chậu, figure" />}>
                      <div className="mx-auto max-w-page px-4 py-6 sm:px-6">
                        <PageHeader title="Đèn ngủ" description="Ánh sáng dịu, in PLA mờ." breadcrumb={[{ label: "Trang chủ", href: "#" }, { label: "Đèn ngủ" }]} />
                      </div>
                    </ShopShell>,
                  ],
                  [
                    "Khung admin",
                    <AdminShell key="admin" nav={adminNav} user={<span className="text-muted">Mai · Kho Thủ Đức</span>}>
                      <div className="flex flex-col gap-4 p-4 sm:p-6">
                        <PageHeader title="Đơn sản xuất" description="Đơn in theo mẫu đã nhận cọc." action={<Button>Tạo đơn</Button>} />
                        <div className="flex items-center gap-2 text-sm text-muted">
                          <MagnifyingGlassIcon aria-hidden className="size-4" />4 đơn hôm nay
                        </div>
                      </div>
                    </AdminShell>,
                  ],
                ] as const
              ).map(([label, shell]) => (
                <figure key={label} className="flex flex-col gap-2">
                  <figcaption className="text-sm font-medium text-muted">{label}</figcaption>
                  {/* Khung xem trước: transform giữ phần tử fixed bên trong; inert + aria-hidden để không lẫn vào điều hướng và tiêu đề thật của trang. */}
                  <div inert aria-hidden="true" className="chamfer h-96 transform-gpu overflow-hidden rounded-lg border border-border-strong bg-background shadow-box-2 select-none">
                    {shell}
                  </div>
                </figure>
              ))}
            </div>
          </Section>
        </main>
      </div>

      <BottomNav
        label="Điều hướng chính"
        items={[
          { href: "/", label: "Trang chủ", icon: <HouseIcon /> },
          { href: "/design", label: "Danh mục", icon: <SquaresFourIcon weight="fill" />, current: true },
          { href: "#gio", label: "Giỏ hàng", icon: <ShoppingBagIcon /> },
          { href: "#toi", label: "Tài khoản", icon: <UserIcon /> },
        ]}
      />
      <Toaster />
    </>
  );
};
