-- Bảng taxon_slug_history (migration 0006_taxon.sql).
CREATE TABLE taxon_slug_history (
  taxon_id    uuid        NOT NULL REFERENCES taxon (id),
  locale      text        NOT NULL,
  slug        text        NOT NULL,
  replaced_at timestamptz NOT NULL,
  PRIMARY KEY (taxon_id, locale, slug)
);

CREATE INDEX taxon_slug_history_lookup ON taxon_slug_history (locale, slug);
