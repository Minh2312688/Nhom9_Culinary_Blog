import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Culinary Blog - Khám phá Ẩm thực",
  description: "Khám phá công thức nấu ăn, câu chuyện đầu bếp và cảm hứng ẩm thực từ cộng đồng Culinary Blog.",
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
