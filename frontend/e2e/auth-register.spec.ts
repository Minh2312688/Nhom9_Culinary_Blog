import { test, expect } from "@playwright/test";

test.describe("FR-AUTH-001: Register Flow E2E", () => {
  test("should register a new user successfully", async ({ page }) => {
    // Intercept backend register call to return successful response
    await page.route("**/api/v1/auth/register", async (route) => {
      await route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          userId: "user_test_12345",
          email: "chef@example.com",
          displayName: "Đầu Bếp Trưởng",
        }),
      });
    });

    // 1. Open /auth/register
    await page.goto("/auth/register");

    // Verify header
    await expect(page.getByRole("heading", { name: "Tạo tài khoản mới" })).toBeVisible();

    // 2. Fill valid form fields
    await page.getByLabel("Tên hiển thị").fill("Đầu Bếp Trưởng");
    await page.getByLabel("Địa chỉ Email").fill("chef@example.com");
    await page.getByLabel("Mật khẩu *", { exact: true }).fill("P@ssword123");
    await page.getByLabel("Xác nhận mật khẩu *", { exact: true }).fill("P@ssword123");

    // 3. Submit
    await page.getByRole("button", { name: "Đăng ký" }).click();

    // 4. Verify success screen
    await expect(page.getByRole("heading", { name: "Đăng ký thành công!" })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole("link", { name: "Đăng nhập ngay" })).toBeVisible();
  });
});
