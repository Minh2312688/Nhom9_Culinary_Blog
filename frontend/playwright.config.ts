import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: "line",
  use: {
    baseURL: "http://127.0.0.1:3001",
    trace: "on-first-retry",
    headless: false, // Per prompt: run visibly so user sees browser execution
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH
      ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH }
      : undefined,
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command: "node e2e/support/mock-recipe-api.mjs",
      url: "http://127.0.0.1:5055/health",
      env: { MOCK_API_PORT: "5055" },
      reuseExistingServer: false,
      timeout: 10_000,
    },
    {
      command: "npm run dev -- --hostname 127.0.0.1 --port 3001",
      url: "http://127.0.0.1:3001",
      env: {
        API_INTERNAL_URL: "http://127.0.0.1:5055",
        NEXT_PUBLIC_API_URL: "http://127.0.0.1:5055",
      },
      reuseExistingServer: false,
      timeout: 120_000,
    },
  ],
});
