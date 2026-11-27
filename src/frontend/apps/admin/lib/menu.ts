/** Quyền trong access token: phạm vi (`*`, `wh:<id>`, `farm:<id>`) → danh sách mã quyền (spec §3.6). */
export type Perms = Record<string, string[]>;

/** Mục menu admin: thuộc một trong ba vùng BRD §13, hiện khi người dùng có ít nhất một mã trong `perms`. */
export type MenuItem = { group: string; label: string; href: string; perms: string[] };

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
  { group: BUSINESS, label: "Hỗ trợ và bảo hành", href: "/ho-tro", perms: ["support.ticket.resolve", "warranty.claim.process"] },
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
  { group: SETTINGS, label: "Vai trò và phân quyền", href: "/cai-dat/phan-quyen", perms: ["identity.role.manage", "identity.assignment.manage"] },
  { group: SETTINGS, label: "Cấu hình hệ thống", href: "/cai-dat/he-thong", perms: ["config.setting.manage"] },
  { group: SETTINGS, label: "Cấu hình tài chính", href: "/cai-dat/tai-chinh", perms: ["finance.setting.manage"] },
  { group: TECH, label: "Tích hợp", href: "/ky-thuat/tich-hop", perms: ["integration.connector.manage", "integration.sync.run"] },
  { group: TECH, label: "Tác vụ nền", href: "/ky-thuat/tac-vu", perms: ["ops.job.manage"] },
  { group: TECH, label: "Lỗi hệ thống", href: "/ky-thuat/loi", perms: ["ops.error.read"] },
  { group: TECH, label: "Nhật ký audit", href: "/ky-thuat/audit", perms: ["audit.log.read"] },
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
