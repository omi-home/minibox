-- Bảng taxon_translation (migration 0006_taxon.sql).
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
