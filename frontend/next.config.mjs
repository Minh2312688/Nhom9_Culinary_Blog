/** @type {import('next').NextConfig} */
const isDev = process.env.NODE_ENV !== "production";

function buildContentSecurityPolicy() {
  if (isDev) {
    return [
      "default-src 'self'",
      "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://accounts.google.com",
      "style-src 'self' 'unsafe-inline' https://accounts.google.com",
      "img-src 'self' data: blob: http://localhost:* http://127.0.0.1:* https://*.googleusercontent.com https://accounts.google.com",
      "font-src 'self' data:",
      "connect-src 'self' http://localhost:* http://127.0.0.1:* ws://localhost:* ws://127.0.0.1:* https://accounts.google.com",
      "frame-src 'self' https://accounts.google.com",
      "object-src 'none'",
      "base-uri 'self'",
      "frame-ancestors 'none'",
    ].join("; ");
  }

  let extraOrigin = "";
  if (process.env.NEXT_PUBLIC_API_URL) {
    try {
      const parsed = new URL(process.env.NEXT_PUBLIC_API_URL);
      if (!parsed.hostname.includes("localhost") && parsed.hostname !== "127.0.0.1") {
        extraOrigin = ` ${parsed.origin}`;
      }
    } catch {
      // Ignore invalid URL
    }
  }

  return [
    "default-src 'self'",
    "script-src 'self' 'unsafe-inline' https://accounts.google.com",
    "style-src 'self' 'unsafe-inline' https://accounts.google.com",
    `img-src 'self' data: blob: https://*.googleusercontent.com https://accounts.google.com${extraOrigin}`,
    "font-src 'self' data:",
    `connect-src 'self' https://accounts.google.com${extraOrigin}`,
    "frame-src 'self' https://accounts.google.com",
    "object-src 'none'",
    "base-uri 'self'",
    "frame-ancestors 'none'",
  ].join("; ");
}

const nextConfig = {
  reactStrictMode: true,
  output: "standalone",
  async headers() {
    return [
      {
        source: "/(.*)",
        headers: [
          {
            key: "Content-Security-Policy",
            value: buildContentSecurityPolicy(),
          },
        ],
      },
    ];
  },
};

export default nextConfig;
