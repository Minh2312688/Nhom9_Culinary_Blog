import { test, expect } from "@playwright/test";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, GET, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, Authorization",
};

test.describe("FR-AUTH-002: Login Flow E2E", () => {
  test("should login with email and password successfully", async ({ page }) => {
    // Intercept backend login call to return tokens with CORS headers
    await page.route("**/api/v1/auth/login", async (route) => {
      if (route.request().method() === "OPTIONS") {
        await route.fulfill({ status: 204, headers: CORS_HEADERS });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: CORS_HEADERS,
        contentType: "application/json",
        body: JSON.stringify({
          accessToken: "sample_e2e_jwt_access_token",
          refreshToken: "sample_e2e_refresh_token",
          expiresIn: 900,
        }),
      });
    });

    // 1. Open /auth/login
    await page.goto("/auth/login");

    // Verify header & buttons
    await expect(page.getByRole("heading", { name: "Đăng nhập Culinary Blog" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Đăng nhập với Google" })).toBeVisible();

    // 2. Fill valid account
    await page.getByLabel("Địa chỉ Email").fill("chef@example.com");
    await page.getByLabel("Mật khẩu *", { exact: true }).fill("P@ssword123");

    // 3. Submit
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();

    // 4. Verify successful UI outcome
    await expect(page.getByRole("heading", { name: "Đăng nhập thành công!" })).toBeVisible({ timeout: 10000 });

    // 5. Verify tokens stored in sessionStorage via auth-store
    const storedToken = await page.evaluate(() => sessionStorage.getItem("culinary_access_token"));
    expect(storedToken).toBe("sample_e2e_jwt_access_token");
  });
});
