-- Bảng taxon (migration 0006_taxon.sql).
CREATE TABLE taxon (
  id          uuid        PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  kind        text        NOT NULL CHECK (kind IN ('collection', 'tag')),
  sort_order  integer     NOT NULL DEFAULT 0,
  archived_at timestamptz,                                          -- NULL = đang hoạt động
  version     bigint      NOT NULL DEFAULT 0
);

CREATE INDEX taxon_kind_idx ON taxon (kind);
