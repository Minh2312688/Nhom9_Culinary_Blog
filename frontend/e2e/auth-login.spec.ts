import { test, expect } from "@playwright/test";

test.describe("FR-AUTH-002: Login Flow E2E", () => {
  test("should login with email and password successfully", async ({ page }) => {
    // Intercept backend login call to return tokens
    await page.route("**/api/v1/auth/login", async (route) => {
      await route.fulfill({
        status: 200,
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
  });
});
