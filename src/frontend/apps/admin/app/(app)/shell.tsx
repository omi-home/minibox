"use client";
import { AdminShell } from "@minibox/ui";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import type { MenuItem } from "../../lib/menu";

/**
 * Khung admin phía client: đánh dấu mục của trang hiện tại.
 *
 * @param props - `items` là menu đã lọc theo quyền ở server; `children` là nội dung trang.
 * @returns Khung trang admin.
 */
export const Shell = ({ items, children }: { items: MenuItem[]; children: ReactNode }) => {
  const path = usePathname();
  const nav = items.map(({ group, label, href }) => ({ group, label, href, current: path === href || path.startsWith(`${href}/`) }));
  return (
    <AdminShell nav={nav} link={Link}>
      {children}
    </AdminShell>
  );
};
