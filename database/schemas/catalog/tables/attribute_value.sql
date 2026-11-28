-- Bảng attribute_value (migration 0007_product-type.sql).
CREATE TABLE attribute_value (
  id           uuid    PRIMARY KEY,                                 -- UUIDv7 do ứng dụng sinh
  attribute_id uuid    NOT NULL REFERENCES attribute (id) ON DELETE CASCADE,
  code         text    NOT NULL CHECK (length(code) <= 50 AND code ~ '^[a-z][a-z0-9]*(-[a-z0-9]+)*$'),
  sort_order   integer NOT NULL,
  UNIQUE (attribute_id, code)
);
