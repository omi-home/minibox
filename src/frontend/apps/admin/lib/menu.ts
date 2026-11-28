/** Quyền trong access token: phạm vi (`*`, `wh:<id>`, `farm:<id>`) → danh sách mã quyền (spec §3.6). */
export type Perms = Record<string, string[]>;

/** Mục menu admin: thuộc một trong ba vùng BRD §13, hiện khi người dùng có ít nhất một mã trong `perms`. */
export type MenuItem = {
  group: string;
  label: string;
  /** Nhãn ngắn cho thanh tab dưới trên mobile. */
  short?: string;
  href: string;
  perms: string[];
};

/** Cookie nhớ sidebar thu gọn (`1`); layout đọc lúc render server để không nháy. */
export const SIDEBAR_COOKIE = "minibox_admin_sidebar";

/** Cookie chế độ giao diện (`light` | `dark`; thiếu = theo hệ thống); layout gốc đặt `data-theme` lúc render server. */
export const THEME_COOKIE = "minibox_theme";

/** Cookie ngôn ngữ giao diện admin chọn ở thanh đầu. */
export const LOCALE_COOKIE = "minibox_locale";

/** Cookie múi giờ hiển thị (IANA). */
export const TIMEZONE_COOKIE = "minibox_timezone";

/** Cookie tiền tệ hiển thị (ISO 4217); giao dịch vẫn tính VND. */
export const CURRENCY_COOKIE = "minibox_currency";

/**
 * Múi giờ hiển thị chọn được; mặc định giờ Việt Nam.
 *
 * @remarks ponytail: danh sách cố định cho múi giờ, tiền tệ (ngôn ngữ đọc từ localization).
 */
export const TIMEZONES = [
  { value: "Asia/Ho_Chi_Minh", label: "Việt Nam", hint: "GMT+7" },
  { value: "Asia/Singapore", label: "Singapore", hint: "GMT+8" },
  { value: "Asia/Tokyo", label: "Tokyo", hint: "GMT+9" },
  { value: "Europe/London", label: "London", hint: "GMT+0" },
  { value: "America/Los_Angeles", label: "Los Angeles", hint: "GMT-8" },
  { value: "UTC", label: "UTC", hint: "GMT+0" },
];

/** Tiền tệ hiển thị chọn được; mặc định VND. */
export const CURRENCIES = [
  { value: "VND", label: "Đồng Việt Nam", hint: "₫" },
  { value: "USD", label: "Đô la Mỹ", hint: "$" },
  { value: "EUR", label: "Euro", hint: "€" },
];

/**
 * Lấy giá trị cookie nếu nằm trong danh sách cho phép, không thì giá trị mặc định (phần tử đầu).
 *
 * @param value - Giá trị cookie.
 * @param allowed - Danh sách giá trị hợp lệ.
 * @returns Giá trị hợp lệ.
 */
export function pick(value: string | undefined, allowed: readonly string[]): string {
  return value && allowed.includes(value) ? value : allowed[0]!;
}

/**
 * Chuẩn hoá giá trị cookie giao diện.
 *
 * @param value - Giá trị cookie.
 * @returns `light`/`dark` nếu hợp lệ, còn lại `system`.
 */
export function themeOf(value: string | undefined): "system" | "light" | "dark" {
  return value === "light" || value === "dark" ? value : "system";
}

const BUSINESS = "Nghiệp vụ";
const SETTINGS = "Cài đặt hệ thống và chính sách";
const TECH = "Kỹ thuật và tích hợp";

/** Menu đầy đủ theo thứ tự hiển thị; mã quyền lấy từ seed `identity.permission`. */
export const MENU: MenuItem[] = [
  { group: BUSINESS, label: "Báo cáo", href: "/bao-cao", perms: ["report.dashboard.read"] },
  { group: BUSINESS, label: "Đơn hàng", href: "/don-hang", perms: ["order.order.create", "order.order.confirm"] },
  { group: BUSINESS, label: "Khách hàng", href: "/khach-hang", perms: ["customer.pii.read"] },
  {
    group: BUSINESS,
    label: "Sản xuất",
    href: "/san-xuat",
    perms: ["production.capacity.approve", "production.machine.manage", "production.batch.manage", "production.progress.update"],
  },
  {
    group: BUSINESS,
    label: "Kho",
    href: "/kho",
    perms: [
      "inventory.receipt.create",
      "inventory.issue.create",
      "inventory.reservation.manage",
      "inventory.transfer.create",
      "inventory.stocktake.perform",
      "inventory.adjustment.create",
      "inventory.adjustment.approve",
      "inventory.package.pack",
    ],
  },
  { group: BUSINESS, label: "QC", href: "/qc", perms: ["qc.checklist.perform", "qc.defect.classify"] },
  { group: BUSINESS, label: "Hỗ trợ và bảo hành", short: "Hỗ trợ", href: "/ho-tro", perms: ["support.ticket.resolve", "warranty.claim.process"] },
  {
    group: BUSINESS,
    label: "Mua hàng",
    href: "/mua-hang",
    perms: ["purchasing.supplier.manage", "purchasing.order.create", "purchasing.receipt.create", "purchasing.return.create"],
  },
  {
    group: BUSINESS,
    label: "Tài chính",
    href: "/tai-chinh",
    perms: [
      "finance.payment.confirm",
      "finance.expense.create",
      "finance.deposit.manage",
      "finance.refund.create",
      "finance.receivable.manage",
      "finance.cod.reconcile",
      "finance.invoice.issue",
    ],
  },
  { group: BUSINESS, label: "Sản phẩm", href: "/san-pham", perms: ["catalog.product.manage"] },
  {
    group: BUSINESS,
    label: "Nội dung",
    href: "/noi-dung",
    perms: ["content.post.manage", "content.post.publish", "content.media.manage", "content.comment.moderate"],
  },
  { group: SETTINGS, label: "Vai trò và phân quyền", short: "Phân quyền", href: "/cai-dat/phan-quyen", perms: ["identity.role.manage", "identity.assignment.manage"] },
  { group: SETTINGS, label: "Cấu hình hệ thống", short: "Hệ thống", href: "/cai-dat/he-thong", perms: ["config.setting.manage"] },
  { group: SETTINGS, label: "Cấu hình tài chính", short: "Cấu hình", href: "/cai-dat/tai-chinh", perms: ["finance.setting.manage"] },
  {
    group: SETTINGS,
    label: "Bản dịch giao diện",
    short: "Bản dịch",
    href: "/cai-dat/ban-dich",
    perms: ["localization.message.manage", "localization.message.publish"],
  },
  { group: SETTINGS, label: "Ngôn ngữ", href: "/cai-dat/ngon-ngu", perms: ["localization.locale.manage"] },
  { group: TECH, label: "Tích hợp", href: "/ky-thuat/tich-hop", perms: ["integration.connector.manage", "integration.sync.run"] },
  { group: TECH, label: "Tác vụ nền", href: "/ky-thuat/tac-vu", perms: ["ops.job.manage"] },
  { group: TECH, label: "Lỗi hệ thống", short: "Lỗi", href: "/ky-thuat/loi", perms: ["ops.error.read"] },
  { group: TECH, label: "Nhật ký audit", short: "Audit", href: "/ky-thuat/audit", perms: ["audit.log.read"] },
];

/**
 * Đọc claim `perms` từ access token (không kiểm chữ ký: token do BFF nhận thẳng từ identity, chỉ dùng để lọc menu; quyền thật
 * do API kiểm). Claim có thể là object hoặc chuỗi JSON.
 *
 * @param accessToken - JWT trong phiên.
 * @returns Quyền theo phạm vi; thiếu/sai dạng → rỗng, giá trị không phải chuỗi bị bỏ (fail-closed).
 */
export function readPerms(accessToken: string | undefined): Perms {
  try {
    const payload = JSON.parse(Buffer.from(accessToken?.split(".")[1] ?? "", "base64url").toString("utf8")) as { perms?: unknown };
    const raw: unknown = typeof payload.perms === "string" ? JSON.parse(payload.perms) : payload.perms;
    if (!raw || typeof raw !== "object") return {};
    return Object.fromEntries(
      Object.entries(raw).flatMap(([scope, codes]) =>
        Array.isArray(codes) ? [[scope, codes.filter((c): c is string => typeof c === "string")]] : [],
      ),
    );
  } catch {
    return {};
  }
}

/**
 * Lọc menu theo quyền ở bất kỳ phạm vi nào (phạm vi dữ liệu kiểm ở API khi mở bản ghi).
 *
 * @param perms - Quyền của người dùng.
 * @returns Các mục được thấy, giữ thứ tự và vùng của {@link MENU}.
 */
export function visibleMenu(perms: Perms): MenuItem[] {
  const has = new Set(Object.values(perms).flat());
  return MENU.filter((i) => i.perms.some((p) => has.has(p)));
}
