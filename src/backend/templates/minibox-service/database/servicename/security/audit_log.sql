-- Bảng chỉ ghi thêm (spec §4.5): role app không sửa/xoá audit.
REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM servicename_app;
