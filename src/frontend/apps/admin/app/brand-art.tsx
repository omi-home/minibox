// Minh họa panel thương hiệu: khối hộp isometric xếp chồng (SVG thuần, màu theo currentColor), thay ảnh khi có bộ nhận diện (#448).

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
 * Nền panel thương hiệu: chuyển màu primary, lưới chấm và chồng hộp isometric; thuần trang trí (ẩn với trình đọc màn hình).
 *
 * @returns Lớp nền tuyệt đối phủ panel.
 */
export const BrandArt = () => (
  <div aria-hidden className="pointer-events-none absolute inset-0">
    <div className="absolute inset-0 bg-[radial-gradient(120%_80%_at_0%_0%,color-mix(in_oklch,var(--color-primary)_70%,white)_0%,transparent_55%),linear-gradient(160deg,var(--color-primary)_0%,var(--color-primary-edge)_100%)]" />
    <div className="absolute inset-0 bg-[radial-gradient(currentColor_1px,transparent_1.5px)] bg-size-[22px_22px] opacity-[0.12] [mask-image:linear-gradient(to_bottom,black,transparent_85%)]" />
    <svg viewBox="40 40 520 340" className="absolute top-[10%] right-[-6%] w-[85%] max-w-none">
      {cubes.map((c, i) => (
        <Cube key={i} {...c} />
      ))}
    </svg>
    <div className="absolute inset-x-0 bottom-0 h-1/3 bg-gradient-to-t from-black/25 to-transparent" />
  </div>
);
