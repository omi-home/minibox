import type { Metadata } from "next";
import type { ReactNode } from "react";
import { baseMetadata, Shell } from "../shell";

export { viewport } from "../shell";

/** Metadata mặc định cho các trang tiếng Việt ở gốc. */
export const metadata: Metadata = baseMetadata();

/**
 * Root layout của các trang tiếng Việt (locale mặc định, không tiền tố).
 *
 * @param props - `children` là trang hiện tại.
 * @returns Khung HTML `lang="vi"`.
 */
const ViLayout = ({ children }: { children: ReactNode }) => <Shell lang="vi">{children}</Shell>;

export default ViLayout;
