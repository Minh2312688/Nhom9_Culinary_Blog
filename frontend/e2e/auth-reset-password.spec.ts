import { test, expect } from "@playwright/test";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, GET, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, Authorization",
};

test.describe("FR-AUTH: Password Reset Flow E2E", () => {
  test("Direct access without token should show helpful missing-link guard", async ({ page }) => {
    await page.goto("/auth/reset-password");

    // Check header
    await expect(page.getByRole("heading", { name: "Đặt lại mật khẩu" })).toBeVisible();

    // Check alert message
    await expect(page.getByText("Chưa có liên kết xác thực")).toBeVisible();
    await expect(
      page.getByText("Trang đặt lại mật khẩu yêu cầu liên kết xác thực hợp lệ")
    ).toBeVisible();

    // Check action buttons
    const requestLinkBtn = page.getByRole("link", { name: "Yêu cầu đặt lại mật khẩu" });
    await expect(requestLinkBtn).toBeVisible();
    await expect(requestLinkBtn).toHaveAttribute("href", "/auth/forgot-password");

    // Click "Tôi đã có mã xác thực thủ công"
    await page.getByRole("button", { name: "Tôi đã có mã xác thực thủ công" }).click();

    // Manual form should appear with token input
    await expect(page.getByLabel("Mã xác thực (Token) *")).toBeVisible();
  });

  test("Access with query parameters should prefill email, hide raw token, and succeed on submit", async ({
    page,
  }) => {
    let resetPayload: any = null;

    await page.route("**/api/v1/auth/reset-password", async (route) => {
      if (route.request().method() === "OPTIONS") {
        await route.fulfill({ status: 204, headers: CORS_HEADERS });
        return;
      }
      resetPayload = route.request().postDataJSON();
      await route.fulfill({
        status: 200,
        headers: CORS_HEADERS,
        contentType: "application/json",
        body: JSON.stringify({ message: "Password reset successful" }),
      });
    });

    const email = "chef@example.com";
    const token = "secure-test-token-xyz+123==";

    await page.goto(
      `/auth/reset-password?email=${encodeURIComponent(email)}&token=${encodeURIComponent(token)}`
    );

    // Verify email account banner
    await expect(page.getByText(`Xác thực cho tài khoản: ${email}`)).toBeVisible();

    // Email input is prefilled and readonly
    const emailInput = page.getByLabel("Địa chỉ Email *");
    await expect(emailInput).toHaveValue(email);

    // Token input is hidden because it was provided via query param
    await expect(page.locator("input[name='token']")).toHaveValue(token);

    // Enter new password and confirmation using exact label match
    await page.getByLabel("Mật khẩu mới *", { exact: true }).fill("NewPassword123!");
    await page.getByLabel("Xác nhận mật khẩu mới *", { exact: true }).fill("NewPassword123!");

    // Submit form
    await page.getByRole("button", { name: "Cập nhật mật khẩu" }).click();

    // Verify payload sent to API
    await expect.poll(() => resetPayload).not.toBeNull();
    expect(resetPayload.email).toBe(email);
    expect(resetPayload.token).toBe(token);
    expect(resetPayload.newPassword).toBe("NewPassword123!");
    expect(resetPayload.confirmPassword).toBe("NewPassword123!");

    // Verify success banner and login link
    await expect(page.getByText("Đặt lại mật khẩu thành công!")).toBeVisible();
    const loginLink = page.getByRole("link", { name: "Đăng nhập ngay" });
    await expect(loginLink).toBeVisible();
    await expect(loginLink).toHaveAttribute("href", "/auth/login");
  });
});
