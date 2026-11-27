import { render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import Page from "./page";

test("ac174_1 admin render trang tổng quan", () => {
  render(<Page />);
  expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Tổng quan");
});
