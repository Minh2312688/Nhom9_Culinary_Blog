import type { Metadata } from "next";
import { getSiteUrl } from "@/lib/site-config";
import { FeedbackProvider } from "./feedback";
import "./globals.css";

const siteUrl = getSiteUrl();

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: { default: "Culinary Blog - Khám phá Ẩm thực", template: "%s | Culinary Blog" },
  description: "Khám phá công thức nấu ăn, câu chuyện đầu bếp và cảm hứng ẩm thực từ cộng đồng Culinary Blog.",
  openGraph: {
    title: "Culinary Blog - Khám phá Ẩm thực",
    description: "Khám phá công thức nấu ăn, câu chuyện đầu bếp và cảm hứng ẩm thực từ cộng đồng Culinary Blog.",
    url: siteUrl,
    type: "website",
    locale: "vi_VN",
    siteName: "Culinary Blog",
  },
  twitter: {
    card: "summary_large_image",
    title: "Culinary Blog - Khám phá Ẩm thực",
    description: "Khám phá công thức nấu ăn, câu chuyện đầu bếp và cảm hứng ẩm thực từ cộng đồng Culinary Blog.",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="vi">
      <body className="antialiased min-h-screen">
        <FeedbackProvider>{children}</FeedbackProvider>
      </body>
    </html>
  );
}
