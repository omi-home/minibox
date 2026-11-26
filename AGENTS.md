# Minibox — quy ước cho người và agent

Nguồn sự thật: `docs/technical-spec.md` (kỹ thuật), `docs/business-requirements.md` (nghiệp vụ). Lệnh chuẩn: `node scripts/{test,accept,e2e,dev}.mjs` (spec §9.3).

## Comment tài liệu (spec §8.7)

- **Backend** (`src/backend`, project không phải `*.Tests`): Microsoft XML documentation cho mọi type/member `public`/`protected` — `<summary>`, `<param>`/`<typeparam>`, `<returns>`, `<exception>`, `<see cref>`/`<inheritdoc/>`. Thiếu hoặc sai là lỗi build (CS1591, CS1572/CS1573/CS1574).
- **Frontend** (`src/frontend`): TSDoc cho mọi export — câu tóm tắt, `@param name - mô tả`, `@returns`, `@throws`, `@remarks`/`@example`. `pnpm lint` chặn thiếu comment và sai cú pháp TSDoc.
- Viết tiếng Việt, nói ý nghĩa/hợp đồng chứ không lặp tên hay kiểu. Không bắt buộc với test và file cấu hình.
