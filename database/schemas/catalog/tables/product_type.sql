-- Bảng product_type (migration 0007_product-type.sql).
CREATE TABLE product_type (
  id          uuid        PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  code        text        NOT NULL UNIQUE CHECK (length(code) <= 50 AND code ~ '^[a-z][a-z0-9]*(-[a-z0-9]+)*$'),
  sort_order  integer     NOT NULL DEFAULT 0,
  archived_at timestamptz,                                          -- NULL = đang hoạt động
  version     bigint      NOT NULL DEFAULT 0
);
