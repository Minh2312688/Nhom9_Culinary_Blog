import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Culinary Blog - Khám phá Ẩm thực",
  description: "Nền tảng chia sẻ công thức nấu ăn và trải nghiệm ẩm thực đỉnh cao",
};

/**
 * Minimal RootLayout for Week 2 Auth screens.
 * NOTE: Minimal frontend foundation created because Week-2 auth routes
 * cannot run without a frontend project.
 * TV3 MUST REVIEW SHARED FRONTEND FOUNDATION.
 */
export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="vi">
      <body className="antialiased min-h-screen flex flex-col justify-between">
        <main className="flex-1">{children}</main>
      </body>
    </html>
  );
}
