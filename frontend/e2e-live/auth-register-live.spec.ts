import { test, expect } from "@playwright/test";

test.describe("Real Live Register Flow", () => {
  test("should register a unique user against live backend without mocks", async ({ page }) => {
    const uniqueEmail = `live_reg_${Date.now()}@example.com`;
    await page.goto("/auth/register");

    await expect(page.getByRole("heading", { name: "Tạo tài khoản mới" })).toBeVisible();

    await page.getByLabel("Tên hiển thị").fill("Live Real Chef");
    await page.getByLabel("Địa chỉ Email").fill(uniqueEmail);
    await page.getByLabel("Mật khẩu *", { exact: true }).fill("P@ssword123!");
    await page.getByLabel("Xác nhận mật khẩu *", { exact: true }).fill("P@ssword123!");

    await page.getByRole("button", { name: "Đăng ký" }).click();

    await expect(page.getByRole("heading", { name: "Đăng ký thành công!" })).toBeVisible({ timeout: 15000 });
  });
});
