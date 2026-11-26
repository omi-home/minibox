-- Idempotency-Key: PostgresIdempotencyStore giữ chỗ khoá và lưu kết quả thành công cùng transaction với dữ liệu nghiệp vụ.
-- key = "<sub>|<loại lệnh>|<Idempotency-Key>"; response chỉ chứa giá trị kết quả thành công (thường là id), không chứa PII.
CREATE TABLE idempotency_key (
  key          text        PRIMARY KEY,
  request_hash text        NOT NULL,              -- SHA-256 nội dung lệnh: cùng khoá khác nội dung → 409
  response     jsonb,                             -- NULL chỉ trong transaction đang chạy
  created_at   timestamptz NOT NULL DEFAULT now()
);
