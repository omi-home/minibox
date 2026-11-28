-- Bảng product_slug_history (migration 0008_product.sql).
CREATE TABLE product_slug_history (
  product_id  uuid        NOT NULL REFERENCES product (id),
  locale      text        NOT NULL,
  slug        text        NOT NULL,
  replaced_at timestamptz NOT NULL,
  PRIMARY KEY (product_id, locale, slug)
);

CREATE INDEX product_slug_history_lookup ON product_slug_history (locale, slug);
