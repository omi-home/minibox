-- Bảng product_translation (migration 0008_product.sql).
CREATE TABLE product_translation (
  product_id      uuid NOT NULL REFERENCES product (id),
  locale          text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name            text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
  description     text CHECK (length(description) <= 20000),
  slug            text NOT NULL CHECK (length(slug) BETWEEN 1 AND 200 AND slug = lower(slug) AND slug !~ '[[:space:]/?#%]'),
  seo_title       text CHECK (length(seo_title) <= 200),
  seo_description text CHECK (length(seo_description) <= 500),
  PRIMARY KEY (product_id, locale)
);

CREATE UNIQUE INDEX product_translation_slug ON product_translation (locale, slug);
