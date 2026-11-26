-- Phân quyền (spec §3.6): mã quyền miền.tài-nguyên.hành-động; vai trò = tập mã quyền (sửa được);
-- phân công = người + vai trò + phạm vi. Mỗi phạm vi một dòng: '*' (mọi nơi), 'wh:<id kho>', 'farm:<id farm>' —
-- trùng khóa của perms trong access token.

CREATE TABLE permission (
  code        text PRIMARY KEY CHECK (code ~ '^[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*$'),
  description text NOT NULL
);

-- kind = customer: vai trò mẫu của khách, quyền dữ liệu theo chủ sở hữu kiểm trong handler nên không gán mã quyền.
CREATE TABLE role (
  id      uuid PRIMARY KEY DEFAULT uuidv7(),
  code    text NOT NULL UNIQUE CHECK (code ~ '^[a-z]+(-[a-z]+)*$'),
  name    text NOT NULL,
  kind    text NOT NULL CHECK (kind IN ('customer', 'staff')),
  version bigint NOT NULL DEFAULT 0
);

CREATE TABLE role_permission (
  role_id         uuid NOT NULL REFERENCES role (id) ON DELETE CASCADE,
  permission_code text NOT NULL REFERENCES permission (code),
  PRIMARY KEY (role_id, permission_code)
);

CREATE TABLE role_assignment (
  account_id uuid        NOT NULL REFERENCES account (id),
  role_id    uuid        NOT NULL REFERENCES role (id),
  scope      text        NOT NULL CHECK (scope ~ '^(\*|(wh|farm):[1-9][0-9]*)$'),
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (account_id, role_id, scope)
);

CREATE INDEX role_assignment_role ON role_assignment (role_id);

-- Seed tham chiếu: danh mục quyền theo công việc BRD §3 (service sau bổ sung mã của miền mình bằng migration).
INSERT INTO permission (code, description) VALUES
  ('identity.role.manage',          'Tạo/sửa vai trò và quyền của vai trò'),
  ('identity.assignment.manage',    'Phân công vai trò và phạm vi cho nhân viên'),
  ('config.setting.manage',         'Sửa cấu hình hệ thống'),
  ('report.dashboard.read',         'Xem báo cáo'),
  ('audit.log.read',                'Xem nhật ký audit'),
  ('customer.pii.read',             'Xem dữ liệu cá nhân của khách'),
  ('order.order.create',            'Tạo đơn hộ khách'),
  ('order.order.confirm',           'Xác nhận đơn'),
  ('support.ticket.resolve',        'Giải quyết yêu cầu hỗ trợ'),
  ('production.capacity.approve',   'Duyệt năng lực sản xuất'),
  ('production.machine.manage',     'Quản lý farm/máy'),
  ('production.batch.manage',       'Lập và điều phối mẻ in'),
  ('production.progress.update',    'Cập nhật tiến độ sản xuất'),
  ('inventory.receipt.create',      'Nhập kho'),
  ('inventory.issue.create',        'Xuất kho'),
  ('inventory.reservation.manage',  'Giữ hàng'),
  ('inventory.transfer.create',     'Điều chuyển kho'),
  ('inventory.stocktake.perform',   'Kiểm kê'),
  ('inventory.adjustment.create',   'Đề xuất điều chỉnh tồn'),
  ('inventory.adjustment.approve',  'Duyệt điều chỉnh tồn'),
  ('inventory.package.pack',        'Đóng gói'),
  ('qc.checklist.perform',          'Thực hiện checklist QC'),
  ('qc.defect.classify',            'Phân loại lỗi'),
  ('warranty.claim.process',        'Xử lý bảo hành'),
  ('purchasing.supplier.manage',    'Quản lý nhà cung cấp'),
  ('purchasing.order.create',       'Tạo đơn mua'),
  ('purchasing.receipt.create',     'Nhận hàng mua'),
  ('purchasing.return.create',      'Trả nhà cung cấp'),
  ('finance.payment.confirm',       'Xác nhận tiền vào'),
  ('finance.expense.create',        'Ghi khoản chi'),
  ('finance.deposit.manage',        'Quản lý tiền cọc'),
  ('finance.refund.create',         'Hoàn tiền'),
  ('finance.receivable.manage',     'Quản lý công nợ'),
  ('finance.cod.reconcile',         'Đối soát COD'),
  ('finance.invoice.issue',         'Xuất hóa đơn'),
  ('finance.setting.manage',        'Cấu hình tài chính'),
  ('catalog.product.manage',        'Quản lý sản phẩm'),
  ('content.post.manage',           'Soạn blog'),
  ('content.post.publish',          'Xuất bản nội dung'),
  ('content.media.manage',          'Quản lý media'),
  ('content.comment.moderate',      'Duyệt bình luận'),
  ('integration.connector.manage',  'Cấu hình tích hợp'),
  ('integration.sync.run',          'Chạy đồng bộ'),
  ('ops.error.read',                'Xem lỗi hệ thống'),
  ('ops.job.manage',                'Quản lý tác vụ nền');

-- 11 vai trò mẫu BRD §3.
INSERT INTO role (code, name, kind) VALUES
  ('guest',       'Khách vãng lai',       'customer'),
  ('customer',    'Khách có tài khoản',   'customer'),
  ('owner',       'Chủ shop/quản trị',    'staff'),
  ('sales',       'Bán hàng/CSKH',        'staff'),
  ('production',  'Điều phối/sản xuất',   'staff'),
  ('warehouse',   'Nhân viên kho',        'staff'),
  ('qc',          'QC/bảo hành',          'staff'),
  ('purchasing',  'Mua hàng',             'staff'),
  ('finance',     'Tài chính',            'staff'),
  ('content',     'Nội dung',             'staff'),
  ('engineering', 'Kỹ thuật',             'staff');

-- Chủ shop: mọi quyền (BRD §3 cho phép kiêm tạo/duyệt giai đoạn nhỏ).
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, p.code FROM role r CROSS JOIN permission p WHERE r.code = 'owner';

-- Bán hàng không có finance.payment.confirm (không tự xác nhận tiền vào); kỹ thuật không có finance.* và customer.pii.read.
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, x.permission_code
FROM (VALUES
  ('sales', 'order.order.create'), ('sales', 'order.order.confirm'), ('sales', 'support.ticket.resolve'), ('sales', 'customer.pii.read'),
  ('production', 'production.capacity.approve'), ('production', 'production.machine.manage'),
  ('production', 'production.batch.manage'), ('production', 'production.progress.update'),
  ('warehouse', 'inventory.receipt.create'), ('warehouse', 'inventory.issue.create'), ('warehouse', 'inventory.reservation.manage'),
  ('warehouse', 'inventory.transfer.create'), ('warehouse', 'inventory.stocktake.perform'), ('warehouse', 'inventory.adjustment.create'),
  ('warehouse', 'inventory.package.pack'),
  ('qc', 'qc.checklist.perform'), ('qc', 'qc.defect.classify'), ('qc', 'warranty.claim.process'),
  ('purchasing', 'purchasing.supplier.manage'), ('purchasing', 'purchasing.order.create'),
  ('purchasing', 'purchasing.receipt.create'), ('purchasing', 'purchasing.return.create'),
  ('finance', 'finance.payment.confirm'), ('finance', 'finance.expense.create'), ('finance', 'finance.deposit.manage'),
  ('finance', 'finance.refund.create'), ('finance', 'finance.receivable.manage'), ('finance', 'finance.cod.reconcile'),
  ('finance', 'finance.invoice.issue'), ('finance', 'report.dashboard.read'),
  ('content', 'catalog.product.manage'), ('content', 'content.post.manage'), ('content', 'content.post.publish'),
  ('content', 'content.media.manage'), ('content', 'content.comment.moderate'),
  ('engineering', 'integration.connector.manage'), ('engineering', 'integration.sync.run'),
  ('engineering', 'ops.error.read'), ('engineering', 'ops.job.manage')
) AS x (role_code, permission_code)
JOIN role r ON r.code = x.role_code;
