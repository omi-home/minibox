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
