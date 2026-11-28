import type { MetadataRoute } from "next";
import { siteUrl, sitemapEntries } from "../lib/seo";
import { homePaths } from "./home";

/** Sinh mỗi request: danh sách locale lấy từ localization lúc chạy. */
export const dynamic = "force-dynamic";

/**
 * Sitemap của shop: mỗi bản đã xuất bản theo locale kèm alternate (`xhtml:link`).
 *
 * @returns Mục sitemap; trang nội dung (sản phẩm, bài viết) thêm khi có ở M1/M6.
 */
const sitemap = async (): Promise<MetadataRoute.Sitemap> => sitemapEntries(siteUrl(), [await homePaths()]);

export default sitemap;
