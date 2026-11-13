import { test, expect, type Page } from "@playwright/test";
import { signInWithCredentials } from "./support/auth";

const mockApiUrl = "http://127.0.0.1:5055";

async function sessionFor(page: Page) {
  const response = await page.context().request.get("/api/auth/session");
  expect(response.ok()).toBe(true);
  return response.json() as Promise<Record<string, any>>;
}

async function signOut(page: Page) {
  const api = page.context().request;
  const csrfResponse = await api.get("/api/auth/csrf");
  expect(csrfResponse.ok()).toBe(true);
  const { csrfToken } = (await csrfResponse.json()) as { csrfToken: string };
  return api.post("/api/auth/signout", {
    form: { csrfToken, callbackUrl: "/" },
    maxRedirects: 0,
  });
}

test.describe("Auth.js credentials and server-side token lifecycle", () => {
  test.beforeEach(async ({ page }) => {
    const resetResponse = await page.context().request.post(`${mockApiUrl}/__test/auth/reset`);
    expect(resetResponse.status()).toBe(204);
  });

  test("email credentials expose only profile data and keep backend tokens out of browser storage", async ({ page }) => {
    await page.goto("/");

    await signInWithCredentials(page, "email-password", {
      email: "chef@example.com",
      password: "Password123!",
    });

    const cookie = (await page.context().cookies()).find((item) => item.name.includes("session-token"));
    expect(cookie?.httpOnly).toBe(true);
    expect(cookie?.sameSite).toBe("Lax");

    const session = await sessionFor(page);
    expect(session.user).toEqual({
      id: "user-chef",
      email: "chef@example.com",
      name: "Chef Example",
      image: null,
      roles: ["Author"],
    });
    expect(JSON.stringify(session)).not.toContain("backend-access-chef");
    expect(JSON.stringify(session)).not.toContain("backend-refresh-chef");
    expect(session).not.toHaveProperty("accessToken");
    expect(session).not.toHaveProperty("refreshToken");

    expect(await page.evaluate(() => sessionStorage.getItem("culinary_access_token"))).toBeNull();
    expect(await page.evaluate(() => sessionStorage.getItem("culinary_refresh_token"))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem("culinary_access_token"))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem("culinary_refresh_token"))).toBeNull();
  });

  test("Google ID-token credentials expose only the returned profile", async ({ page }) => {
    await page.goto("/");

    await signInWithCredentials(page, "google-id-token", { idToken: "test-google-id-token" });

    const cookie = (await page.context().cookies()).find((item) => item.name.includes("session-token"));
    expect(cookie?.httpOnly).toBe(true);
    expect(cookie?.sameSite).toBe("Lax");

    const session = await sessionFor(page);
    expect(session.user).toEqual({
      id: "user-google",
      email: "google@example.com",
      name: "Google Chef",
      image: null,
      roles: ["Member"],
    });
    expect(JSON.stringify(session)).not.toContain("backend-access-google");
    expect(JSON.stringify(session)).not.toContain("backend-refresh-google");
    expect(session).not.toHaveProperty("accessToken");
    expect(session).not.toHaveProperty("refreshToken");

    expect(await page.evaluate(() => sessionStorage.getItem("culinary_access_token"))).toBeNull();
    expect(await page.evaluate(() => sessionStorage.getItem("culinary_refresh_token"))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem("culinary_access_token"))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem("culinary_refresh_token"))).toBeNull();
  });

  test("invalid credentials do not create an authenticated session", async ({ page }) => {
    await page.goto("/");

    const response = await signInWithCredentials(page, "email-password", {
      email: "wrong@example.com",
      password: "WrongPassword",
    });

    expect(response.headers().location).toContain("code=invalid-credentials");
    expect(await sessionFor(page)).toBeNull();
  });

  test("locked accounts do not create an authenticated session", async ({ page }) => {
    await page.goto("/");

    const response = await signInWithCredentials(page, "email-password", {
      email: "locked-chef@example.com",
      password: "Password123!",
    });

    expect(response.headers().location).toContain("code=account-locked");
    expect(await sessionFor(page)).toBeNull();
  });

  test("concurrent expired sessions share one refresh and return only the safe session", async ({ page }) => {
    await page.goto("/");
    await signInWithCredentials(page, "email-password", {
      email: "expired-chef@example.com",
      password: "Password123!",
    });

    const api = page.context().request;
    const [first, second] = await Promise.all([
      api.get("/api/auth/session"),
      api.get("/api/auth/session"),
    ]);
    expect(first.ok()).toBe(true);
    expect(second.ok()).toBe(true);

    const firstSession = await first.json();
    const secondSession = await second.json();
    expect(firstSession.user?.email).toBe("expired-chef@example.com");
    expect(secondSession.user?.email).toBe("expired-chef@example.com");
    expect(JSON.stringify([firstSession, secondSession])).not.toContain("backend-access-expired-rotated");
    expect(JSON.stringify([firstSession, secondSession])).not.toContain("backend-refresh-expired-rotated");

    const refreshCountResponse = await api.get(`${mockApiUrl}/__test/auth/refresh-count`);
    expect(await refreshCountResponse.json()).toEqual({ count: 1 });

    await signOut(page);
    const logoutResponse = await api.get(`${mockApiUrl}/__test/auth/logout-count`);
    expect(await logoutResponse.json()).toEqual({ count: 1, authorized: true });
  });

  test("refreshes before access-token expiry within the 30-second skew", async ({ page }) => {
    await page.goto("/");
    await signInWithCredentials(page, "email-password", {
      email: "near-expiry-chef@example.com",
      password: "Password123!",
    });

    const session = await sessionFor(page);
    expect(session.user?.email).toBe("near-expiry-chef@example.com");

    const refreshCountResponse = await page.context().request.get(`${mockApiUrl}/__test/auth/refresh-count`);
    expect(await refreshCountResponse.json()).toEqual({ count: 1 });
  });

  test("failed refresh marks the Auth.js session as expired without exposing tokens", async ({ page }) => {
    await page.goto("/");
    await signInWithCredentials(page, "email-password", {
      email: "refresh-failure-chef@example.com",
      password: "Password123!",
    });

    const session = await sessionFor(page);
    expect(session.error).toBe("RefreshTokenError");
    expect(JSON.stringify(session)).not.toContain("backend-access-refresh-failure");
    expect(JSON.stringify(session)).not.toContain("backend-refresh-refresh-failure");
    expect(session).not.toHaveProperty("accessToken");
    expect(session).not.toHaveProperty("refreshToken");
  });

  test("sign-out revokes backend tokens and clears the Auth.js session", async ({ page }) => {
    await page.goto("/");
    await signInWithCredentials(page, "email-password", {
      email: "chef@example.com",
      password: "Password123!",
    });

    await signOut(page);

    expect(await sessionFor(page)).toBeNull();
    const logoutResponse = await page.context().request.get(`${mockApiUrl}/__test/auth/logout-count`);
    expect(await logoutResponse.json()).toEqual({ count: 1, authorized: true });
  });

  test("backend revoke failure does not prevent local sign-out", async ({ page }) => {
    await page.goto("/");
    await signInWithCredentials(page, "email-password", {
      email: "logout-failure-chef@example.com",
      password: "Password123!",
    });

    await signOut(page);

    expect(await sessionFor(page)).toBeNull();
    const logoutResponse = await page.context().request.get(`${mockApiUrl}/__test/auth/logout-count`);
    expect(await logoutResponse.json()).toEqual({ count: 1, authorized: true });
  });
});
