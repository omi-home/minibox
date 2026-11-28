// Build image app (src/backend/Dockerfile, src/frontend/Dockerfile) và kiểm nội dung. Cần Docker; chạy ở job CI `images`
// (node --test scripts/images.docker-test.mjs), không nằm trong scripts/*.test.mjs vì build 4 image tốn vài phút.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { root } from "./run.mjs";

const docker = (args) => execFileSync("docker", args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 64 << 20 });
const inImage = (image, script) => docker(["run", "--rm", "--entrypoint", "sh", image, "-c", script]).trim();
const build = (name, dockerfile, args) => {
  const tag = `minibox-test/${name}:ci`;
  docker(["build", "-q", "-f", dockerfile, ...Object.entries(args).flatMap(([k, v]) => ["--build-arg", `${k}=${v}`]), "-t", tag, "."]);
  return tag;
};

for (const [svc, project] of [["identity", "Identity"], ["localization", "Localization"]]) {
  test(`ac220_1 image backend-${svc}: user không phải root, không XML doc/pdb, health check`, () => {
    const image = build(`backend-${svc}`, "src/backend/Dockerfile", { SERVICE: svc, PROJECT: project });
    assert.notEqual(inImage(image, "id -u"), "0");
    assert.equal(inImage(image, "find /app -name '*.xml' -o -name '*.pdb'"), "");
    assert.ok(inImage(image, "ls /app").includes(`Minibox.${project}.Api.dll`));
    assert.match(docker(["image", "inspect", "-f", "{{json .Config.Healthcheck}}", image]), /health\/ready/);
    // Không có cấu hình thì dừng ở kiểm cấu hình (không treo, không kết nối gì).
    let out = "";
    try { docker(["run", "--rm", image]); } catch (e) { out = `${e.stdout}${e.stderr}`; }
    assert.match(out, /Thiếu (ConnectionStrings:App|Identity:Authority)/);
  });
}

for (const app of ["admin", "shop"]) {
  test(`ac220_1 image frontend-${app}: standalone, user không phải root, không devDependencies/packages/config`, () => {
    const image = build(`frontend-${app}`, "src/frontend/Dockerfile", { APP: app });
    assert.notEqual(inImage(image, "id -u"), "0");
    assert.equal(inImage(image, `test -f apps/${app}/server.js && echo ok`), "ok");
    assert.equal(inImage(image, "find /app -path '*packages/config*' | head -1"), "");
    assert.equal(inImage(image, "ls node_modules/.pnpm 2>/dev/null | grep -E '^(eslint|typescript|vitest|@playwright)@' | head -1"), "");
  });
}
