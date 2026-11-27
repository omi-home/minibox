"use client";
/*
 * Điều hướng (spec #448 §6): tab, đường dẫn, phân trang, các bước. Mục hiện tại luôn có dấu hiệu ngoài màu (vạch, đậm, aria-current).
 */
import { CaretLeftIcon, CaretRightIcon, CheckIcon } from "@phosphor-icons/react";
import { Tabs as RTabs } from "radix-ui";
import { Fragment, type ElementType, type ReactNode } from "react";
import { cx } from "./field";

/** Một tab của {@link Tabs}. */
export type TabItem = {
  /** Khoá ổn định. */
  value: string;
  /** Nhãn tab. */
  label: string;
  /** Số mục (vd. số đơn chờ), hiện cạnh nhãn. */
  count?: number;
  /** Nội dung khi tab được chọn. */
  content: ReactNode;
};

/** Thuộc tính của {@link Tabs}. */
export type TabsProps = {
  /** Nhãn của nhóm tab cho trình đọc màn hình. */
  label: string;
  /** Các tab. */
  items: TabItem[];
  /** Tab mở sẵn; mặc định tab đầu. */
  defaultValue?: string;
};

/**
 * Tab chuyển nội dung cùng cấp (trạng thái đơn, mô tả/thông số sản phẩm). Hàng tab cuộn ngang trên mobile; tab chọn có vạch dưới.
 *
 * @param props - {@link TabsProps}.
 * @returns Nhóm tab và nội dung.
 */
export const Tabs = ({ label, items, defaultValue }: TabsProps) => {
  return (
    <RTabs.Root defaultValue={defaultValue ?? items[0]?.value} className="flex flex-col gap-4">
      <RTabs.List aria-label={label} className="-mx-4 flex gap-1 overflow-x-auto border-b border-border px-4 sm:mx-0 sm:px-0">
        {items.map((t) => (
          <RTabs.Trigger
            key={t.value}
            value={t.value}
            className="relative flex h-11 shrink-0 items-center gap-2 px-3 text-sm font-medium text-muted transition-colors after:absolute after:inset-x-2 after:-bottom-px after:h-0.5 after:scale-x-0 after:bg-primary after:transition-transform hover:text-foreground data-[state=active]:text-foreground data-[state=active]:after:scale-x-100"
          >
            {t.label}
            {t.count !== undefined && <span className="rounded-sm bg-surface-sunken px-1.5 font-mono text-xs tabular-nums">{t.count}</span>}
          </RTabs.Trigger>
        ))}
      </RTabs.List>
      {items.map((t) => (
        <RTabs.Content key={t.value} value={t.value} className="outline-none">
          {t.content}
        </RTabs.Content>
      ))}
    </RTabs.Root>
  );
};

/** Thuộc tính của {@link Breadcrumb}. */
export type BreadcrumbProps = {
  /** Từ gốc tới trang hiện tại; mục cuối không có `href`. */
  items: { label: string; href?: string }[];
  /** Component link (vd. `next/link`); mặc định `<a>`. */
  link?: ElementType;
};

/**
 * Đường dẫn cho trang sâu từ 3 cấp. Mobile chỉ hiện mục cha gần nhất kèm mũi tên lùi để tiết kiệm chỗ.
 *
 * @param props - {@link BreadcrumbProps}.
 * @returns Vùng điều hướng đường dẫn.
 */
export const Breadcrumb = ({ items, link: Link = "a" }: BreadcrumbProps) => {
  const parent = items.at(-2);
  return (
    <nav aria-label="Đường dẫn" className="text-sm">
      {parent?.href && (
        <Link href={parent.href} className="inline-flex h-8 items-center gap-1 text-muted hover:text-foreground sm:hidden">
          <CaretLeftIcon aria-hidden className="size-4" />
          {parent.label}
        </Link>
      )}
      <ol className="hidden flex-wrap items-center gap-1.5 text-muted sm:flex">
        {items.map((it, i) => (
          <Fragment key={it.label}>
            {i > 0 && (
              <li aria-hidden className="text-border-strong">
                /
              </li>
            )}
            <li>
              {i === items.length - 1 || !it.href ? (
                <span aria-current={i === items.length - 1 ? "page" : undefined} className="font-medium text-foreground">
                  {it.label}
                </span>
              ) : (
                <Link href={it.href} className="hover:text-foreground hover:underline">
                  {it.label}
                </Link>
              )}
            </li>
          </Fragment>
        ))}
      </ol>
    </nav>
  );
};

/**
 * Dải số trang rút gọn: luôn có trang đầu, trang cuối và hai trang kề trang hiện tại; chỗ bị lược là "…".
 *
 * @param page - Trang hiện tại (từ 1).
 * @param count - Tổng số trang.
 * @returns Danh sách số trang và dấu lược.
 */
export const pageRange = (page: number, count: number): (number | "…")[] => {
  const keep = new Set([1, count, page - 1, page, page + 1].filter((p) => p >= 1 && p <= count));
  const out: (number | "…")[] = [];
  let prev = 0;
  for (const p of [...keep].sort((a, b) => a - b)) {
    if (p - prev > 2) out.push("…");
    else if (p - prev === 2) out.push(p - 1);
    out.push(p);
    prev = p;
  }
  return out;
};

/** Thuộc tính của {@link Pagination}. */
export type PaginationProps = {
  /** Trang hiện tại (từ 1). */
  page: number;
  /** Tổng số trang. */
  pageCount: number;
  /** Gọi với trang mới. */
  onPageChange: (page: number) => void;
  /** Nhãn vùng phân trang; mặc định "Phân trang". */
  label?: string;
};

const pageBtn = "flex h-(--control-h) min-w-(--control-h) items-center justify-center rounded-box px-2 text-sm font-medium tabular-nums transition-colors hover:bg-surface-sunken disabled:pointer-events-none disabled:opacity-40";

/**
 * Phân trang cho danh sách dài (admin). Mobile chỉ hiện trước/sau và "Trang i/n".
 *
 * @param props - {@link PaginationProps}.
 * @returns Vùng điều hướng phân trang.
 */
export const Pagination = ({ page, pageCount, onPageChange, label = "Phân trang" }: PaginationProps) => {
  return (
    <nav aria-label={label} className="flex items-center justify-between gap-2 sm:justify-start">
      <button type="button" aria-label="Trang trước" className={pageBtn} disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        <CaretLeftIcon aria-hidden className="size-4" />
      </button>
      <span className="text-sm text-muted tabular-nums sm:hidden">
        Trang {page}/{pageCount}
      </span>
      <ul className="hidden items-center gap-1 sm:flex">
        {pageRange(page, pageCount).map((p, i) => (
          <li key={`${p}-${i}`}>
            {p === "…" ? (
              <span aria-hidden className="px-1 text-muted">
                …
              </span>
            ) : (
              <button
                type="button"
                aria-label={`Trang ${p}`}
                aria-current={p === page ? "page" : undefined}
                className={cx(pageBtn, "aria-[current=page]:bg-foreground aria-[current=page]:text-background")}
                onClick={() => onPageChange(p)}
              >
                {p}
              </button>
            )}
          </li>
        ))}
      </ul>
      <button type="button" aria-label="Trang sau" className={pageBtn} disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>
        <CaretRightIcon aria-hidden className="size-4" />
      </button>
    </nav>
  );
};

/** Thuộc tính của {@link Stepper}. */
export type StepperProps = {
  /** Tên các bước theo thứ tự (động từ hoặc danh từ ngắn). */
  steps: string[];
  /** Chỉ số bước hiện tại (từ 0). */
  current: number;
};

/**
 * Các bước của luồng nhiều bước (checkout, thiết lập MFA). Bước xong có dấu check; mobile hiện "Bước i/n" và tên bước.
 *
 * @param props - {@link StepperProps}.
 * @returns Danh sách bước có thứ tự.
 */
export const Stepper = ({ steps, current }: StepperProps) => {
  return (
    <div className="flex flex-col gap-2">
      <p className="text-sm text-muted sm:hidden">
        Bước {current + 1}/{steps.length}: <span className="font-medium text-foreground">{steps[current]}</span>
      </p>
      <ol className="flex items-center gap-2">
        {steps.map((s, i) => {
          const done = i < current;
          return (
            <li key={s} aria-current={i === current ? "step" : undefined} className="flex flex-1 items-center gap-2 last:flex-none">
              <span
                className={cx(
                  "chamfer-sm flex size-7 shrink-0 items-center justify-center rounded-sm border font-mono text-xs font-semibold",
                  done && "border-primary bg-primary text-primary-foreground",
                  i === current && "border-primary text-primary",
                  i > current && "border-border-strong text-muted",
                )}
              >
                {done ? <CheckIcon aria-hidden weight="bold" className="size-3.5" /> : i + 1}
              </span>
              <span className={cx("sr-only text-sm sm:not-sr-only", i === current ? "font-semibold" : "text-muted")}>
                {s}
                {done && <span className="sr-only"> (xong)</span>}
              </span>
              {i < steps.length - 1 && <span aria-hidden className={cx("h-px flex-1", done ? "bg-primary" : "bg-border")} />}
            </li>
          );
        })}
      </ol>
    </div>
  );
};
