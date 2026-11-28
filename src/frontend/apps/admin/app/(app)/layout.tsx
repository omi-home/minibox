import { cookies } from "next/headers";
import type { ReactNode } from "react";
import {
  CURRENCIES,
  CURRENCY_COOKIE,
  pick,
  readPerms,
  SIDEBAR_COOKIE,
  THEME_COOKIE,
  themeOf,
  TIMEZONE_COOKIE,
  TIMEZONES,
  visibleMenu,
} from "../../lib/menu";
import { getTranslator, localization, requestLocale } from "../../lib/i18n";
import { notifications } from "../../lib/notifications";
import { SESSION_COOKIE, unsealSession } from "../../lib/session";
import { Shell } from "./shell";

/**
 * Layout các trang tác nghiệp: khung admin với menu ba vùng lọc theo `perms` của phiên (chỉ để tiện; API tự kiểm quyền).
 *
 * @param props - `children` là trang hiện tại.
 * @returns Trang trong khung admin.
 */
const AppLayout = async ({ children }: { children: ReactNode }) => {
  const jar = await cookies();
  const session = unsealSession(jar.get(SESSION_COOKIE)?.value);
  const [locale, locales, t] = await Promise.all([requestLocale(), localization().locales(), getTranslator()]);
  return (
    <Shell
      items={visibleMenu(readPerms(session?.accessToken))}
      collapsed={jar.get(SIDEBAR_COOKIE)?.value === "1"}
      theme={themeOf(jar.get(THEME_COOKIE)?.value)}
      homeLabel={t("dashboard.title")}
      locale={locale}
      locales={locales}
      timezone={pick(jar.get(TIMEZONE_COOKIE)?.value, TIMEZONES.map((t) => t.value))}
      currency={pick(jar.get(CURRENCY_COOKIE)?.value, CURRENCIES.map((c) => c.value))}
      notifications={notifications()}
    >
      {children}
    </Shell>
  );
};

export default AppLayout;
