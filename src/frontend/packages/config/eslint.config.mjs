import next from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

export default [
  ...next,
  ...nextTs,
  { settings: { react: { version: "19" } } },
  { ignores: [".next/**", "next-env.d.ts", "coverage/**"] },
];
