-- LoginThrottle và RefreshToken thành aggregate (bug #456): cột version làm concurrency token của EF Core (EfRepository.UpdateAsync).
ALTER TABLE login_throttle ADD COLUMN version bigint NOT NULL DEFAULT 0;
ALTER TABLE refresh_token ADD COLUMN version bigint NOT NULL DEFAULT 0;
