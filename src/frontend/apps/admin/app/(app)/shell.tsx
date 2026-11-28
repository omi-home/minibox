"use client";
import {
  ArticleIcon,
  CheckSquareIcon,
  ChartBarIcon,
  ClipboardTextIcon,
  ClockCountdownIcon,
  CoinsIcon,
  CubeIcon,
  GlobeIcon,
  GearSixIcon,
  HouseIcon,
  LifebuoyIcon,
  PackageIcon,
  PlugsConnectedIcon,
  PrinterIcon,
  ReceiptIcon,
  SealCheckIcon,
  ShieldCheckIcon,
  SignOutIcon,
  TranslateIcon,
  TruckIcon,
  UsersIcon,
  WalletIcon,
  WarningOctagonIcon,
} from "@phosphor-icons/react";
import { AdminShell, type ThemeMode, toast } from "@minibox/ui";
import GB from "country-flag-icons/react/3x2/GB";
import VN from "country-flag-icons/react/3x2/VN";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { type ReactNode, useState } from "react";
import {
  CURRENCIES,
  CURRENCY_COOKIE,
  LOCALE_COOKIE,
  LOCALES,
  type MenuItem,
  SIDEBAR_COOKIE,
  THEME_COOKIE,
  TIMEZONE_COOKIE,
  TIMEZONES,
} from "../../lib/menu";
import type { Notification } from "../../lib/notifications";

const FLAGS = { VN: <VN title="" />, GB: <GB title="" /> };

const NOTIFICATION_ICONS: Record<Notification["kind"], ReactNode> = {
  order: <ReceiptIcon />,
  stock: <PackageIcon />,
  approval: <CheckSquareIcon />,
};

const ICONS: Record<string, ReactNode> = {
  "/": <HouseIcon />,
  "/bao-cao": <ChartBarIcon />,
  "/don-hang": <ReceiptIcon />,
  "/khach-hang": <UsersIcon />,
  "/san-xuat": <PrinterIcon />,
  "/kho": <PackageIcon />,
  "/qc": <SealCheckIcon />,
  "/ho-tro": <LifebuoyIcon />,
  "/mua-hang": <TruckIcon />,
  "/tai-chinh": <WalletIcon />,
  "/san-pham": <CubeIcon />,
  "/noi-dung": <ArticleIcon />,
  "/cai-dat/phan-quyen": <ShieldCheckIcon />,
  "/cai-dat/he-thong": <GearSixIcon />,
  "/cai-dat/tai-chinh": <CoinsIcon />,
  "/cai-dat/ban-dich": <TranslateIcon />,
  "/cai-dat/ngon-ngu": <GlobeIcon />,
  "/ky-thuat/tich-hop": <PlugsConnectedIcon />,
  "/ky-thuat/tac-vu": <ClockCountdownIcon />,
  "/ky-thuat/loi": <WarningOctagonIcon />,
  "/ky-thuat/audit": <ClipboardTextIcon />,
};


/**
 * Khung admin phía client: gắn icon, đánh dấu trang hiện tại, chuyển trang bằng router, nhớ sidebar thu gọn vào cookie.
 *
 * @param props - `items` là menu đã lọc theo quyền ở server; `collapsed`, `theme`, `locale`, `timezone`, `currency` là lựa
 * chọn đọc từ cookie; `notifications` là thông báo của người dùng; `children` là nội dung trang.
 * @returns Khung trang admin.
 */
export const Shell = ({
  items,
  collapsed,
  theme: initialTheme,
  locale: initialLocale,
  timezone: initialTimezone,
  currency: initialCurrency,
  notifications,
  children,
}: {
  items: MenuItem[];
  collapsed: boolean;
  theme: ThemeMode;
  locale: string;
  timezone: string;
  currency: string;
  notifications: Notification[];
  children: ReactNode;
}) => {
  const path = usePathname();
  const router = useRouter();
  const [theme, setTheme] = useState(initialTheme);
  const [locale, setLocale] = useState(initialLocale);
  const [timezone, setTimezone] = useState(initialTimezone);
  const [currency, setCurrency] = useState(initialCurrency);
  const remember = (name: string, value: string) => {
    document.cookie = `${name}=${value}; Path=/; Max-Age=31536000; SameSite=Lax`;
  };
  const logout = async () => {
    const res = await fetch("/api/auth/logout", { method: "POST" }).catch(() => undefined);
    if (!res?.ok) {
      toast({ tone: "danger", title: "Chưa đăng xuất được", description: "Kiểm tra kết nối rồi thử lại." });
      return;
    }
    router.replace("/");
    router.refresh();
  };
  const nav = [{ href: "/", label: "Tổng quan" }, ...items].map((i) => ({
    ...i,
    icon: ICONS[i.href],
    current: i.href === "/" ? path === "/" : path === i.href || path.startsWith(`${i.href}/`),
  }));
  return (
    <AdminShell
      nav={nav}
      link={Link}
      onNavigate={(href) => router.push(href)}
      defaultCollapsed={collapsed}
      theme={{
        value: theme,
        onChange: (mode) => {
          setTheme(mode);
          remember(THEME_COOKIE, mode);
          if (mode === "system") delete document.documentElement.dataset.theme;
          else document.documentElement.dataset.theme = mode;
        },
      }}
      // ponytail: mới lưu lựa chọn; chuỗi, giờ và tiền chưa đổi theo tới khi admin lấy từ service localization (CR #387).
      region={{
        locale: {
          value: locale,
          options: LOCALES.map((l) => ({ ...l, flag: FLAGS[l.flag] })),
          onChange: (code) => {
            setLocale(code);
            remember(LOCALE_COOKIE, code);
          },
        },
        timezone: {
          value: timezone,
          options: TIMEZONES,
          onChange: (tz) => {
            setTimezone(tz);
            remember(TIMEZONE_COOKIE, tz);
          },
        },
        currency: {
          value: currency,
          options: CURRENCIES,
          onChange: (c) => {
            setCurrency(c);
            remember(CURRENCY_COOKIE, c);
          },
        },
      }}
      notifications={{
        count: notifications.filter((n) => n.unread).length,
        items: notifications.map((n) => ({ ...n, icon: NOTIFICATION_ICONS[n.kind] })),
      }}
      onCollapsedChange={(c) => {
        document.cookie = `${SIDEBAR_COOKIE}=${c ? 1 : 0}; Path=/; Max-Age=31536000; SameSite=Lax`;
      }}
      // ponytail: token chưa có tên/hồ sơ nhân viên; hiện tên chung tới khi identity có endpoint hồ sơ.
      user={{ name: "Nhân viên", detail: "Tài khoản quản trị", menu: [{ label: "Đăng xuất", icon: <SignOutIcon />, tone: "danger", onSelect: logout }] }}
    >
      {children}
    </AdminShell>
  );
};
