/*
 * Mẫu bố cục trang (spec #448 §6): khung shop, khung admin, đầu trang. Điều hướng theo thiết bị (BRD §5.2):
 * mobile dùng điều hướng dưới; tablet đủ chỗ cho hàng link trên; laptop trở lên có điều hướng đầy đủ (shop: header, admin: sidebar).
 */
import { ShoppingBagIcon } from "@phosphor-icons/react";
import { type ElementType, type ReactNode } from "react";
import { Logo } from "./brand";
import { BottomNav, type BottomNavItem } from "./components";
import { cx } from "./field";
import { Breadcrumb, type BreadcrumbProps } from "./navigation";

const skip =
  "sr-only z-toast rounded-box bg-foreground px-3 py-2 text-sm font-semibold text-background focus:not-sr-only focus:fixed focus:top-3 focus:left-3";

const navLink =
  "flex h-10 items-center gap-2 rounded-box px-3 text-sm font-medium text-muted transition-colors hover:bg-surface-sunken hover:text-foreground aria-[current=page]:text-foreground [&_svg]:size-5";

/** Thuộc tính của {@link ShopShell}. */
export type ShopShellProps = {
  /** Mục điều hướng cấp cao nhất (≤ 5): header từ tablet, điều hướng dưới trên mobile. */
  nav: BottomNavItem[];
  /** Số sản phẩm trong giỏ. */
  cartCount?: number;
  /** Ô tìm kiếm (giữa header từ laptop). */
  search?: ReactNode;
  /** Nội dung trang. */
  children: ReactNode;
  /** Component link (vd. `next/link`); mặc định `<a>`. */
  link?: ElementType;
};

/**
 * Khung trang shop: link bỏ qua, header dính một dòng (logo, điều hướng, tìm kiếm, giỏ), nội dung, footer gọn, điều hướng dưới trên mobile.
 *
 * @param props - {@link ShopShellProps}.
 * @returns Khung trang.
 */
export const ShopShell = ({ nav, cartCount = 0, search, children, link: Link = "a" }: ShopShellProps) => {
  return (
    <div className="flex min-h-dvh flex-col">
      <a href="#noi-dung" className={skip}>
        Bỏ qua tới nội dung
      </a>
      <header className="sticky top-0 z-sticky border-b border-border bg-background/85 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-page items-center gap-4 px-4 sm:px-6 lg:px-8">
          <Link href="/" className="shrink-0 rounded-box text-lg">
            <Logo title="Minibox, về trang chủ" />
          </Link>
          <nav aria-label="Điều hướng chính" className="hidden sm:block">
            <ul className="flex items-center gap-1">
              {nav.map((i) => (
                <li key={i.href}>
                  <Link href={i.href} aria-current={i.current ? "page" : undefined} className={navLink}>
                    {i.label}
                  </Link>
                </li>
              ))}
            </ul>
          </nav>
          <div className="ml-auto hidden max-w-sm flex-1 lg:block">{search}</div>
          <Link
            href="/gio-hang"
            aria-label={`Giỏ hàng, ${cartCount} sản phẩm`}
            className="relative ml-auto flex size-11 items-center justify-center rounded-box text-foreground transition-colors hover:bg-surface-sunken lg:ml-0"
          >
            <ShoppingBagIcon aria-hidden className="size-6" />
            {cartCount > 0 && (
              <span aria-hidden className="absolute top-1 right-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-primary px-1 font-mono text-[0.6875rem] font-semibold text-primary-foreground">
                {cartCount}
              </span>
            )}
          </Link>
        </div>
      </header>
      <main id="noi-dung" tabIndex={-1} className="flex-1 outline-none">
        {children}
      </main>
      <footer className="border-t border-border">
        <div className="mx-auto flex max-w-page flex-col gap-2 px-4 py-8 text-sm text-muted sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
          <Logo className="text-base" title="Minibox" />
          <p>Đồ in 3D trong những chiếc hộp nhỏ.</p>
        </div>
      </footer>
      <BottomNav label="Điều hướng chính" items={nav} link={Link} />
    </div>
  );
};

/** Thuộc tính của {@link AdminShell}. */
export type AdminShellProps = {
  /** Mục điều hướng quản trị: sidebar từ laptop, hàng link trên tablet, điều hướng dưới (5 mục đầu) trên mobile. */
  nav: BottomNavItem[];
  /** Khu người dùng (tên, đăng xuất) ở cuối sidebar và trên thanh đầu. */
  user?: ReactNode;
  /** Nội dung trang. */
  children: ReactNode;
  /** Component link (vd. `next/link`); mặc định `<a>`. */
  link?: ElementType;
};

/**
 * Khung trang admin cho tác nghiệp: mật độ compact, sidebar từ laptop, thanh đầu + điều hướng dưới trên mobile.
 *
 * @param props - {@link AdminShellProps}.
 * @returns Khung trang.
 */
export const AdminShell = ({ nav, user, children, link: Link = "a" }: AdminShellProps) => {
  const links = (vertical: boolean) => (
    <ul className={cx("flex gap-1", vertical ? "flex-col" : "items-center")}>
      {nav.map((i) => (
        <li key={i.href}>
          <Link href={i.href} aria-current={i.current ? "page" : undefined} className={cx(navLink, vertical && "aria-[current=page]:bg-accent aria-[current=page]:text-accent-foreground")}>
            {i.icon && <span aria-hidden>{i.icon}</span>}
            {i.label}
          </Link>
        </li>
      ))}
    </ul>
  );
  return (
    <div data-density="compact" className="flex min-h-dvh">
      <a href="#noi-dung" className={skip}>
        Bỏ qua tới nội dung
      </a>
      <aside className="sticky top-0 hidden h-dvh w-60 shrink-0 flex-col gap-6 border-r border-border bg-surface px-3 py-4 lg:flex">
        <Link href="/" className="px-3 text-lg">
          <Logo title="Minibox quản trị, về bảng điều khiển" />
        </Link>
        <nav aria-label="Điều hướng quản trị" className="flex-1">
          {links(true)}
        </nav>
        {user && <div className="border-t border-border px-3 pt-4 text-sm">{user}</div>}
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-sticky flex h-14 items-center gap-4 border-b border-border bg-background/85 px-4 backdrop-blur-md sm:px-6 lg:hidden">
          <Link href="/" className="text-base">
            <Logo title="Minibox quản trị, về bảng điều khiển" />
          </Link>
          <nav aria-label="Điều hướng quản trị" className="hidden overflow-x-auto sm:block">
            {links(false)}
          </nav>
          {user && <div className="ml-auto text-sm">{user}</div>}
        </header>
        <main id="noi-dung" tabIndex={-1} className="flex-1 outline-none">
          {children}
        </main>
      </div>
      <BottomNav label="Điều hướng quản trị" items={nav.slice(0, 5)} link={Link} />
    </div>
  );
};

/** Thuộc tính của {@link PageHeader}. */
export type PageHeaderProps = {
  /** Tiêu đề trang (h1 duy nhất). */
  title: ReactNode;
  /** Mô tả một câu. */
  description?: ReactNode;
  /** Đường dẫn phía trên tiêu đề. */
  breadcrumb?: BreadcrumbProps["items"];
  /** Một hành động chính (nút primary). */
  action?: ReactNode;
};

/**
 * Đầu trang: đường dẫn, tiêu đề h1, mô tả và một hành động chính (xuống dòng dưới tiêu đề trên mobile).
 *
 * @param props - {@link PageHeaderProps}.
 * @returns Khối đầu trang.
 */
export const PageHeader = ({ title, description, breadcrumb, action }: PageHeaderProps) => {
  return (
    <div className="flex flex-col gap-3">
      {breadcrumb && <Breadcrumb items={breadcrumb} />}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-balance sm:text-3xl">{title}</h1>
          {description && <p className="max-w-prose text-muted">{description}</p>}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </div>
    </div>
  );
};
