import { ImageResponse } from "next/og";

export const alt = "Culinary Blog - Khám phá Ẩm thực";
export const size = {
  width: 1200,
  height: 630,
};
export const contentType = "image/png";

export default async function Image() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          background: "linear-gradient(to bottom right, #f97316, #ea580c)",
          color: "#ffffff",
          fontFamily: "sans-serif",
        }}
      >
        <div style={{ fontSize: 64, fontWeight: "bold", marginBottom: 16 }}>
          Culinary Blog
        </div>
        <div style={{ fontSize: 28, opacity: 0.9 }}>
          Khám phá Ẩm thực
        </div>
      </div>
    ),
    {
      ...size,
    },
  );
}

