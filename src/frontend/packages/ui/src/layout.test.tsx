import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, test } from "vitest";
import { AdminShell, PageHeader, ShopShell } from ".";

afterEach(cleanup);

const nav = [
  { href: "/", label: "Trang chủ", current: true },
  { href: "/danh-muc", label: "Danh mục" },
];

test("ac231_2 khung shop: link bỏ qua tới nội dung, banner, main, giỏ có số", () => {
  render(
    <ShopShell nav={nav} cartCount={2}>
      Nội dung
    </ShopShell>,
  );
  const skip = screen.getByRole("link", { name: "Bỏ qua tới nội dung" });
  expect(skip.getAttribute("href")).toBe("#noi-dung");
  expect(screen.getByRole("banner")).toBeTruthy();
  expect(screen.getByRole("main").id).toBe("noi-dung");
  expect(screen.getByRole("link", { name: "Giỏ hàng, 2 sản phẩm" })).toBeTruthy();
});

test("ac231_2 khung admin bật mật độ compact và có điều hướng chính", () => {
  const { container } = render(<AdminShell nav={nav}>Bảng</AdminShell>);
  expect(container.querySelector("[data-density='compact']")).not.toBeNull();
  expect(screen.getAllByRole("navigation", { name: "Điều hướng quản trị" }).length).toBeGreaterThan(0);
});

test("ac231_2 đầu trang có h1, mô tả và hành động", () => {
  render(<PageHeader title="Đơn sản xuất" description="Đơn in theo mẫu đã nhận cọc" action={<button type="button">Tạo đơn</button>} />);
  expect(screen.getByRole("heading", { level: 1, name: "Đơn sản xuất" })).toBeTruthy();
  expect(screen.getByRole("button", { name: "Tạo đơn" })).toBeTruthy();
});
