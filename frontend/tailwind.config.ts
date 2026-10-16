import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        background: "var(--background)",
        foreground: "var(--foreground)",
        primary: Object.fromEntries(
          [50, 100, 200, 300, 400, 500, 600, 700, 800, 900].map((shade) => [
            shade,
            `var(--coral-${shade})`,
          ]),
        ),
        orange: Object.fromEntries(
          [50, 100, 200, 300, 400, 500, 600, 700, 800, 900].map((shade) => [
            shade,
            `var(--coral-${shade})`,
          ]),
        ),
        amber: Object.fromEntries(
          [50, 100, 200, 300, 400, 500, 600, 700, 800, 900].map((shade) => [
            shade,
            `var(--turmeric-${shade})`,
          ]),
        ),
        teal: Object.fromEntries(
          [50, 100, 200, 300, 400, 500, 600, 700, 800, 900].map((shade) => [
            shade,
            `var(--teal-${shade})`,
          ]),
        ),
      },
    },
  },
  plugins: [],
};
export default config;
