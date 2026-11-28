import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, expect, test, vi as mock } from "vitest";
import { DatePicker, DateRangePicker, DateTimePicker, TimePicker } from ".";

afterEach(cleanup);

const dayCell = (grid: HTMLElement, n: number) =>
  within(grid)
    .getAllByRole("gridcell")
    .find((c) => !c.className.includes("is-outside") && c.textContent === String(n))!
    .querySelector("button")!;

test("ac231_2 chọn ngày: lịch tiếng Việt, tuần bắt đầu thứ Hai, hiện dd/mm/yyyy, gửi form yyyy-mm-dd", () => {
  const onChange = mock.fn();
  const { container } = render(<DatePicker label="Ngày nhận" name="ngay" defaultValue={new Date(2026, 10, 3)} onChange={onChange} />);
  const trigger = screen.getByRole("button", { name: "Ngày nhận" });
  expect(trigger.textContent).toBe("03/11/2026");
  fireEvent.click(trigger);
  const grid = screen.getByRole("grid");
  expect(grid.getAttribute("aria-label")).toBe("Tháng 11 năm 2026");
  expect(screen.getByText("Tháng 11 / 2026")).toBeTruthy();
  expect(grid.querySelector("th")!.textContent).toMatch(/^T2|^th 2/i);
  fireEvent.click(dayCell(grid, 15));
  expect(onChange.mock.calls[0]![0]).toEqual(new Date(2026, 10, 15));
  expect(screen.getByRole("button", { name: "Ngày nhận" }).textContent).toBe("15/11/2026");
  expect((container.querySelector('input[name="ngay"]') as HTMLInputElement).value).toBe("2026-11-15");
});

test("ac231_2 chọn ngày: ngày ngoài [min, max] bị khoá", () => {
  render(<DatePicker label="Ngày" defaultValue={new Date(2026, 10, 10)} min={new Date(2026, 10, 5)} max={new Date(2026, 10, 20)} />);
  fireEvent.click(screen.getByRole("button", { name: "Ngày" }));
  const grid = screen.getByRole("grid");
  expect(dayCell(grid, 4).disabled).toBe(true);
  expect(dayCell(grid, 21).disabled).toBe(true);
  expect(dayCell(grid, 12).disabled).toBe(false);
});

test("ac231_2 chọn khoảng ngày: chọn nháp hai đầu, chỉ ghi nhận khi Áp dụng", () => {
  const onChange = mock.fn();
  render(<DateRangePicker label="Kỳ báo cáo" defaultValue={{ from: new Date(2026, 10, 1), to: new Date(2026, 10, 2) }} onChange={onChange} />);
  const trigger = screen.getByRole("button", { name: "Kỳ báo cáo" });
  expect(trigger.textContent).toBe("01/11/2026 → 02/11/2026");
  fireEvent.click(trigger);
  const [grid] = screen.getAllByRole("grid");
  fireEvent.click(dayCell(grid!, 10));
  fireEvent.click(dayCell(grid!, 16));
  expect(onChange).not.toHaveBeenCalled();
  expect(screen.getByText(/10\/11\/2026 → 16\/11\/2026 \(7 ngày\)/)).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Áp dụng" }));
  expect(onChange.mock.calls[0]![0]).toEqual({ from: new Date(2026, 10, 10), to: new Date(2026, 10, 16) });
  expect(screen.getByRole("button", { name: "Kỳ báo cáo" }).textContent).toBe("10/11/2026 → 16/11/2026");
});

test("ac231_2 chọn khoảng ngày: khoảng chọn nhanh tiếng Việt", () => {
  render(<DateRangePicker label="Kỳ" />);
  fireEvent.click(screen.getByRole("button", { name: "Kỳ" }));
  for (const p of ["Hôm nay", "7 ngày qua", "30 ngày qua", "Tháng này", "Tháng trước"]) expect(screen.getByRole("button", { name: p })).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "7 ngày qua" }));
  expect(screen.getByText(/\(7 ngày\)/)).toBeTruthy();
});

test("ac231_2 chọn ngày giờ: giờ 24h, giữ giờ khi đổi ngày, gửi form yyyy-mm-ddTHH:mm", () => {
  const { container } = render(<DateTimePicker label="Hẹn lấy hàng" name="hen" defaultValue={new Date(2026, 10, 3, 14, 30)} />);
  const trigger = screen.getByRole("button", { name: "Hẹn lấy hàng" });
  expect(trigger.textContent).toBe("03/11/2026 14:30");
  fireEvent.click(trigger);
  fireEvent.click(dayCell(screen.getByRole("grid"), 9));
  fireEvent.click(screen.getByRole("button", { name: "16 giờ" }));
  fireEvent.click(screen.getByRole("button", { name: "45 phút" }));
  expect(screen.getByRole("button", { name: "Hẹn lấy hàng" }).textContent).toBe("09/11/2026 16:45");
  expect((container.querySelector('input[name="hen"]') as HTMLInputElement).value).toBe("2026-11-09T16:45");
});

test("ac231_2 chọn giờ: HH:mm 24h", () => {
  render(<TimePicker label="Giờ mở cửa" defaultValue="08:00" minuteStep={30} />);
  fireEvent.click(screen.getByRole("button", { name: "Giờ mở cửa" }));
  expect(screen.getAllByRole("button", { name: /phút$/ })).toHaveLength(2);
  fireEvent.click(screen.getByRole("button", { name: "20 giờ" }));
  expect(screen.getByRole("button", { name: "Giờ mở cửa" }).textContent).toBe("20:00");
});
