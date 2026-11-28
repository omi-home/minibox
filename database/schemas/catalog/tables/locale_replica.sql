-- Bản sao locale của localization (nhận localization.LocaleChanged.v1); seed vi/en ở migration 0004.
CREATE TABLE locale_replica (
  code          text        PRIMARY KEY CHECK (code ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name          text        NOT NULL CHECK (length(name) BETWEEN 1 AND 100),
  enabled       boolean     NOT NULL,
  sort_order    integer     NOT NULL,
  fallback_code text        CHECK (fallback_code <> code),   -- NULL = rơi về vi
  updated_at    timestamptz NOT NULL DEFAULT now()
);
