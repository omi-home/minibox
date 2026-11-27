# Minibox — quy ước cho người và agent

Nguồn sự thật: `docs/technical-spec.md` (kỹ thuật), `docs/business-requirements.md` (nghiệp vụ). Lệnh chuẩn: `node scripts/{test,accept,e2e,dev}.mjs` (spec §9.3).

## Comment tài liệu (spec §8.7)

- **Backend** (`src/backend`, project không phải `*.Tests`): Microsoft XML documentation cho mọi type/member `public`/`protected` — `<summary>`, `<param>`/`<typeparam>`, `<returns>`, `<exception>`, `<see cref>`/`<inheritdoc/>`. Thiếu hoặc sai là lỗi build (CS1591, CS1572/CS1573/CS1574).
- **Frontend** (`src/frontend`): TSDoc cho mọi export — câu tóm tắt, `@param name - mô tả`, `@returns`, `@throws`, `@remarks`/`@example`. `pnpm lint` chặn thiếu comment và sai cú pháp TSDoc.
- Viết tiếng Việt, nói ý nghĩa/hợp đồng chứ không lặp tên hay kiểu. Không bắt buộc với test và file cấu hình.

## Kiến trúc backend (spec §5.1–§5.4, §4.5)

- Khung chuẩn = template `src/backend/templates/minibox-service` (`dotnet new minibox-service -n <Tên> -o services/<tên>`); đặt code theo bảng §5.2.
- `Program.cs` chỉ đăng ký + map: endpoint ở `Api/Endpoints/<Tên>/` (request → command/query → `ISender.Send` → `.ToHttpResult()`), không dùng Infrastructure/Domain.
- Handler chỉ ở `Application/Features/<Tên>/{Commands,Queries}/…`; port ở `Application/Abstractions/`; Infrastructure chỉ cài port, `DbContext`, `Configurations/`, setup.
- Command qua aggregate + repository, pipeline Mediator lo validation → quyền → transaction/outbox/audit → idempotency (không `SaveChanges`/transaction tay); query qua read service `AsNoTracking` + `Select` ra DTO.
- SQL thô chỉ qua `FromSql`/`ExecuteSql` có tham số; cấm `FromSqlRaw`/`ExecuteSqlRaw`.
- `ArchitectureTests` (`DependencyRules` + `StructureRules`) và test repo chặn vi phạm.

## Component React

- Mọi component (kể cả page/layout/route của Next.js) viết dạng **arrow function**: `export const Button = (props: ButtonProps) => …`;
  default export thì `const Page = () => …; export default Page;`; component generic dùng `<T,>(…) => …`. Hook và hàm tiện ích không bắt buộc.
- `pnpm lint` chặn (`react/function-component-definition` + cấm `export default function`), cấu hình ở `src/frontend/packages/config/eslint.config.mjs`.

## Giao diện

- Trước khi làm UI đọc `docs/design-system.md` (token, component nào dùng khi nào, hình khối, chuyển động, giọng văn, checklist); xem thật ở route `/design`.
