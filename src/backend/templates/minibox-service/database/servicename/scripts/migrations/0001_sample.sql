-- Migration mẫu của template: tạo bảng sample (khớp tables/sample.sql). Thay bằng migration thật.
-- version: concurrency token của EF Core (EfRepository.UpdateAsync).
CREATE TABLE sample (
  id      uuid   PRIMARY KEY,           -- UUIDv7 do ứng dụng sinh
  name    text   NOT NULL,
  version bigint NOT NULL DEFAULT 0
);
