-- Bản sao danh sách locale của localization (spec §6.6, CR #387) để truy vấn bảng *_translation của catalog theo chuỗi fallback.
-- Sinh từ database/templates/locale-replica.sql; ghi bởi handler localization.LocaleChanged.v1 (queue catalog.localization.LocaleChanged.v1).
-- Không FK tới bảng *_translation: bản sao có thể trễ hơn bản dịch; locale lạ chỉ bị bỏ qua khi đọc.

CREATE TABLE locale_replica (
  code          text        PRIMARY KEY CHECK (code ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name          text        NOT NULL CHECK (length(name) BETWEEN 1 AND 100),
  enabled       boolean     NOT NULL,
  sort_order    integer     NOT NULL,
  fallback_code text        CHECK (fallback_code <> code),   -- NULL = rơi về vi
  updated_at    timestamptz NOT NULL DEFAULT now()
);

-- Cùng seed với localization (0001): service mới dùng được vi/en ngay cả khi chưa nhận sự kiện nào.
INSERT INTO locale_replica (code, name, enabled, sort_order, fallback_code) VALUES
  ('vi', 'Tiếng Việt', true, 0, NULL),
  ('en', 'English',    true, 1, 'vi')
ON CONFLICT (code) DO NOTHING;
