-- Bảng category_slug_history (migration 0005_category.sql).
CREATE TABLE category_slug_history (
  category_id uuid        NOT NULL REFERENCES category (id),
  locale      text        NOT NULL,
  slug        text        NOT NULL,
  replaced_at timestamptz NOT NULL,
  PRIMARY KEY (category_id, locale, slug)
);

-- Tra redirect: (locale, slug cũ) → danh mục.
CREATE INDEX category_slug_history_lookup ON category_slug_history (locale, slug);
