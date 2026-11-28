"use client";
/*
 * Khung app admin (BRD §5.2, §13): thanh đầu ghim (tìm kiếm, thông báo, tài khoản) ở mọi cỡ màn hình;
 * laptop trở lên có sidebar chia vùng, thu gọn còn icon; tablet mở menu dạng ngăn kéo trái;
 * mobile như app native: thanh app có tiêu đề trang, thanh tab dưới (4 mục + Menu) và sheet menu dạng lưới.
 */
import {
  BellIcon,
  CaretDownIcon,
  CheckIcon,
  DesktopIcon,
  ListIcon,
  MagnifyingGlassIcon,
  MoonIcon,
  SidebarSimpleIcon,
  SunIcon,
  XIcon,
} from "@phosphor-icons/react";
import { Command } from "cmdk";
import { Dialog, DropdownMenu as RMenu, Popover } from "radix-ui";
import { type ElementType, type ReactNode, useEffect, useState, useSyncExternalStore } from "react";
import { Logo } from "./brand";
import { BrandScene } from "./scene";
import { type BottomNavItem, overlay } from "./components";
import { EmptyState } from "./feedback";
import { cx } from "./cx";
import { vnFilter } from "./form";
import { Breadcrumb, type BreadcrumbProps } from "./navigation";
import { DropdownMenu, type MenuItem, Tooltip } from "./overlay";

/** Mục điều hướng admin. */
export type AdminNavItem = BottomNavItem & {
  /** Vùng (vd. "Nghiệp vụ"); sidebar và menu chia nhóm theo thứ tự xuất hiện. */
  group?: string;
  /** Nhãn ngắn cho thanh tab dưới trên mobile khi nhãn đầy đủ quá dài. */
  short?: string;
};

/** Người dùng đang đăng nhập, hiện ở nút avatar. */
export type AdminUser = {
  /** Tên hiển thị; chữ cái đầu làm avatar. */
  name: string;
  /** Dòng phụ (vai trò, kho). */
  detail?: string;
  /** Mục của menu tài khoản; mục `danger` (Đăng xuất) tự tách xuống cuối. */
  menu?: MenuItem[];
};

/** Chế độ giao diện: theo hệ điều hành hoặc ép sáng/tối (đặt `data-theme` trên `<html>`). */
export type ThemeMode = "system" | "light" | "dark";

/** Một ngôn ngữ chọn được ở thanh đầu. */
export type LocaleOption = {
  /** Mã locale (vd. `vi`, `en`); nút hiện mã viết hoa. */
  code: string;
  /** Tên ngôn ngữ viết bằng chính ngôn ngữ đó (vd. "Tiếng Việt", "English"). */
  label: string;
  /** Cờ (SVG, app truyền vào; trang trí). */
  flag?: ReactNode;
};

/** Một lựa chọn múi giờ hoặc tiền tệ hiển thị. */
export type PreferenceOption = {
  /** Giá trị lưu (vd. `Asia/Ho_Chi_Minh`, `VND`). */
  value: string;
  /** Nhãn đọc được (vd. "Hà Nội, TP.HCM"). */
  label: string;
  /** Chú thích ngắn bên phải (vd. "GMT+7", "₫"). */
  hint?: string;
};

/** Một nhóm tùy chọn chọn một giá trị (giá trị hiện tại, danh sách, hàm đổi). */
export type PreferenceChoice<T> = { value: string; options: T[]; onChange: (value: string) => void };

/** Tùy chọn vùng hiển thị (CR #387): ngôn ngữ, múi giờ và tiền tệ hiển thị; giao dịch vẫn tính VND. */
export type RegionSettings = {
  /** Ngôn ngữ giao diện. */
  locale: PreferenceChoice<LocaleOption>;
  /** Múi giờ hiển thị thời gian. */
  timezone?: PreferenceChoice<PreferenceOption>;
  /** Tiền tệ hiển thị (quy đổi để xem). */
  currency?: PreferenceChoice<PreferenceOption>;
};

/** Một thông báo trong bảng thông báo. */
export type AdminNotification = {
  /** Khóa ổn định. */
  id: string;
  /** Câu chính (việc cụ thể). */
  title: string;
  /** Chi tiết một dòng. */
  description?: string;
  /** Thời điểm đã định dạng sẵn theo múi giờ người xem (vd. "5 phút trước"). */
  time: string;
  /** Chưa đọc: in đậm, có chấm trạng thái. */
  unread?: boolean;
  /** Trang mở khi bấm. */
  href?: string;
  /** Icon loại thông báo (trang trí). */
  icon?: ReactNode;
};

/** Thuộc tính của {@link AdminShell}. */
export type AdminShellProps = {
  /** Mục điều hướng đã lọc theo quyền; 4 mục đầu lên thanh tab dưới trên mobile. */
  nav: AdminNavItem[];
  /** Tiêu đề trên thanh app mobile; mặc định nhãn mục hiện tại. */
  title?: string;
  /**
   * Đường dẫn dưới thanh đầu (từ tablet). Mặc định suy từ menu: trang chủ (mục `/`) › vùng › mục hiện tại; trang sâu hơn
   * truyền đủ danh sách.
   */
  breadcrumb?: BreadcrumbProps["items"];
  /** Tài khoản ở góc phải thanh đầu. */
  user?: AdminUser;
  /** Số thông báo chưa đọc (badge trên chuông) và danh sách trong bảng; không có mục thì hiện trạng thái trống. */
  notifications?: { count: number; items?: AdminNotification[] };
  /** Chế độ giao diện hiện tại và hàm đổi (app lưu lựa chọn, vd. cookie). Không truyền thì không hiện bộ chọn. */
  theme?: { value: ThemeMode; onChange: (mode: ThemeMode) => void };
  /** Ngôn ngữ, múi giờ, tiền tệ hiển thị; nút mở là cờ + mã ngôn ngữ. Không truyền thì không hiện. */
  region?: RegionSettings;
  /** Sidebar thu gọn lúc đầu (vd. đọc từ cookie để không nháy khi tải trang). */
  defaultCollapsed?: boolean;
  /** Gọi khi người dùng thu gọn/mở rộng sidebar, để lưu lựa chọn. */
  onCollapsedChange?: (collapsed: boolean) => void;
  /** Chuyển trang khi chọn kết quả tìm kiếm (vd. `router.push`); mặc định `location.assign`. */
  onNavigate?: (href: string) => void;
  /** Nội dung trang. */
  children: ReactNode;
  /** Component link (vd. `next/link`); mặc định `<a>`. */
  link?: ElementType;
};

const iconButton =
  "relative size-11 shrink-0 cursor-pointer items-center justify-center rounded-box text-muted transition-[color,background-color,transform] hover:bg-surface-sunken hover:text-foreground active:scale-95 lg:size-9 [&_svg]:size-5";

// Mục trên mảng thương hiệu: chữ nhạt, mục đang chọn nền sáng hơn + vạch hổ phách bên trái (điểm nhấn hổ phách duy nhất của khung).
const sidebarLink =
  "relative flex h-10 items-center gap-3 rounded-box px-3 text-sm font-medium text-brand-muted transition-colors before:absolute before:inset-y-2.5 before:left-0 before:w-0.75 before:rounded-sm before:bg-highlight before:opacity-0 hover:bg-brand-active hover:text-brand-foreground aria-[current=page]:bg-brand-active aria-[current=page]:text-brand-foreground aria-[current=page]:before:opacity-100 [&_svg]:size-5 [&_svg]:shrink-0";

// Nút icon trên thanh đầu: dưới 640px thanh đầu là mảng thương hiệu nên đổi màu theo.
const headerIcon = "max-sm:text-brand-muted max-sm:hover:bg-brand-active max-sm:hover:text-brand-foreground";

const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0]?.toUpperCase())
    .join("");

const groupsOf = (nav: AdminNavItem[]) =>
  [...new Set(nav.map((i) => i.group))].map((g) => [g, nav.filter((i) => i.group === g)] as const);

/**
 * Khung trang admin cho tác nghiệp: mật độ compact, thanh đầu ghim, sidebar thu gọn được (Ctrl+B), tìm trang nhanh (Ctrl+K),
 * mobile có thanh app + thanh tab dưới + sheet menu.
 *
 * @param props - {@link AdminShellProps}.
 * @returns Khung trang.
 */
export const AdminShell = ({
  nav,
  title,
  breadcrumb,
  user,
  notifications = { count: 0 },
  theme,
  region,
  defaultCollapsed = false,
  onCollapsedChange,
  onNavigate = (href) => location.assign(href),
  children,
  link: Link = "a",
}: AdminShellProps) => {
  const [collapsed, setCollapsed] = useState(defaultCollapsed);
  const [menuOpen, setMenuOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  // Vùng đã thu gọn: tiện ích riêng từng người xem, lưu trình duyệt; server render mở hết để hydrate khớp.
  const closedGroups = parseGroups(useSyncExternalStore(subscribeGroups, readGroups, () => "[]"));
  const toggleGroup = (g: string) => {
    const next = closedGroups.includes(g) ? closedGroups.filter((x) => x !== g) : [...closedGroups, g];
    try {
      localStorage.setItem(GROUPS_KEY, JSON.stringify(next));
    } catch {
      /* bộ nhớ bị chặn: không nhớ được lựa chọn, chấp nhận */
    }
    dispatchEvent(new Event(GROUPS_KEY));
  };
  const groups = groupsOf(nav);
  const current = nav.find((i) => i.current);
  const tabs = nav.slice(0, 4);
  const home = nav.find((i) => i.href === "/");
  const trail =
    breadcrumb ??
    (current
      ? current === home
        ? [{ label: current.label }]
        : [...(home ? [{ label: home.label, href: home.href }] : []), ...(current.group ? [{ label: current.group }] : []), { label: current.label }]
      : []);

  const toggle = () => {
    setCollapsed(!collapsed);
    onCollapsedChange?.(!collapsed);
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!(e.ctrlKey || e.metaKey)) return;
      if (e.key === "k") {
        e.preventDefault();
        setSearchOpen(true);
      } else if (e.key === "b" && matchMedia("(min-width: 64rem)").matches) {
        e.preventDefault();
        setCollapsed((c) => {
          onCollapsedChange?.(!c);
          return !c;
        });
      }
    };
    addEventListener("keydown", onKey);
    return () => removeEventListener("keydown", onKey);
  }, [onCollapsedChange]);

  const sidebarItem = (i: AdminNavItem) => {
    const a = (
      <Link href={i.href} aria-current={i.current ? "page" : undefined} className={sidebarLink}>
        {i.icon && <span aria-hidden>{i.icon}</span>}
        <span className={collapsed ? "sr-only" : "truncate"}>{i.label}</span>
      </Link>
    );
    return <li key={i.href}>{collapsed ? <Tooltip content={i.label}>{a}</Tooltip> : a}</li>;
  };

  return (
    <div
      data-density="compact"
      data-sidebar={collapsed ? "collapsed" : "expanded"}
      className="group/shell min-h-dvh touch-manipulation [-webkit-tap-highlight-color:transparent]"
    >
      {/* Cảnh nền rừng thông (#448) phía sau nội dung; khung để trong suốt cho cảnh hiện ra, nội dung dày đặt trên thẻ bề mặt. */}
      <BrandScene />
      <a
        href="#noi-dung"
        className="sr-only z-toast rounded-box bg-foreground px-3 py-2 text-sm font-semibold text-background focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
      >
        Bỏ qua tới nội dung
      </a>

      <aside className="fixed inset-y-0 left-0 z-sticky hidden w-60 flex-col border-r border-brand-border bg-brand text-brand-foreground group-data-[sidebar=collapsed]/shell:w-17 lg:flex">
        <div className="flex h-14 shrink-0 items-center border-b border-brand-border px-4.5">
          <Link href="/" className="text-lg">
            <Logo tone="inverse" withWordmark={!collapsed} title="Minibox quản trị, về bảng điều khiển" />
          </Link>
        </div>
        <nav aria-label="Điều hướng quản trị" className="flex flex-1 flex-col gap-4 overflow-x-hidden overflow-y-auto px-3 py-4">
          {groups.map(([group, items], gi) => {
            // Sidebar thu gọn chỉ còn icon: luôn hiện mọi mục, không có nút đóng/mở vùng.
            const open = collapsed || !group || !closedGroups.includes(group);
            const listId = `vung-${gi}`;
            return (
              <div key={group ?? ""} className="flex flex-col gap-1">
                {group && (
                  <h2 className={collapsed ? "sr-only" : undefined}>
                    <button
                      type="button"
                      onClick={() => toggleGroup(group)}
                      aria-expanded={open}
                      aria-controls={listId}
                      className="flex min-h-8 w-full cursor-pointer items-center gap-2 rounded-box px-3 py-1 text-left text-xs font-semibold text-brand-muted transition-colors hover:text-brand-foreground [&_svg]:size-3.5"
                    >
                      <span className="min-w-0 flex-1">{group}</span>
                      <CaretDownIcon aria-hidden weight="bold" className={cx("shrink-0 transition-transform", !open && "-rotate-90")} />
                    </button>
                  </h2>
                )}
                {group && collapsed && <hr aria-hidden className="mx-2 mb-1 border-brand-border" />}
                <ul id={listId} hidden={!open} className="flex flex-col gap-0.5">
                  {items.map(sidebarItem)}
                </ul>
              </div>
            );
          })}
        </nav>
        {user && <UserCard user={user} compact={collapsed} className="border-brand-border" />}
      </aside>

      <div className="flex min-h-dvh flex-col lg:pl-60 lg:group-data-[sidebar=collapsed]/shell:pl-17">
        <header className="sticky top-0 z-sticky border-b border-brand-border bg-brand pt-[env(safe-area-inset-top)] text-brand-foreground sm:border-border sm:bg-surface/90 sm:text-foreground sm:backdrop-blur-md">
          <div className="flex h-14 items-center gap-1 px-2 sm:gap-2 sm:px-4 lg:px-6">
            <button
              type="button"
              onClick={toggle}
              aria-label={collapsed ? "Mở rộng thanh bên" : "Thu gọn thanh bên"}
              aria-expanded={!collapsed}
              title="Ctrl+B"
              className={cx(iconButton, "hidden lg:flex")}
            >
              <SidebarSimpleIcon aria-hidden />
            </button>
            <button type="button" onClick={() => setMenuOpen(true)} aria-label="Mở menu" className={cx(iconButton, "hidden sm:flex lg:hidden")}>
              <ListIcon aria-hidden />
            </button>
            <Link href="/" className="hidden px-1 text-base sm:inline-flex lg:hidden">
              <Logo title="Minibox quản trị, về bảng điều khiển" />
            </Link>
            <Link href="/" className="px-1.5 text-xl sm:hidden">
              <Logo tone="inverse" withWordmark={false} title="Minibox quản trị, về bảng điều khiển" />
            </Link>
            <p className="min-w-0 truncate font-display text-lg font-semibold sm:hidden">{title ?? current?.label ?? "Bảng điều khiển"}</p>

            {/* Ô tìm kiếm lấp đầy phần giữa thanh đầu; bo tròn hẳn để tách khỏi nút/ô nhập thường (design-system §3). */}
            <div className="hidden min-w-0 flex-1 justify-center px-2 sm:flex lg:px-6">
              <button
                type="button"
                onClick={() => setSearchOpen(true)}
                className="flex h-10 w-full cursor-pointer items-center gap-2.5 rounded-full border border-border bg-surface-sunken pr-2 pl-4 text-sm text-muted shadow-inset transition-colors hover:border-border-strong hover:bg-surface [&_svg]:size-4.5"
              >
                <MagnifyingGlassIcon aria-hidden />
                <span className="truncate">Tìm trang, chức năng</span>
                <kbd className="ml-auto hidden rounded-full border border-border bg-surface px-2 py-0.5 font-mono text-xs lg:inline">Ctrl K</kbd>
              </button>
            </div>

            <div className="ml-auto flex items-center gap-0.5 sm:gap-1">
              <button type="button" onClick={() => setSearchOpen(true)} aria-label="Tìm kiếm" className={cx(iconButton, headerIcon, "flex sm:hidden")}>
                <MagnifyingGlassIcon aria-hidden />
              </button>
              {theme && <ThemeToggle {...theme} className="hidden sm:flex" />}
              {region && <RegionMenu {...region} className="hidden sm:flex" />}
              <Notifications {...notifications} link={Link} />
              {user && <UserMenu user={user} />}
            </div>
          </div>
          {trail.length > 0 && (
            <div className="hidden h-10 items-center border-t border-border px-4 sm:flex lg:px-6">
              <Breadcrumb items={trail} link={Link} />
            </div>
          )}
        </header>

        <main id="noi-dung" tabIndex={-1} className="flex-1 pb-[calc(4.25rem+env(safe-area-inset-bottom))] outline-none sm:pb-0">
          {children}
        </main>
      </div>

      <nav
        aria-label="Điều hướng nhanh"
        className="fixed inset-x-0 bottom-0 z-sticky border-t border-border bg-surface/90 pb-[env(safe-area-inset-bottom)] backdrop-blur-md sm:hidden"
      >
        <ul className="flex h-[4.25rem]">
          {tabs.map((i) => (
            <li key={i.href} className="min-w-0 flex-1">
              <Link href={i.href} aria-current={i.current ? "page" : undefined} className={tab}>
                <span aria-hidden className={tabIcon}>
                  {i.icon}
                </span>
                <span className="max-w-full truncate px-1">{i.short ?? i.label}</span>
              </Link>
            </li>
          ))}
          <li className="min-w-0 flex-1">
            <button type="button" onClick={() => setMenuOpen(true)} aria-expanded={menuOpen} className={cx(tab, "w-full cursor-pointer")}>
              <span aria-hidden className={tabIcon}>
                <ListIcon />
              </span>
              Menu
            </button>
          </li>
        </ul>
      </nav>

      <NavMenu
        open={menuOpen}
        onOpenChange={setMenuOpen}
        groups={groups}
        user={user}
        link={Link}
        // Mobile: thanh app chật nên chọn giao diện/ngôn ngữ nằm ở chân sheet Menu (như phần cài đặt của app).
        settings={
          (theme || region) && (
            <div className="flex items-center gap-1 sm:hidden">
              {theme && <ThemeToggle {...theme} plain className="flex" />}
              {region && <RegionMenu {...region} plain className="flex" />}
            </div>
          )
        }
      />
      <Search open={searchOpen} onOpenChange={setSearchOpen} groups={groups} onNavigate={onNavigate} />
    </div>
  );
};

const tab =
  "group flex h-full flex-col items-center justify-center gap-1 text-xs font-medium text-muted transition-[color,transform] active:scale-95 aria-[current=page]:text-foreground";
const tabIcon =
  "flex h-8 w-14 items-center justify-center rounded-box transition-colors group-aria-[current=page]:bg-accent group-aria-[current=page]:text-accent-foreground [&_svg]:size-6";

type Groups = ReturnType<typeof groupsOf>;

/** Menu đầy đủ: sheet lưới từ dưới lên trên mobile, ngăn kéo trái trên tablet. */
const NavMenu = ({
  open,
  onOpenChange,
  groups,
  user,
  settings,
  link: Link,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  groups: Groups;
  user?: AdminUser;
  settings?: ReactNode;
  link: ElementType;
}) => {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className={overlay} />
        <Dialog.Content
          aria-describedby={undefined}
          className={cx(
            "chamfer fixed inset-x-0 bottom-0 z-modal flex max-h-[85dvh] flex-col rounded-t-lg border border-border bg-surface shadow-box-3 outline-none",
            "data-[state=closed]:animate-sheet-down data-[state=open]:animate-sheet-up",
            "sm:inset-y-0 sm:right-auto sm:left-0 sm:max-h-none sm:w-72 sm:rounded-none sm:rounded-r-lg sm:border-brand-border sm:bg-brand sm:text-brand-foreground",
            "sm:data-[state=closed]:animate-drawer-out sm:data-[state=open]:animate-drawer-in",
          )}
        >
          <div aria-hidden className="mx-auto mt-2 h-1 w-10 rounded-sm bg-border-strong sm:hidden" />
          <div className="flex items-center justify-between px-4 pt-2 pb-1 sm:h-14 sm:border-b sm:border-brand-border sm:pt-0 sm:pb-0">
            <Dialog.Title className="font-display text-lg font-semibold">Menu</Dialog.Title>
            <Dialog.Close aria-label="Đóng" className={cx(iconButton, "-mr-2 flex sm:text-brand-muted sm:hover:bg-brand-active sm:hover:text-brand-foreground")}>
              <XIcon aria-hidden />
            </Dialog.Close>
          </div>
          <nav aria-label="Menu đầy đủ" className="flex flex-1 flex-col gap-5 overflow-y-auto overscroll-contain px-4 pt-2 pb-4">
            {groups.map(([group, items]) => (
              <section key={group ?? ""} className="flex flex-col gap-2">
                {group && <h2 className="text-xs font-semibold text-muted sm:text-brand-muted">{group}</h2>}
                <ul className="grid grid-cols-3 gap-2 sm:grid-cols-1 sm:gap-0.5">
                  {items.map((i) => (
                    <li key={i.href}>
                      <Link
                        href={i.href}
                        onClick={() => onOpenChange(false)}
                        aria-current={i.current ? "page" : undefined}
                        className={cx(
                          "chamfer-sm flex h-20 flex-col items-center justify-center gap-1.5 rounded-lg border border-border bg-surface-raised px-1 text-center text-xs font-medium transition-[color,background-color,transform] active:scale-95 [&_svg]:size-6",
                          "max-sm:aria-[current=page]:border-accent max-sm:aria-[current=page]:bg-accent max-sm:aria-[current=page]:text-accent-foreground",
                          // Ngăn kéo tablet: cùng kiểu mục với sidebar.
                          "sm:h-10 sm:flex-row sm:justify-start sm:gap-3 sm:rounded-box sm:border-0 sm:bg-transparent sm:px-3 sm:text-left sm:text-sm sm:text-brand-muted sm:[corner-top-right-shape:round] sm:hover:bg-brand-active sm:hover:text-brand-foreground sm:aria-[current=page]:bg-brand-active sm:aria-[current=page]:text-brand-foreground sm:[&_svg]:size-5",
                        )}
                      >
                        {i.icon && <span aria-hidden>{i.icon}</span>}
                        <span className="line-clamp-2 sm:truncate">{i.label}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </nav>
          {user && (
            <UserCard
              user={user}
              action={settings}
              className="border-border pb-[calc(0.75rem+env(safe-area-inset-bottom))] sm:border-brand-border sm:pb-3 max-sm:[&_[data-avatar]]:bg-accent max-sm:[&_[data-avatar]]:text-accent-foreground max-sm:[&_[data-detail]]:text-muted"
            />
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
};

/** Tìm trang nhanh (Ctrl+K): gõ không dấu, Enter để mở. */
const Search = ({
  open,
  onOpenChange,
  groups,
  onNavigate,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  groups: Groups;
  onNavigate: (href: string) => void;
}) => {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className={overlay} />
        <Dialog.Content
          aria-describedby={undefined}
          className="chamfer fixed inset-x-0 top-0 z-modal flex max-h-[80dvh] origin-top flex-col overflow-hidden rounded-b-lg border border-border bg-surface-raised pt-[env(safe-area-inset-top)] shadow-box-3 outline-none data-[state=open]:animate-lid-open sm:inset-x-auto sm:top-[12dvh] sm:left-1/2 sm:w-[36rem] sm:-translate-x-1/2 sm:rounded-lg sm:pt-0"
        >
          <Dialog.Title className="sr-only">Tìm trang, chức năng</Dialog.Title>
          <Command filter={vnFilter} loop label="Tìm trang, chức năng" className="flex min-h-0 flex-col">
            <div className="flex items-center gap-2 border-b border-border px-3 [&_svg]:size-5 [&_svg]:text-muted">
              <MagnifyingGlassIcon aria-hidden />
              <Command.Input placeholder="Tìm trang, chức năng" className="h-13 w-full bg-transparent text-base outline-none placeholder:text-muted" />
              <Dialog.Close aria-label="Đóng" className={cx(iconButton, "-mr-1 flex sm:hidden")}>
                <XIcon aria-hidden />
              </Dialog.Close>
            </div>
            <Command.List className="overflow-y-auto overscroll-contain p-1.5">
              <Command.Empty className="px-3 py-8 text-center text-sm text-muted">Không có trang nào khớp</Command.Empty>
              {groups.map(([group, items]) => (
                <Command.Group
                  key={group ?? ""}
                  heading={group}
                  className="[&_[cmdk-group-heading]]:px-3 [&_[cmdk-group-heading]]:pt-2 [&_[cmdk-group-heading]]:pb-1 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:font-semibold [&_[cmdk-group-heading]]:text-muted"
                >
                  {items.map((i) => (
                    <Command.Item
                      key={i.href}
                      value={i.href}
                      keywords={[i.label, group ?? ""]}
                      onSelect={() => {
                        onOpenChange(false);
                        onNavigate(i.href);
                      }}
                      className="flex h-11 cursor-pointer items-center gap-3 rounded-box px-3 text-sm data-[selected=true]:bg-accent data-[selected=true]:text-accent-foreground [&_svg]:size-5"
                    >
                      {i.icon && <span aria-hidden>{i.icon}</span>}
                      {i.label}
                    </Command.Item>
                  ))}
                </Command.Group>
              ))}
            </Command.List>
          </Command>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
};

const GROUPS_KEY = "minibox-admin-closed-groups";

const subscribeGroups = (onChange: () => void) => {
  addEventListener("storage", onChange);
  addEventListener(GROUPS_KEY, onChange);
  return () => {
    removeEventListener("storage", onChange);
    removeEventListener(GROUPS_KEY, onChange);
  };
};

const readGroups = () => {
  try {
    return localStorage.getItem(GROUPS_KEY) ?? "[]";
  } catch {
    return "[]";
  }
};

const parseGroups = (raw: string): string[] => {
  try {
    const v: unknown = JSON.parse(raw);
    return Array.isArray(v) ? v.filter((x): x is string => typeof x === "string") : [];
  } catch {
    return [];
  }
};

const THEMES: { value: ThemeMode; label: string; icon: ReactNode }[] = [
  { value: "light", label: "Sáng", icon: <SunIcon /> },
  { value: "dark", label: "Tối", icon: <MoonIcon /> },
  { value: "system", label: "Theo hệ thống", icon: <DesktopIcon /> },
];

/**
 * Nút giao diện một chạm: mỗi lần bấm đổi vòng Sáng → Tối → Theo hệ thống; icon là chế độ đang dùng. Khung admin tự gắn;
 * trang ngoài khung (đăng nhập) đặt riêng, thêm class `flex` để hiện.
 *
 * @param props - `value` chế độ hiện tại, `onChange` nhận chế độ kế tiếp; `plain` bỏ kiểu nút trên mảng thương hiệu (mobile).
 * @returns Nút đổi giao diện có nhãn đọc chế độ hiện tại và chế độ kế tiếp.
 */
export const ThemeToggle = ({ value, onChange, className, plain }: { value: ThemeMode; onChange: (m: ThemeMode) => void; className?: string; plain?: boolean }) => {
  const i = Math.max(0, THEMES.findIndex((t) => t.value === value));
  const current = THEMES[i]!;
  const next = THEMES[(i + 1) % THEMES.length]!;
  const label = `Giao diện: ${current.label}. Bấm để chuyển sang ${next.label}`;
  return (
    <button type="button" onClick={() => onChange(next.value)} aria-label={label} title={label} className={cx(iconButton, !plain && headerIcon, className)}>
      <span aria-hidden key={current.value} className="animate-fade-in">
        {current.icon}
      </span>
    </button>
  );
};

const menuLabel = "px-3 pt-2 pb-1 text-xs font-semibold text-muted";
const menuItem =
  "flex h-10 cursor-pointer items-center gap-2.5 rounded-box px-3 text-sm outline-none select-none data-highlighted:bg-surface-sunken [&_svg]:size-4";

/** Một nhóm chọn một giá trị trong menu vùng (menuitemradio có dấu tick). */
const RadioSection = ({ label, choice }: { label: string; choice: PreferenceChoice<{ value: string; label: string; hint?: string; icon?: ReactNode }> }) => {
  return (
    <RMenu.Group>
      <RMenu.Label className={menuLabel}>{label}</RMenu.Label>
      <RMenu.RadioGroup value={choice.value} onValueChange={choice.onChange}>
        {choice.options.map((o) => (
          <RMenu.RadioItem key={o.value} value={o.value} className={menuItem}>
            {o.icon && (
              <span aria-hidden className="flex w-5 justify-center">
                {o.icon}
              </span>
            )}
            <span className="flex-1">{o.label}</span>
            {o.hint && <span className="font-mono text-xs text-muted">{o.hint}</span>}
            <span className="flex w-4 justify-center">
              <RMenu.ItemIndicator>
                <CheckIcon aria-hidden weight="bold" />
              </RMenu.ItemIndicator>
            </span>
          </RMenu.RadioItem>
        ))}
      </RMenu.RadioGroup>
    </RMenu.Group>
  );
};

/**
 * Nút vùng hiển thị: cờ + mã ngôn ngữ; menu gồm ngôn ngữ, múi giờ, tiền tệ hiển thị (múi giờ, tiền tệ tùy chọn). Khung admin tự gắn;
 * trang ngoài khung (đăng nhập) đặt riêng chỉ với `locale`, thêm class `flex` để hiện.
 *
 * @param props - {@link RegionSettings}; `plain` bỏ kiểu nút trên mảng thương hiệu (mobile).
 * @returns Nút mở menu ngôn ngữ và vùng.
 */
export const RegionMenu = ({ locale, timezone, currency, className, plain }: RegionSettings & { className?: string; plain?: boolean }) => {
  const current = locale.options.find((o) => o.code === locale.value);
  const summary = [current?.label ?? locale.value, timezone && timezone.options.find((o) => o.value === timezone.value)?.label, currency?.value]
    .filter(Boolean)
    .join(", ");
  return (
    // Không modal: menu chọn nhanh, không cần khóa phần còn lại của trang (Radix modal gắn aria-hidden lên phần đó).
    <RMenu.Root modal={false}>
      <RMenu.Trigger
        aria-label={`Ngôn ngữ và vùng: ${summary}`}
        title={`Ngôn ngữ và vùng: ${summary}`}
        className={cx(iconButton, !plain && headerIcon, "w-auto gap-1.5 px-2 lg:w-auto", className)}
      >
        {current?.flag && (
          <span aria-hidden className="flex h-3.5 w-5 overflow-hidden rounded-[2px] shadow-[0_0_0_1px_var(--color-border)] [&_svg]:size-full">
            {current.flag}
          </span>
        )}
        {/* Mã ngôn ngữ vẽ bằng ::before để nhãn đọc không lệch chữ hiển thị. */}
        <span aria-hidden data-code={locale.value} className="font-mono text-xs font-semibold uppercase before:content-[attr(data-code)]" />
      </RMenu.Trigger>
      <RMenu.Portal>
        <RMenu.Content
          aria-label="Ngôn ngữ và vùng"
          align="end"
          sideOffset={6}
          collisionPadding={8}
          className="z-modal max-h-[var(--radix-dropdown-menu-content-available-height)] w-72 origin-top overflow-y-auto rounded-lg border border-border bg-surface-raised p-1 text-foreground shadow-box-3 data-[state=open]:animate-lid-open"
        >
          <RadioSection label="Ngôn ngữ" choice={{ ...locale, options: locale.options.map((o) => ({ value: o.code, label: o.label, hint: o.code.toUpperCase(), icon: o.flag })) }} />
          {timezone && (
            <>
              <RMenu.Separator className="my-1 h-px bg-border" />
              <RadioSection label="Múi giờ hiển thị" choice={timezone} />
            </>
          )}
          {currency && (
            <>
              <RMenu.Separator className="my-1 h-px bg-border" />
              <RadioSection label="Tiền tệ hiển thị" choice={currency} />
              <p className="px-3 pt-1 pb-2 text-xs text-muted">Giao dịch vẫn tính bằng VND; tiền tệ khác chỉ để xem.</p>
            </>
          )}
        </RMenu.Content>
      </RMenu.Portal>
    </RMenu.Root>
  );
};

/** Nút chuông có badge số chưa đọc + bảng thông báo. */
const Notifications = ({ count, items = [], link: Link }: { count: number; items?: AdminNotification[]; link: ElementType }) => {
  const [open, setOpen] = useState(false);
  return (
    <Popover.Root open={open} onOpenChange={setOpen}>
      <Popover.Trigger aria-label={count > 0 ? `Thông báo, ${count} chưa đọc` : "Thông báo"} className={cx(iconButton, headerIcon, "flex")}>
        <BellIcon aria-hidden />
        {count > 0 && (
          <span
            aria-hidden
            className="absolute top-1 right-0.5 flex h-4.5 min-w-4.5 items-center justify-center rounded-full bg-danger px-1 font-mono text-[0.6875rem] leading-none font-semibold text-danger-foreground ring-2 ring-surface max-sm:ring-brand lg:-top-0.5 lg:-right-1"
          >
            {count > 99 ? "99+" : count}
          </span>
        )}
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          aria-label="Thông báo"
          align="end"
          sideOffset={8}
          collisionPadding={8}
          className="chamfer z-modal flex w-[min(24rem,calc(100vw-1rem))] origin-top-right flex-col rounded-lg border border-border bg-surface-raised shadow-box-3 outline-none data-[state=open]:animate-lid-open"
        >
          <div className="flex items-baseline justify-between border-b border-border px-4 py-3">
            <p className="font-display font-semibold">Thông báo</p>
            {count > 0 && <p className="text-xs text-muted">{count} chưa đọc</p>}
          </div>
          <div className="max-h-[60dvh] overflow-y-auto overscroll-contain p-1.5">
            {items.length === 0 ? (
              <EmptyState title="Chưa có thông báo" description="Đơn mới, cảnh báo kho và việc chờ duyệt sẽ hiện ở đây." />
            ) : (
              <ul className="flex flex-col gap-0.5">
                {items.map((n) => {
                  const Row = n.href ? Link : "div";
                  return (
                    <li key={n.id}>
                      <Row
                        {...(n.href ? { href: n.href, onClick: () => setOpen(false) } : {})}
                        className="flex gap-3 rounded-box px-2.5 py-2.5 transition-colors hover:bg-surface-sunken"
                      >
                        <span aria-hidden className="flex size-8 shrink-0 items-center justify-center rounded-full bg-accent text-accent-foreground [&_svg]:size-4">
                          {n.icon ?? <BellIcon />}
                        </span>
                        <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                          <span className={cx("text-sm", n.unread ? "font-semibold" : "text-muted")}>
                            {n.unread && <span className="sr-only">Chưa đọc: </span>}
                            {n.title}
                          </span>
                          {n.description && <span className="truncate text-xs text-muted">{n.description}</span>}
                          <span className="text-xs text-muted">{n.time}</span>
                        </span>
                        {n.unread && <span aria-hidden className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" />}
                      </Row>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
};

/** Avatar tròn chữ cái đầu tên (trang trí; tên đọc nằm ở phần tử chứa). */
const Avatar = ({ name, className }: { name: string; className?: string }) => {
  return (
    // Chữ cái đầu vẽ bằng ::before: chỉ để nhìn, không thành chữ của nút (nhãn đọc là "Tài khoản …").
    <span
      aria-hidden
      data-avatar
      data-initials={initials(name)}
      className={cx(
        "flex size-8 shrink-0 items-center justify-center rounded-full font-display text-sm font-semibold before:content-[attr(data-initials)]",
        className,
      )}
    />
  );
};

/** Tên + vai trò ghim ở chân sidebar/menu; `compact` (sidebar thu gọn) chỉ còn avatar, tên vẫn cho trình đọc màn hình. */
const UserCard = ({ user, compact, action, className }: { user: AdminUser; compact?: boolean; action?: ReactNode; className?: string }) => {
  return (
    <div className={cx("flex shrink-0 items-center gap-3 border-t px-4.5 py-3", className)} title={compact ? user.name : undefined}>
      <Avatar name={user.name} className="bg-brand-active text-brand-foreground" />
      <div className={compact ? "sr-only" : "flex min-w-0 flex-1 flex-col leading-tight"}>
        <span className="truncate text-sm font-semibold">{user.name}</span>
        {user.detail && (
          <span data-detail className="truncate text-xs text-brand-muted">
            {user.detail}
          </span>
        )}
      </div>
      {action}
    </div>
  );
};

/** Nút avatar tròn trên thanh đầu, mở menu tài khoản. */
const UserMenu = ({ user }: { user: AdminUser }) => {
  return (
    <DropdownMenu
      label="Tài khoản"
      items={user.menu ?? []}
      trigger={
        <button
          type="button"
          aria-label={`Tài khoản ${user.name}`}
          className="ml-1 flex size-11 cursor-pointer items-center justify-center rounded-full transition-[background-color,transform] hover:bg-surface-sunken active:scale-95 max-sm:hover:bg-brand-active lg:size-10"
        >
          <Avatar name={user.name} className="bg-accent text-accent-foreground" />
        </button>
      }
    />
  );
};
