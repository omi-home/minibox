import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { Button, ConfirmDialog, Dialog, DialogContent, DialogTrigger, DropdownMenu, Tooltip } from ".";

afterEach(cleanup);

test("ac231_2 hộp thoại mở bằng trigger, có tiêu đề, Esc đóng", () => {
  render(
    <Dialog>
      <DialogTrigger asChild>
        <Button>Đổi địa chỉ</Button>
      </DialogTrigger>
      <DialogContent title="Địa chỉ giao hàng">Nội dung</DialogContent>
    </Dialog>,
  );
  fireEvent.click(screen.getByRole("button", { name: "Đổi địa chỉ" }));
  expect(screen.getByRole("dialog", { name: "Địa chỉ giao hàng" })).toBeTruthy();
  fireEvent.keyDown(screen.getByRole("dialog"), { key: "Escape" });
  expect(screen.queryByRole("dialog")).toBeNull();
});

test("ac231_2 hộp thoại xác nhận gọi onConfirm rồi đóng", async () => {
  const onConfirm = vi.fn().mockResolvedValue(undefined);
  render(<ConfirmDialog trigger={<Button variant="danger">Huỷ đơn</Button>} title="Huỷ đơn MB-2041?" confirmLabel="Huỷ đơn" tone="danger" onConfirm={onConfirm} />);
  fireEvent.click(screen.getByRole("button", { name: "Huỷ đơn" }));
  const dialog = screen.getByRole("alertdialog", { name: "Huỷ đơn MB-2041?" });
  await act(async () => fireEvent.click(dialog.querySelector("[data-confirm]")!));
  expect(onConfirm).toHaveBeenCalledOnce();
  expect(screen.queryByRole("alertdialog")).toBeNull();
});

test("ac231_2 menu thả xuống mở bằng bàn phím, chọn mục gọi onSelect", () => {
  const onSelect = vi.fn();
  render(<DropdownMenu label="Thao tác đơn" trigger={<Button variant="ghost">Thao tác</Button>} items={[{ label: "In phiếu", onSelect }, { label: "Huỷ đơn", tone: "danger", onSelect: () => {} }]} />);
  const trigger = screen.getByRole("button", { name: "Thao tác" });
  fireEvent.keyDown(trigger, { key: "Enter" });
  fireEvent.click(screen.getByRole("menuitem", { name: "In phiếu" }));
  expect(onSelect).toHaveBeenCalledOnce();
});

test("ac231_2 tooltip bọc phần tử con mà không đổi tên của nó", () => {
  render(
    <Tooltip content="Sao chép mã đơn">
      <Button aria-label="Sao chép">MB</Button>
    </Tooltip>,
  );
  expect(screen.getByRole("button", { name: "Sao chép" })).toBeTruthy();
});
