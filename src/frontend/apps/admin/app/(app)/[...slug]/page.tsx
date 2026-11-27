import { EmptyState, PageHeader } from "@minibox/ui";
import { notFound } from "next/navigation";
import { MENU } from "../../../lib/menu";

/**
 * Trang chờ cho mục menu chưa có phân hệ: vẫn trong khung admin (menu, breadcrumb) thay vì rơi ra trang 404.
 *
 * @param props - `params.slug` là các đoạn đường dẫn.
 * @returns Trang chờ của mục menu; đường dẫn ngoài menu trả 404.
 * @remarks Phân hệ thật thêm route riêng (vd. `app/(app)/kho/page.tsx`) sẽ được ưu tiên hơn route bắt-tất-cả này.
 */
const Pending = async ({ params }: { params: Promise<{ slug: string[] }> }) => {
  const href = `/${(await params).slug.join("/")}`;
  const item = MENU.find((i) => i.href === href);
  if (!item) notFound();
  return (
    <div className="flex w-full flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
      <PageHeader title={item.label} description={item.group} />
      <EmptyState title="Phân hệ đang được xây dựng" description={`Màn hình ${item.label.toLowerCase()} sẽ có ở các mốc sau của dự án.`} />
    </div>
  );
};

export default Pending;
