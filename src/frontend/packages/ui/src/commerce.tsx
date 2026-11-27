/*
 * Thành phần thương mại (spec #448 §5, §6): giá VND, khung ảnh sản phẩm trên bục trưng bày, tem hộp thông số in 3D.
 */
import { cx } from "./field";

const vnd = new Intl.NumberFormat("vi-VN");

/**
 * Định dạng số tiền VND kiểu Việt Nam: dấu chấm phân nhóm, ký hiệu ₫ phía sau.
 *
 * @param n - Số tiền (đồng).
 * @returns Chuỗi như "14.500 ₫".
 */
export const formatVnd = (n: number) => `${vnd.format(n)} ₫`;

const priceSizes = { sm: "text-sm", md: "text-lg", lg: "text-3xl" };

/** Thuộc tính của {@link Price}. */
export type PriceProps = {
  /** Giá bán hiện tại (đồng). */
  value: number;
  /** Giá cũ trước giảm, hiện gạch ngang. */
  compareAt?: number;
  /** Tiền cọc cho hàng in theo mẫu, hiện dòng riêng. */
  deposit?: number;
  /** Cỡ chữ giá; mặc định `md`. */
  size?: keyof typeof priceSizes;
};

/**
 * Giá sản phẩm: giá bán đậm bằng font tiêu đề, chữ số đều; giá cũ gạch ngang (có nhãn cho trình đọc màn hình); cọc nếu có.
 *
 * @param props - {@link PriceProps}.
 * @returns Khối giá.
 */
export const Price = ({ value, compareAt, deposit, size = "md" }: PriceProps) => {
  return (
    <div className="flex flex-col">
      <div className="flex flex-wrap items-baseline gap-x-2">
        <span className={cx("font-display font-semibold tracking-tight tabular-nums", priceSizes[size])}>{formatVnd(value)}</span>
        {compareAt !== undefined && compareAt > value && (
          <s className="text-sm text-muted tabular-nums">
            <span className="sr-only">Giá cũ </span>
            {formatVnd(compareAt)}
          </s>
        )}
      </div>
      {deposit !== undefined && <span className="text-xs text-muted tabular-nums">Cọc {formatVnd(deposit)}</span>}
    </div>
  );
};

/** Thuộc tính của {@link ProductMedia}. */
export type ProductMediaProps = {
  /** Ảnh sản phẩm (nên nền trong suốt hoặc nền liền); thiếu thì hiện khối thay thế. */
  src?: string;
  /** Mô tả ảnh (tên sản phẩm + biến thể). */
  alt: string;
  /** Tỉ lệ khung: `square` 1:1 (mặc định, lưới sản phẩm) hoặc `portrait` 4:5 (trang chi tiết). */
  aspect?: "square" | "portrait";
  /** Rê chuột thì sản phẩm nhấc nhẹ khỏi bục (thẻ bấm được). */
  interactive?: boolean;
  /** Class bổ sung cho khung. */
  className?: string;
};

/**
 * Khung ảnh sản phẩm như tủ trưng bày: góc vát, nền sáng dần xuống mặt sàn, sản phẩm đứng trên bục có bóng tiếp xúc.
 * Mọi sản phẩm nhìn đồng bộ dù ảnh chụp khác nhau. Chưa có ảnh thì hiện khối isometric thay thế.
 *
 * @param props - {@link ProductMediaProps}.
 * @returns Khung ảnh.
 */
export const ProductMedia = ({ src, alt, aspect = "square", interactive, className }: ProductMediaProps) => {
  return (
    <div
      className={cx(
        "group/media chamfer relative isolate flex items-end justify-center overflow-hidden rounded-lg border border-border",
        "bg-[radial-gradient(120%_80%_at_50%_0%,var(--color-surface-raised)_0%,var(--color-surface)_55%,var(--color-surface-sunken)_100%)]",
        aspect === "square" ? "aspect-square" : "aspect-[4/5]",
        className,
      )}
    >
      {/* Mặt sàn của bục + bóng tiếp xúc. */}
      <span aria-hidden className="absolute inset-x-0 bottom-0 -z-10 h-[22%] border-t border-border/70 bg-surface-sunken/70" />
      <span
        aria-hidden
        className={cx(
          "absolute bottom-[15%] -z-10 h-[6%] w-[46%] rounded-[50%] bg-[color-mix(in_oklab,var(--shade)_30%,transparent)] blur-[6px] transition-transform duration-300 ease-out-box",
          interactive && "group-hover/media:scale-x-90 group-hover/media:opacity-80",
        )}
      />
      <div className={cx("mb-[17%] flex h-[66%] w-[66%] items-end justify-center transition-transform duration-300 ease-out-box", interactive && "group-hover/media:-translate-y-[4%]")}>
        {src ? (
          // eslint-disable-next-line @next/next/no-img-element -- package UI không phụ thuộc next/image; app bọc ảnh tối ưu khi cần.
          <img src={src} alt={alt} loading="lazy" className="max-h-full max-w-full object-contain" />
        ) : (
          <svg role="img" aria-label={alt} viewBox="0 0 32 34" className="h-3/4 w-auto text-border-strong">
            <polygon points="16,4 28,11 16,18 4,11" className="fill-surface-raised stroke-current" strokeWidth={0.75} />
            <polygon points="4,11 16,18 16,30 4,23" className="fill-surface-sunken stroke-current" strokeWidth={0.75} />
            <polygon points="28,11 16,18 16,30 28,23" className="fill-surface stroke-current" strokeWidth={0.75} />
          </svg>
        )}
      </div>
    </div>
  );
};

/** Thuộc tính của {@link SpecLabel}. */
export type SpecLabelProps = {
  /** Cặp thông số: vật liệu, kích thước (dùng ×), độ dày lớp in, SKU. */
  items: { label: string; value: string }[];
  /** Class bổ sung. */
  className?: string;
};

/**
 * Tem hộp: thông số in 3D trình bày như tem dán thùng hàng (font mono, viền nét đứt, góc vát).
 *
 * @param props - {@link SpecLabelProps}.
 * @returns Danh sách mô tả thông số.
 */
export const SpecLabel = ({ items, className }: SpecLabelProps) => {
  return (
    <dl className={cx("chamfer-sm grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 rounded-sm border border-dashed border-border-strong bg-surface p-3 font-mono text-xs", className)}>
      {items.map((it) => (
        <div key={it.label} className="contents">
          <dt className="text-muted">{it.label}</dt>
          <dd className="font-medium text-foreground tabular-nums">{it.value}</dd>
        </div>
      ))}
    </dl>
  );
};
