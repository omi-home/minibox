// Check nhanh cho vòng sửa (omi `check`, #539): lint + typecheck + test của phần bị sửa so với origin/develop.
// `node scripts/check.mjs [base]` chạy test; `node scripts/check.mjs --ci [base]` in output cho job `changes` của CI.
// File không phân loại được → chạy toàn bộ như scripts/test.mjs (an toàn hơn bỏ sót).
import { execFileSync } from "node:child_process";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { basename, join } from "node:path";
import { backend, isMain, root, runAll } from "./run.mjs";
import { step, testSteps } from "./test.mjs";

const ignored = [/^docs\//, /\.md$/i, /^\.github\/ISSUE_TEMPLATE\//];

const scriptTestFiles = () =>
  ["scripts", "database/tests"].flatMap((d) =>
    readdirSync(join(root, d)).filter((f) => f.endsWith(".test.mjs")).map((f) => `${d}/${f}`),
  );

const serviceTests = (svc) => {
  const dir = join(backend, "services", svc, "tests");
  if (!existsSync(dir)) return null;
  return readdirSync(dir)
    .filter((p) => existsSync(join(dir, p, `${p}.csproj`)))
    .map((p) => `services/${svc}/tests/${p}/${p}.csproj`);
};

/** Phân loại file đổi (đường dẫn tương đối root, dấu /) thành phần cần test. */
export function select(files) {
  const sel = { all: false, dotnet: new Set(), contracts: false, frontend: false, deploy: false, workflow: false, scripts: new Set() };
  const tests = scriptTestFiles();
  const referencing = (f) => tests.filter((t) => readFileSync(join(root, t), "utf8").includes(basename(f)));
  const addDotnet = (projects) => (projects ? projects.forEach((p) => sel.dotnet.add(p)) : sel.dotnet.add("*"));

  for (const f of files) {
    if (ignored.some((r) => r.test(f))) continue;
    let m;
    if ((m = f.match(/^src\/backend\/services\/([^/]+)\//))) {
      addDotnet(serviceTests(m[1]));
      sel.contracts = true;
    } else if (f.startsWith("src/backend/")) {
      addDotnet(null);
      sel.contracts = true;
    } else if (f.startsWith("src/frontend/")) {
      sel.frontend = true;
      if (f.startsWith("src/frontend/packages/api-clients/")) sel.contracts = true;
    } else if (f.startsWith("contracts/")) {
      sel.contracts = true;
    } else if (tests.includes(f)) {
      sel.scripts.add(f);
    } else if (f.startsWith("database/")) {
      if ((m = f.match(/^database\/schemas\/([^/]+)\//))) addDotnet(serviceTests(m[1]));
      tests.filter((t) => t.startsWith("database/")).forEach((t) => sel.scripts.add(t));
      referencing(f).forEach((t) => sel.scripts.add(t));
    } else if (/^(scripts|deploy|\.github)\//.test(f)) {
      // deploy/**: thêm test đọc compose qua scripts/dev.mjs; CI build lại image + smoke.
      // .github/workflows/**: CI chạy đủ mọi job để kiểm chính workflow; check local chỉ chạy test đọc workflow.
      if (f.startsWith("deploy/")) sel.deploy = true;
      if (f.startsWith(".github/workflows/")) sel.workflow = true;
      const refs = [...referencing(f), ...(f.startsWith("deploy/") ? referencing("dev.mjs") : [])];
      if (refs.length) refs.forEach((t) => sel.scripts.add(t));
      else if (f.startsWith("scripts/")) sel.scripts.add("*");
      else sel.all = true;
    } else {
      sel.all = true; // file gốc repo (.nvmrc, .gitattributes...) ảnh hưởng mọi phần
    }
  }
  if (sel.dotnet.has("*")) sel.dotnet = new Set(["*"]);
  return sel;
}

export function checkSteps(sel) {
  if (sel.all) return testSteps();
  const steps = [];
  if (sel.dotnet.has("*")) steps.push(step.dotnet);
  else for (const p of [...sel.dotnet].sort()) steps.push({ cmd: "dotnet", args: ["test", "--project", p], cwd: backend });
  if (sel.contracts) {
    // openapi.test so contracts/openapi với bản build vừa sinh: chưa build thì build cả solution.
    if (!sel.dotnet.size) steps.push({ cmd: "dotnet", args: ["build", "Minibox.slnx"], cwd: backend });
    steps.push(step.install, step.contracts);
  }
  if (sel.frontend) steps.push(step.install, step.lint, step.typecheck, step.test);
  if (sel.scripts.has("*")) steps.push(step.scripts);
  else if (sel.scripts.size) steps.push({ cmd: "node", args: ["--test", ...[...sel.scripts].sort()], cwd: root });
  return [...new Set(steps)];
}

/** Output job `changes` của CI: job nặng nào cần chạy. */
export function ciOutputs(sel) {
  if (sel.all || sel.workflow) return { backend: true, frontend: true, scripts: true, e2e: true, images: true };
  const backendJob = sel.dotnet.size > 0 || sel.contracts;
  const frontendJob = sel.frontend;
  return {
    backend: backendJob,
    frontend: frontendJob,
    scripts: sel.scripts.size > 0,
    e2e: frontendJob,
    images: backendJob || frontendJob || sel.deploy,
  };
}

const git = (...args) => execFileSync("git", ["-c", "core.quotepath=off", ...args], { cwd: root, encoding: "utf8" });
const lines = (s) => s.split("\n").map((l) => l.trim()).filter(Boolean);

/** File đổi so với điểm rẽ nhánh từ base (gồm thay đổi chưa commit và file chưa theo dõi); null khi không xác định được. */
export function changedFiles(base) {
  if (!base) return null;
  try {
    const mb = git("merge-base", base, "HEAD").trim();
    return [...new Set([...lines(git("diff", "--name-only", mb)), ...lines(git("ls-files", "--others", "--exclude-standard"))])];
  } catch (e) {
    console.error(`Không so được với ${base} (${e.message.split("\n")[0]}) → chạy toàn bộ.`);
    return null;
  }
}

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  const ci = args[0] === "--ci";
  const base = ci ? args[1] : (args[0] ?? "origin/develop");
  const files = changedFiles(base);
  const sel = files ? select(files) : { ...select([]), all: true };
  if (ci) {
    for (const [k, v] of Object.entries(ciOutputs(sel))) console.log(`${k}=${v}`);
  } else {
    console.log(files ? `File đổi so với ${base}: ${files.length}` : "Chạy toàn bộ.");
    const steps = checkSteps(sel);
    if (!steps.length) console.log("Không có thay đổi cần test (chỉ docs/*.md).");
    process.exit(runAll(steps));
  }
}
