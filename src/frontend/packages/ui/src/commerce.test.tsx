import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, test } from "vitest";
import { Price, ProductMedia, SpecLabel, formatVnd } from ".";

afterEach(cleanup);

test("ac231_2 định dạng VND kiểu Việt Nam", () => {
  expect(formatVnd(14500)).toBe("14.500 ₫");
  expect(formatVnd(1250000)).toBe("1.250.000 ₫");
});

test("ac231_2 giá: giá cũ gạch ngang có nhãn đọc được, giá cọc riêng dòng", () => {
  render(<Price value={189000} compareAt={229000} deposit={60000} />);
  expect(screen.getByText("189.000 ₫")).toBeTruthy();
  expect(screen.getByText("229.000 ₫").closest("s")).not.toBeNull();
  expect(screen.getByText(/Giá cũ/)).toBeTruthy();
  expect(screen.getByText(/Cọc 60.000 ₫/)).toBeTruthy();
});

test("ac231_2 khung ảnh sản phẩm: có ảnh dùng alt; chưa có ảnh hiện khối thay thế có tên", () => {
  render(
    <>
      <ProductMedia src="/a.png" alt="Đèn ngủ khối lập phương" />
      <ProductMedia alt="Chậu cây lục giác" />
    </>,
  );
  expect(screen.getByRole("img", { name: "Đèn ngủ khối lập phương" }).tagName).toBe("IMG");
  expect(screen.getByRole("img", { name: "Chậu cây lục giác" }).tagName.toLowerCase()).toBe("svg");
});

test("ac231_2 tem hộp hiện cặp thông số", () => {
  render(<SpecLabel items={[{ label: "Vật liệu", value: "PLA mờ" }, { label: "Kích thước", value: "12 × 12 × 14 cm" }]} />);
  expect(screen.getByText("Vật liệu").tagName).toBe("DT");
  expect(screen.getByText("12 × 12 × 14 cm").tagName).toBe("DD");
});
