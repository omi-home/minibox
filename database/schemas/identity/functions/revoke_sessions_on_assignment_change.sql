-- Đổi phân công → thu hồi refresh token của người bị ảnh hưởng (spec §3.6). Trigger: tables/role_assignment.sql.
-- search_path ghim schema để không bị bảng giả trong schema khác che (trigger chạy với search_path của người gọi).
CREATE FUNCTION revoke_sessions_on_assignment_change() RETURNS trigger LANGUAGE plpgsql
SET search_path = identity, pg_temp AS $$
BEGIN
  UPDATE refresh_token SET revoked_at = now()
  WHERE revoked_at IS NULL AND account_id IN (NEW.account_id, OLD.account_id);
  RETURN NULL;
END $$;
