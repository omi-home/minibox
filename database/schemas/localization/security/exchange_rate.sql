-- Bảng chỉ ghi thêm (spec §4.5): role app không sửa/xoá tỷ giá đã nhập.
REVOKE UPDATE, DELETE, TRUNCATE ON exchange_rate FROM localization_app;
