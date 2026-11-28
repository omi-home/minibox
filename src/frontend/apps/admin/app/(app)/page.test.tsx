import { render, screen } from "@testing-library/react";
import { expect, test, vi } from "vitest";
import Page from "./page";

vi.mock("../../lib/i18n", () => ({
  getTranslator: async () => (key: string) => ({ "dashboard.title": "Tổng quan" })[key] ?? key,
}));

test("ac174_1 admin render trang tổng quan", async () => {
  render(await Page());
  expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Tổng quan");
});
