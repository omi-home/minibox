"use client";
import {
  ArrowRightIcon,
  FunnelSimpleIcon,
  HouseIcon,
  PackageIcon,
  PlusIcon,
  ShoppingBagIcon,
  SquaresFourIcon,
  TrashIcon,
  UserIcon,
} from "@phosphor-icons/react";
import {
  BottomNav,
  Box,
  Button,
  Checkbox,
  Combobox,
  DataTable,
  DatePicker,
  DateRangePicker,
  DateTimePicker,
  MultiCombobox,
  Input,
  QuantityInput,
  RadioGroup,
  Select,
  Sheet,
  SheetClose,
  SheetContent,
  SheetTrigger,
  Switch,
  Textarea,
  TimePicker,
} from ".";
import { useState, type ReactNode } from "react";

// Dữ liệu mẫu cho trang xem trước (không phải số liệu thật).
const stock = [
  { sku: "HC-2015", name: "Hộp carton 3 lớp 20×15×10", qty: 1240, price: 4300, status: "Còn hàng" },
  { sku: "HC-3020", name: "Hộp carton 3 lớp 30×20×15", qty: 386, price: 6900, status: "Còn hàng" },
  { sku: "HC-4030", name: "Hộp carton 5 lớp 40×30×25", qty: 27, price: 14500, status: "Sắp hết" },
  { sku: "HN-1010", name: "Hộp nắp gài 10×10×10", qty: 0, price: 3200, status: "Hết hàng" },
];

const provinces = [
  ["hn", "Hà Nội", "Miền Bắc"],
  ["hp", "Hải Phòng", "Miền Bắc"],
  ["qn", "Quảng Ninh", "Miền Bắc"],
  ["bn", "Bắc Ninh", "Miền Bắc"],
  ["hue", "Huế", "Miền Trung"],
  ["dn", "Đà Nẵng", "Miền Trung"],
  ["kh", "Khánh Hòa", "Miền Trung"],
  ["dl", "Lâm Đồng", "Miền Trung"],
  ["hcm", "TP. Hồ Chí Minh", "Miền Nam"],
  ["dnai", "Đồng Nai", "Miền Nam"],
  ["ct", "Cần Thơ", "Miền Nam"],
  ["ag", "An Giang", "Miền Nam"],
].map(([value, label, group]) => ({ value: value!, label: label!, group }));

const vnd = (n: number) => n.toLocaleString("vi-VN") + " ₫";

const swatches = [
  ["background", "Nền trang"],
  ["surface", "Bề mặt"],
  ["surface-sunken", "Bề mặt chìm"],
  ["border", "Viền"],
  ["foreground", "Chữ"],
  ["muted", "Chữ phụ"],
  ["primary", "Chính"],
  ["accent", "Nhấn nhạt"],
  ["success", "Thành công"],
  ["warning", "Cảnh báo"],
  ["danger", "Nguy hiểm"],
  ["focus", "Viền focus"],
] as const;

const sections = [
  ["token", "Token"],
  ["nut", "Nút"],
  ["o-nhap", "Ô nhập"],
  ["chon", "Chọn và đánh dấu"],
  ["the-hop", "Thẻ hộp"],
  ["sheet", "Sheet"],
  ["bang", "Bảng ↔ thẻ"],
] as const;

/**
 * Một mục trên trang design: tiêu đề, mô tả một dòng, khung xem trước.
 *
 * @param props - `id` là neo liên kết; `title`/`desc` là tiêu đề và mô tả; `children` là nội dung xem trước; `i` là thứ tự để hiện lần lượt khi tải.
 * @returns Khối section.
 */
const Section = ({ id, title, desc, children, i }: { id: string; title: string; desc: string; children: ReactNode; i: number }) => {
  return (
    <section
      id={id}
      aria-labelledby={`${id}-h`}
      className="scroll-mt-6 flex animate-rise flex-col gap-4 [transform-origin:top] perspective-[60rem]"
      style={{ animationDelay: `${(i + 1) * 70}ms` }}
    >
      <div className="flex flex-col gap-1">
        <h2 id={`${id}-h`} className="text-xl font-semibold tracking-tight">
          {title}
        </h2>
        <p className="max-w-prose text-sm text-muted">{desc}</p>
      </div>
      {children}
    </section>
  );
}

/**
 * Trang xem trước design system khối hộp (token + component cơ bản) ở 4 breakpoint.
 *
 * @returns Nội dung trang `/design` (shop và admin dùng chung).
 */
export const DesignShowcase = () => {
  const [qty, setQty] = useState(2);
  return (
    <div className="mx-auto grid max-w-page gap-10 px-4 py-8 sm:px-6 lg:grid-cols-[12rem_1fr] lg:gap-14 lg:px-8 lg:py-12">
      <aside className="hidden lg:block">
        <nav aria-label="Mục lục" className="sticky top-12 flex flex-col gap-1 text-sm">
          {sections.map(([id, label]) => (
            <a key={id} href={`#${id}`} className="rounded-box px-3 py-2 text-muted transition-colors hover:bg-surface-sunken hover:text-foreground">
              {label}
            </a>
          ))}
        </nav>
      </aside>

      <main className="flex min-w-0 flex-col gap-14">
        <header className="flex animate-rise flex-col gap-4">
          <div className="flex items-center gap-2.5 text-sm font-semibold">
            <span className="flex size-8 items-center justify-center rounded-box bg-primary text-primary-foreground shadow-box-1">
              <PackageIcon aria-hidden weight="fill" className="size-5" />
            </span>
            Minibox UI
          </div>
          <h1 className="text-3xl font-bold tracking-tight text-balance sm:text-4xl">Design system khối hộp</h1>
          <p className="max-w-prose text-muted">
            Token và component dùng chung cho cửa hàng và trang quản trị. Màu và font đang là bản tạm, chờ bộ nhận diện chính thức.
          </p>
        </header>

        <Section i={0} id="token" title="Token" desc="Một màu nhấn xanh lục, nền xám lạnh. Dark mode tự đổi theo hệ điều hành.">
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 2xl:grid-cols-6">
            {swatches.map(([token, label]) => (
              <li key={token} className="flex flex-col gap-2">
                <span className="h-16 rounded-lg border border-border shadow-box-1" style={{ background: `var(--color-${token})` }} />
                <span className="flex flex-col text-sm leading-tight">
                  <span className="font-medium">{label}</span>
                  <code className="font-mono text-xs text-muted">{token}</code>
                </span>
              </li>
            ))}
          </ul>
          <div className="grid gap-3 sm:grid-cols-3">
            {([1, 2, 3] as const).map((d) => (
              <Box key={d} depth={d} className="flex h-24 flex-col justify-end">
                <span className="text-sm font-medium">Độ sâu {d}</span>
                <code className="font-mono text-xs text-muted">shadow-box-{d}</code>
              </Box>
            ))}
          </div>
        </Section>

        <Section i={1} id="nut" title="Nút" desc="Nổi như phím bấm, nhấn thì lún xuống. Cao 44px cho ngón tay; mỗi màn hình chỉ một nút chính.">
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
            </div>
          </Box>
        </Section>

        <Section i={2} id="o-nhap" title="Ô nhập" desc="Nền trắng khi dùng được, nền xám chỉ khi bị khoá. Nhãn ở trên, gợi ý và lỗi ở dưới; lỗi có icon.">
          <Box className="grid gap-5 sm:grid-cols-2">
            <Input label="Họ tên người nhận" autoComplete="name" placeholder="Trần Thị Mai" required />
            <Input label="Email" type="email" autoComplete="email" hint="Gửi xác nhận đơn và hoá đơn" />
            <Input label="Số điện thoại" type="tel" autoComplete="tel" defaultValue="0912 34" error="Số điện thoại cần đủ 10 chữ số" />
            <Input label="Mã giảm giá" disabled defaultValue="Không áp dụng cho đơn này" />
          </Box>
        </Section>

        <Section i={3} id="chon" title="Chọn và đánh dấu" desc="Combobox tìm kiếm không dấu cho danh sách dài. Lịch tiếng Việt dd/mm/yyyy, giờ 24h, chọn khoảng ngày có khoảng chọn nhanh.">
          <Box className="grid gap-5 sm:grid-cols-2">
            <Combobox
              label="Tỉnh/thành"
              name="tinh"
              required
              placeholder="Chọn tỉnh/thành"
              searchPlaceholder="Gõ tên, có dấu hoặc không"
              hint="Thử gõ “da nang” hoặc “ho chi minh”"
              options={provinces}
            />
            <Combobox label="Sản phẩm" name="sp" placeholder="Tìm sản phẩm" searchPlaceholder="Tên hoặc mã sản phẩm" options={stock.map((p) => ({ value: p.sku, label: p.name, description: `${p.sku} · ${p.status}`, disabled: p.qty === 0 }))} />
            <div className="sm:col-span-2">
              <MultiCombobox label="Khu vực giao hàng" name="khuVuc" max={5} defaultValue={["hn", "dn"]} placeholder="Chọn tỉnh/thành" searchPlaceholder="Gõ tên, có dấu hoặc không" hint="Chọn tối đa 5" options={provinces} />
            </div>
            <Select label="Quy cách (select native)" defaultValue="3">
              <optgroup label="Carton">
                <option value="3">3 lớp</option>
                <option value="5">5 lớp</option>
              </optgroup>
              <optgroup label="Hộp cứng">
                <option value="rigid">Nắp gài</option>
              </optgroup>
            </Select>
            <DatePicker label="Ngày nhận hàng" name="ngayNhan" min={new Date()} hint="Từ hôm nay, giao trong giờ hành chính" />
            <DateTimePicker label="Hẹn lấy hàng lúc" name="henLay" min={new Date()} />
            <TimePicker label="Giờ mở cửa kho" name="gioMo" defaultValue="08:00" minuteStep={30} />
            <DateRangePicker label="Kỳ báo cáo" nameFrom="tu" nameTo="den" max={new Date()} hint="Có khoảng chọn nhanh; 2 tháng cạnh nhau từ tablet" />
            <QuantityInput label="Số lượng" value={qty} onChange={setQty} max={27} hint="Còn 27 hộp" />
            <div className="sm:col-span-2">
              <Textarea label="Ghi chú cho đơn" placeholder="In logo mặt trên, giao trước 10 giờ" />
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
                  { value: "std", label: "Giao tiêu chuẩn", description: "2-3 ngày làm việc", aside: vnd(22000) },
                  { value: "fast", label: "Giao nhanh", description: "Trong 24 giờ, nội thành", aside: vnd(35000) },
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
                  { value: "bank", label: "Chuyển khoản ngân hàng" },
                ]}
              />
              <div className="flex flex-col border-t border-border pt-2">
                <Switch label="Thông báo trạng thái đơn" hint="Qua email và Zalo" defaultChecked />
                <Switch label="Nhận ưu đãi" />
              </div>
            </Box>
          </div>
        </Section>

        <Section i={4} id="the-hop" title="Thẻ hộp" desc="Khối chứa nội dung, có cạnh đáy như thành hộp. Thẻ bấm được nổi lên và nghiêng nhẹ khi rê chuột.">
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {stock.slice(0, 3).map((p) => (
              <li key={p.sku}>
                <Box interactive className="group flex h-full flex-col gap-4">
                  <div className="flex aspect-[4/3] items-center justify-center rounded-box bg-accent text-accent-foreground">
                    <PackageIcon aria-hidden weight="duotone" className="size-16 drop-shadow-[0_6px_6px_oklch(0.2_0.05_162/0.25)] transition-transform duration-300 ease-out-box group-hover:-translate-y-1.5 group-hover:scale-105" />
                  </div>
                  <div className="flex flex-1 flex-col gap-1">
                    <h3 className="font-semibold leading-snug">{p.name}</h3>
                    <p className="text-sm text-muted">Mã {p.sku}</p>
                  </div>
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-lg font-bold tabular-nums">{vnd(p.price)}</span>
                    <Button variant="secondary" aria-label={`Thêm ${p.name} vào giỏ`} className="w-11 px-0">
                      <PlusIcon aria-hidden />
                    </Button>
                  </div>
                </Box>
              </li>
            ))}
          </ul>
        </Section>

        <Section i={5} id="sheet" title="Sheet" desc="Trượt từ dưới lên trên điện thoại, mở thành panel bên phải từ tablet. Dùng cho bộ lọc và chọn biến thể.">
          <Box className="flex flex-wrap items-center justify-between gap-4">
            <p className="text-sm text-muted">Thử thu nhỏ cửa sổ dưới 640px để thấy kiểu bottom sheet.</p>
            <Sheet>
              <SheetTrigger asChild>
                <Button variant="secondary">
                  <FunnelSimpleIcon aria-hidden />
                  Bộ lọc
                </Button>
              </SheetTrigger>
              <SheetContent
                title="Lọc sản phẩm"
                description="Thu hẹp theo kích thước và giá"
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
                <Input label="Chiều dài tối thiểu (cm)" inputMode="numeric" placeholder="20" />
                <Input label="Giá tối đa" inputMode="numeric" placeholder="10.000 ₫" />
              </SheetContent>
            </Sheet>
          </Box>
        </Section>

        <Section i={6} id="bang" title="Bảng ↔ thẻ" desc="Bảng từ tablet trở lên; trên điện thoại mỗi dòng thành một thẻ. Cột số canh phải, chữ số đều.">
          <DataTable
            caption="Tồn kho hộp carton"
            rows={stock}
            rowKey={(r) => r.sku}
            columns={[
              { header: "Sản phẩm", cell: (r) => r.name },
              { header: "Mã", cell: (r) => <code className="font-mono text-xs">{r.sku}</code> },
              { header: "Trạng thái", cell: (r) => r.status },
              { header: "Tồn", cell: (r) => r.qty.toLocaleString("vi-VN"), numeric: true },
              { header: "Đơn giá", cell: (r) => vnd(r.price), numeric: true },
            ]}
          />
          <DataTable caption="Đơn chờ xử lý" rows={[]} rowKey={() => ""} columns={[]} empty="Không có đơn nào đang chờ xử lý" />
        </Section>
      </main>

      <BottomNav
        label="Điều hướng chính"
        items={[
          { href: "/", label: "Trang chủ", icon: <HouseIcon /> },
          { href: "/design", label: "Danh mục", icon: <SquaresFourIcon weight="fill" />, current: true },
          { href: "#gio", label: "Giỏ hàng", icon: <ShoppingBagIcon /> },
          { href: "#toi", label: "Tài khoản", icon: <UserIcon /> },
        ]}
      />
    </div>
  );
};

