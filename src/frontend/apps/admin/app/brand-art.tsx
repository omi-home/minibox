// Minh họa panel thương hiệu (#448): khối hộp isometric xếp chồng như hộp Minibox, SVG thuần màu theo currentColor trên mảng bg-brand.

const K = Math.sqrt(3) / 2;

/** Một khối hộp isometric tâm mặt trên (x, y), cạnh s; ba mặt sáng/tối khác nhau tạo khối. */
const Cube = ({ x, y, s, tape }: { x: number; y: number; s: number; tape?: boolean }) => {
  const top = `${x},${y - s / 2} ${x + K * s},${y} ${x},${y + s / 2} ${x - K * s},${y}`;
  const left = `${x - K * s},${y} ${x},${y + s / 2} ${x},${y + 1.5 * s} ${x - K * s},${y + s}`;
  const right = `${x + K * s},${y} ${x},${y + s / 2} ${x},${y + 1.5 * s} ${x + K * s},${y + s}`;
  return (
    <g strokeLinejoin="round" strokeWidth={1.5}>
      <polygon points={left} fill="currentColor" fillOpacity={0.16} stroke="currentColor" strokeOpacity={0.35} />
      <polygon points={right} fill="currentColor" fillOpacity={0.08} stroke="currentColor" strokeOpacity={0.35} />
      <polygon points={top} fill="currentColor" fillOpacity={0.24} stroke="currentColor" strokeOpacity={0.45} />
      {tape && (
        <polyline
          points={`${x - (K * s) / 2},${y - s / 4} ${x + (K * s) / 2},${y + s / 4} ${x + (K * s) / 2},${y + 1.25 * s}`}
          fill="none"
          stroke="currentColor"
          strokeOpacity={0.5}
          strokeWidth={s / 9}
        />
      )}
    </g>
  );
};

// Thứ tự vẽ từ sau ra trước để khối gần che khối xa.
const cubes = [
  { x: 300, y: 150, s: 70 },
  { x: 239, y: 185, s: 70, tape: true },
  { x: 361, y: 185, s: 70 },
  { x: 300, y: 220, s: 70, tape: true },
  { x: 300, y: 150 - 70, s: 70, tape: true },
  { x: 170, y: 300, s: 46 },
  { x: 440, y: 300, s: 54, tape: true },
  { x: 120, y: 120, s: 30 },
  { x: 470, y: 90, s: 24 },
];

/**
 * Nền panel thương hiệu: mảng xanh rừng `bg-brand` có quầng hổ phách, lưới chấm và chồng hộp isometric; thuần trang trí (ẩn với trình đọc màn hình).
 *
 * @returns Lớp nền tuyệt đối phủ panel.
 */
export const BrandArt = () => (
  <div aria-hidden className="pointer-events-none absolute inset-0">
    <div className="absolute inset-0 bg-brand bg-[radial-gradient(90%_70%_at_100%_0%,color-mix(in_oklch,var(--color-highlight)_22%,transparent)_0%,transparent_60%),linear-gradient(160deg,var(--color-brand-active)_0%,var(--color-brand)_70%)]" />
    <div className="absolute inset-0 bg-[radial-gradient(currentColor_1px,transparent_1.5px)] bg-size-[22px_22px] opacity-[0.12] [mask-image:linear-gradient(to_bottom,black,transparent_85%)]" />
    <svg viewBox="40 40 520 340" className="absolute top-[10%] right-[-6%] w-[85%] max-w-none">
      {cubes.map((c, i) => (
        <Cube key={i} {...c} />
      ))}
    </svg>
    <div className="absolute inset-x-0 bottom-0 h-1/3 bg-gradient-to-t from-black/25 to-transparent" />
  </div>
);
