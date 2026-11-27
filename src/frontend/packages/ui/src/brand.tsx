/*
 * Nhận diện Minibox (spec #448 §5): logo là khối lập phương chữ M tự xoay; biểu tượng tải là khối isometric 30° vẽ bằng currentColor
 * (ba mặt độ đậm khác nhau) nên theo màu chữ của nơi đặt (nút, trang).
 */
import { type ComponentProps } from "react";
import { cx } from "./field";

/** Ba mặt (trên, trái, phải) của khối isometric trên lưới 32×32, tâm (16, 17). */
const faces = {
  top: "16,4 28,11 16,18 4,11",
  left: "4,11 16,18 16,30 4,23",
  right: "28,11 16,18 16,30 28,23",
};

/** Sáu mặt của khối chữ M (theme.css `.logo-cube [data-face]`). */
const cubeFaces = ["front", "right", "back", "left", "top", "bottom"];

/** Thuộc tính của {@link Logo}. */
export type LogoProps = {
  /** Kèm chữ "inibox.vn" nối sau khối chữ M (bản đầy đủ đọc "Minibox.vn"); mặc định có. Tắt để còn bản rút gọn: chỉ khối M. */
  withWordmark?: boolean;
  /** Tên đọc cho trình đọc màn hình; mặc định "Minibox" (đổi khi logo là link, vd. "Về trang chủ Minibox"). */
  title?: string;
  /** Class cho khung ngoài (cỡ chữ quyết định cỡ logo: khối cạnh 1.15em). Đổi màu khối bằng biến `--cube-face`, `--cube-ink`. */
  className?: string;
};

/**
 * Logo Minibox: khối lập phương 3D có chữ M trên bốn mặt bên, vân lớp in và mặt trên lưới infill như vật vừa in 3D xong.
 * Khi tải, khối được in dựng từ dưới lên theo từng lớp, rồi tự xoay từng nấc 90° như tay xoay hộp (theme.css `.logo-cube`).
 * Bản đầy đủ nối chữ "inibox.vn" ngay sau khối để đọc thành "Minibox.vn". Đứng yên ở mặt trước khi người dùng bật giảm chuyển động.
 *
 * @param props - {@link LogoProps}.
 * @returns Khung logo có vai trò ảnh và tên đọc; khối và chữ bên trong ẩn với trình đọc màn hình.
 */
export const Logo = ({ withWordmark = true, title = "Minibox", className }: LogoProps) => {
  return (
    <span role="img" aria-label={title} className={cx("inline-flex items-center gap-[0.08em] text-foreground", className)}>
      <span aria-hidden className="logo-cube">
        <span>
          {cubeFaces.map((f) => (
            <i key={f} data-face={f} />
          ))}
        </span>
      </span>
      {withWordmark && (
        <span aria-hidden className="font-display text-[1.125em] font-bold leading-none tracking-tight">
          inibox<span className="text-primary">.vn</span>
        </span>
      )}
    </span>
  );
};

/**
 * Hiệu ứng thêm vào giỏ: một hộp Minibox (khối chữ M) bật lên từ `source`, bay theo đường vòng vào phần tử `[data-cart-target]`
 * (nút giỏ trên header, {@link ShopShell} đã gắn sẵn), rồi giỏ nảy nhẹ. Chỉ để trang trí: không chặn thao tác, bỏ qua khi người dùng
 * bật giảm chuyển động hoặc trang không có giỏ. Gọi sau khi thêm giỏ thành công; cập nhật số trong giỏ khi promise xong.
 *
 * @param source - Phần tử hộp bay ra (nút "Thêm vào giỏ" hoặc ảnh sản phẩm).
 * @returns Promise xong khi hộp đã vào giỏ, hoặc ngay lập tức nếu bỏ qua hiệu ứng.
 */
export const flyToCart = async (source: Element) => {
  const target = document.querySelector("[data-cart-target]");
  const reduced = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
  if (!target || reduced || typeof source.animate !== "function") return;

  const size = 32;
  const from = source.getBoundingClientRect();
  const to = target.getBoundingClientRect();
  const x0 = from.left + from.width / 2 - size / 2;
  const y0 = from.top + from.height / 2 - size / 2;
  const dx = to.left + to.width / 2 - size / 2 - x0;
  const dy = to.top + to.height / 2 - size / 2 - y0;
  // Đỉnh vòng bay cao hơn điểm cao nhất 110px nhưng không vượt mép trên màn hình (giỏ thường nằm sát đỉnh trên header).
  const peak = Math.max(Math.min(y0, y0 + dy) - 110, 12) - y0;

  // Lớp ngoài chạy ngang đều, lớp giữa lên rồi rơi: hai chuyển động ghép thành đường parabol.
  const outer = document.createElement("span");
  outer.className = "cart-fly";
  outer.setAttribute("aria-hidden", "true");
  Object.assign(outer.style, { left: `${x0}px`, top: `${y0}px`, fontSize: `${size / 1.15}px` });
  outer.innerHTML = `<span><span class="logo-cube"><span>${cubeFaces.map((f) => `<i data-face="${f}"></i>`).join("")}</span></span></span>`;
  document.body.append(outer);

  const duration = 800;
  outer.animate([{ transform: "translateX(0)" }, { transform: `translateX(${dx}px)` }], { duration, easing: "cubic-bezier(0.3, 0, 0.6, 1)" });
  const arc = (outer.firstElementChild as HTMLElement).animate(
    [
      { transform: "translateY(0) scale(0.5)", opacity: 0, easing: "cubic-bezier(0.2, 0.7, 0.4, 1)" },
      { transform: `translateY(${peak}px) scale(1.15)`, opacity: 1, offset: 0.42, easing: "cubic-bezier(0.6, 0, 0.8, 0.4)" },
      { transform: `translateY(${dy}px) scale(0.3)`, opacity: 0.85 },
    ],
    { duration },
  );
  await arc.finished.catch(() => undefined);
  outer.remove();
  target.animate([{ transform: "scale(1)" }, { transform: "scale(1.22) rotate(-6deg)" }, { transform: "scale(0.96)" }, { transform: "scale(1)" }], {
    duration: 420,
    easing: "cubic-bezier(0.32, 0.72, 0, 1)",
  });
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
