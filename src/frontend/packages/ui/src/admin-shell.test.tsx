import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { AdminShell, type AdminNavItem } from ".";

afterEach(cleanup);
// cmdk đo danh sách bằng ResizeObserver, jsdom không có.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver;
Element.prototype.scrollIntoView ??= () => {};

const nav: AdminNavItem[] = [
  { href: "/kho", label: "Kho", group: "Nghiệp vụ", current: true },
  { href: "/loi", label: "Lỗi hệ thống", group: "Kỹ thuật và tích hợp" },
  { href: "/don", label: "Đơn hàng", group: "Nghiệp vụ" },
  { href: "/phan-quyen", label: "Vai trò và phân quyền", short: "Phân quyền", group: "Cài đặt" },
  { href: "/audit", label: "Nhật ký audit", group: "Kỹ thuật và tích hợp" },
];

const shell = (props: Partial<Parameters<typeof AdminShell>[0]> = {}) =>
  render(
    <AdminShell nav={nav} {...props}>
      Bảng
    </AdminShell>,
  );

test("ac231_2 khung admin bật mật độ compact, có điều hướng chính, thanh đầu và main", () => {
  const { container } = shell();
  expect(container.querySelector("[data-density='compact']")).not.toBeNull();
  expect(screen.getByRole("navigation", { name: "Điều hướng quản trị" })).toBeTruthy();
  expect(screen.getByRole("banner")).toBeTruthy();
  expect(screen.getByRole("main").id).toBe("noi-dung");
});

test("ac208_3 sidebar chia vùng theo group, giữ thứ tự", () => {
  shell();
  const sidebar = screen.getByRole("navigation", { name: "Điều hướng quản trị" });
  expect(within(sidebar).getAllByRole("heading", { level: 2 }).map((h) => h.textContent)).toEqual(["Nghiệp vụ", "Kỹ thuật và tích hợp", "Cài đặt"]);
});

test("nút toggle thu gọn sidebar còn icon (nhãn chỉ cho trình đọc màn hình) và báo lựa chọn để lưu", () => {
  const onCollapsedChange = vi.fn();
  const { container } = shell({ onCollapsedChange });
  fireEvent.click(screen.getByRole("button", { name: "Thu gọn thanh bên" }));
  expect(container.querySelector("[data-sidebar='collapsed']")).not.toBeNull();
  expect(screen.getByRole("button", { name: "Mở rộng thanh bên" }).getAttribute("aria-expanded")).toBe("false");
  const sidebar = screen.getByRole("navigation", { name: "Điều hướng quản trị" });
  expect(within(sidebar).getByRole("link", { name: "Kho" }).querySelector(".sr-only")?.textContent).toBe("Kho");
  expect(onCollapsedChange).toHaveBeenCalledWith(true);
});

test("mobile: thanh tab dưới có 4 mục đầu (nhãn ngắn) + Menu mở menu đầy đủ", () => {
  shell();
  const tabs = screen.getByRole("navigation", { name: "Điều hướng nhanh" });
  expect(within(tabs).getAllByRole("link").map((a) => a.textContent)).toEqual(["Kho", "Lỗi hệ thống", "Đơn hàng", "Phân quyền"]);
  fireEvent.click(within(tabs).getByRole("button", { name: "Menu" }));
  const menu = screen.getByRole("navigation", { name: "Menu đầy đủ" });
  expect(within(menu).getAllByRole("link")).toHaveLength(nav.length);
});

test("Ctrl+K mở tìm trang; gõ không dấu lọc được và Enter chuyển trang", () => {
  const onNavigate = vi.fn();
  shell({ onNavigate });
  fireEvent.keyDown(window, { key: "k", ctrlKey: true });
  const input = screen.getByRole("combobox");
  fireEvent.change(input, { target: { value: "phan quyen" } });
  expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual(["Vai trò và phân quyền"]);
  fireEvent.keyDown(input, { key: "Enter" });
  expect(onNavigate).toHaveBeenCalledWith("/phan-quyen");
});

test("chuông có badge số chưa đọc; thanh đầu chỉ có avatar tròn mở menu, tên + vai trò ghim ở chân sidebar", () => {
  shell({ notifications: { count: 3 }, user: { name: "Mai Anh", detail: "Kho Thủ Đức" } });
  const bell = screen.getByRole("button", { name: "Thông báo, 3 chưa đọc" });
  expect(bell.textContent).toBe("3");
  const avatar = screen.getByRole("button", { name: "Tài khoản Mai Anh" });
  expect(avatar.querySelector("[data-initials]")?.getAttribute("data-initials")).toBe("MA");
  const sidebar = screen.getByRole("navigation", { name: "Điều hướng quản trị" }).parentElement!;
  expect(within(sidebar).getByText("Kho Thủ Đức")).toBeTruthy();
});

test("tiêu đề vùng là nút thu gọn/mở rộng (aria-expanded), sidebar thu gọn thì luôn hiện mục", () => {
  shell();
  const sidebar = screen.getByRole("navigation", { name: "Điều hướng quản trị" });
  const toggle = within(sidebar).getByRole("button", { name: "Nghiệp vụ" });
  expect(toggle.getAttribute("aria-expanded")).toBe("true");
  fireEvent.click(toggle);
  expect(toggle.getAttribute("aria-expanded")).toBe("false");
  expect(within(sidebar).queryByRole("link", { name: "Kho" })).toBeNull();
  fireEvent.click(toggle);
  expect(within(sidebar).getByRole("link", { name: "Kho" })).toBeTruthy();
});

test("nút giao diện một chạm đổi vòng Sáng → Tối → Theo hệ thống", () => {
  const onChange = vi.fn();
  shell({ theme: { value: "light", onChange } });
  fireEvent.click(screen.getAllByRole("button", { name: /^Giao diện: Sáng/ })[0]!);
  expect(onChange).toHaveBeenCalledWith("dark");
  cleanup();
  shell({ theme: { value: "system", onChange } });
  fireEvent.click(screen.getAllByRole("button", { name: /^Giao diện: Theo hệ thống/ })[0]!);
  expect(onChange).toHaveBeenLastCalledWith("light");
});

test("nút vùng (cờ + mã) nói ngôn ngữ, múi giờ, tiền tệ đang dùng", () => {
  shell({
    region: {
      locale: { value: "vi", options: [{ code: "vi", label: "Tiếng Việt" }, { code: "en", label: "English" }], onChange: () => {} },
      timezone: { value: "Asia/Ho_Chi_Minh", options: [{ value: "Asia/Ho_Chi_Minh", label: "Hà Nội, TP.HCM", hint: "GMT+7" }], onChange: () => {} },
      currency: { value: "VND", options: [{ value: "VND", label: "Đồng Việt Nam", hint: "₫" }], onChange: () => {} },
    },
  });
  expect(screen.getAllByRole("button", { name: "Ngôn ngữ và vùng: Tiếng Việt, Hà Nội, TP.HCM, VND" }).length).toBeGreaterThan(0);
});

test("bảng thông báo liệt kê mục, mục chưa đọc có nhãn cho trình đọc màn hình", () => {
  shell({ notifications: { count: 1, items: [{ id: "1", title: "Đơn DH-1042 chờ xác nhận", time: "5 phút trước", unread: true }] } });
  fireEvent.click(screen.getByRole("button", { name: "Thông báo, 1 chưa đọc" }));
  expect(screen.getByText("Đơn DH-1042 chờ xác nhận").textContent).toContain("Chưa đọc");
});

test("breadcrumb dưới thanh đầu suy từ menu: trang chủ › vùng › mục hiện tại", () => {
  shell({ nav: [{ href: "/", label: "Tổng quan" }, ...nav] });
  const crumbs = screen.getByRole("navigation", { name: "Đường dẫn" });
  expect(within(crumbs).getByRole("link", { name: "Tổng quan" }).getAttribute("href")).toBe("/");
  expect(within(crumbs).getByText("Nghiệp vụ")).toBeTruthy();
  expect(within(crumbs).getByText("Kho").getAttribute("aria-current")).toBe("page");
});
