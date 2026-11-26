-- Đổi quyền của vai trò → thu hồi refresh token của mọi người có vai trò đó (spec §3.6). Trigger: tables/role_permission.sql.
CREATE FUNCTION revoke_sessions_on_role_permission_change() RETURNS trigger LANGUAGE plpgsql
SET search_path = identity, pg_temp AS $$
BEGIN
  UPDATE refresh_token SET revoked_at = now()
  WHERE revoked_at IS NULL
    AND account_id IN (SELECT account_id FROM role_assignment WHERE role_id IN (NEW.role_id, OLD.role_id));
  RETURN NULL;
END $$;
