-- Danh mục nhiều cấp (BRD BR-CAT-01): cây cha–con (vòng lặp chặn ở aggregate Category, dưới pg_advisory_xact_lock 'catalog.category'),
-- bản dịch theo locale (database/templates/translation-table.sql) và lịch sử slug để redirect 301 (spec §6.6).
-- Xóa mềm bằng archived_at; không xóa dòng nên FK không cascade.

CREATE TABLE category (
  id          uuid        PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  parent_id   uuid        REFERENCES category (id) CHECK (parent_id <> id),
  sort_order  integer     NOT NULL DEFAULT 0,
  archived_at timestamptz,                                          -- NULL = đang hoạt động
  version     bigint      NOT NULL DEFAULT 0
);

CREATE INDEX category_parent_idx ON category (parent_id);

CREATE TABLE category_translation (
  category_id uuid NOT NULL REFERENCES category (id),
  locale      text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name        text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
  description text CHECK (length(description) <= 5000),
  -- Slug theo ngôn ngữ: chữ thường (cho phép chữ không Latin), không khoảng trắng/ký tự URL đặc biệt.
  slug        text NOT NULL CHECK (length(slug) BETWEEN 1 AND 200 AND slug = lower(slug) AND slug !~ '[[:space:]/?#%]'),
  PRIMARY KEY (category_id, locale)
);

-- Slug duy nhất theo locale (kể cả danh mục đã xóa: URL cũ không bị danh mục khác chiếm).
CREATE UNIQUE INDEX category_translation_slug ON category_translation (locale, slug);

CREATE TABLE category_slug_history (
  category_id uuid        NOT NULL REFERENCES category (id),
  locale      text        NOT NULL,
  slug        text        NOT NULL,
  replaced_at timestamptz NOT NULL,
  PRIMARY KEY (category_id, locale, slug)
);

-- Tra redirect: (locale, slug cũ) → danh mục.
CREATE INDEX category_slug_history_lookup ON category_slug_history (locale, slug);
