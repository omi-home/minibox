import { Alert, PageHeader } from "@minibox/ui";
import { localizationAdmin } from "../../../../lib/i18n";
import { LOCALES_DESCRIPTION, LOCALES_TITLE, NAMESPACES, summarize } from "../../../../lib/translations";
import { Locales } from "./locales";

/**
 * Màn quản lý ngôn ngữ: thêm, bật/tắt, sắp xếp, đặt locale fallback; kèm số key giao diện còn thiếu của từng ngôn ngữ.
 *
 * @returns Trang ngôn ngữ, hoặc thông báo lỗi khi không gọi được localization.
 */
const LocalesPage = async () => {
  const client = await localizationAdmin();
  const [locales, ...byNamespace] = await Promise.all([
    client.GET("/admin/locales").catch(() => undefined),
    ...NAMESPACES.map((n) => client.GET("/admin/messages", { params: { query: { namespace: n.value } } }).catch(() => undefined)),
  ]);
  const header = <PageHeader title={LOCALES_TITLE} description={LOCALES_DESCRIPTION} />;
  if (!locales?.data)
    return (
      <div className="flex w-full flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
        {header}
        <Alert
          tone="danger"
          title={locales?.response.status === 403 ? "Bạn chưa có quyền quản lý ngôn ngữ" : "Chưa tải được danh sách ngôn ngữ"}
        >
          {locales?.response.status === 403 ? "Nhờ quản trị viên cấp quyền localization.locale.manage." : "Tải lại trang sau ít phút."}
        </Alert>
      </div>
    );
  const codes = locales.data.map((l) => l.code);
  // Không có quyền xem bản dịch thì không hiện số key thiếu (undefined), thay vì hiện 0 gây hiểu nhầm.
  const missing = byNamespace.every((r) => r?.data)
    ? Object.fromEntries(codes.map((c) => [c, byNamespace.reduce((sum, r) => sum + summarize(r!.data!, codes)[c]!.missing, 0)]))
    : undefined;
  return (
    <Locales
      locales={[...locales.data]
        .sort((a, b) => Number(a.sortOrder) - Number(b.sortOrder) || a.code.localeCompare(b.code))
        .map((l) => ({
          ...l,
          sortOrder: Number(l.sortOrder),
          version: Number(l.version),
        }))}
      missing={missing}
    />
  );
};

export default LocalesPage;
