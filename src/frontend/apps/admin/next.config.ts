import { fileURLToPath } from "node:url";
import type { NextConfig } from "next";

const config: NextConfig = {
  reactStrictMode: true,
  // Image Docker (src/frontend/Dockerfile): server tự chứa, trace từ gốc pnpm workspace để kèm package nội bộ.
  output: "standalone",
  outputFileTracingRoot: fileURLToPath(new URL("../..", import.meta.url)),
  transpilePackages: ["@minibox/ui", "@minibox/api-clients"],
};

export default config;
