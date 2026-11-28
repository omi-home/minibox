-- Bộ sưu tập và thẻ (BRD BR-CAT-01): độc lập với cây danh mục, chung bảng taxon phân biệt bằng kind.
-- Bản dịch theo locale (database/templates/translation-table.sql), lịch sử slug để redirect 301 (spec §6.6).
-- Một sản phẩm thuộc nhiều taxon qua product_taxon. Xóa mềm bằng archived_at; không xóa dòng nên FK không cascade.

CREATE TABLE taxon (
  id          uuid        PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  kind        text        NOT NULL CHECK (kind IN ('collection', 'tag')),
  sort_order  integer     NOT NULL DEFAULT 0,
  archived_at timestamptz,                                          -- NULL = đang hoạt động
  version     bigint      NOT NULL DEFAULT 0
);

CREATE INDEX taxon_kind_idx ON taxon (kind);

CREATE TABLE taxon_translation (
  taxon_id    uuid NOT NULL REFERENCES taxon (id),
  locale      text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name        text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
  description text CHECK (length(description) <= 5000),
  slug        text NOT NULL CHECK (length(slug) BETWEEN 1 AND 200 AND slug = lower(slug) AND slug !~ '[[:space:]/?#%]'),
  PRIMARY KEY (taxon_id, locale)
);

-- Slug duy nhất theo locale trên cả hai loại (kể cả taxon đã xóa).
CREATE UNIQUE INDEX taxon_translation_slug ON taxon_translation (locale, slug);

CREATE TABLE taxon_slug_history (
  taxon_id    uuid        NOT NULL REFERENCES taxon (id),
  locale      text        NOT NULL,
  slug        text        NOT NULL,
  replaced_at timestamptz NOT NULL,
  PRIMARY KEY (taxon_id, locale, slug)
);

CREATE INDEX taxon_slug_history_lookup ON taxon_slug_history (locale, slug);

-- product_id chưa có FK: bảng product thêm ở task Product, khi đó bổ sung REFERENCES product (id).
CREATE TABLE product_taxon (
  product_id uuid NOT NULL,
  taxon_id   uuid NOT NULL REFERENCES taxon (id),
  PRIMARY KEY (product_id, taxon_id)
);

CREATE INDEX product_taxon_taxon_idx ON product_taxon (taxon_id);
