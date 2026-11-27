import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { Breadcrumb, Pagination, Stepper, Tabs, pageRange } from ".";

afterEach(cleanup);

test("ac231_2 tab đổi nội dung khi chọn, có số đếm", () => {
  render(
    <Tabs
      label="Đơn hàng"
      items={[
        { value: "cho", label: "Chờ xử lý", count: 3, content: "Danh sách chờ" },
        { value: "in", label: "Đang in", content: "Danh sách đang in" },
      ]}
    />,
  );
  expect(screen.getByRole("tabpanel").textContent).toBe("Danh sách chờ");
  const tab = screen.getByRole("tab", { name: "Đang in" });
  fireEvent.mouseDown(tab);
  fireEvent.click(tab);
  expect(screen.getByRole("tabpanel").textContent).toBe("Danh sách đang in");
  expect(screen.getByRole("tab", { name: /Chờ xử lý/ }).textContent).toContain("3");
});

test("ac231_2 đường dẫn: mục cuối là trang hiện tại", () => {
  render(<Breadcrumb items={[{ label: "Trang chủ", href: "/" }, { label: "Đèn ngủ", href: "/den-ngu" }, { label: "Đèn khối lập phương" }]} />);
  const nav = screen.getByRole("navigation", { name: "Đường dẫn" });
  expect(nav.querySelector("[aria-current='page']")?.textContent).toBe("Đèn khối lập phương");
});

test("ac231_2 dải trang rút gọn quanh trang hiện tại", () => {
  expect(pageRange(5, 10)).toEqual([1, "…", 4, 5, 6, "…", 10]);
  expect(pageRange(1, 4)).toEqual([1, 2, 3, 4]);
  expect(pageRange(2, 10)).toEqual([1, 2, 3, "…", 10]);
});

test("ac231_2 phân trang: trang hiện tại aria-current, bấm sau gọi trang kế", () => {
  const onPageChange = vi.fn();
  render(<Pagination page={5} pageCount={10} onPageChange={onPageChange} />);
  expect(screen.getByRole("button", { name: "Trang 5" }).getAttribute("aria-current")).toBe("page");
  fireEvent.click(screen.getByRole("button", { name: "Trang sau" }));
  expect(onPageChange).toHaveBeenCalledWith(6);
});

test("ac231_2 các bước: bước hiện tại aria-current step", () => {
  render(<Stepper steps={["Giỏ hàng", "Giao hàng", "Thanh toán"]} current={1} />);
  expect(screen.getByText("Giao hàng", { selector: "li *" }).closest("li")?.getAttribute("aria-current")).toBe("step");
});
