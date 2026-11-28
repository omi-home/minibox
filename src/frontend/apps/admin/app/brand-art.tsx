// Panel thương hiệu trang đăng nhập (#448): "hệ hộp" quay quanh một hộp lớn như hành tinh quanh mặt trời, trên mảng bg-brand.
// Hình học và chuyển động ở globals.css (.orbit-*); hộp là SVG isometric màu theo currentColor.
import type { CSSProperties } from "react";

const K = Math.sqrt(3) / 2;

/**
 * Hộp isometric cạnh `size` px (ba mặt độ đậm khác nhau), có thể dán băng keo; tâm hộp ở gốc toạ độ của phần tử cha.
 * `solid` cho hộp tâm: mặt gần như đặc, sáng như mặt trời của hệ.
 */
const Crate = ({ size, tape, solid }: { size: number; tape?: boolean; solid?: boolean }) => {
  const [l, r, tp] = solid ? [0.72, 0.5, 0.95] : [0.22, 0.1, 0.32];
  const s = 10;
  const top = `0,${-s} ${K * s},${-s / 2} 0,0 ${-K * s},${-s / 2}`;
  const left = `${-K * s},${-s / 2} 0,0 0,${s} ${-K * s},${s / 2}`;
  const right = `${K * s},${-s / 2} 0,0 0,${s} ${K * s},${s / 2}`;
  return (
    <svg
      viewBox={`${-K * s - 1} ${-s - 1} ${2 * K * s + 2} ${2 * s + 2}`}
      width={size}
      height={size}
      className="absolute -translate-1/2 overflow-visible"
      strokeLinejoin="round"
      strokeWidth={0.4}
    >
      <polygon points={left} fill="currentColor" fillOpacity={l} stroke="currentColor" strokeOpacity={0.5} />
      <polygon points={right} fill="currentColor" fillOpacity={r} stroke="currentColor" strokeOpacity={0.5} />
      <polygon points={top} fill="currentColor" fillOpacity={tp} stroke="currentColor" strokeOpacity={0.6} />
      {tape && (
        <polyline
          points={`${(-K * s) / 2},${(-s * 3) / 4} ${(K * s) / 2},${-s / 4} ${(K * s) / 2},${(s * 3) / 4}`}
          fill="none"
          stroke={solid ? "var(--color-brand)" : "currentColor"}
          strokeOpacity={solid ? 0.35 : 0.55}
          strokeWidth={1.4}
        />
      )}
    </svg>
  );
};

/** Ba quỹ đạo: bán kính (px), chu kỳ một vòng; mỗi hộp có cỡ và pha riêng (tỉ lệ vòng đã đi) để không chạy dồn cục. */
const orbits = [
  { r: 96, period: 28, crates: [{ size: 34, at: 0.1, tape: true }] },
  {
    r: 148,
    period: 46,
    crates: [
      { size: 46, at: 0.35 },
      { size: 28, at: 0.85, tape: true },
    ],
  },
  {
    r: 200,
    period: 70,
    crates: [
      { size: 56, at: 0.6, tape: true },
      { size: 30, at: 0.05 },
      { size: 22, at: 0.3 },
    ],
  },
];

const vars = (v: Record<string, string>) => v as CSSProperties;

/**
 * Nền panel thương hiệu: mảng xanh rừng `bg-brand` có quầng sáng và lưới chấm mờ; thuần trang trí (ẩn với trình đọc màn hình).
 *
 * @returns Lớp nền tuyệt đối phủ panel.
 */
export const BrandArt = () => (
  <div aria-hidden className="pointer-events-none absolute inset-0">
    <div className="absolute inset-0 bg-brand bg-[radial-gradient(70%_55%_at_50%_38%,var(--color-brand-active)_0%,transparent_70%)]" />
    <div className="absolute inset-0 bg-[radial-gradient(currentColor_1px,transparent_1.5px)] bg-size-[22px_22px] opacity-[0.08] [mask-image:radial-gradient(70%_60%_at_50%_40%,black,transparent)]" />
  </div>
);

/**
 * Hệ hộp quay quanh một hộp lớn ở giữa, chiếm phần trống giữa logo và chữ của panel; thuần trang trí. Đứng yên khi người dùng
 * bật giảm chuyển động.
 *
 * @returns Sân khấu hệ hộp (giãn theo chỗ trống, tối thiểu đủ cho quỹ đạo ngoài cùng).
 */
export const OrbitStage = () => (
  <div aria-hidden className="pointer-events-none relative min-h-48 flex-1 scale-[0.85] xl:scale-100 2xl:scale-115">
    <div className="orbit-system">
      {orbits.map((o) => (
        <div key={o.r} className="orbit-ring" style={vars({ "--r": `${o.r}px` })} />
      ))}
      <div className="orbit-upright">
        {/* Quầng sáng sau khối tâm, như mặt trời. */}
        <div className="absolute size-56 -translate-1/2 rounded-full bg-[radial-gradient(closest-side,color-mix(in_oklab,var(--color-brand-foreground)_22%,transparent),transparent)]" />
        <div className="orbit-float">
          <Crate size={112} tape solid />
        </div>
      </div>
      {orbits.flatMap((o) =>
        o.crates.map((c) => {
          const style = vars({
            "--r": `${o.r}px`,
            "--period": `${o.period}s`,
            "--phase": `${(-c.at * o.period).toFixed(1)}s`,
          });
          return (
            <div key={`${o.r}-${c.at}`} className="orbit-arm" style={style}>
              <div className="orbit-body">
                <Crate size={c.size} tape={c.tape} />
              </div>
            </div>
          );
        }),
      )}
    </div>
  </div>
);
