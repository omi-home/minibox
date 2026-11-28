// Dữ liệu mẫu cho trang /design (không phải số liệu thật). Ảnh minh hoạ: đồ in 3D thật trên Unsplash (giấy phép Unsplash, dùng miễn phí).

/** Một sản phẩm mẫu. */
export type Product = { sku: string; name: string; material: string; price: number; compareAt?: number; deposit?: number; size: string; layer: string; stock: number; made: "stock" | "print"; /** Ảnh minh hoạ (có nền, hiển thị kiểu phủ khung). */ image?: string };

/** Sản phẩm in theo mẫu dùng cho khối chi tiết sản phẩm. */
export const featured: Product = { sku: "MB-FIG-0142", name: "Figure cáo con", material: "PLA nhiều màu", price: 420000, deposit: 130000, size: "8 × 6 × 9 cm", layer: "0,12 mm", stock: 0, made: "print", image: "https://images.unsplash.com/photo-1792571394341-1ae20dfbf43c?w=720&q=75&auto=format&fit=crop" };

/** Sản phẩm mẫu: đồ in 3D trong danh mục Minibox. */
export const products: Product[] = [
  { sku: "MB-DEN-0107", name: "Đèn ngủ gân xoắn", material: "PLA mờ", price: 289000, compareAt: 329000, size: "12 × 12 × 24 cm", layer: "0,2 mm", stock: 14, made: "stock", image: "https://images.unsplash.com/photo-1748682492528-8b0582128c7a?w=720&q=75&auto=format&fit=crop" },
  { sku: "MB-CHAU-0212", name: "Chậu cây bí ngô", material: "PLA lụa", price: 145000, size: "10 × 10 × 9 cm", layer: "0,2 mm", stock: 3, made: "stock", image: "https://images.unsplash.com/photo-1730267252256-67bee55353e8?w=720&q=75&auto=format&fit=crop" },
  featured,
  { sku: "MB-BINH-0031", name: "Bình hoa tổ ong", material: "PETG trong", price: 199000, size: "11 × 11 × 22 cm", layer: "0,2 mm", stock: 27, made: "stock", image: "https://images.unsplash.com/photo-1786840320376-79cd01e84fa5?w=720&q=75&auto=format&fit=crop" },
];

/** Đơn sản xuất mẫu cho bảng tác nghiệp. */
export const jobs = [
  { id: "SX-2611", product: "Figure cáo con", printer: "FDM 02", status: "Đang in", tone: "info", qty: 2, total: 840000 },
  { id: "SX-2612", product: "Đèn ngủ gân xoắn", printer: "FDM 05", status: "Hậu xử lý", tone: "warning", qty: 6, total: 1734000 },
  { id: "SX-2613", product: "Chậu cây bí ngô", printer: "FDM 01", status: "Đạt QC", tone: "success", qty: 12, total: 1740000 },
  { id: "SX-2614", product: "Bình hoa tổ ong", printer: "FDM 03", status: "Lỗi lớp", tone: "danger", qty: 1, total: 199000 },
] as const;

/** Tỉnh/thành mẫu cho combobox tìm không dấu. */
export const provinces = [
  ["hn", "Hà Nội", "Miền Bắc"],
  ["hp", "Hải Phòng", "Miền Bắc"],
  ["qn", "Quảng Ninh", "Miền Bắc"],
  ["bn", "Bắc Ninh", "Miền Bắc"],
  ["hue", "Huế", "Miền Trung"],
  ["dn", "Đà Nẵng", "Miền Trung"],
  ["kh", "Khánh Hòa", "Miền Trung"],
  ["dl", "Lâm Đồng", "Miền Trung"],
  ["hcm", "TP. Hồ Chí Minh", "Miền Nam"],
  ["dnai", "Đồng Nai", "Miền Nam"],
  ["ct", "Cần Thơ", "Miền Nam"],
  ["ag", "An Giang", "Miền Nam"],
].map(([value, label, group]) => ({ value: value!, label: label!, group }));

/** Ô màu trên trang: [token, nhãn]. */
export const swatches = {
  "Nền và chữ": [
    ["background", "Nền trang"],
    ["surface", "Bề mặt"],
    ["surface-sunken", "Bề mặt chìm"],
    ["border-strong", "Viền đậm"],
    ["muted", "Chữ phụ"],
    ["foreground", "Chữ"],
  ],
  "Màu nhấn": [
    ["primary", "Chính"],
    ["primary-hover", "Chính khi rê"],
    ["accent", "Nhấn nhạt"],
    ["accent-foreground", "Chữ trên nhấn nhạt"],
    ["highlight", "Điểm nhấn"],
    ["focus", "Viền focus"],
  ],
  "Trạng thái": [
    ["success", "Thành công"],
    ["warning", "Cảnh báo"],
    ["danger", "Nguy hiểm"],
    ["info", "Thông tin"],
    ["success-soft", "Nền thành công"],
    ["danger-soft", "Nền nguy hiểm"],
  ],
} as const;
