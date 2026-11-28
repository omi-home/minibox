// Kiểm workflow deploy (spec §8.3): develop → build GHCR một lần theo commit → staging; main → production đúng commit staging, qua Environment.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { root } from "./run.mjs";

const wf = readFileSync(join(root, ".github", "workflows", "deploy.yml"), "utf8").replace(/\r\n/g, "\n");
/** Khối YAML của một job (tới job kế tiếp). */
const job = (name) => wf.match(new RegExp(`\\n  ${name}:\\n((?:    .*\\n|\\n)*)`))?.[1] ?? "";

test("ac220_1 merge develop: build 4 image một lần, đẩy GHCR theo tag commit, rồi deploy staging cùng tag", () => {
  assert.match(wf, /^on:\n  push:\n    branches: \[develop, main\]/m);
  const build = job("build");
  assert.match(build, /if: github\.ref == 'refs\/heads\/develop'/);
  assert.match(build, /packages: write/);
  for (const name of ["backend-identity", "backend-localization", "backend-catalog", "frontend-admin", "frontend-shop"]) assert.match(build, new RegExp(`name: ${name},`));
  assert.match(build, /push: true/);
  assert.match(build, /tags: \$\{\{ env\.REGISTRY \}\}\/\$\{\{ matrix\.name \}\}:\$\{\{ github\.sha \}\}/);
  assert.match(wf, /REGISTRY: ghcr\.io\/omi-home\/minibox/);
  const staging = job("deploy-staging");
  assert.match(staging, /needs: build/);
  assert.match(staging, /deploy\/deploy\.sh staging \$\{\{ github\.sha \}\}/);
  assert.match(staging, /git checkout -q --detach \$\{\{ github\.sha \}\}/);
});

test("ac220_2 main: production qua Environment production, dùng commit develop đã chạy staging, không build lại", () => {
  const prod = job("deploy-production");
  assert.match(prod, /if: github\.ref == 'refs\/heads\/main'/);
  assert.match(prod, /environment: production/);
  assert.doesNotMatch(prod, /needs: build|build-push-action/);
  assert.match(prod, /git rev-parse --verify HEAD\^2/);
  assert.match(prod, /deploy\/deploy\.sh production \$\{\{ steps\.staged\.outputs\.sha \}\}/);
});

test("ac220_1 deploy vào tailnet bằng node Tailscale tạm thời có tag, host key SSH được ghim, không chạy song song cùng môi trường", () => {
  for (const name of ["deploy-staging", "deploy-production"]) {
    const j = job(name);
    assert.match(j, /tailscale\/github-action@v3/);
    assert.match(j, /tags: tag:ci/);
    assert.match(j, /known_hosts/);
    assert.doesNotMatch(j, /StrictHostKeyChecking=no/);
    assert.match(j, new RegExp(`concurrency: \\{ group: ${name}, cancel-in-progress: false \\}`));
  }
});

test("ac220_2 secret deploy chỉ dùng trong job có Environment (secret cấp Environment, branch policy chặn nhánh khác)", () => {
  const jobs = wf.split(/\n  (?=[a-z-]+:\n)/).slice(1);
  for (const j of jobs.filter((x) => /secrets\.(DEPLOY_SSH_KEY|TS_OAUTH)/.test(x)))
    assert.match(j, /\n    environment: (staging|production)\n/, j.split("\n")[0]);
  const doc = readFileSync(join(root, "docs", "deploy.md"), "utf8");
  assert.match(doc, /`staging` \| chỉ `develop`/);
  assert.match(doc, /`production` \| chỉ `main`/);
  assert.match(doc, /không đặt ở cấp repo/);
});
