-- Bảng product_type_translation (migration 0007_product-type.sql).
CREATE TABLE product_type_translation (
  product_type_id uuid NOT NULL REFERENCES product_type (id),
  locale          text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name            text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
  PRIMARY KEY (product_type_id, locale)
);
