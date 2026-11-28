import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, test } from "vitest";
import { Alert, Badge, Button, EmptyState, Skeleton, Toaster, toast } from ".";

afterEach(cleanup);

test("ac231_2 badge theo sắc thái, nền nhạt + chữ màu trạng thái", () => {
  render(<Badge tone="success">Còn hàng</Badge>);
  const b = screen.getByText("Còn hàng");
  expect(b.className).toMatch(/bg-success-soft/);
  expect(b.className).toMatch(/text-success/);
});

test("ac231_2 alert: lỗi/cảnh báo là role alert, thông tin là role status", () => {
  render(
    <>
      <Alert tone="danger" title="Không lưu được đơn">
        Kiểm tra kết nối rồi thử lại.
      </Alert>
      <Alert tone="info" title="Đơn in cần duyệt mẫu" />
    </>,
  );
  expect(screen.getByRole("alert").textContent).toContain("Không lưu được đơn");
  expect(screen.getByRole("status").textContent).toContain("Đơn in cần duyệt mẫu");
});

test("ac231_2 toast hiện tiêu đề khi gọi toast() và có nút đóng", () => {
  render(<Toaster />);
  act(() => toast({ title: "Đã thêm vào giỏ", description: "Chậu cây khối lục giác" }));
  expect(screen.getByText("Đã thêm vào giỏ")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Đóng thông báo" })).toBeTruthy();
});

test("ac231_2 skeleton ẩn với trình đọc màn hình; trạng thái rỗng có tiêu đề và hành động", () => {
  const { container } = render(
    <>
      <Skeleton className="h-4 w-32" />
      <EmptyState title="Giỏ hàng đang trống" description="Thêm sản phẩm để đặt hàng." action={<Button>Xem sản phẩm</Button>} />
    </>,
  );
  expect(container.querySelector("[aria-hidden='true'].animate-shimmer")).not.toBeNull();
  expect(screen.getByRole("heading", { name: "Giỏ hàng đang trống" })).toBeTruthy();
  expect(screen.getByRole("button", { name: "Xem sản phẩm" })).toBeTruthy();
});
