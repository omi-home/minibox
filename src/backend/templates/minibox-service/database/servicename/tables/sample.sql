-- Bảng mẫu của template (trạng thái đích, một đối tượng một file): thay bằng bảng thật của service.
-- version: concurrency token của EF Core (EfRepository.UpdateAsync).
CREATE TABLE sample (
  id      uuid   PRIMARY KEY,           -- UUIDv7 do ứng dụng sinh
  name    text   NOT NULL,
  version bigint NOT NULL DEFAULT 0
);
