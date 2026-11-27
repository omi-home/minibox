import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { afterEach, expect, test } from "vitest";
import { Checkbox, Input, QuantityInput, RadioGroup, Select, Switch, Textarea } from ".";

afterEach(cleanup);

test("ac231_2 ô dùng được nền trắng, chỉ ô bị khoá mới nền xám (không nhầm là disabled)", () => {
  render(
    <>
      <Input label="Mở" />
      <Input label="Khoá" disabled />
    </>,
  );
  const open = screen.getByRole("textbox", { name: "Mở" }).className.split(" ");
  expect(open).toContain("bg-surface-raised");
  expect(open).not.toContain("bg-surface-sunken");
  expect(open).toContain("disabled:bg-surface-sunken");
});

test("ac231_2 ngày giờ dùng input native, textarea có nhãn", () => {
  render(
    <>
      <Input label="Ngày giao" type="date" />
      <Input label="Hẹn lúc" type="datetime-local" />
      <Textarea label="Ghi chú" />
    </>,
  );
  expect(screen.getByLabelText("Ngày giao").getAttribute("type")).toBe("date");
  expect(screen.getByLabelText("Hẹn lúc").getAttribute("type")).toBe("datetime-local");
  expect(screen.getByRole("textbox", { name: "Ghi chú" }).tagName).toBe("TEXTAREA");
});

test("ac231_2 select native có nhãn, placeholder chọn sẵn và không chọn lại được", () => {
  render(
    <Select label="Tỉnh/thành" placeholder="Chọn tỉnh/thành">
      <option value="hn">Hà Nội</option>
      <option value="hcm">TP. Hồ Chí Minh</option>
    </Select>,
  );
  const select = screen.getByRole("combobox", { name: "Tỉnh/thành" }) as HTMLSelectElement;
  expect(select.value).toBe("");
  expect((select.options[0] as HTMLOptionElement).disabled).toBe(true);
  fireEvent.change(select, { target: { value: "hcm" } });
  expect(select.value).toBe("hcm");
});

const Qty = ({ max }: { max?: number }) => {
  const [v, setV] = useState(1);
  return <QuantityInput label="Số lượng" value={v} onChange={setV} max={max} />;
}

test("ac231_2 số lượng: nút −/+ trong [min, max], gõ tay được kẹp khi rời ô", () => {
  render(<Qty max={3} />);
  const input = screen.getByRole("textbox", { name: "Số lượng" }) as HTMLInputElement;
  const dec = screen.getByRole("button", { name: "Giảm" }) as HTMLButtonElement;
  const inc = screen.getByRole("button", { name: "Tăng" }) as HTMLButtonElement;
  expect(dec.disabled).toBe(true);
  fireEvent.click(inc);
  fireEvent.click(inc);
  expect(input.value).toBe("3");
  expect(inc.disabled).toBe(true);
  fireEvent.change(input, { target: { value: "9a" } });
  fireEvent.blur(input);
  expect(input.value).toBe("3");
  fireEvent.change(input, { target: { value: "0" } });
  fireEvent.blur(input);
  expect(input.value).toBe("1");
});

test("ac231_2 checkbox và switch có nhãn, bấm thì đổi trạng thái", () => {
  render(
    <>
      <Checkbox label="Đồng ý điều khoản" />
      <Switch label="Nhận thông báo đơn" />
    </>,
  );
  const cb = screen.getByRole("checkbox", { name: "Đồng ý điều khoản" });
  fireEvent.click(cb);
  expect(cb.getAttribute("aria-checked")).toBe("true");
  const sw = screen.getByRole("switch", { name: "Nhận thông báo đơn" });
  fireEvent.click(sw);
  expect(sw.getAttribute("aria-checked")).toBe("true");
});

test("ac231_2 nhóm radio dạng thẻ: có tên nhóm, chọn một", () => {
  render(
    <RadioGroup
      label="Giao hàng"
      variant="card"
      defaultValue="std"
      options={[
        { value: "std", label: "Tiêu chuẩn", aside: "22.000 ₫" },
        { value: "fast", label: "Nhanh", aside: "35.000 ₫" },
      ]}
    />,
  );
  expect(screen.getByRole("radiogroup", { name: "Giao hàng" })).toBeTruthy();
  const fast = screen.getByRole("radio", { name: /Nhanh/ });
  fireEvent.click(fast);
  expect(fast.getAttribute("aria-checked")).toBe("true");
  expect(screen.getByRole("radio", { name: /Tiêu chuẩn/ }).getAttribute("aria-checked")).toBe("false");
});
