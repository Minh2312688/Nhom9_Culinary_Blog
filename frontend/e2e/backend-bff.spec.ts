import { test, expect, type APIRequestContext } from "@playwright/test";
import { request as httpRequest } from "node:http";
import { signInWithCredentials } from "./support/auth";

const mockApiUrl = "http://127.0.0.1:5055";

async function clearBackendRequests(api: APIRequestContext) {
  const response = await api.delete(`${mockApiUrl}/__test__/requests`);
  expect(response.status()).toBe(204);
}

function getRawPathStatus(path: string, cookie: string): Promise<number> {
  return new Promise((resolve, reject) => {
    const request = httpRequest({
      hostname: "127.0.0.1",
      port: 3001,
      method: "GET",
      path,
      headers: { Cookie: cookie },
    }, (response) => {
      response.resume();
      response.on("end", () => resolve(response.statusCode ?? 0));
    });
    request.on("error", reject);
    request.end();
  });
}

test.beforeEach(async ({ request }) => {
  const resetResponse = await request.post(`${mockApiUrl}/__test/auth/reset`);
  expect(resetResponse.status()).toBe(204);
  await clearBackendRequests(request);
});

test("anonymous private API calls are unauthorized", async ({ request }) => {
  const response = await request.get("/api/backend/recipes");
  expect(response.status()).toBe(401);
});

test("signed-in requests reach the backend with server-side authorization", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "chef@example.com",
    password: "Password123!",
  });
  await clearBackendRequests(page.context().request);

  const response = await page.context().request.get("/api/backend/recipes?page=2&search=soup");
  expect(response.status()).toBe(200);

  const recordsResponse = await page.context().request.get(`${mockApiUrl}/__test__/requests`);
  expect(await recordsResponse.json()).toContainEqual({
    method: "GET",
    path: "/api/v1/recipes",
    query: "?page=2&search=soup",
    authorizationPresent: true,
  });
});

test("my-recipes requests use the authenticated owner-only backend route", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "chef@example.com",
    password: "Password123!",
  });
  await clearBackendRequests(page.context().request);

  const response = await page.context().request.get("/api/backend/recipes/mine?page=2&pageSize=12");

  expect(response.status()).toBe(200);
  const recordsResponse = await page.context().request.get(`${mockApiUrl}/__test__/requests`);
  expect(await recordsResponse.json()).toContainEqual({
    method: "GET",
    path: "/api/v1/recipes/mine",
    query: "?page=2&pageSize=12",
    authorizationPresent: true,
  });
});

test("my-recipes BFF route does not allow recipe mutations", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "chef@example.com",
    password: "Password123!",
  });
  await clearBackendRequests(page.context().request);

  const response = await page.context().request.delete("/api/backend/recipes/mine");

  expect(response.status()).toBe(405);
  const recordsResponse = await page.context().request.get(`${mockApiUrl}/__test__/requests`);
  expect(await recordsResponse.json()).toEqual([]);
});

test("state-changing requests reject a non-matching Origin", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "chef@example.com",
    password: "Password123!",
  });
  await clearBackendRequests(page.context().request);

  const response = await page.context().request.post("/api/backend/recipes", {
    headers: { Origin: "https://attacker.example.test" },
    data: { title: "Not forwarded" },
  });
  expect(response.status()).toBe(403);

  const recordsResponse = await page.context().request.get(`${mockApiUrl}/__test__/requests`);
  expect(await recordsResponse.json()).toEqual([]);
});

test("unsupported resources and encoded separators are rejected", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "chef@example.com",
    password: "Password123!",
  });
  await clearBackendRequests(page.context().request);

  const unsupported = await page.context().request.get("/api/backend/auth/refresh");
  const encodedSlash = await page.context().request.get("/api/backend/recipes/recipe-1%2Fimages");
  expect(unsupported.status()).toBe(404);
  expect(encodedSlash.status()).toBe(404);
});

test("dot segments are checked against the normalized BFF allowlist", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "chef@example.com",
    password: "Password123!",
  });
  await clearBackendRequests(page.context().request);

  const cookies = await page.context().cookies("http://127.0.0.1:3001");
  const cookie = cookies.map(({ name, value }) => `${name}=${value}`).join("; ");
  const status = await getRawPathStatus("/api/backend/recipes/../auth/me", cookie);
  const records = await page.context().request.get(`${mockApiUrl}/__test__/requests`);

  expect(status).toBe(200);
  expect(await records.json()).toEqual([
    {
      method: "GET",
      path: "/api/v1/auth/me",
      query: "",
      authorizationPresent: true,
    },
  ]);
});

test("refreshed session cookies are retained when a request is rejected", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "expired-chef@example.com",
    password: "Password123!",
  });

  const api = page.context().request;
  const rejected = await api.get("/api/backend/auth/refresh");
  expect(rejected.status()).toBe(404);
  expect(rejected.headers()["set-cookie"]).toContain("authjs.session-token");

  const nextPrivateCall = await api.get("/api/backend/recipes");
  expect(nextPrivateCall.status()).toBe(200);
});

test("HEAD and OPTIONS still authenticate BFF requests", async ({ page, request }) => {
  const anonymousHead = await request.fetch("/api/backend/recipes", { method: "HEAD" });
  const anonymousOptions = await request.fetch("/api/backend/recipes", { method: "OPTIONS" });
  expect(anonymousHead.status()).toBe(401);
  expect(anonymousOptions.status()).toBe(401);

  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "chef@example.com",
    password: "Password123!",
  });
  const api = page.context().request;
  const signedInHead = await api.fetch("/api/backend/recipes", { method: "HEAD" });
  const signedInOptions = await api.fetch("/api/backend/recipes", { method: "OPTIONS" });
  expect(signedInHead.status()).toBe(405);
  expect(signedInOptions.status()).toBe(405);
});

test("a rejected Auth.js refresh makes the next private BFF call unauthorized", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "refresh-failure-chef@example.com",
    password: "Password123!",
  });
  await clearBackendRequests(page.context().request);

  const response = await page.context().request.get("/api/backend/recipes");
  expect(response.status()).toBe(401);
  expect(response.headers()["set-cookie"]).toContain("Max-Age=0");
});

test("retries a private request once when the backend rejects an expired access token", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "chef@example.com",
    password: "Password123!",
  });
  const api = page.context().request;
  const forcedResponse = await api.post(`${mockApiUrl}/__test/responses`, {
    data: {
      method: "GET",
      path: "/api/v1/recipes/mine",
      status: 401,
      body: { status: 401, title: "Unauthorized" },
    },
  });
  expect(forcedResponse.status()).toBe(201);

  const response = await api.get("/api/backend/recipes/mine");

  expect(response.status()).toBe(200);
  const refreshCountResponse = await api.get(`${mockApiUrl}/__test/auth/refresh-count`);
  expect(await refreshCountResponse.json()).toEqual({ count: 1 });
  const recordsResponse = await api.get(`${mockApiUrl}/__test__/requests`);
  expect((await recordsResponse.json()).filter(
    (record: { path: string }) => record.path === "/api/v1/recipes/mine",
  )).toHaveLength(2);
});

test("expired private sessions redirect to login and return to the requested recipe page", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "refresh-failure-chef@example.com",
    password: "Password123!",
  });

  await page.goto("/dashboard/recipes");

  await expect(page).toHaveURL(/\/auth\/login\?callbackUrl=/);
  await expect(page.getByRole("heading", { name: "Đăng nhập Culinary Blog" })).toBeVisible();
  await expect(page.getByText("Unauthorized")).toHaveCount(0);
  expect((await page.context().cookies()).some(({ name }) => name.startsWith("authjs.session-token"))).toBe(false);
  await page.waitForLoadState("networkidle");

  await page.getByLabel(/địa chỉ email/i).fill("chef@example.com");
  await page.locator("#password").fill("Password123!");
  await page.locator('form button[type="submit"]').click();

  await expect(page).toHaveURL(/\/dashboard\/recipes(?:\?.*)?$/);
});

test("a successful refresh forwards the rotated access token", async ({ page }) => {
  await page.goto("/");
  await signInWithCredentials(page, "email-password", {
    email: "expired-chef@example.com",
    password: "Password123!",
  });
  await clearBackendRequests(page.context().request);

  const first = await page.context().request.get("/api/backend/recipes");
  const second = await page.context().request.get("/api/backend/recipes");
  expect(first.status()).toBe(200);
  expect(second.status()).toBe(200);
});

test("anonymous protected-page redirects preserve only a local callback", async ({ request }) => {
  const response = await request.get("/profile?callbackUrl=https%3A%2F%2Fattacker.example.test", {
    maxRedirects: 0,
  });
  expect([302, 307]).toContain(response.status());

  const location = response.headers().location;
  expect(location).toBeTruthy();
  const redirect = new URL(location!, "http://127.0.0.1:3001");
  expect(redirect.pathname).toBe("/auth/login");
  expect(redirect.searchParams.get("callbackUrl")).toBe(
    "/profile?callbackUrl=https%3A%2F%2Fattacker.example.test",
  );
});
