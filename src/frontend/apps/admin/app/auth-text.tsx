"use client";
import { createContext, type ReactNode, useContext } from "react";

/**
 * Chuỗi màn đăng nhập, MFA và xác thực lại (namespace `admin`, key `auth.*`): bản tiếng Việt làm mặc định khi service
 * localization chưa có key hoặc tạm lỗi. Bản xuất bản (vi, en…) seed ở database/schemas/localization/.../0006_admin-auth-messages.sql
 * và sửa được ở trang Bản dịch.
 */
export const AUTH_TEXT = {
  "auth.brand.eyebrow": "Hệ thống vận hành Minibox",
  "auth.brand.title": "Từ máy in đến tay khách hàng.",
  "auth.brand.lead": "Mỗi chiếc hộp được theo dõi qua đơn hàng, sản xuất và kho trên cùng một hệ thống.",
  "auth.brand.orders": "Đơn hàng, thanh toán, hoàn tiền và Minixu",
  "auth.brand.production": "Farm máy in, BOM và lệnh sản xuất",
  "auth.brand.warehouse": "Tồn kho, kiểm tra chất lượng và giao hàng",
  "auth.brand.notice": "Chỉ dành cho nhân viên Minibox. Mỗi lần đăng nhập và thao tác nhạy cảm đều được ghi nhật ký.",
  "auth.footer": "© {year} Minibox · Cần hỗ trợ? Liên hệ quản trị hệ thống.",
  "auth.login.title": "Đăng nhập",
  "auth.login.description": "Dùng tài khoản nhân viên Minibox. Bước sau cần mã xác thực hai lớp.",
  "auth.login.identifier": "Email hoặc tên đăng nhập",
  "auth.login.password": "Mật khẩu",
  "auth.login.submit": "Đăng nhập",
  "auth.mfa.title": "Xác thực hai lớp",
  "auth.mfa.description": "Bảo vệ tài khoản quản trị bằng mã từ ứng dụng xác thực trên điện thoại.",
  "auth.mfa.code": "Mã xác thực",
  "auth.mfa.codeHint": "6 chữ số trong ứng dụng xác thực, hoặc một mã khôi phục.",
  "auth.mfa.verify": "Xác minh",
  "auth.mfa.firstTime": "Lần đầu đăng nhập?",
  "auth.mfa.setup": "Thiết lập ứng dụng xác thực",
  "auth.enroll.steps": "Các bước thiết lập",
  "auth.enroll.stepScan": "Quét mã",
  "auth.enroll.stepSave": "Lưu mã khôi phục",
  "auth.enroll.stepVerify": "Xác minh",
  "auth.enroll.qr": "Mã QR thiết lập ứng dụng xác thực",
  "auth.enroll.openApp": "Mở ứng dụng xác thực (Google Authenticator, Microsoft Authenticator…) trên điện thoại.",
  "auth.enroll.scanQr": "Quét mã QR, rồi nhập mã 6 số ứng dụng hiển thị.",
  "auth.enroll.manual": "Không quét được? Nhập khóa thủ công",
  "auth.enroll.key": "Khóa thiết lập",
  "auth.enroll.copyKey": "Chép",
  "auth.enroll.code": "Mã 6 số từ ứng dụng",
  "auth.enroll.enable": "Bật xác thực hai lớp",
  "auth.enroll.finish": "Nhập mã mới trong ứng dụng xác thực (đợi mã đổi) hoặc một mã khôi phục để hoàn tất đăng nhập.",
  "auth.recovery.warning": "Mỗi mã khôi phục dùng được một lần khi mất điện thoại. Mã chỉ hiện lần này, hãy lưu ở nơi an toàn.",
  "auth.recovery.list": "Mã khôi phục",
  "auth.recovery.copy": "Sao chép",
  "auth.recovery.copied": "Đã sao chép",
  "auth.recovery.download": "Tải xuống",
  "auth.recovery.saved": "Tôi đã lưu mã khôi phục ở nơi an toàn",
  "auth.recovery.continue": "Tiếp tục",
  "auth.stepUp.title": "Xác thực lại",
  "auth.stepUp.description": "Thao tác này cần xác thực hai lớp gần đây. Nhập mã để tiếp tục.",
  "auth.stepUp.submit": "Xác thực và tiếp tục",
  "auth.error.Identity.InvalidCredentials": "Sai email/tên đăng nhập hoặc mật khẩu. Kiểm tra lại rồi thử lần nữa.",
  "auth.error.Identity.LoginThrottled": "Nhập sai nhiều lần. Vui lòng đợi ít phút rồi thử lại.",
  "auth.error.Identity.InvalidMfaCode": "Mã xác thực không đúng hoặc đã được dùng.",
  "auth.error.Identity.MfaNotEnrolled": "Tài khoản chưa bật xác thực hai lớp. Bấm “Thiết lập ứng dụng xác thực” bên dưới.",
  "auth.error.Identity.MfaAlreadyEnrolled": "Tài khoản đã bật xác thực hai lớp. Nhập mã từ ứng dụng xác thực hoặc mã khôi phục.",
  "auth.error.unauthenticated": "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại.",
  "auth.error.unknown": "Có lỗi xảy ra. Vui lòng thử lại.",
} as const;

/** Key chuỗi màn xác thực. */
export type AuthTextKey = keyof typeof AUTH_TEXT;

const Messages = createContext<Record<string, string>>({});

/**
 * Cung cấp chuỗi đã dịch theo ngôn ngữ của request cho {@link useAuthText}.
 *
 * @param props - `messages` là chuỗi `auth.*` đã xuất bản của ngôn ngữ hiện tại (nạp ở server); `children` là phần dùng.
 * @returns Provider.
 */
export const AuthTextProvider = ({ messages, children }: { messages: Record<string, string>; children: ReactNode }) => (
  <Messages.Provider value={messages}>{children}</Messages.Provider>
);

/**
 * Hàm dịch cho màn xác thực: chuỗi đã xuất bản, thiếu thì bản tiếng Việt mặc định; `{tên}` thay bằng `values`.
 *
 * @returns `t(key, values?)`; `t.error(code)` đổi mã lỗi BFF thành câu cho người dùng.
 */
export const useAuthText = () => {
  const messages = useContext(Messages);
  const t = (key: AuthTextKey, values?: Record<string, string | number>) =>
    (messages[key] ?? AUTH_TEXT[key]).replace(/\{(\w+)\}/g, (m, name: string) => (values && name in values ? String(values[name]) : m));
  // Mã lỗi `{ code }` từ BFF → câu đã dịch; mã lạ hiện câu lỗi chung.
  const error = (code: unknown) => {
    const key = `auth.error.${String(code)}`;
    return key in AUTH_TEXT ? t(key as AuthTextKey) : t("auth.error.unknown");
  };
  return Object.assign(t, { error });
};