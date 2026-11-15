import { expect, test, type Page } from "@playwright/test";
import { signInAs, signInAsByApi } from "./support/auth";

const mockApiUrl = "http://127.0.0.1:5055";

type TestProfile = {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  bio: string | null;
  emailConfirmed: boolean;
  createdAt: string;
  roles: string[];
};

async function setProfile(page: Page, email: string, profile: TestProfile) {
  const response = await page.context().request.post(`${mockApiUrl}/__test/auth/profile`, {
    data: { email, profile },
  });
  expect(response.ok()).toBe(true);
}

test.beforeEach(async ({ page }) => {
  const reset = await page.context().request.post(`${mockApiUrl}/__test/auth/reset`);
  expect(reset.status()).toBe(204);
  const cleared = await page.context().request.delete(`${mockApiUrl}/__test__/requests`);
  expect(cleared.status()).toBe(204);
});

test.describe("Auth.js Profile and logout", () => {
  test("redirects unauthenticated users to login", async ({ page }) => {
    await page.goto("/profile");
    await expect(page).toHaveURL(/\/auth\/login\?callbackUrl=/);
  });

  test("UI sign-in shows the session user's name and keeps a protected page after reload", async ({ page }) => {
    await page.goto("/profile");
    await expect(page).toHaveURL(/\/auth\/login\?callbackUrl=/);

    await signInAs(page);

    await expect(page).toHaveURL(new URL("/", page.url()).toString());
    await expect(page.getByTestId("nav-profile")).toBeVisible();
    const profileAfterAccountClick = page.waitForResponse((response) =>
      response.url().includes("/api/backend/auth/me") &&
      response.request().method() === "GET",
    );
    await page.getByTestId("nav-profile").click();
    expect((await profileAfterAccountClick).status()).toBe(200);
    await expect(page).toHaveURL(/\/profile$/);
    await expect(page.getByRole("heading", { name: "Chef Example" })).toBeVisible();

    await page.goto(new URL("/", page.url()).toString());
    await expect(page.getByTestId("nav-profile")).toContainText("Chef Example");
    await page.goto(new URL("/profile", page.url()).toString());
    const reloadedProfileResponse = page.waitForResponse((response) =>
      response.url().includes("/api/backend/auth/me") &&
      response.request().method() === "GET",
    );
    await page.reload();

    expect((await reloadedProfileResponse).status()).toBe(200);
    await expect(page).toHaveURL(/\/profile$/);
    await expect(page.getByRole("heading", { name: "Chef Example" })).toBeVisible();

    const recordsResponse = await page.context().request.get(`${mockApiUrl}/__test__/requests`);
    const records = await recordsResponse.json();
    expect(records).toContainEqual(expect.objectContaining({
      method: "GET",
      path: "/api/v1/auth/me",
      authorizationPresent: true,
    }));
  });

  test("signed-in users have a direct link to their recipe dashboard", async ({ page }) => {
    await signInAs(page);
    await page.goto("/");

    await expect(page.getByRole("link", { name: "Công thức của tôi" }))
      .toHaveAttribute("href", "/dashboard/recipes");
  });

  test("admins see Admin instead of the recipe link label", async ({ page }) => {
    await signInAsByApi(page, "admin@example.com");
    await page.goto("/");

    await expect(page.getByTestId("nav-my-recipes"))
      .toHaveText("Admin");
    await expect(page.getByTestId("nav-my-recipes"))
      .toHaveAttribute("href", "/dashboard/recipes");
  });

  test("UI sign-out clears the Auth.js cookie when backend logout returns 503", async ({ page }) => {
    await signInAs(page, "logout-failure-chef@example.com");
    await page.getByTestId("nav-profile").click();
    await expect(page).toHaveURL(/\/profile$/);
    const homeUrl = new URL("/", page.url()).toString();
    const cookieBefore = (await page.context().cookies()).find((cookie) =>
      cookie.name.includes("session-token"),
    );
    expect(cookieBefore?.httpOnly).toBe(true);

    await page.getByRole("button", { name: /đăng xuất/i }).click();

    await expect(page).toHaveURL(homeUrl);
    const sessionResponse = await page.context().request.get("/api/auth/session");
    expect(await sessionResponse.json()).toBeNull();
    expect((await page.context().cookies()).some((cookie) =>
      cookie.name.includes("session-token"),
    )).toBe(false);
    const logoutCount = await page.context().request.get(`${mockApiUrl}/__test/auth/logout-count`);
    expect(await logoutCount.json()).toEqual({ count: 1, authorized: true });
  });

  test("loads an expired backend token through BFF refresh before rendering Profile", async ({ page }) => {
    await signInAs(page, "expired-chef@example.com");
    await page.goto("/profile");

    await expect(page.getByRole("heading", { name: "Chef Example" })).toBeVisible();
    const refreshCount = await page.context().request.get(`${mockApiUrl}/__test/auth/refresh-count`);
    expect(await refreshCount.json()).toEqual({ count: 1 });
  });

  test("loads and updates Profile through the authenticated BFF", async ({ page }) => {
    const profile: TestProfile = {
      id: "chef-123",
      email: "chef@example.com",
      displayName: "Master Chef Nam",
      avatarUrl: "https://example.com/initial-avatar.jpg",
      bio: "Initial culinary bio",
      emailConfirmed: true,
      createdAt: "2026-01-15T00:00:00Z",
      roles: ["Author"],
    };
    await setProfile(page, "chef@example.com", profile);
    await signInAs(page);
    const profileResponse = page.waitForResponse((response) =>
      response.url().includes("/api/backend/auth/me") &&
      response.request().method() === "GET",
    );
    await page.goto("/profile");

    expect((await profileResponse).status()).toBe(200);
    await expect(page.getByRole("heading", { name: "Master Chef Nam" })).toBeVisible();
    await expect(page.getByText("chef@example.com")).toBeVisible();
    await expect(page.getByText("Initial culinary bio")).toBeVisible();
    await page.getByRole("button", { name: "Chỉnh sửa" }).click();
    await page.locator("#edit-display-name").fill("Master Chef Phu Nam");
    await page.locator("#edit-bio").fill("Refined master of traditional Vietnamese spices.");
    const updateResponse = page.waitForResponse((response) =>
      response.url().includes("/api/backend/auth/me") &&
      response.request().method() === "PATCH",
    );
    await page.locator("#save-profile-button").click();
    expect((await updateResponse).status()).toBe(200);

    await expect(page.getByText("Cập nhật hồ sơ thành công!")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Master Chef Phu Nam" })).toBeVisible();
    await expect(page.getByText("Refined master of traditional Vietnamese spices.")).toBeVisible();
  });

  test("validates an invalid avatar URL before sending a Profile update", async ({ page }) => {
    await signInAs(page);
    await page.goto("/profile");
    await page.getByRole("button", { name: "Chỉnh sửa" }).click();
    await page.locator("#edit-avatar-url").fill("ftp://invalid-avatar-url.jpg");
    await page.locator("#save-profile-button").click();

    await expect(page.getByText(/URL ảnh đại diện phải có giao thức http:\/\/ hoặc https:\/\//)).toBeVisible();
    await expect(page.locator("#save-profile-button")).toBeVisible();
  });

  test("cancels Profile editing without changing the saved value", async ({ page }) => {
    await setProfile(page, "chef@example.com", {
      id: "chef-123",
      email: "chef@example.com",
      displayName: "Original Chef Name",
      avatarUrl: null,
      bio: "Original Bio",
      emailConfirmed: true,
      createdAt: "2026-01-15T00:00:00Z",
      roles: ["Author"],
    });
    await signInAs(page);
    await page.goto("/profile");
    await page.getByRole("button", { name: "Chỉnh sửa" }).click();
    await page.locator("#edit-display-name").fill("Discarded Name");
    await page.locator("#cancel-edit-button").click();

    await expect(page.locator("#save-profile-button")).not.toBeVisible();
    await expect(page.getByRole("heading", { name: "Original Chef Name" })).toBeVisible();
  });
});
