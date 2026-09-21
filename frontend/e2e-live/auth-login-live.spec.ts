import { test, expect } from "@playwright/test";

test.describe("Real Live Login Flow", () => {
  test("should register and then login against live backend without mocks", async ({ page }) => {
    const uniqueEmail = `live_login_${Date.now()}@example.com`;
    const password = "P@ssword123!";

    // 1. Register first
    await page.goto("/auth/register");
    await page.getByLabel("Tên hiển thị").fill("Live Login Chef");
    await page.getByLabel("Địa chỉ Email").fill(uniqueEmail);
    await page.getByLabel("Mật khẩu *", { exact: true }).fill(password);
    await page.getByLabel("Xác nhận mật khẩu *", { exact: true }).fill(password);
    await page.getByRole("button", { name: "Đăng ký" }).click();
    await expect(page.getByRole("heading", { name: "Đăng ký thành công!" })).toBeVisible({ timeout: 15000 });

    // 2. Click login link
    await page.getByRole("link", { name: "Đăng nhập ngay" }).click();
    await expect(page.getByRole("heading", { name: "Đăng nhập Culinary Blog" })).toBeVisible({ timeout: 10000 });

    // 3. Login with registered user
    await page.getByLabel("Địa chỉ Email").fill(uniqueEmail);
    await page.getByLabel("Mật khẩu *", { exact: true }).fill(password);
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();

    // 4. Verify login outcome
    await expect(page.getByRole("heading", { name: "Đăng nhập thành công!" })).toBeVisible({ timeout: 15000 });
  });
});
