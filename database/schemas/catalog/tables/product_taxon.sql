-- Bảng product_taxon (migration 0006_taxon.sql).
-- FK product_id thêm ở migration 0008_product.sql.
CREATE TABLE product_taxon (
  product_id uuid NOT NULL REFERENCES product (id),
  taxon_id   uuid NOT NULL REFERENCES taxon (id),
  PRIMARY KEY (product_id, taxon_id)
);

CREATE INDEX product_taxon_taxon_idx ON product_taxon (taxon_id);
