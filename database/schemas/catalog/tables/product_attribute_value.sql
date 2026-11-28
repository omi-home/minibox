-- Bảng product_attribute_value (migration 0008_product.sql).
CREATE TABLE product_attribute_value (
  product_id   uuid NOT NULL REFERENCES product (id),
  attribute_id uuid NOT NULL REFERENCES attribute (id),
  value        text NOT NULL CHECK (length(value) BETWEEN 1 AND 2000),
  PRIMARY KEY (product_id, attribute_id, value)
);

CREATE INDEX product_attribute_value_attribute_idx ON product_attribute_value (attribute_id);
