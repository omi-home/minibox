import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, expect, test } from "vitest";
import { BottomNav, Box, Button, DataTable, Input, Sheet, SheetContent, SheetTrigger } from ".";

afterEach(cleanup);

test("ac231_2 nút: mặc định type=button, asChild giữ phần tử con", () => {
  render(
    <>
      <Button>Mua</Button>
      <Button asChild variant="secondary">
        <a href="/gio">Giỏ</a>
      </Button>
    </>,
  );
  expect(screen.getByRole("button", { name: "Mua" }).getAttribute("type")).toBe("button");
  const link = screen.getByRole("link", { name: "Giỏ" });
  expect(link.className).toContain("h-11");
});

test("ac231_2 ô nhập gắn nhãn, gợi ý và lỗi", () => {
  render(<Input label="Email" hint="Dùng để đăng nhập" error="Sai định dạng" />);
  const input = screen.getByRole("textbox", { name: "Email" });
  expect(input.getAttribute("aria-invalid")).toBe("true");
  expect(input.getAttribute("aria-describedby")?.split(" ").map((id) => document.getElementById(id)?.textContent)).toEqual([
    "Dùng để đăng nhập",
    "Sai định dạng",
  ]);
});

test("ac231_2 thẻ hộp theo tầng độ sâu", () => {
  render(<Box depth={3}>Hộp</Box>);
  expect(screen.getByText("Hộp").className).toMatch(/rounded-lg.*shadow-box-3/);
});

test("ac231_2 sheet mở bằng trigger, có tiêu đề và nút đóng", () => {
  render(
    <Sheet>
      <SheetTrigger asChild>
        <Button>Bộ lọc</Button>
      </SheetTrigger>
      <SheetContent title="Lọc sản phẩm" footer={<Button>Áp dụng</Button>}>
        nội dung
      </SheetContent>
    </Sheet>,
  );
  expect(screen.queryByRole("dialog")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Bộ lọc" }));
  const dialog = screen.getByRole("dialog", { name: "Lọc sản phẩm" });
  expect(within(dialog).getByRole("button", { name: "Áp dụng" }).parentElement?.className).toContain("safe-area-inset-bottom");
  fireEvent.click(within(dialog).getByRole("button", { name: "Đóng" }));
  expect(screen.queryByRole("dialog")).toBeNull();
});

test("ac231_2 bảng↔thẻ: bảng ẩn trên mobile, thẻ ẩn từ tablet, cùng dữ liệu", () => {
  const rows = [{ id: "1", name: "Hộp A", qty: 3 }, { id: "2", name: "Hộp B", qty: 5 }];
  render(
    <DataTable
      caption="Tồn kho"
      rows={rows}
      rowKey={(r) => r.id}
      columns={[{ header: "Tên", cell: (r) => r.name }, { header: "Số lượng", cell: (r) => r.qty, numeric: true }]}
    />,
  );
  const table = screen.getByRole("table", { name: "Tồn kho" });
  expect(table.parentElement?.className).toMatch(/\bhidden\b.*\bsm:block\b/);
  expect(within(table).getAllByRole("row")).toHaveLength(3);
  const cards = screen.getByRole("list", { name: "Tồn kho" });
  expect(cards.className).toContain("sm:hidden");
  expect(within(cards).getAllByRole("listitem").map((li) => li.textContent)).toEqual(["Hộp ASố lượng3", "Hộp BSố lượng5"]);
});

test("ac231_2 điều hướng dưới: chỉ mobile, chừa safe area, đánh dấu trang hiện tại", () => {
  render(
    <BottomNav
      label="Điều hướng chính"
      items={[{ href: "/", label: "Trang chủ", current: true }, { href: "/gio", label: "Giỏ" }]}
    />,
  );
  const nav = screen.getByRole("navigation", { name: "Điều hướng chính" });
  expect(nav.className).toMatch(/fixed.*bottom-0.*safe-area-inset-bottom.*sm:hidden/);
  expect(within(nav).getByRole("link", { name: "Trang chủ" }).getAttribute("aria-current")).toBe("page");
  expect(within(nav).getByRole("link", { name: "Giỏ" }).getAttribute("aria-current")).toBeNull();
});

test("ac231_2 bảng rỗng hiện trạng thái rỗng thay vì bảng trống", () => {
  render(<DataTable caption="Tồn kho" rows={[]} rowKey={() => ""} columns={[]} empty="Chưa có hộp nào" />);
  expect(screen.getByRole("status").textContent).toBe("Chưa có hộp nào");
  expect(screen.queryByRole("table")).toBeNull();
});

test("ac231_2 nút loading bị khoá và aria-busy", () => {
  render(<Button loading>Lưu</Button>);
  const b = screen.getByRole("button", { name: "Lưu" }) as HTMLButtonElement;
  expect(b.disabled).toBe(true);
  expect(b.getAttribute("aria-busy")).toBe("true");
});
