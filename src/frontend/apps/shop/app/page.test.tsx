import { render, screen } from "@testing-library/react";
import { expect, test, vi } from "vitest";
import { Home } from "./home";

vi.mock("../lib/i18n", () => ({
  getTranslator: async () => (key: string) => ({ "home.title": "Minibox" })[key] ?? key,
  localization: () => ({ locales: async () => [{ code: "vi", name: "Tiếng Việt" }, { code: "en", name: "English" }] }),
}));

test("ac174_1 shop render trang chủ", async () => {
  render(await Home({ locale: "en" }));
  expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Minibox");
  expect(screen.getByRole("link", { name: "Tiếng Việt" }).getAttribute("href")).toBe("/");
  expect(screen.getByRole("link", { name: "English" }).getAttribute("aria-current")).toBe("page");
});
