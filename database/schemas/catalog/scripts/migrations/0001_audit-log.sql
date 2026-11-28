-- Bảng audit của service — EfUnitOfWork (BuildingBlocks.Infrastructure) ghi một dòng cho mỗi domain event,
-- cùng transaction với dữ liệu và outbox. Sinh từ database/templates/audit-log.sql.
-- Chỉ ghi thêm như append-only-table.sql: thu UPDATE/DELETE/TRUNCATE khỏi role app.

CREATE TABLE audit_log (
  id             uuid        PRIMARY KEY,           -- UUIDv7 do ứng dụng sinh
  occurred_at    timestamptz NOT NULL,
  actor          text        NOT NULL,              -- sub của người gọi, hoặc 'system'
  action         text        NOT NULL,              -- tên domain event
  aggregate_type text        NOT NULL,
  aggregate_id   text        NOT NULL,
  payload        jsonb       NOT NULL
);

CREATE INDEX audit_log_aggregate ON audit_log (aggregate_type, aggregate_id, occurred_at);

REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM catalog_app;
