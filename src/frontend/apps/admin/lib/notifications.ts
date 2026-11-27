/** Thông báo hiện trên chuông ở thanh đầu admin. */
export type Notification = { id: string; title: string; description?: string; time: string; unread?: boolean; href?: string; kind: "order" | "stock" | "approval" };

/**
 * Thông báo của người đang đăng nhập.
 *
 * @returns Danh sách mới nhất trước; rỗng khi chưa có nguồn thông báo.
 * @remarks ponytail: chưa có service thông báo. `ADMIN_DEMO=1` (chỉ máy dev, để duyệt giao diện) trả dữ liệu mẫu; thay bằng
 * API thông báo khi service đó có.
 */
export function notifications(): Notification[] {
  if (process.env.ADMIN_DEMO !== "1") return [];
  return [
    { id: "d1", kind: "order", title: "Đơn DH-1042 chờ xác nhận", description: "Khách Trần Minh Thư, 2 sản phẩm", time: "5 phút trước", unread: true, href: "/don-hang" },
    { id: "d2", kind: "stock", title: "Kho Thủ Đức sắp hết PLA trắng", description: "Còn 3 cuộn, dưới mức tối thiểu 5", time: "32 phút trước", unread: true, href: "/kho" },
    { id: "d3", kind: "approval", title: "Phiếu điều chỉnh tồn PC-118 chờ duyệt", time: "1 giờ trước", unread: true, href: "/kho" },
    { id: "d4", kind: "order", title: "Đơn DH-1039 đã giao xong", time: "Hôm qua" },
  ];
}
