// Sinh type TS từ contracts/openapi/<svc>.json vào src/generated/<svc>.ts (spec §6.3). Không sửa tay file sinh;
// contracts/tests/clients.test.mjs (ac212_3) chạy lại script này và fail nếu kết quả khác bản đã commit.
import { mkdirSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import openapiTS, { astToString } from "openapi-typescript";

const contracts = new URL("../../../../../contracts/openapi/", import.meta.url);
const out = new URL("../src/generated/", import.meta.url);

// Xoá hết để service bị gỡ khỏi contracts cũng mất client.
rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });
for (const file of readdirSync(contracts).filter((f) => f.endsWith(".json")).sort()) {
  const ast = await openapiTS(new URL(file, contracts));
  const header = `// File sinh bởi packages/api-clients/scripts/gen.mjs từ contracts/openapi/${file} — không sửa tay.\n`;
  writeFileSync(new URL(file.replace(/\.json$/, ".ts"), out), header + astToString(ast));
  console.log(`sinh ${fileURLToPath(new URL(file.replace(/\.json$/, ".ts"), out))}`);
}
