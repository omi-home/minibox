# Tài liệu Minibox

| Tài liệu | Nội dung | Khi nào đọc |
|---|---|---|
| [business-requirements.md](business-requirements.md) | BRD v1.0: nghiệp vụ, mã `BR-*`, thông số `CFG-*`, điểm mở `OPEN-*`, mốc M0–M6 | Nguồn yêu cầu duy nhất; đọc trước mọi việc |
| [technical-spec.md](technical-spec.md) | Đặc tả kỹ thuật: kiến trúc, service, dữ liệu, bảo mật, hạ tầng, test, ngưỡng NFR | Trước khi lập plan hoặc viết code |

Quy ước:
- Hai tài liệu trên là đầu vào; đổi nội dung đã duyệt phải có quyết định của PO (BRD §2).
- Issue/Task dẫn mã `BR-*`/`CFG-*`/`OPEN-*` (BRD) và mục spec (`technical-spec §n`).
- Tài liệu vận hành (runbook deploy, backup/DR) đặt trong `docs/` khi được viết ở mốc tương ứng.
