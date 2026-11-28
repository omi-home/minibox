import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeAll, expect, test, vi as mock } from "vitest";
import { Combobox, foldVietnamese, MultiCombobox } from ".";

beforeAll(() => {
  // jsdom thiếu các API trình duyệt mà cmdk gọi khi cuộn tới mục đang chọn.
  Element.prototype.scrollIntoView ||= () => {};
  globalThis.ResizeObserver ||= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
});
afterEach(cleanup);

const provinces = [
  { value: "hn", label: "Hà Nội", group: "Miền Bắc" },
  { value: "hp", label: "Hải Phòng", group: "Miền Bắc" },
  { value: "dn", label: "Đà Nẵng", group: "Miền Trung", description: "Thành phố trực thuộc TW" },
  { value: "hcm", label: "TP. Hồ Chí Minh", group: "Miền Nam" },
  { value: "ct", label: "Cần Thơ", group: "Miền Nam", disabled: true },
];

const visible = () => screen.queryAllByRole("option").map((o) => o.textContent);

test("ac231_2 bỏ dấu tiếng Việt để tìm kiếm", () => {
  expect(foldVietnamese("Đà Nẵng")).toBe("da nang");
  expect(foldVietnamese("TP. Hồ Chí Minh")).toBe("tp. ho chi minh");
});

test("ac231_2 combobox: tìm không dấu, theo từ bất kỳ thứ tự, theo mô tả; không có kết quả thì báo", () => {
  render(<Combobox label="Tỉnh/thành" options={provinces} />);
  fireEvent.click(screen.getByRole("combobox", { name: "Tỉnh/thành" }));
  const search = screen.getByPlaceholderText("Tìm kiếm…");
  fireEvent.change(search, { target: { value: "ha noi" } });
  expect(visible()).toEqual(["Hà Nội"]);
  fireEvent.change(search, { target: { value: "minh ho" } });
  expect(visible()).toEqual(["TP. Hồ Chí Minh"]);
  fireEvent.change(search, { target: { value: "truc thuoc" } });
  expect(visible()[0]).toMatch(/^Đà Nẵng/);
  fireEvent.change(search, { target: { value: "xyz" } });
  expect(visible()).toEqual([]);
  expect(screen.getByText("Không tìm thấy kết quả")).toBeTruthy();
});

test("ac231_2 combobox: chọn bằng chuột/Enter, hiện nhãn, gửi form value, bỏ chọn được", () => {
  const onChange = mock.fn();
  const { container } = render(<Combobox label="Tỉnh/thành" name="tinh" options={provinces} onChange={onChange} />);
  const trigger = screen.getByRole("combobox", { name: "Tỉnh/thành" });
  fireEvent.click(trigger);
  fireEvent.click(screen.getByRole("option", { name: "Hải Phòng" }));
  expect(onChange).toHaveBeenLastCalledWith("hp");
  expect(trigger.textContent).toBe("Hải Phòng");
  expect((container.querySelector('input[name="tinh"]') as HTMLInputElement).value).toBe("hp");

  fireEvent.click(trigger);
  const search = screen.getByPlaceholderText("Tìm kiếm…");
  fireEvent.change(search, { target: { value: "da nang" } });
  fireEvent.keyDown(search, { key: "Enter" });
  expect(onChange).toHaveBeenLastCalledWith("dn");

  fireEvent.click(trigger);
  fireEvent.click(screen.getByRole("button", { name: "Bỏ chọn" }));
  expect(onChange).toHaveBeenLastCalledWith(null);
  expect(trigger.textContent).toBe("Chọn…");
});

test("ac231_2 combobox: lựa chọn bị khoá không chọn được; bắt buộc thì không có Bỏ chọn", () => {
  const onChange = mock.fn();
  render(<Combobox label="Tỉnh" required defaultValue="hn" options={provinces} onChange={onChange} />);
  fireEvent.click(screen.getByRole("combobox", { name: /Tỉnh/ }));
  expect(screen.getByRole("option", { name: "Cần Thơ" }).getAttribute("aria-disabled")).toBe("true");
  fireEvent.click(screen.getByRole("option", { name: "Cần Thơ" }));
  expect(onChange).not.toHaveBeenCalled();
  expect(screen.queryByRole("button", { name: "Bỏ chọn" })).toBeNull();
});

test("ac231_2 chọn nhiều: bấm chọn/bỏ giữ popover mở, chip có nút bỏ, gửi form nhiều giá trị", () => {
  const onChange = mock.fn();
  const { container } = render(<MultiCombobox label="Kho giao" name="kho" options={provinces} onChange={onChange} />);
  const trigger = screen.getByRole("combobox", { name: "Kho giao" });
  fireEvent.click(trigger);
  fireEvent.click(screen.getByRole("option", { name: /^Hà Nội/ }));
  fireEvent.click(screen.getByRole("option", { name: /^Đà Nẵng/ }));
  fireEvent.click(screen.getByRole("option", { name: /^Hải Phòng/ }));
  expect(screen.getByRole("listbox")).toBeTruthy();
  expect(onChange).toHaveBeenLastCalledWith(["hn", "dn", "hp"]);
  expect(trigger.textContent).toBe("Đã chọn 3");
  expect(screen.getByText("Đã chọn 3", { selector: "[aria-live]" })).toBeTruthy();

  fireEvent.click(screen.getByRole("option", { name: /Hà Nội/ }));
  expect(onChange).toHaveBeenLastCalledWith(["dn", "hp"]);
  expect(trigger.textContent).toBe("Đà Nẵng, Hải Phòng");

  fireEvent.click(screen.getByRole("button", { name: "Xong" }));
  expect(screen.queryByRole("listbox")).toBeNull();
  const chips = screen.getByRole("list", { name: "Đã chọn" });
  fireEvent.click(within(chips).getByRole("button", { name: "Bỏ Đà Nẵng" }));
  expect(onChange).toHaveBeenLastCalledWith(["hp"]);
  expect([...container.querySelectorAll<HTMLInputElement>('input[name="kho"]')].map((i) => i.value)).toEqual(["hp"]);
});

test("ac231_2 chọn nhiều: đạt max thì khoá mục chưa chọn; bỏ chọn tất cả", () => {
  const onChange = mock.fn();
  render(<MultiCombobox label="Kho" max={2} defaultValue={["hn", "hp"]} options={provinces} onChange={onChange} />);
  fireEvent.click(screen.getByRole("combobox", { name: "Kho" }));
  expect(screen.getByText("Đã chọn 2/2")).toBeTruthy();
  expect(screen.getByRole("option", { name: "TP. Hồ Chí Minh" }).getAttribute("aria-disabled")).toBe("true");
  expect(screen.getByRole("option", { name: /Hà Nội/ }).getAttribute("aria-disabled")).toBe("false");
  fireEvent.click(screen.getByRole("button", { name: "Bỏ chọn tất cả" }));
  expect(onChange).toHaveBeenLastCalledWith([]);
  expect(screen.getByRole("option", { name: "TP. Hồ Chí Minh" }).getAttribute("aria-disabled")).toBe("false");
});
