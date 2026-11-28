-- Bảng category (migration 0005_category.sql).
CREATE TABLE category (
  id          uuid        PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  parent_id   uuid        REFERENCES category (id) CHECK (parent_id <> id),
  sort_order  integer     NOT NULL DEFAULT 0,
  archived_at timestamptz,                                          -- NULL = đang hoạt động
  version     bigint      NOT NULL DEFAULT 0
);

CREATE INDEX category_parent_idx ON category (parent_id);
