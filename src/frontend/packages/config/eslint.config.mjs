import next from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import jsdoc from "eslint-plugin-jsdoc";
import tsdoc from "eslint-plugin-tsdoc";

/** Mọi export public phải có comment TSDoc (spec §8.7). Không áp cho test và file cấu hình. */
const docs = {
  files: ["**/*.{ts,tsx}"],
  ignores: ["**/*.{test,spec}.{ts,tsx}", "**/*.config.{ts,mts}", "e2e/**"],
  plugins: { jsdoc, tsdoc },
  rules: {
    "tsdoc/syntax": "error",
    "jsdoc/require-jsdoc": ["error", {
      publicOnly: true,
      require: { FunctionDeclaration: true, ArrowFunctionExpression: true, ClassDeclaration: true, MethodDefinition: true },
      contexts: ["TSInterfaceDeclaration", "TSTypeAliasDeclaration", "TSEnumDeclaration", "ExportNamedDeclaration > VariableDeclaration"],
    }],
    "jsdoc/require-description": ["error", { contexts: ["any"] }],
  },
};

export default [
  ...next,
  ...nextTs,
  { settings: { react: { version: "19" } } },
  docs,
  { ignores: [".next/**", "next-env.d.ts", "coverage/**", "src/generated/**"] },
];
