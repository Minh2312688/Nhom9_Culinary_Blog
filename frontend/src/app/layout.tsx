import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Culinary Blog - Khám phá Ẩm thực",
  description: "Nền tảng chia sẻ công thức nấu ăn và trải nghiệm ẩm thực đỉnh cao",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="vi">
      <body className="antialiased min-h-screen">{children}</body>
    </html>
  );
}
