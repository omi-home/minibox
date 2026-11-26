-- Mẫu migration cho bảng chỉ ghi thêm (audit, sổ tiền, sổ xu, lịch sử trạng thái, kết quả QC) — spec §4.5.
-- Chép vào database/schemas/<svc>/scripts/migrations/NNNN_mo-ta.sql (và tables/{{table}}.sql + security/{{table}}.sql cho trạng thái đích), thay {{svc}} và {{table}}, giữ nguyên câu REVOKE.
-- Default privileges của bootstrap cấp DML cho <svc>_app trên mọi bảng mới; câu REVOKE thu lại UPDATE/DELETE
-- (TRUNCATE vốn không được cấp, thu thêm cho chắc). Bootstrap không GRANT lại trên bảng có sẵn nên quyền này giữ nguyên.
-- Sửa sai bằng bản ghi bù (dòng mới tham chiếu dòng cũ), không sửa/xoá dòng cũ.

CREATE TABLE {{table}} (
  id          uuid        PRIMARY KEY,           -- UUIDv7 do ứng dụng sinh
  occurred_at timestamptz NOT NULL DEFAULT now(),
  actor       text        NOT NULL,              -- người hoặc hệ thống
  reason      text,
  payload     jsonb       NOT NULL
);

REVOKE UPDATE, DELETE, TRUNCATE ON {{table}} FROM {{svc}}_app;
