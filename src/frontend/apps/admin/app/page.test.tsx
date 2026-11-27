import { render, screen } from "@testing-library/react";
import { expect, test, vi } from "vitest";
import Page from "./page";

vi.mock("next/navigation", () => ({ useRouter: () => ({ replace: vi.fn() }) }));

test("ac174_1 admin render trang chủ", () => {
  render(<Page />);
  expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Minibox Admin");
});
