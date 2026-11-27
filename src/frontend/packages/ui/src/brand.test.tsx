import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { flyToCart, Logo } from ".";

afterEach(cleanup);

test("ac231_2 logo là ảnh có tên Minibox, kèm chữ thương hiệu", () => {
  render(<Logo />);
  expect(screen.getByRole("img", { name: "Minibox" })).toBeTruthy();
  expect(screen.getByText("inibox")).toBeTruthy();
});

test("ac231_2 logo rút gọn chỉ còn khối M khi withWordmark=false", () => {
  const { container } = render(<Logo withWordmark={false} title="Về trang chủ Minibox" />);
  expect(screen.getByRole("img", { name: "Về trang chủ Minibox" })).toBeTruthy();
  expect(screen.queryByText("inibox")).toBeNull();
  expect(container.querySelectorAll(".logo-cube [data-face]")).toHaveLength(6);
});

test("ac231_2 flyToCart thả hộp chữ M bay vào giỏ rồi dọn đi; không có giỏ thì bỏ qua", async () => {
  const button = document.createElement("button");
  document.body.append(button);
  await flyToCart(button);
  expect(document.querySelector(".cart-fly")).toBeNull();

  const cart = document.createElement("a");
  cart.setAttribute("data-cart-target", "");
  document.body.append(cart);
  let land!: () => void;
  const animate = vi.fn(() => ({ finished: new Promise<void>((r) => (land = r)) }));
  Element.prototype.animate = animate as unknown as Element["animate"];
  const flight = flyToCart(button);
  expect(document.querySelectorAll(".cart-fly .logo-cube [data-face]")).toHaveLength(6);
  land();
  await flight;
  expect(document.querySelector(".cart-fly")).toBeNull();
  expect(animate).toHaveBeenLastCalledWith(expect.any(Array), expect.objectContaining({ duration: 420 }));
  button.remove();
  cart.remove();
});
