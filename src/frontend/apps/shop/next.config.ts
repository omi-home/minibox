import type { NextConfig } from "next";

const config: NextConfig = {
  reactStrictMode: true,
  // Hai root layout (`(vi)` và `[locale]`, để <html lang> đúng locale) cần trang 404 toàn cục.
  experimental: { globalNotFound: true },
  transpilePackages: ["@minibox/ui", "@minibox/api-clients"],
};

export default config;
