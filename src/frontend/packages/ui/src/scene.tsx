/*
 * Cảnh nền "Rừng sâu" (spec #448 §5): đồi xa và ba hàng thông. Buổi sáng khi giao diện sáng (sương sớm, mặt trời, mây),
 * ban đêm khi giao diện tối (trăng, sao, đom đóm): mọi màu là biến --scene-* khai light-dark() trong theme.css.
 * Lớp gần cùng tông sáng với nền (sương sớm / đêm sâu) để chữ đặt trên nền vẫn đọc được.
 */
import type { CSSProperties } from "react";

/** Số giả ngẫu nhiên cố định theo hạt giống: cảnh giống hệt nhau giữa server và client (không lệch khi hydrate). */
const seeded = (seed: number) => () => {
  seed = (seed * 16807) % 2147483647;
  return seed / 2147483647;
};

const W = 1440;
const H = 900;

/** Độ trễ animation để các đốm không nhấp nháy cùng lúc. */
const delay = (s: number): CSSProperties => ({ animationDelay: `${s.toFixed(2)}s` });

/** Trời: dải màu từ nền trang (trên, nơi có chữ) tới màu chân trời; mặt trời/trăng, sao và mây trôi. */
const Sky = ({ id, seed }: { id: string; seed: number }) => {
  const r = seeded(seed);
  const stars = Array.from({ length: 70 }, () => ({ x: r() * W, y: r() * 520, s: 0.6 + r() * 1.6, d: r() * 4 }));
  return (
    <>
      <defs>
        <linearGradient id={`sky-${id}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0.15" style={{ stopColor: "var(--color-background)" }} />
          <stop offset="0.75" style={{ stopColor: "var(--scene-sky)" }} />
        </linearGradient>
        <radialGradient id={`halo-${id}`}>
          <stop offset="0" style={{ stopColor: "var(--scene-halo)" }} />
          <stop offset="1" style={{ stopColor: "var(--scene-halo)", stopOpacity: 0 }} />
        </radialGradient>
      </defs>
      <rect width={W} height={H} fill={`url(#sky-${id})`} />
      <g fill="var(--scene-star)">
        {stars.map((s, i) => (
          <circle key={i} cx={s.x} cy={s.y} r={s.s} className="scene-twinkle" style={delay(s.d)} />
        ))}
      </g>
      <circle cx="1130" cy="300" r="170" fill={`url(#halo-${id})`} />
      <circle cx="1130" cy="300" r="52" fill="var(--scene-sun)" />
      <path d="M1131.9 260 A40 40 0 1 0 1164.9 319.5 A34 34 0 1 1 1131.9 260Z" fill="var(--scene-moon)" />
      <g fill="var(--scene-cloud)" className="scene-drift">
        <ellipse cx="260" cy="250" rx="120" ry="22" />
        <ellipse cx="320" cy="232" rx="70" ry="22" />
        <ellipse cx="820" cy="190" rx="150" ry="18" />
        <ellipse cx="760" cy="176" rx="60" ry="18" />
        <ellipse cx="1300" cy="420" rx="110" ry="16" />
      </g>
    </>
  );
};

/** Một cây thông ba tầng tán, gốc tại (x, base), cao h. */
const pine = (x: number, base: number, h: number) => {
  const w = h * 0.42;
  const p = (dx: number, dy: number) => `${(x + dx).toFixed(1)},${(base - dy).toFixed(1)}`;
  return [p(0, h), p(w * 0.45, h * 0.62), p(w * 0.25, h * 0.62), p(w * 0.55, h * 0.3), p(w * 0.32, h * 0.3), p(w * 0.62, 0), p(-w * 0.62, 0), p(-w * 0.32, h * 0.3), p(-w * 0.55, h * 0.3), p(-w * 0.25, h * 0.62), p(-w * 0.45, h * 0.62)].join(" ");
};

/** Một hàng thông phủ kín bề ngang, kèm dải đất liền dưới gốc. */
const PineRow = ({ base, min, max, gap, fill, seed }: { base: number; min: number; max: number; gap: number; fill: string; seed: number }) => {
  const r = seeded(seed);
  const trees: string[] = [];
  for (let x = -gap / 2; x < W + gap; x += gap * (0.7 + r() * 0.6)) trees.push(pine(x, base, min + r() * (max - min)));
  return (
    <g fill={fill}>
      <rect x="0" y={base - 4} width={W} height={H - base + 4} />
      {trees.map((t, i) => (
        <polygon key={i} points={t} />
      ))}
    </g>
  );
};

/** Rừng: đồi xa và ba hàng thông; ban đêm có đom đóm bay lơ lửng. */
const Forest = () => {
  const r = seeded(29);
  const flies = Array.from({ length: 22 }, () => ({ x: r() * W, y: 640 + r() * 220, d: r() * 6 }));
  return (
    <g>
      <Sky id="b" seed={23} />
      <path fill="var(--scene-far)" d="M0 650 Q360 570 720 630 T1440 610 V900 H0Z" />
      <PineRow base={700} min={50} max={100} gap={26} fill="var(--scene-far)" seed={3} />
      <PineRow base={790} min={90} max={150} gap={44} fill="var(--scene-mid)" seed={5} />
      <PineRow base={905} min={150} max={240} gap={80} fill="var(--scene-near)" seed={7} />
      <g fill="var(--scene-glow)">
        {flies.map((f, i) => (
          <circle key={i} cx={f.x} cy={f.y} r="2.4" className="scene-float" style={delay(f.d)} />
        ))}
      </g>
    </g>
  );
};

/**
 * Cảnh nền rừng cố định sau nội dung trang, đổi ngày/đêm theo chế độ sáng/tối. Dùng cho trang shop có nền trống (trang chủ, /design);
 * không dùng trong admin (mật độ cao, cần nền phẳng).
 * Có chuyển động nhẹ (mây trôi, sao nhấp nháy, đom đóm bay); đứng yên khi người dùng bật giảm chuyển động.
 *
 * @returns SVG trang trí phủ toàn màn hình, ẩn với trình đọc màn hình.
 */
export const BrandScene = () => (
  <svg aria-hidden viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="xMidYMax slice" className="pointer-events-none fixed inset-0 -z-10 size-full">
    <Forest />
  </svg>
);
