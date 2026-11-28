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

/** Mọi component React viết dạng arrow function (quy ước dự án), kể cả page/layout của Next.js. */
const arrowComponents = {
  files: ["**/*.{ts,tsx}"],
  rules: {
    "react/function-component-definition": ["error", { namedComponents: "arrow-function", unnamedComponents: "arrow-function" }],
    // Rule trên bỏ qua `export default function` (page/layout Next.js): viết `const Page = () => …; export default Page;`.
    "no-restricted-syntax": [
      "error",
      { selector: "ExportDefaultDeclaration > FunctionDeclaration", message: "Component viết dạng arrow function: const X = () => …; export default X;" },
    ],
  },
};

export default [
  ...next,
  ...nextTs,
  { settings: { react: { version: "19" } } },
  arrowComponents,
  docs,
  { ignores: [".next/**", "next-env.d.ts", "coverage/**", "src/generated/**"] },
];
