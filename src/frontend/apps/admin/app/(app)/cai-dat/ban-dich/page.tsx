import { Alert, PageHeader } from "@minibox/ui";
import { localizationAdmin } from "../../../../lib/i18n";
import {
  type AdminLocale,
  buildKeys,
  NAMESPACES,
  summarize,
  TRANSLATIONS_DESCRIPTION,
  TRANSLATIONS_TITLE,
} from "../../../../lib/translations";
import { Translations } from "./translations";

type Search = { ns?: string; missing?: string; q?: string };

/**
 * Màn quản lý bản dịch: chuỗi của một namespace theo key, mỗi locale một cột; lọc key thiếu theo locale, tìm, sửa nháp và
 * xuất bản theo locale. Số key thiếu và số nháp chờ xuất bản hiện ở đầu trang.
 *
 * @param props - `searchParams`: `ns` namespace (mặc định `admin`), `missing` locale cần lọc key thiếu, `q` từ khóa.
 * @returns Trang bản dịch, hoặc thông báo lỗi khi không gọi được localization.
 */
const TranslationsPage = async ({ searchParams }: { searchParams: Promise<Search> }) => {
  const search = await searchParams;
  const namespace = NAMESPACES.find((n) => n.value === search.ns)?.value ?? NAMESPACES[0].value;
  const client = await localizationAdmin();
  const [messages, all] = await Promise.all([
    client.GET("/admin/messages", { params: { query: { namespace } } }).catch(() => undefined),
    client.GET("/admin/locales").catch(() => undefined),
  ]);
  // Người chỉ có quyền bản dịch không xem được /admin/locales: dùng danh sách locale đang bật (công khai).
  const locales: Pick<AdminLocale, "code" | "name" | "enabled">[] =
    all?.data ?? ((await client.GET("/locales").catch(() => undefined))?.data ?? []).map((l) => ({ ...l, enabled: true }));
  const header = <PageHeader title={TRANSLATIONS_TITLE} description={TRANSLATIONS_DESCRIPTION} />;
  if (!messages?.data)
    return (
      <div className="flex w-full flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
        {header}
        <Alert tone="danger" title={messages?.response.status === 403 ? "Bạn chưa có quyền xem bản dịch" : "Chưa tải được bản dịch"}>
          {messages?.response.status === 403 ? "Nhờ quản trị viên cấp quyền localization.message.manage." : "Tải lại trang sau ít phút."}
        </Alert>
      </div>
    );
  const missing = locales.some((l) => l.code === search.missing) ? search.missing : undefined;
  return (
    <Translations
      namespace={namespace}
      locales={locales.map(({ code, name, enabled }) => ({
        code,
        name,
        enabled,
      }))}
      summary={summarize(
        messages.data,
        locales.map((l) => l.code),
      )}
      total={buildKeys(messages.data, {}).length}
      rows={buildKeys(messages.data, { missing, q: search.q })}
      missing={missing}
      q={search.q ?? ""}
    />
  );
};

export default TranslationsPage;
