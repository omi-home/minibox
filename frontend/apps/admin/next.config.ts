import type { NextConfig } from "next";

const config: NextConfig = {
  reactStrictMode: true,
  transpilePackages: ["@minibox/ui", "@minibox/api-clients"],
};

export default config;
