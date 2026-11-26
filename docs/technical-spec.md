# Minibox — Đặc tả kỹ thuật

Phiên bản: 1.0 — Ngày lập: 25/11/2026 — Nguồn yêu cầu: [business-requirements.md](business-requirements.md) (BRD v1.0).

Tài liệu này chốt các quyết định kỹ thuật để triển khai BRD. Mọi mã `BR-*`, `CFG-*`, `OPEN-*` tham chiếu BRD. Thông số `CFG` vẫn là đề xuất chờ PO (OPEN-01); điểm `OPEN` ảnh hưởng tiền/pháp lý/quyền được giữ trạng thái chờ, không đoán.

## 0. Tóm tắt quyết định

| # | Chủ đề | Quyết định |
|---|---|---|
| D1 | Stack | Backend .NET 10, frontend Next.js (bản ổn định mới nhất lúc khởi tạo, ghim lockfile), PostgreSQL 18, monorepo |
| D2 | Ranh giới | 11 microservice theo miền BRD, mỗi service một schema + role DB riêng |
| D3 | Giao tiếp | REST đồng bộ cho truy vấn; lệnh/sự kiện qua RabbitMQ với transactional outbox/inbox (Wolverine); saga đặt hàng do `order` điều phối |
| D4 | Xác thực | Identity tự xây (không dùng ASP.NET Core Identity), tự cấp JWT + refresh token; MFA TOTP bắt buộc cho nhân viên |
| D5 | Phân quyền | Quyền + phạm vi farm/kho nằm trong access token 5 phút; thư viện kiểm tra dùng chung |
| D6 | Frontend | 2 app Next.js App Router (`shop`, `admin`) làm BFF; không có API gateway; client TS sinh từ OpenAPI |
| D7 | File | SeaweedFS (API S3) tự host, bucket và credential theo service, presigned URL |
| D8 | Tìm kiếm | PostgreSQL `unaccent` + `pg_trgm` trong schema `catalog` |
| D9 | Database as code | Bootstrap SQL idempotent + migration SQL thuần có phiên bản (DbUp); truy cập dữ liệu bằng Dapper |
| D10 | Kiến trúc mã | Clean Architecture, CQRS, Generic Repository, Generic Unit of Work, Mediator pipeline; luật phụ thuộc kiểm bằng architecture test |
| D11 | Repo | Root sạch (không `package.json`/`node_modules`): code ở `src/frontend/`, `src/backend/`; `database/`, `contracts/`, `deploy/`, `scripts/` cùng cấp `src/` (CR #348) |
| D12 | Môi trường | dev trên máy dev; staging + production trên homeserver Ubuntu bằng Docker Compose |
| D13 | Deploy | Image trên GHCR; GitHub Actions vào tailnet (Tailscale) và SSH chạy script deploy; production qua Environment có owner duyệt |
| D14 | Secret | File trên server ngoài repo, mount dạng Docker secret |
| D15 | Observability | OpenTelemetry → Grafana LGTM (Loki, Tempo, Prometheus, Grafana) |
| D16 | Backup | pgBackRest + WAL liên tục (PITR); restic cho file và secret; restore thử tự động hằng tuần |
| D17 | Test | xUnit v3, Testcontainers, NetArchTest, oasdiff, Vitest, Playwright; lệnh chuẩn qua pnpm script ở root |
| D18 | "Hợp đồng mẫu" M0 | Hợp đồng API/sự kiện mẫu (OpenAPI của `identity` + sự kiện `identity.AccountRegistered.v1`), không phải văn bản pháp lý |

## 1. Kiến trúc tổng thể và ranh giới service

11 service .NET 10 sau Caddy (TLS) và hai app Next.js làm BFF. Chỉ Caddy mở ra ngoài; service chỉ nằm trong mạng nội bộ của môi trường.

| Service | Sở hữu (nguồn sự thật) | Xuất hiện từ |
|---|---|---|
| `identity` | Tài khoản khách/nhân viên, mật khẩu, MFA, liên kết Google/Facebook, token, vai trò/quyền/phạm vi farm-kho, claim đơn guest | M0 |
| `catalog` | Danh mục, thuộc tính, sản phẩm/biến thể/SKU, giá niêm yết, xuất bản, file số + hồ sơ quyền + giấy phép + quyền tải, tìm kiếm, wishlist/báo có hàng | M1 |
| `content` | CMS, media công khai, đánh giá, hỏi đáp/bình luận | M1 (media), M6 đầy đủ |
| `order` | Giỏ, báo giá, khuyến mãi, đơn/dòng đơn, snapshot, saga đặt hàng, trả hàng/bảo hành theo dòng | M1 (giỏ), M2 |
| `payment` | Kế hoạch thanh toán, nghĩa vụ (file/cọc/dư/phí giao), tiền vào và phân bổ, VietQR, COD, hoàn tiền thủ công, đối soát | M2 |
| `minixu` | Sổ xu theo lô, giữ/tiêu/hoàn/thu hồi, hạn, claim xu guest | M2 (giữ/tiêu), M4 đầy đủ |
| `inventory` | Kho/khu/bin, tồn theo trạng thái, lô/serial/cuộn, giữ tồn, điều chuyển, kiểm kê, cách ly/loại B, đóng kiện, vận đơn GHN/GHTK/SPX | M2 (giữ tồn), M3 |
| `farm` | Farm, máy, vật liệu, BOM/routing có phiên bản, yêu cầu in, mẻ, QC, lỗi/in lại, bảo trì, giá thành sản xuất | M3 |
| `purchasing` | Nhà cung cấp, đề nghị/đơn mua, nhận hàng, trả NCC, công nợ, đề xuất bổ sung tồn | M5 (nhận hàng cơ bản ở M3) |
| `finance` | Chủ thể pháp lý theo thời gian, hóa đơn điện tử, thu/chi/điều chỉnh, kỳ đối soát, báo cáo lợi nhuận/tiền (read model từ sự kiện) | M4 |
| `support` | Ticket, ghi chú nội bộ, thông báo email/in-app (gộp, chống gửi lặp) | M4 |

Quy tắc ranh giới:

- Không service nào đọc schema của service khác. Dữ liệu chéo lấy qua REST hoặc read model dựng từ sự kiện.
- Mỗi service giữ phần CFG của mình (bảng tham số có lịch hiệu lực) và audit append-only trong schema của mình, qua building blocks dùng chung.
- Dashboard admin do BFF `admin` ghép từ API từng service; báo cáo liên miền (lợi nhuận, tiền) do `finance` dựng từ sự kiện.
- Building blocks chỉ chứa hạ tầng (quyền, outbox/inbox, audit, kiểu tiền, OpenTelemetry), không chứa nghiệp vụ.

## 2. Giao tiếp và tính nhất quán

### 2.1 Kênh

- **REST đồng bộ** chỉ cho truy vấn (ví dụ `order` hỏi `catalog` giá/biến thể khi báo giá). Có timeout; chỉ retry GET. Không dùng REST để đổi trạng thái service khác.
- **Lệnh/sự kiện qua RabbitMQ** bằng Wolverine (MIT). Bảng outbox/inbox nằm trong schema của service, ghi cùng `NpgsqlTransaction` với dữ liệu nghiệp vụ. Bảng của Wolverine được tạo bằng migration DbUp; tắt tự sinh DDL lúc khởi động (role runtime không có DDL).
- Phía nhận khử trùng theo message id; mọi handler idempotent. Retry có backoff; quá số lần → dead-letter queue + cảnh báo + công cụ replay cho vai trò Kỹ thuật.
- **Hợp đồng sự kiện**: JSON Schema có phiên bản trong `contracts/events/`; thay đổi phá vỡ phải tạo phiên bản mới (CI kiểm).
- **Rủi ro cần spike ở M0**: Wolverine enlist outbox vào transaction Npgsql/Dapper thuần. Nếu không đạt, dự phòng là bảng outbox tối giản tự viết + publisher dùng `RabbitMQ.Client`.

### 2.2 Saga đặt hàng (`order` điều phối, trạng thái saga lưu trong schema `order`)

1. Tạo đơn `pending_confirmation` kèm snapshot (giá, ưu đãi, chính sách, pháp lý) và kế hoạch thanh toán dự kiến.
2. Gửi lần lượt `inventory.ReserveStock` → `minixu.ReserveXu` → `payment.OpenPaymentPlan`; mỗi bước trả `Reserved` hoặc `Rejected`.
3. Đủ cả ba → chuyển trạng thái kế tiếp theo phương thức (chờ chuyển khoản, xác nhận COD, chờ duyệt phần in).
4. Bước nào bị từ chối hoặc quá timeout → lệnh bù trừ (`ReleaseStock`, `ReleaseXu`), đơn về `awaiting_customer` hoặc `cancelled` kèm lý do.

Khách không thấy "đã xác nhận" trước bước 3 (BRD §6.1 bước 3); BFF hiển thị "đang xử lý" và poll trạng thái.

### 2.3 Hạn giữ và tiền đến muộn

- Service sở hữu nguồn lực tự quản hạn: `inventory` tự giải phóng giữ tồn theo CFG-11/12/13 và phát sự kiện, không phụ thuộc `order`.
- Tiền đến sau khi hết hạn: `payment` ghi vào tài khoản chờ xử lý + hàng đợi đối soát, không tự khởi động lại đơn (BRD §6.5).

### 2.4 Chống trùng và tranh chấp

- Mọi API ghi từ BFF bắt buộc header `Idempotency-Key`; service lưu khóa + kết quả, nên bấm lặp/retry không tạo bản ghi trùng (BRD §5.2).
- Giữ tồn, trừ ngân sách khuyến mãi, giữ xu theo lô dùng cập nhật có điều kiện (`UPDATE … WHERE available >= @qty`) cùng ràng buộc `CHECK (available >= 0)`: không bán âm, không vượt ngân sách khi đặt đồng thời.

### 2.5 Tiền, sổ và thời gian

- Tiền VND lưu `bigint` theo đồng, không dùng số thực. Phân bổ ưu đãi/xu/phần dư theo phần dư lớn nhất với thứ tự xác định, lưu kết quả trên từng dòng (BRD §6.4).
- Sổ tiền (`payment`) và sổ xu (`minixu`) chỉ ghi thêm; sửa bằng bút toán điều chỉnh. Bất biến có test: tổng phân bổ = tổng tiền vào; số dư xu = tổng sổ.
- Thời gian lưu UTC (`timestamptz`); hiển thị và kỳ báo cáo theo `Asia/Ho_Chi_Minh`.

## 3. Identity và phân quyền

### 3.1 Tài khoản và mật khẩu

- Một bảng tài khoản cho khách và nhân viên, phân biệt `kind` (`customer` | `staff`). Định danh đăng nhập: email hoặc username.
- Số điện thoại chỉ là thông tin liên hệ; đăng nhập bằng số điện thoại tắt bằng cờ đến khi OPEN-07 được quyết định.
- Mật khẩu băm Argon2id, tham số tối thiểu theo OWASP (19 MiB, t=2, p=1); băm lại khi đăng nhập nếu đổi tham số. Tối thiểu 10 ký tự, chặn danh sách mật khẩu phổ biến (offline).
- Không tiết lộ tài khoản tồn tại (thông báo lỗi chung). Giới hạn tốc độ theo tài khoản và IP, khóa tạm tăng dần.

### 3.2 Token (tự viết)

- **Access token**: JWT ES256, 5 phút. Claim: `sub`, `kind`, `perms`, `auth_time`, `amr`, `jti`. Khóa ký xoay vòng; JWKS công bố trong mạng nội bộ, service cache JWKS để tự kiểm tra.
- **Refresh token**: ngẫu nhiên 256 bit, lưu bản băm, xoay vòng mỗi lần dùng; phát hiện dùng lại token cũ → thu hồi cả chuỗi. Khách: 30 ngày trượt. Nhân viên: tối đa 8 giờ, hết hạn khi không hoạt động 30 phút.
- **BFF**: token chỉ ở phía server. Trình duyệt giữ cookie phiên mã hóa `httpOnly; Secure; SameSite=Lax`. `shop` và `admin` dùng domain và cookie riêng.
- **Service ↔ service**: REST nội bộ dùng token `kind=service` cấp theo client credentials cho từng service. RabbitMQ: mỗi môi trường một vhost, mỗi service một user chỉ có quyền trên queue của mình.

### 3.3 Đăng nhập xã hội và liên kết danh tính

BFF chạy OAuth2 authorization code + PKCE với Google/Facebook rồi chuyển code cho `identity` xác minh. Trùng email với tài khoản có sẵn **không tự gộp**: người dùng phải đăng nhập tài khoản cũ hoặc nhập mã xác minh gửi qua email mới liên kết được (BRD §5.3).

### 3.4 Xác minh email và claim đơn guest

- Mã/link xác minh email hết hạn sau 10 phút (CFG-21).
- Claim đơn guest cần email đã xác minh **và** token ngẫu nhiên trong link gửi tới email đặt hàng; mã đơn không đủ.
- Claim thành công → `identity` phát sự kiện để `order`, `catalog` (quyền tải) và `minixu` (xu chờ, trong hạn CFG-07) gắn dữ liệu vào tài khoản. Một đơn chỉ claim một lần (BR-XU-10).

### 3.5 MFA và xác thực lại

- TOTP (RFC 6238); secret mã hóa AES-GCM khi lưu; 10 mã khôi phục lưu bản băm.
- Nhân viên bắt buộc MFA: tài khoản staff chưa có MFA chỉ vào được màn hình đăng ký MFA. Khách tự chọn bật.
- Endpoint nhạy cảm (đổi quyền, hoàn tiền, điều chỉnh kho, cấu hình tài chính, xuất dữ liệu nhạy cảm, đổi tài khoản nhận hoàn) đòi `auth_time` ≤ 5 phút (cấu hình được) và `amr` có `mfa`; không đạt → `401 step_up_required`, BFF bật màn hình xác thực lại.

### 3.6 Phân quyền

- **Mã quyền**: `miền.tài-nguyên.hành-động` (ví dụ `inventory.adjustment.approve`, `customer.pii.read`).
- **Vai trò**: tập mã quyền; seed 11 vai trò BRD §3 làm mẫu, sửa được.
- **Phân công**: người + vai trò + phạm vi (`all` hoặc danh sách farm/kho); một người có nhiều phân công.
- Token mang `perms` gom theo phạm vi, ví dụ `{"*":[…], "wh:2":[…], "farm:1":[…]}`.
- Kiểm tra hai tầng: attribute trên endpoint (hành động) và `authz.Ensure(perm, scope)` trong handler sau khi tải bản ghi (phạm vi dữ liệu). Gọi API trực tiếp thiếu quyền → 403, không phụ thuộc menu.
- Đổi quyền → thu hồi refresh token của người đó; hiệu lực trong ≤ 5 phút. Thay đổi quyền, đăng nhập, thay đổi MFA ghi audit append-only.
- Phân tách nhiệm vụ (người tạo ≠ người duyệt) và ngưỡng duyệt (CFG-23) do service nghiệp vụ áp dụng qua building blocks, bật/tắt theo loại nghiệp vụ (BRD §3).

## 4. Dữ liệu và database as code

### 4.1 Bố trí

- Mỗi môi trường một instance PostgreSQL 18, một database `minibox`, 11 schema (một schema mỗi service).
- Extension `unaccent`, `pg_trgm` trong schema `extensions`; chỉ cấp `USAGE` cho service cần.

### 4.2 Role

- Mỗi service hai role: `<svc>_migrator` (chủ schema, có DDL, chỉ dùng lúc deploy) và `<svc>_app` (runtime, chỉ DML trên schema của mình, không DDL, không chạm schema khác).
- Thu hồi quyền `PUBLIC` trên database và schema. Không ứng dụng nào dùng superuser. Role `db_admin` chỉ dùng cho bootstrap.

### 4.3 Bootstrap

`database/bootstrap/*.sql` idempotent (khối `DO`, `IF NOT EXISTS`), chạy bằng `db_admin`: tạo schema, role, extension, grant và `ALTER DEFAULT PRIVILEGES` để bảng mới của migrator tự cấp DML cho role app. Mật khẩu role chưa có được sinh ngẫu nhiên và ghi vào thư mục secret của môi trường. Lệnh: `node database/bootstrap/run.mjs <env> [--secrets <dir>]` (đẩy SQL vào psql của container `database-postgres`; file secret `<role>.password`, dev ở `deploy/dev/secrets/` đã .gitignore). Chỉ `catalog` được `USAGE` trên `extensions`. Bootstrap không `GRANT ... ON ALL TABLES` để không trả lại quyền mà migration đã thu hồi.

### 4.4 Migration (DbUp)

- Script tại `database/services/<svc>/migrations/NNNN_mo-ta.sql`, được nhúng vào image của service (`EmbeddedResource`). Image có chế độ `--migrate` chạy bằng role migrator; journal `<svc>.schema_versions`, chạy lại không đổi gì.
- Chỉ đi tiến, không có script down. Quay lui bằng migration mới; PITR là phương án cuối.
- Thay đổi phá vỡ theo expand → migrate → contract để bản cũ vẫn chạy trong lúc deploy.

### 4.5 Truy cập dữ liệu và quy ước

- Dapper + Npgsql, SQL viết tay trong repository/read service của layer Infrastructure; không dùng EF Core.
- Tên `snake_case`; khóa chính UUIDv7 (`Guid.CreateVersion7()`); mã cho người đọc (SKU, số đơn, số phiếu) là cột riêng có unique, không dùng làm bằng chứng quyền truy cập.
- Tiền `bigint` (đồng); thời gian `timestamptz`; bảng sửa được có cột `version` (optimistic concurrency).
- Không xóa mềm chung; đối tượng nghiệp vụ kết thúc bằng trạng thái (`archived`, `cancelled`…).
- **Bảng chỉ ghi thêm** (audit, sổ tiền, sổ xu, lịch sử trạng thái, kết quả QC): `REVOKE UPDATE, DELETE` khỏi role app trong migration.
- **Lịch sử trạng thái**: mỗi đối tượng có trạng thái có bảng lịch sử ghi người/hệ thống, thời điểm, lý do, phiên bản (BRD §6.3).
- **CFG có lịch hiệu lực**: bảng `cfg_parameter(key, value jsonb, effective_from, approved_by)` mỗi service; giao dịch lưu snapshot giá trị đã áp dụng, không hồi tố (BRD §14).

### 4.6 Seed

Dữ liệu tham chiếu (vai trò mẫu, mã quyền) đi bằng migration. Dữ liệu demo ở `database/seed/demo/`, chỉ chạy khi `ENVIRONMENT` là `dev` hoặc `staging`; production từ chối (BRD §18).

## 5. Kiến trúc mã (Clean Architecture)

### 5.1 Một service = 5 project, phụ thuộc hướng vào trong

```
src/backend/services/<svc>/
  src/Minibox.<Svc>.Domain/          # không phụ thuộc gì
  src/Minibox.<Svc>.Application/     # → Domain, BuildingBlocks.Application
  src/Minibox.<Svc>.Infrastructure/  # → Application (Dapper, Wolverine, S3, adapter)
  src/Minibox.<Svc>.Api/             # → Application, Infrastructure (composition root, endpoint, --migrate)
  src/Minibox.<Svc>.Contracts/       # integration event/message công khai, không phụ thuộc gì
  tests/{UnitTests, IntegrationTests, ArchitectureTests}/
```

Service khác chỉ được tham chiếu `*.Contracts`. `ArchitectureTests` (NetArchTest) kiểm luật phụ thuộc giữa layer và giữa service; vi phạm làm CI fail. Template `dotnet new minibox-service` (trong `src/backend/templates/`) sinh đủ khung cho service mới.

### 5.2 Bên trong layer

| Layer | Nội dung |
|---|---|
| Domain | `Aggregates/<Tên>/` (aggregate root, entity, value object, domain event, máy trạng thái), `Services/`, `Errors/` |
| Application | `Features/<Tên>/Commands/<Lệnh>/` (command + handler + validator), `Features/<Tên>/Queries/<Truy vấn>/` (query + handler + DTO), `Abstractions/` (port: repository riêng, read service, adapter ngoài), `Behaviors/` riêng nếu có |
| Infrastructure | `Persistence/` (repository Dapper, read service), `Messaging/` (Wolverine, consumer), `Storage/` (S3), `Integrations/<Provider>/` (adapter + fake) |
| Api | `Endpoints/<Tên>/` (Minimal API mỏng: request → command/query → kết quả), `Program.cs`, cấu hình auth/OpenAPI/health |

### 5.3 Building blocks (`src/backend/building-blocks/`)

- `BuildingBlocks.Domain`: `Entity<TId>`, `AggregateRoot<TId>` (gom domain event), `ValueObject`, `Result<T>`/`Error`, `Money` (VND, bigint, phân bổ phần dư lớn nhất), `Quantity` (kèm đơn vị).
- `BuildingBlocks.Application`: `ICommand`/`IQuery`, `IRepository<TAggregate,TId>`, `IUnitOfWork`, `ICurrentUser`, `IAuthorizer`, `IClock`, pipeline behavior chung.
- `BuildingBlocks.Infrastructure`: `DapperRepository<TAggregate,TId>`, `NpgsqlUnitOfWork`, outbox/inbox, audit writer, DbUp runner, OpenTelemetry.
- `BuildingBlocks.Web`: idempotency middleware, attribute phân quyền, ánh xạ `Result` → ProblemDetails, health check.

### 5.4 Pattern

| Pattern | Cách dùng |
|---|---|
| CQRS | Command đi qua aggregate + repository + UoW. Query không qua repository: read service Dapper trả DTO trực tiếp. Hai phía cùng DB; chỉ dữ liệu liên service mới có read model riêng |
| Mediator + pipeline | `Mediator` (martinothamar, MIT, source-generated); không dùng MediatR (giấy phép thương mại từ v13). Thứ tự behavior: logging/trace → validation (FluentValidation) → authorization → idempotency → transaction (chỉ command) |
| Generic Repository | `IRepository<TAggregate,TId>`: `GetAsync`, `GetForUpdateAsync` (`SELECT … FOR UPDATE`), `AddAsync`, `UpdateAsync` (kiểm `version`). Chỉ cho aggregate root; không có `IQueryable`/`Find(predicate)`. Truy vấn đặc thù đặt trong repository riêng kế thừa bản generic (ví dụ `IStockRepository.ReserveAsync`) |
| Generic Unit of Work | `IUnitOfWork` giữ một `NpgsqlConnection` + transaction dùng chung cho mọi repository trong một command. `CommitAsync`: lưu thay đổi → domain event thành integration event vào outbox → ghi audit → commit, trong một transaction |
| Domain event / integration event | Domain event xử lý nội bộ trong transaction; integration event (trong `*.Contracts`) đi qua outbox ra RabbitMQ |
| Result pattern | Lỗi nghiệp vụ trả `Result`, không ném exception; exception chỉ cho lỗi hạ tầng. Api ánh xạ sang RFC 9457 |
| State machine | Mỗi chiều trạng thái BRD §6.3 có bảng chuyển tường minh trong aggregate; chuyển sai trả lỗi, chuyển đúng ghi lịch sử |
| Strategy | Phân bổ tiền/ưu đãi, quy tắc khuyến mãi, chọn hãng giao, đề xuất farm/kho |
| Adapter + fake | Mỗi provider ngoài một adapter kèm fake (§7.3) |
| Saga / process manager | Saga đặt hàng trong `order` (§2.2), trạng thái lưu DB |
| Specification | Không dùng làm chuẩn chung; chỉ thêm khi một quy tắc chọn lọc lặp ở nhiều nơi |

Không áp dụng: event sourcing (sổ append-only đã đủ truy vết), DB đọc riêng (chưa cần ở < 100 đơn/ngày), repository `IQueryable` (xung đột với Dapper).

## 6. Frontend và hợp đồng API

### 6.1 Cấu trúc

pnpm workspace gốc tại `src/frontend/` (`package.json`, `pnpm-workspace.yaml`, lockfile, `node_modules` chỉ ở đây): `apps/shop`, `apps/admin`, `packages/{ui,api-clients,config}`, `e2e/`. React 19, TypeScript `strict`. Trong mỗi app: `app/` chỉ chứa route và layout mỏng; `features/<miền>/{components, server (action + query gọi api-clients), schemas (zod)}`.

### 6.2 `shop`

App Router. Trang danh mục, sản phẩm, blog dùng SSR/ISR (revalidate 60 giây) cho SEO; giỏ, checkout, tài khoản render động. Server Component/Action/Route Handler gọi service bằng client sinh sẵn với token từ phiên server. Mỗi thao tác ghi sinh một `Idempotency-Key` ở client và giữ nguyên khi retry.

### 6.3 `admin`

Domain riêng, `noindex`. Menu ba vùng theo BRD §13, lọc theo `perms` (chỉ để tiện; quyền thật do API kiểm). Bảng nhiều cột có chế độ thẻ/chi tiết trên màn hình nhỏ; màn hình tác nghiệp (quét mã, nhận hàng, QC) thiết kế mobile-first. Không ghi nghiệp vụ offline; form giữ nháp ở `localStorage`, hiện "chưa lưu", retry cùng idempotency key (BRD §5.2).

### 6.4 Design system (`packages/ui`)

- Token CSS variables (màu, chữ, khoảng cách, bo góc, độ sâu/bóng) với Tailwind CSS v4 và primitive Radix. Bảng màu/font tạm, chờ OPEN-08.
- Breakpoint: mobile < 640, tablet 640–1023, laptop 1024–1535, desktop lớn ≥ 1536; giới hạn độ dài dòng ~75 ký tự. Mobile có điều hướng dưới, safe area, sheet cho biến thể/bộ lọc (BRD §5.2).
- Không dùng Storybook; mỗi app có route `/design` làm trang demo component và responsive.
- 3D: three.js qua react-three-fiber, tải lười, chỉ ở trang chủ, bộ sưu tập và viewer sản phẩm; luôn có ảnh/video dự phòng; lỗi 3D không chặn mua hàng (BRD §5.1). Cảnh 3D thật ở M6.

### 6.5 Hợp đồng API

- Mỗi service sinh OpenAPI lúc build bằng `Microsoft.AspNetCore.OpenApi` và commit ra `contracts/openapi/<svc>.json`.
- Client TS sinh bằng `openapi-typescript` + `openapi-fetch` vào `src/frontend/packages/api-clients`.
- CI fail khi: file OpenAPI lệch code; thay đổi phá vỡ so với `develop` (`oasdiff`) mà chưa tăng phiên bản API; client sinh lại khác bản đã commit.
- Lỗi theo RFC 9457 với `type` ổn định; BFF ánh xạ sang thông báo tiếng Việt.
- **Hợp đồng mẫu M0**: OpenAPI của `identity` + sự kiện `identity.AccountRegistered.v1` (JSON Schema), client sinh tự động được `admin` dùng thật.

### 6.6 Bảo mật web và ngôn ngữ

- CSP có nonce, HSTS, `frame-ancestors 'none'`. Chống CSRF: cookie `SameSite=Lax` + kiểm tra `Origin` ở Route Handler; Server Action đã kiểm origin sẵn.
- Giao diện tiếng Việt; chuỗi gom vào một file messages mỗi app; chưa thêm thư viện i18n cho đến khi mở quốc tế. CMS có sẵn cấu trúc bản dịch ở tầng dữ liệu (`content`, BRD §12).

## 7. File, tìm kiếm, tích hợp ngoài

### 7.1 File (SeaweedFS, API S3)

- Bucket theo service: `catalog-files` (file in, riêng tư), `catalog-preview` (model xem trước tối ưu, tách khỏi file in), `catalog-evidence` (bằng chứng nguồn/giấy phép); `content-media` (công khai); `farm-evidence`, `inventory-evidence`, `payment-evidence`, `order-evidence`; `support-attachments`. Mỗi service có S3 credential chỉ trên bucket của mình.
- Upload: service sở hữu kiểm quyền → presigned PUT có giới hạn dung lượng/loại → file vào prefix `quarantine/` → kiểm định dạng thật + quét ClamAV → mới chuyển sang dùng được. Ảnh công khai được mã hóa lại bằng libvips (NetVips, MIT), xóa EXIF/GPS, sinh sẵn các kích thước.
- File số: mỗi phiên bản bất biến, có SHA-256. Hồ sơ nguồn: chủ sở hữu, URL, giấy phép, ảnh bằng chứng lúc nhận, phạm vi, yêu cầu ghi công; ba quyền tách riêng: in để bán, phân phối file, cấp quyền in-bán cho người mua (BRD §4.2).
- Tải riêng tư: `catalog` kiểm quyền tải còn hiệu lực → presigned GET 5 phút, `Content-Disposition: attachment` (CFG-21). Mỗi lần cấp link ghi log tải (báo cáo không coi lượt tải là doanh số). Giới hạn tốc độ thích ứng theo tài khoản/IP, không giới hạn tổng lượt. Hoàn tiền → thu hồi quyền, không cấp link mới; không tuyên bố xóa được bản khách đã tải (BR-AFT-03).
- Media công khai qua Caddy, cache header dài, tên file theo hash nội dung.

### 7.2 Tìm kiếm (trong `catalog`)

- Bảng `search_document` cập nhật trong cùng transaction với xuất bản/gỡ, chỉ chứa biến thể `published`; bản nháp/file bị gỡ không thể lọt vào kết quả (BRD §5.3).
- Truy vấn: khớp SKU chính xác trước → văn bản chuẩn hóa (`unaccent` + lower) bằng GIN trigram → gợi ý theo `similarity`. Bộ lọc/facet bằng SQL.
- Bộ lọc "có sẵn" dùng read model tồn khả dụng từ sự kiện `inventory` (có độ trễ ngắn); checkout luôn kiểm tồn thật.
- Ngưỡng p95 ≤ 300 ms với 5.000 SKU; không đạt thì chuyển Meilisearch.

### 7.3 Tích hợp ngoài (interface theo loại provider, chưa chọn provider)

| Cổng | Service | Hiện tại | Chờ |
|---|---|---|---|
| VietQR | `payment` | Sinh chuỗi VietQR (EMVCo/Napas) cục bộ | — |
| Dữ liệu giao dịch ngân hàng | `payment` | Interface webhook + polling, xác minh HMAC, khử trùng theo mã giao dịch provider. Trước khi có provider: Tài chính xác nhận thủ công kèm bằng chứng; ảnh chụp chuyển khoản không tự động là tiền vào | OPEN-04 |
| Email | `support` | Interface; dev dùng SMTP + Mailpit | OPEN-04 |
| GHN / GHTK / SPX | `inventory` | Adapter theo hãng (báo giá, tạo vận đơn, webhook theo dõi, nhập đối soát COD); SPX tắt bằng cờ | OPEN-05 |
| Hóa đơn điện tử | `finance` | Interface; timeout/gửi trùng → tra cứu trước khi phát hành lại (BRD §11) | OPEN-04, OPEN-06 |
| Google / Facebook | `identity` | Cần app thật theo môi trường | Tài khoản dev |

- Mỗi adapter có fake cho dev/test. Production **từ chối khởi động** nếu một cổng đang bật mà trỏ vào fake (BRD §15 điều kiện 3). Có adapter không tự bật kênh `LATER` (BRD §18).
- Gọi ra ngoài có timeout + circuit breaker; chỉ retry thao tác idempotent.
- `order` có cờ `ordering_paused`: admin bật khi dịch vụ trọng yếu sập → shop hiện thông báo, ngừng nhận đơn mới; bật/tắt có audit (BRD §15 điều kiện 8).

## 8. Repo, môi trường và vận hành

### 8.1 Cấu trúc repo

```
minibox/                             # root sạch: không package.json, không node_modules
├─ src/
│  ├─ frontend/                      # toàn bộ TypeScript/Next.js; pnpm workspace gốc
│  │  ├─ package.json  pnpm-workspace.yaml  pnpm-lock.yaml
│  │  ├─ apps/{shop,admin}/
│  │  ├─ packages/{ui,api-clients,config}/
│  │  └─ e2e/                        # Playwright toàn hệ thống
│  └─ backend/                       # toàn bộ .NET
│     ├─ Minibox.slnx  global.json  Directory.Build.props  Directory.Packages.props
│     ├─ building-blocks/
│     ├─ services/<svc>/{src,tests}/
│     ├─ templates/minibox-service/
│     └─ tests/                      # test cấp repo
├─ database/                         # toàn bộ SQL, nguồn sự thật của schema
│  ├─ bootstrap/
│  ├─ services/<svc>/migrations/
│  ├─ seed/demo/
│  └─ tests/
├─ contracts/{openapi,events}/       # hợp đồng chung frontend ↔ backend, service ↔ service
├─ deploy/                           # compose, Caddy, OTel/Grafana, pgBackRest/restic, deploy.sh
├─ scripts/                          # lệnh chuẩn: test / accept / e2e / dev (.mjs, Node thuần)
├─ docs/                             # README (mục lục), business-requirements.md, technical-spec.md
└─ .github/
```

- `database/` là nơi duy nhất chứa DDL. Infrastructure chỉ chứa repository/read service.
- `global.json` nằm ở `src/backend/` nên lệnh `dotnet` chạy từ thư mục đó (hoặc qua `scripts/`); lệnh pnpm chạy trong `src/frontend/`.
- `src/frontend/` không tham chiếu `src/backend/`; hai bên gặp nhau qua `contracts/openapi` → `packages/api-clients`. Service ↔ service qua `contracts/` và `*.Contracts`.

### 8.2 Môi trường

- **dev**: máy lập trình viên, `deploy/compose.yaml` + `deploy/dev/compose.yaml` (PostgreSQL, RabbitMQ, SeaweedFS, ClamAV, Mailpit, service với adapter fake, 2 app).
- **staging** và **production**: homeserver, mỗi môi trường một Compose project riêng (network, volume, DB, vhost RabbitMQ, thư mục secret riêng). Một Caddy chung route theo hostname. Staging chỉ vào qua Tailscale, không khách thật, không bị index.
- Ingress production: mở cổng hoặc Cloudflare Tunnel nếu CGNAT (chờ OPEN-02); thiết kế hỗ trợ cả hai.
- Compose: `deploy/compose.yaml` chứa phần chung (image, volume, health check); mỗi môi trường một thư mục `deploy/<env>/` (`compose.yaml` đặt tên cụm, `container_name`, cổng; `.env.example`). Cấu hình dùng chung giữa môi trường (Caddy, OTel/Grafana, backup) để ở thư mục riêng dưới `deploy/` khi tới task đó. Đặt tên: cụm (Compose project) `minibox-<env>`; dịch vụ `<layer>-<tên>` với layer ∈ `frontend|backend|database|infra|observability` (vd. `database-postgres`, `infra-rabbitmq`, `backend-identity`); container `minibox-<env>-<layer>-<tên>`. Dev mở cổng trên `127.0.0.1`, giá trị mặc định ghi đè được bằng `deploy/dev/.env` (mẫu `deploy/dev/.env.example`).

### 8.3 Deploy

- PR → build + test. Merge `develop` → build image một lần, tag theo commit, đẩy GHCR → tự deploy staging.
- Release lên `main` → dùng lại đúng digest đã chạy staging, gắn tag `vX.Y.Z` → deploy production qua GitHub Environment `production` (owner duyệt).
- Job deploy vào tailnet bằng khóa Tailscale tạm thời có tag, SSH chạy `deploy/deploy.sh <env> <tag>`: pull → (production) backup incremental pgBackRest → bootstrap DB → `--migrate` từng service (lỗi thì dừng) → `compose up -d` → chờ `/health/ready` mọi service → không đạt thì quay về tag cũ (an toàn nhờ expand/contract).

### 8.4 Secret

`/srv/minibox/<env>/secrets/` (root, quyền `600`), mount dạng Docker secret file. Repo chỉ có `.env.example`. GitHub chỉ giữ khóa Tailscale và khóa SSH deploy.

### 8.5 Observability

- OpenTelemetry SDK (.NET và Next.js) → OTel Collector → Loki (log), Tempo (trace), Prometheus (metric) → Grafana. Một bộ chung, nhãn `env`; Grafana chỉ qua Tailscale.
- Trace truyền qua header RabbitMQ; theo được một đơn qua mọi service.
- Log JSON có `trace_id`; không ghi mật khẩu, token, OTP; che một phần email/số điện thoại.
- Lưu: log 30 ngày, trace 7 ngày, metric 90 ngày.
- Cảnh báo (email + Telegram): service không phản hồi > 2 phút, DLQ có message, outbox tồn > 5 phút, backup/WAL archive lỗi, đĩa > 80%, chứng chỉ sắp hết hạn. Thêm uptime check miễn phí bên ngoài.

### 8.6 Backup và DR

- PostgreSQL bằng pgBackRest: full hằng tuần, diff hằng ngày, WAL archive `archive_timeout = 300s`. repo1 đĩa cục bộ (nên là đĩa khác, chờ OPEN-02), repo2 ngoài nhà qua API S3 (chờ OPEN-03). Giữ 2 bản full, 14 ngày PITR.
- restic mỗi giờ cho dữ liệu SeaweedFS và thư mục secret, mã hóa, đẩy ra ngoài nhà.
- Restore thử tự động hằng tuần vào container tạm: đếm bản ghi, checksum bảng sổ, migration chạy lại không đổi; kết quả thành metric có cảnh báo.
- Diễn tập DR toàn phần hằng quý (thủ công): dựng lại trên máy sạch, bấm giờ so với RTO.
- Khi OPEN-03 chưa chốt: **chưa đạt điều kiện DR mở bán** (BRD §15 điều kiện 6).

### 8.7 Comment tài liệu

- Viết bằng tiếng Việt, mô tả *ý nghĩa/hợp đồng* (làm gì, điều kiện, lỗi trả về), không chép lại tên hay kiểu. Không bắt buộc với test và file cấu hình.
- **Backend**: chuẩn Microsoft XML documentation cho mọi type/member `public`/`protected` ở project không phải `*.Tests`: `<summary>`; `<param>`/`<typeparam>` cho từng tham số; `<returns>` khi có giá trị trả về; `<exception>` cho ngoại lệ chủ động ném; `<see cref>`/`<inheritdoc/>` khi phù hợp. `Directory.Build.props` bật `GenerateDocumentationFile` nên thiếu doc (CS1591) hoặc sai tên param/cref (CS1572/CS1573/CS1574) là lỗi build.
- **Frontend**: chuẩn TSDoc cho mọi export (function, component, class, interface, type, enum, hằng): câu tóm tắt; `@param name - mô tả` (tên không có dấu chấm, mô tả prop của object trong câu); `@returns`; `@throws`; `@remarks`/`@example` khi cần. ESLint (`@minibox/config/eslint`) chặn thiếu comment (`jsdoc/require-jsdoc`, `jsdoc/require-description`) và sai cú pháp (`tsdoc/syntax`).
## 9. Test và lệnh chuẩn

### 9.1 Tầng test

| Tầng | Công cụ | Phạm vi | Chạy khi |
|---|---|---|---|
| Unit | xUnit v3; FsCheck cho bất biến tiền/xu | Domain (aggregate, máy trạng thái, `Money`, phân bổ, FIFO lô xu), handler Application với fake port | Mọi PR |
| Architecture | NetArchTest | Luật phụ thuộc layer và giữa service | Mọi PR |
| Integration | Testcontainers (PostgreSQL 18, RabbitMQ) | Repository/UoW/outbox trên DB thật đã bootstrap + migrate với role thật; endpoint qua `WebApplicationFactory`; consumer idempotent | Mọi PR |
| Database | Testcontainers (`database/tests`) | Migrate DB sạch, nâng cấp từ release trước, chạy lại; quyền chéo schema; bảng append-only chặn UPDATE/DELETE | Mọi PR |
| Contract | `oasdiff`, JSON Schema | OpenAPI lệch/phá vỡ; message phát ra khớp schema; client TS lệch | Mọi PR |
| Frontend | Vitest + Testing Library, `tsc`, ESLint | Component, schema zod, server action với API giả | Mọi PR |
| E2E | Playwright (Chromium; thêm WebKit mobile khi kiểm thiết bị) | Luồng xuyên hệ thống trên stack Compose với adapter fake | PR: smoke; merge `develop`: đầy đủ trên staging |

### 9.2 Quy ước nghiệm thu omi

AC thứ i của issue #p ↔ test có tên chứa chuỗi chữ thường `ac<p>_<i>`: .NET: tên method chứa `ac42_1` (`accept` lọc bằng `--filter-method`), kèm `DisplayName = "ac42_1 …"` cho dễ đọc; script Node `test("ac42_1 …")` (`node:test`); Playwright `test('ac42_1 …')`. Một test có thể phủ nhiều AC. AC không tự động hóa được ghi `[manual]`.

### 9.3 Lệnh chuẩn (`scripts/*.mjs` ở root, Node thuần không dependency; chạy trên Windows và Linux; cần Docker)

| Lệnh | Việc |
|---|---|
| `node scripts/test.mjs` | Build backend + frontend; unit, architecture, integration, database, contract, frontend. Exit ≠ 0 khi lỗi |
| `node scripts/accept.mjs <n>` | `dotnet test` lọc `ac<n>_` (gồm `database/tests`) + `playwright test -g "ac<n>_" --reporter=list` trong `src/frontend`, in tên test. Không có test khớp không tính là lỗi (omi phán quyết) |
| `node scripts/e2e.mjs` | Toàn bộ Playwright (`src/frontend/e2e`) |
| `node scripts/dev.mjs` | Compose môi trường dev |

Cấu hình omi: `OMI_CLI init --repo-dir minibox --test-cmd "node scripts/test.mjs" --accept-cmd "node scripts/accept.mjs {n}"`.

## 10. Ngưỡng phi chức năng

| Nhóm | Chỉ số và ngưỡng | Cách đo |
|---|---|---|
| Hiệu năng API | Đọc p95 ≤ 300 ms; ghi p95 ≤ 800 ms ở 10 rps | k6 trên staging |
| Saga đặt hàng | Tạo đơn → giữ xong hoặc từ chối p95 ≤ 5 s | Trace Tempo |
| Năng lực | Đỉnh 5 rps checkout liên tục 10 phút; 0 oversell, 0 trừ xu hai lần | k6 + kiểm tra bất biến |
| Tìm kiếm | p95 ≤ 300 ms với 5.000 SKU | k6 |
| Web shop | LCP ≤ 2,5 s, INP ≤ 200 ms, CLS ≤ 0,1 (p75, mobile 4G) | Lighthouse CI |
| Accessibility | WCAG 2.2 AA cơ bản (bàn phím, tương phản, nhãn); 0 lỗi nghiêm trọng | axe trong Playwright |
| Bảo mật | 0 truy cập chéo schema; 0 PII/secret trong log; không còn lỗ hổng high/critical chưa xử lý khi release | Test DB, test log, Dependabot + `dotnet list package --vulnerable` + `pnpm audit` |
| Tính đúng tiền/xu | Tổng phân bổ = tổng tiền vào; số dư xu = tổng sổ; sai lệch → cảnh báo | Test bất biến + job đối chiếu hằng ngày |
| Sẵn sàng | 99,0%/tháng trừ bảo trì báo trước (`PROPOSED`, chờ PO); không cam kết 24/7 | Uptime check ngoài |
| DR | RPO ≤ 1 giờ (thiết kế ~5 phút); RTO 4–8 giờ | Restore thử hằng tuần, diễn tập hằng quý |
| CI | PR ≤ 15 phút | Lịch sử Actions |

## 11. Điểm còn chờ ảnh hưởng kỹ thuật

| OPEN | Ảnh hưởng | Thiết kế để không bị chặn |
|---|---|---|
| OPEN-01 | Bộ CFG | Bảng CFG có lịch hiệu lực; giá trị BRD §14 chỉ dùng ở dev/staging |
| OPEN-02 | Cấu hình server, CGNAT, đĩa thứ hai, UPS | Ingress mở cổng hoặc Cloudflare Tunnel; giới hạn tài nguyên container cấu hình được |
| OPEN-03 | Đích backup ngoài nhà | pgBackRest/restic trỏ S3 bất kỳ; chưa có → chưa đạt DR |
| OPEN-04 | Provider ngân hàng, email, hóa đơn | Interface + fake; production từ chối fake; xác nhận tiền thủ công có bằng chứng |
| OPEN-05 | GHN/GHTK/SPX | Adapter theo hãng; SPX tắt bằng cờ |
| OPEN-06 | Thuế, hóa đơn, PII, Minixu, bản quyền | Không mã hóa cứng chính sách; thời gian lưu PII và nội dung điều khoản là cấu hình chờ duyệt |
| OPEN-07 | Đăng nhập bằng số điện thoại | Tắt bằng cờ |
| OPEN-08 | Logo, màu, font, asset 3D | Token tạm trong `packages/ui` |
| OPEN-09 | Bảo hành theo nhóm | Chính sách bảo hành là dữ liệu cấu hình theo nhóm, snapshot khi mua |
