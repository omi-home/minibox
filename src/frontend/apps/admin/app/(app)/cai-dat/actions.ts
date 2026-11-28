"use server";
import { revalidatePath } from "next/cache";
import { localizationAdmin } from "../../../lib/i18n";

/** Kết quả server action: `error` là câu báo lỗi cho người dùng (lấy `detail` ProblemDetails của API). */
export type ActionResult = { error?: string; published?: number };

type Call = { error?: unknown; data?: unknown; response: Response };

const STATUS_TEXT: Record<number, string> = {
  401: "Phiên đăng nhập đã hết. Đăng nhập lại rồi thử lại.",
  403: "Tài khoản của bạn chưa có quyền thao tác này.",
};

async function run(path: string, call: Promise<Call>): Promise<ActionResult> {
  try {
    const { error, data, response } = await call;
    if (response.ok) {
      revalidatePath(path);
      const published = (data as { published?: unknown } | undefined)?.published;
      return published === undefined ? {} : { published: Number(published) };
    }
    const detail = (error as { detail?: unknown } | undefined)?.detail;
    return {
      error: STATUS_TEXT[response.status] ?? (typeof detail === "string" ? detail : "Không lưu được. Thử lại sau ít phút."),
    };
  } catch {
    return {
      error: "Không kết nối được dịch vụ bản dịch. Thử lại sau ít phút.",
    };
  }
}

/**
 * Lưu bản nháp một chuỗi (tạo mới khi `version` là `null`); người dùng shop/admin chỉ thấy sau khi xuất bản.
 *
 * @param input - `namespace`, `key`, `locale` xác định chuỗi; `text` văn bản thuần; `version` phiên bản lúc tải.
 * @returns Lỗi nếu có (vd. người khác vừa sửa).
 */
export async function saveDraft(input: {
  namespace: string;
  key: string;
  locale: string;
  text: string;
  version: number | null;
}): Promise<ActionResult> {
  const client = await localizationAdmin();
  return run(
    "/cai-dat/ban-dich",
    client.PUT("/admin/messages/{namespace}/{key}/{locale}", {
      params: {
        path: {
          namespace: input.namespace,
          key: input.key,
          locale: input.locale,
        },
      },
      body: { text: input.text, version: input.version },
    }),
  );
}

/**
 * Xuất bản mọi bản nháp của (namespace, locale); BFF làm mới chuỗi qua sự kiện `MessagesPublished`.
 *
 * @param namespace - Nhóm chuỗi.
 * @param locale - Mã locale.
 * @returns Số chuỗi đã xuất bản hoặc lỗi.
 */
export async function publish(namespace: string, locale: string): Promise<ActionResult> {
  const client = await localizationAdmin();
  return run(
    "/cai-dat/ban-dich",
    client.POST("/admin/messages/{namespace}/{locale}/publish", {
      params: { path: { namespace, locale } },
    }),
  );
}

/**
 * Thêm ngôn ngữ (mặc định tắt cho tới khi bật).
 *
 * @param input - `code` BCP 47, `name` tên tự gọi, `sortOrder` thứ tự, `fallbackCode` locale rơi về (`null` = `vi`).
 * @returns Lỗi nếu có (vd. trùng mã).
 */
export async function createLocale(input: {
  code: string;
  name: string;
  sortOrder: number;
  fallbackCode: string | null;
}): Promise<ActionResult> {
  const client = await localizationAdmin();
  return run("/cai-dat/ngon-ngu", client.POST("/admin/locales", { body: input }));
}

/**
 * Sửa ngôn ngữ: tên, bật/tắt, thứ tự, locale fallback.
 *
 * @param code - Mã locale.
 * @param input - Giá trị mới kèm `version` lúc tải.
 * @returns Lỗi nếu có (vd. fallback tạo vòng lặp, `vi` không tắt được).
 */
export async function updateLocale(
  code: string,
  input: {
    name: string;
    enabled: boolean;
    sortOrder: number;
    fallbackCode: string | null;
    version: number;
  },
): Promise<ActionResult> {
  const client = await localizationAdmin();
  return run(
    "/cai-dat/ngon-ngu",
    client.PUT("/admin/locales/{code}", {
      params: { path: { code } },
      body: input,
    }),
  );
}
