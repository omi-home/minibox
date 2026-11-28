// Kiểm workflow CI (spec §9.3, NFR #217, #539): mỗi PR chạy các nhóm test chuẩn + e2e song song trên ubuntu có Docker (Testcontainers), cache NuGet/pnpm.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { composeArgs } from "./dev.mjs";
import { root } from "./run.mjs";

const ci = readFileSync(join(root, ".github", "workflows", "ci.yml"), "utf8");

// Khối YAML của từng job (thụt 2 dấu cách dưới `jobs:`).
const jobs = Object.fromEntries(
  ci.slice(ci.indexOf("\njobs:\n")).split(/\n(?= {2}[\w-]+:\n)/).slice(1).map((b) => [b.match(/^ {2}([\w-]+):/)[1], `${b}\n`]),
);
const HEAVY = ["backend", "frontend", "scripts", "e2e"];

test("ac217_1 CI chạy trên mọi PR: đủ nhóm test chuẩn (gồm Testcontainers) và e2e smoke trên ubuntu-latest", () => {
  assert.match(ci, /^on:\n(?: {2}.*\n)* {2}pull_request:/m);
  for (const job of HEAVY) {
    assert.ok(jobs[job], `thiếu job ${job}`);
    assert.match(jobs[job], /runs-on: ubuntu-latest/); // Docker có sẵn cho Testcontainers
    assert.ok(jobs[job].includes(`run: node scripts/test.mjs ${job}\n`), job);
    assert.match(jobs[job], /timeout-minutes: 15/);
  }
});

test("#539 job nặng chạy song song: chỉ phụ thuộc job changes, không nối tiếp nhau", () => {
  for (const job of [...HEAVY, "images"]) assert.ok(jobs[job].includes("\n    needs: changes\n"), job);
  assert.doesNotMatch(jobs.changes, /needs:/);
});

test("#539 job nặng có if: theo output của job changes (scripts/check.mjs --ci), không dùng paths cấp workflow", () => {
  for (const job of [...HEAVY, "images"]) {
    assert.ok(jobs[job].includes(`\n    if: needs.changes.outputs.${job} == 'true'\n`), job);
    assert.ok(jobs.changes.includes(`\n      ${job}: \${{ steps.c.outputs.${job} }}`), job);
  }
  assert.match(jobs.changes, /node scripts\/check\.mjs --ci/);
  assert.match(jobs.changes, /fetch-depth: 0/);
  assert.doesNotMatch(ci, /paths(-ignore)?:/);
});

test("#539 push chỉ main (PR đã chạy CI, merge squash vào develop); giữ concurrency và quyền tối thiểu", () => {
  assert.ok(ci.includes("\n  push:\n    branches: [main]\n"));
  assert.ok(ci.includes("concurrency:\n  group: ci-${{ github.ref }}\n  cancel-in-progress: true\n"));
  assert.ok(ci.includes("\npermissions:\n  contents: read\n"));
});

test("#539 test.mjs không tham số vẫn chạy đủ backend/frontend/scripts", async () => {
  const { testSteps, groups } = await import("./test.mjs");
  assert.deepEqual(testSteps(), [...new Set([...groups.scripts, ...groups.backend, ...groups.frontend])]);
});

test("ac217_1 CI ghim toolchain theo repo và cache NuGet/pnpm", () => {
  assert.match(ci, /global-json-file: src\/backend\/global\.json/);
  assert.match(ci, /node-version-file: \.nvmrc/);
  const pnpm = JSON.parse(readFileSync(join(root, "src", "frontend", "package.json"), "utf8")).packageManager.split("@")[1];
  assert.match(ci, new RegExp(`version: ${pnpm.replaceAll(".", "\.")}`));
  assert.match(ci, /path: ~\/\.nuget\/packages/);
  assert.match(ci, /cache: pnpm/);
  assert.match(ci, /cache-dependency-path: src\/frontend\/pnpm-lock\.yaml/);
});

test("ac217_1 CI chạy smoke e2e trên stack Compose (sau khi build image) và cache trình duyệt Playwright", () => {
  const images = ci.slice(ci.indexOf("\n  images:"));
  assert.ok(images.indexOf("scripts/images.docker-test.mjs") < images.indexOf("run: node scripts/smoke.mjs"), "smoke sau build image");
  assert.match(images, /playwright install --with-deps chromium/);
  assert.equal(ci.match(/path: ~\/\.cache\/ms-playwright/g)?.length, 2);
});

test("ac217_1 action ghim theo commit SHA, dependabot nâng github-actions", () => {
  const uses = [...ci.matchAll(/uses: (\S+)/g)].map((m) => m[1]);
  assert.ok(uses.length > 0);
  for (const u of uses) assert.match(u, /@[0-9a-f]{40}$/, u);
  assert.match(readFileSync(join(root, ".github", "dependabot.yml"), "utf8"), /package-ecosystem: github-actions/);
});

test("ac217_1 compose ci: image app build tại chỗ từ Dockerfile, chỉ admin mở cổng trên 127.0.0.1", () => {
  const c = JSON.parse(execFileSync("docker", [...composeArgs("ci"), "config", "--format", "json"], { cwd: root, encoding: "utf8" }));
  assert.equal(c.name, "minibox-ci");
  for (const svc of ["backend-identity", "backend-localization", "backend-catalog", "frontend-admin", "frontend-shop"]) {
    assert.match(c.services[svc].build?.dockerfile ?? "", /src\/(backend|frontend)\/Dockerfile$/, svc);
    assert.ok(c.services[svc].healthcheck?.test, `${svc} thiếu healthcheck`);
  }
  const published = Object.entries(c.services).filter(([, s]) => s.ports?.length).map(([n, s]) => [n, s.ports.map((p) => p.host_ip)]);
  assert.deepEqual(published, [["frontend-admin", ["127.0.0.1"]]]);
});
