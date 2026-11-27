// @vitest-environment node
import { expect, test } from "vitest";
import { MENU, readPerms, visibleMenu } from "./menu";

const jwt = (payload: unknown) => `h.${Buffer.from(JSON.stringify(payload)).toString("base64url")}.s`;

test("ac208_3 menu ba vùng chỉ hiện mục người dùng có quyền (ở bất kỳ phạm vi nào)", () => {
  const items = visibleMenu({ "wh:2": ["inventory.receipt.create"], "*": ["ops.error.read"] });
  expect(items.map((i) => i.label)).toEqual(["Kho", "Lỗi hệ thống"]);
  expect(items.map((i) => i.group)).toEqual(["Nghiệp vụ", "Kỹ thuật và tích hợp"]);
});

test("ac208_3 ba vùng theo BRD §13; mỗi mục khai ít nhất một mã quyền", () => {
  expect([...new Set(MENU.map((i) => i.group))]).toEqual(["Nghiệp vụ", "Cài đặt hệ thống và chính sách", "Kỹ thuật và tích hợp"]);
  for (const i of MENU) expect(i.perms.length).toBeGreaterThan(0);
});

test("ac208_3 không quyền hoặc token hỏng → menu rỗng (fail-closed)", () => {
  expect(visibleMenu({})).toEqual([]);
  expect(readPerms(jwt({ perms: { "*": ["audit.log.read"] } }))).toEqual({ "*": ["audit.log.read"] });
  expect(readPerms(jwt({ perms: JSON.stringify({ "*": ["audit.log.read"] }) }))).toEqual({ "*": ["audit.log.read"] });
  expect(readPerms(jwt({ perms: { "*": "audit.log.read", "wh:2": [null, "x.y.z"] } }))).toEqual({ "wh:2": ["x.y.z"] });
  expect(readPerms(jwt({}))).toEqual({});
  expect(readPerms("khong-phai-jwt")).toEqual({});
  expect(readPerms(undefined)).toEqual({});
});
