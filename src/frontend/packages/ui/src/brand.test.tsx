import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, test } from "vitest";
import { Logo } from ".";

afterEach(cleanup);

test("ac231_2 logo là ảnh có tên Minibox, kèm chữ thương hiệu", () => {
  render(<Logo />);
  expect(screen.getByRole("img", { name: "Minibox" })).toBeTruthy();
  expect(screen.getByText("minibox")).toBeTruthy();
});

test("ac231_2 logo chỉ mark khi withWordmark=false; có đủ 3 biến thể phương án", () => {
  const { container } = render(<Logo withWordmark={false} title="Về trang chủ Minibox" />);
  expect(screen.getByRole("img", { name: "Về trang chủ Minibox" })).toBeTruthy();
  expect(screen.queryByText("minibox")).toBeNull();
  for (const b of ["a", "b", "c"]) expect(container.querySelector(`.logo-${b}`)).not.toBeNull();
});
