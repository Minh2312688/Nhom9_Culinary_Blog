import { expect, type Page } from "@playwright/test";

export async function signInWithCredentials(
  page: Page,
  provider: "email-password" | "google-id-token",
  credentials: Record<string, string>,
  callbackUrl = "/",
) {
  const api = page.context().request;
  const csrfResponse = await api.get("/api/auth/csrf");
  expect(csrfResponse.ok()).toBe(true);
  const { csrfToken } = (await csrfResponse.json()) as { csrfToken: string };

  return api.post(`/api/auth/callback/${provider}`, {
    form: { csrfToken, callbackUrl, ...credentials },
    maxRedirects: 0,
  });
}

export async function signInAs(page: Page, email = "chef@example.com"): Promise<void> {
  if (new URL(page.url()).pathname !== "/auth/login") {
    await page.goto("/auth/login");
  }
  await page.getByLabel(/email/i).fill(email);
  await page.locator("#password").fill("Password123!");
  await page.locator('form button[type="submit"]').click();
  await expect(page).toHaveURL(new URL("/", page.url()).toString(), { timeout: 30_000 });
}

export async function signInAsByApi(page: Page, email = "chef@example.com"): Promise<void> {
  const currentUrl = new URL(page.url());
  const requestedCallback = currentUrl.pathname === "/auth/login"
    ? currentUrl.searchParams.get("callbackUrl")
    : null;
  const callbackUrl = requestedCallback?.startsWith("/") && !requestedCallback.startsWith("//")
    ? requestedCallback
    : "/";

  await signInWithCredentials(page, "email-password", {
    email,
    password: "Password123!",
  }, callbackUrl);

  const sessionResponse = await page.context().request.get("/api/auth/session");
  expect(sessionResponse.ok()).toBe(true);
  const session = await sessionResponse.json();
  expect(session.user?.email).toBe(email);

  await page.goto(callbackUrl, { waitUntil: "domcontentloaded" });
}
