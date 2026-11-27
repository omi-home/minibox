/*
 * Nhận diện Minibox: khối lập phương isometric 30° (spec #448 §5). Ba mặt của khối vẽ bằng currentColor với độ đậm khác nhau,
 * nên khối luôn theo màu chữ của nơi đặt (nút, trang).
 */
import { type ComponentProps } from "react";
import { cx } from "./field";

/** Ba mặt (trên, trái, phải) của khối isometric trên lưới 32×32, tâm (16, 17). */
const faces = {
  top: "16,4 28,11 16,18 4,11",
  left: "4,11 16,18 16,30 4,23",
  right: "28,11 16,18 16,30 28,23",
};

/** Thuộc tính của {@link Logo}. */
export type LogoProps = {
  /** Kèm chữ "minibox" bên phải mark; mặc định có. */
  withWordmark?: boolean;
  /** Tên đọc cho trình đọc màn hình; mặc định "Minibox" (đổi khi logo là link, vd. "Về trang chủ Minibox"). */
  title?: string;
  /** Class cho khung ngoài (cỡ chữ quyết định cỡ logo: mark cao 1.75em). */
  className?: string;
};

/**
 * Logo Minibox: mark khối lập phương isometric + chữ thương hiệu bằng font tiêu đề của phương án.
 * Mặt trên của khối rơi xuống lắp vào khi tải trang (một lần). PHA 1 #448: vẽ cả 3 biến thể mark, theme.css chỉ hiện biến thể
 * của `data-brand` đang chọn (A khối đặc 3 sắc độ, B hộp mở nắp nét đều, C khung cạnh có mặt trên sáng).
 *
 * @param props - {@link LogoProps}.
 * @returns Khung logo gồm SVG có tên và chữ thương hiệu (ẩn với trình đọc màn hình vì SVG đã có tên).
 */
export const Logo = ({ withWordmark = true, title = "Minibox", className }: LogoProps) => {
  return (
    <span className={cx("inline-flex items-center gap-[0.4em] text-foreground", className)}>
      <svg role="img" viewBox="0 -3 32 37" className="h-[1.75em] w-auto shrink-0 overflow-visible">
        <title>{title}</title>
        <g className="logo-a">
          <polygon points={faces.left} className="fill-primary-hover" />
          <polygon points={faces.right} className="fill-primary" />
          <polygon points={faces.top} className="animate-assemble fill-[color-mix(in_oklab,var(--color-primary)_55%,var(--color-white))]" />
        </g>
        <g className="logo-b fill-none stroke-current" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round">
          <path d="M4 13 16 20 28 13 28 25 16 32 4 25Z M16 20V32" className="fill-accent" />
          <path d="M4 13 1 6 13 -1 16 6 M28 13 31 6 19 -1 16 6" className="animate-assemble stroke-primary" />
        </g>
        <g className="logo-c fill-none stroke-current" strokeWidth={1.75} strokeLinejoin="round">
          <polygon points={faces.top} className="animate-assemble fill-primary stroke-primary" />
          <path d="M4 11V23L16 30 28 23V11 M16 18V30" />
        </g>
      </svg>
      {withWordmark && (
        <span aria-hidden className="font-display text-[1.125em] font-bold leading-none tracking-tight">
          minibox
        </span>
      )}
    </span>
  );
};

const sizes = { sm: "size-4", md: "size-8", lg: "size-14" };

/** Thuộc tính của {@link CubeLoader}. */
export type CubeLoaderProps = {
  /** Cỡ: `sm` trong nút, `md` trong khối nội dung (mặc định), `lg` cả trang. */
  size?: keyof typeof sizes;
  /** Nhãn cho trình đọc màn hình; mặc định "Đang tải". Truyền `null` khi đặt trong phần tử đã báo bận (nút `aria-busy`). */
  label?: string | null;
  /** Class bổ sung (màu theo `text-*`). */
  className?: string;
};

/**
 * Khối lập phương quay từng nấc 120° (các mặt đổi chỗ như lật hộp), thay cho vòng quay tròn.
 * Đứng yên khi người dùng bật giảm chuyển động.
 *
 * @param props - {@link CubeLoaderProps}.
 * @returns Biểu tượng tải, có `role="status"` khi có nhãn.
 */
export const CubeLoader = ({ size = "md", label = "Đang tải", className }: CubeLoaderProps) => {
  const a11y: ComponentProps<"svg"> = label === null ? { "aria-hidden": true } : { role: "status", "aria-label": label };
  return (
    <svg viewBox="0 0 32 34" className={cx(sizes[size], "shrink-0", className)} {...a11y}>
      <g className="origin-[16px_17px] animate-cube-spin">
        <polygon points={faces.top} fill="currentColor" />
        <polygon points={faces.left} fill="currentColor" fillOpacity={0.45} />
        <polygon points={faces.right} fill="currentColor" fillOpacity={0.7} />
      </g>
    </svg>
  );
};
