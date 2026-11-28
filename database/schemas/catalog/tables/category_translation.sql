-- Bảng category_translation (migration 0005_category.sql).
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
