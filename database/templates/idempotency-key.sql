-- Mẫu migration bảng Idempotency-Key của service — PostgresIdempotencyStore (BuildingBlocks.Infrastructure) giữ chỗ khoá và lưu
-- kết quả thành công cùng transaction với dữ liệu nghiệp vụ. Chép vào database/services/<svc>/migrations/NNNN_idempotency-key.sql.
-- key = "<sub>|<loại lệnh>|<Idempotency-Key>"; response chỉ chứa giá trị kết quả thành công (thường là id), không chứa PII.
-- ponytail: chưa có TTL; khi bảng lớn thêm job dọn: DELETE FROM idempotency_key WHERE created_at < now() - interval '7 days'.

CREATE TABLE idempotency_key (
  key          text        PRIMARY KEY,
  request_hash text        NOT NULL,              -- SHA-256 nội dung lệnh: cùng khoá khác nội dung → 409
  response     jsonb,                             -- NULL chỉ trong transaction đang chạy
  created_at   timestamptz NOT NULL DEFAULT now()
);
