import { cookies } from "next/headers";
import type { ReactNode } from "react";
import { readPerms, visibleMenu } from "../../lib/menu";
import { SESSION_COOKIE, unsealSession } from "../../lib/session";
import { Shell } from "./shell";

/**
 * Layout các trang tác nghiệp: khung admin với menu ba vùng lọc theo `perms` của phiên (chỉ để tiện; API tự kiểm quyền).
 *
 * @param props - `children` là trang hiện tại.
 * @returns Trang trong khung admin.
 */
const AppLayout = async ({ children }: { children: ReactNode }) => {
  const session = unsealSession((await cookies()).get(SESSION_COOKIE)?.value);
  return <Shell items={visibleMenu(readPerms(session?.accessToken))}>{children}</Shell>;
};

export default AppLayout;
