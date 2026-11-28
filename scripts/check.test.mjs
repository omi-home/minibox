// scripts/check.mjs (#539): chọn nhóm test theo file đổi so với origin/develop; dùng chung cho `check` và job `changes` của CI.
import { test } from "node:test";
import assert from "node:assert/strict";
import { ciOutputs, checkSteps, select } from "./check.mjs";
import { groups, testSteps } from "./test.mjs";

const cmds = (steps) => steps.map((s) => [s.cmd, ...s.args].join(" "));

test("#539 chỉ docs/*.md/ISSUE_TEMPLATE → không chạy gì, CI bỏ qua mọi job nặng", () => {
  const sel = select(["docs/technical-spec.md", "README.md", "src/frontend/README.md", ".github/ISSUE_TEMPLATE/bug.yml"]);
  assert.deepEqual(checkSteps(sel), []);
  assert.deepEqual(ciOutputs(sel), { backend: false, frontend: false, scripts: false, e2e: false, images: false });
});

test("#539 sửa một service backend → dotnet test đúng project test của service đó + test hợp đồng", () => {
  const sel = select(["src/backend/services/identity/src/Minibox.Identity.Api/Program.cs"]);
  const all = cmds(checkSteps(sel));
  assert.ok(all.some((c) => /dotnet test --project services\/identity\/tests\/Minibox\.Identity\.UnitTests\//.test(c)), all.join("\n"));
  assert.ok(!all.some((c) => /localization|--solution/.test(c)), all.join("\n"));
  assert.ok(all.includes("node --test contracts/tests/*.test.mjs"));
  assert.ok(!all.some((c) => /pnpm (lint|typecheck|test|build)$/.test(c)));
  assert.deepEqual(ciOutputs(sel), { backend: true, frontend: false, scripts: false, e2e: false, images: true });
});

test("#539 building-blocks/props backend → dotnet test cả solution", () => {
  const all = cmds(checkSteps(select(["src/backend/Directory.Build.props"])));
  assert.ok(all.includes("dotnet test --solution Minibox.slnx"));
});

test("#539 migration database/schemas/<svc> → test service đó + database/tests", () => {
  const sel = select(["database/schemas/localization/0002_x.sql"]);
  const all = cmds(checkSteps(sel));
  assert.ok(all.some((c) => c.includes("services/localization/tests/")));
  assert.ok(all.some((c) => c.startsWith("node --test") && c.includes("database/tests/seed.test.mjs")));
  assert.equal(ciOutputs(sel).backend, true);
  assert.equal(ciOutputs(sel).scripts, true);
});

test("#539 frontend đổi → pnpm install/lint/typecheck/test, không dotnet; CI chạy frontend + e2e + images", () => {
  const sel = select(["src/frontend/apps/admin/app/page.tsx"]);
  assert.deepEqual(cmds(checkSteps(sel)), ["pnpm install --frozen-lockfile", "pnpm lint", "pnpm typecheck", "pnpm test"]);
  assert.deepEqual(ciOutputs(sel), { backend: false, frontend: true, scripts: false, e2e: true, images: true });
});

test("#539 script đổi → chỉ node --test các test file nhắc tới nó", () => {
  const all = cmds(checkSteps(select(["scripts/accept.mjs"])));
  assert.equal(all.length, 1);
  assert.match(all[0], /^node --test .*scripts\/scripts\.test\.mjs/);
  assert.doesNotMatch(all[0], /ci\.test|database\/tests/);
  // workflow CI được ci.test.mjs đọc
  const wf = select([".github/workflows/ci.yml"]);
  assert.match(cmds(checkSteps(wf))[0], /scripts\/ci\.test\.mjs/);
  assert.ok(Object.values(ciOutputs(wf)).every(Boolean), "sửa workflow → CI chạy đủ job");
  // chính file test
  assert.deepEqual(cmds(checkSteps(select(["scripts/check.test.mjs"]))), ["node --test scripts/check.test.mjs"]);
});

test("#539 deploy/** → test compose (qua dev.mjs) và CI chạy images", () => {
  const sel = select(["deploy/ci/compose.yaml"]);
  assert.match(cmds(checkSteps(sel))[0], /scripts\/compose\.test\.mjs.*scripts\/ci\.test\.mjs|scripts\/ci\.test\.mjs.*scripts\/compose\.test\.mjs/);
  assert.deepEqual(ciOutputs(sel), { backend: false, frontend: false, scripts: true, e2e: false, images: true });
});

test("#539 hợp đồng/client TS đổi → build backend + pnpm install + test hợp đồng", () => {
  const sel = select(["contracts/openapi/identity.json"]);
  assert.deepEqual(cmds(checkSteps(sel)), [
    "dotnet build Minibox.slnx",
    "pnpm install --frozen-lockfile",
    "node --test contracts/tests/*.test.mjs",
  ]);
  assert.equal(ciOutputs(sel).backend, true);
});

test("#539 file không phân loại được → chạy toàn bộ như scripts/test.mjs", () => {
  const sel = select([".nvmrc"]);
  assert.deepEqual(cmds(checkSteps(sel)), cmds(testSteps()));
  assert.deepEqual(ciOutputs(sel), { backend: true, frontend: true, scripts: true, e2e: true, images: true });
});

test("#539 test.mjs: không tham số = đủ nhóm scripts/backend/frontend như cũ; có tham số = đúng nhóm", () => {
  assert.deepEqual(cmds(testSteps()), [
    "node --test scripts/*.test.mjs database/tests/*.test.mjs",
    "dotnet test --solution Minibox.slnx",
    "pnpm install --frozen-lockfile",
    "node --test contracts/tests/*.test.mjs",
    "pnpm lint",
    "pnpm typecheck",
    "pnpm test",
    "pnpm build",
  ]);
  assert.deepEqual(cmds(testSteps(["e2e"])), ["pnpm install --frozen-lockfile", "node scripts/e2e.mjs"]);
  assert.deepEqual(Object.keys(groups).sort(), ["backend", "e2e", "frontend", "scripts"]);
  assert.throws(() => testSteps(["nope"]), /nope/);
});
