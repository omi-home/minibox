-- Đổi quyền → thu hồi refresh token của người bị ảnh hưởng (spec §3.6): access token đang cầm hết hạn trong ≤ 5 phút,
-- sau đó phải đăng nhập lại để nhận perms mới. Đặt ở trigger để mọi đường ghi (API, migration, xóa role cascade) đều thu hồi.

CREATE FUNCTION revoke_sessions_on_assignment_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  UPDATE refresh_token SET revoked_at = now()
  WHERE revoked_at IS NULL AND account_id IN (NEW.account_id, OLD.account_id);
  RETURN NULL;
END $$;

CREATE TRIGGER role_assignment_revoke_sessions
AFTER INSERT OR UPDATE OR DELETE ON role_assignment
FOR EACH ROW EXECUTE FUNCTION revoke_sessions_on_assignment_change();

CREATE FUNCTION revoke_sessions_on_role_permission_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  UPDATE refresh_token SET revoked_at = now()
  WHERE revoked_at IS NULL
    AND account_id IN (SELECT account_id FROM role_assignment WHERE role_id IN (NEW.role_id, OLD.role_id));
  RETURN NULL;
END $$;

CREATE TRIGGER role_permission_revoke_sessions
AFTER INSERT OR UPDATE OR DELETE ON role_permission
FOR EACH ROW EXECUTE FUNCTION revoke_sessions_on_role_permission_change();
