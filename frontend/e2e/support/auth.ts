import { expect, type Page } from "@playwright/test";

export async function signInAs(page: Page, email = "chef@example.com"): Promise<void> {
  await page.goto("/auth/login");
  const callbackUrl = new URL(page.url()).searchParams.get("callbackUrl") ?? "/";

  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/mật khẩu/i).fill("Password123!");
  await page.getByRole("button", { name: /đăng nhập/i }).click();

  await expect(page).toHaveURL(new URL(callbackUrl, page.url()).toString());
}
