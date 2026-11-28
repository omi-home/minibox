/** Mã lỗi (401) API trả khi thao tác nhạy cảm cần xác thực lại MFA (spec §3.5). */
export const STEP_UP_REQUIRED = "step_up_required";

/**
 * Gửi request tới BFF; nếu nhận `401 step_up_required` thì hỏi người dùng xác thực lại rồi gửi lại đúng một lần.
 *
 * @param send - Hàm gửi request (gọi lại được, cùng `Idempotency-Key` nếu có).
 * @param reauth - Mở màn hình xác thực lại; `true` khi người dùng xác thực xong, `false` khi hủy.
 * @returns Response cuối: của lần gửi lại, hoặc response 401 gốc nếu không cần/không xác thực lại.
 */
export async function withStepUp(send: () => Promise<Response>, reauth: () => Promise<boolean>): Promise<Response> {
  const res = await send();
  if (res.status !== 401) return res;
  const { code } = (await res.clone().json().catch(() => ({}))) as { code?: unknown };
  if (code !== STEP_UP_REQUIRED || !(await reauth())) return res;
  return send();
}
