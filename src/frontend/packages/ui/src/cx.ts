/**
 * Ghép class, bỏ giá trị rỗng; dùng được cả ở Server Component.
 *
 * @param c - Các class hoặc giá trị rỗng.
 * @returns Chuỗi class.
 */
export const cx = (...c: (string | false | undefined)[]) => c.filter(Boolean).join(" ");
