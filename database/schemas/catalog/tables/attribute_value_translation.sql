-- Bảng attribute_value_translation (migration 0007_product-type.sql).
CREATE TABLE attribute_value_translation (
  attribute_value_id uuid NOT NULL REFERENCES attribute_value (id) ON DELETE CASCADE,
  locale             text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  label              text NOT NULL CHECK (length(label) BETWEEN 1 AND 200),
  PRIMARY KEY (attribute_value_id, locale)
);
