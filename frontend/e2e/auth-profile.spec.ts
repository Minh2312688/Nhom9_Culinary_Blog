import { test, expect } from "@playwright/test";

test.describe("FR-AUTH-006 & FR-AUTH-005: Profile & Logout E2E", () => {
  test("should redirect unauthenticated user to /auth/login", async ({ page }) => {
    // Navigate directly to /profile with empty sessionStorage
    await page.goto("/profile");

    // Must be redirected to /auth/login
    await expect(page).toHaveURL(/.*\/auth\/login/);
  });

  test("should render profile for authenticated user", async ({ page }) => {
    // Intercept GET /api/v1/auth/me
    await page.route("**/api/v1/auth/me", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          id: "chef-123",
          email: "chef@example.com",
          displayName: "Master Chef Nam",
          avatarUrl: null,
          bio: "Passionate Vietnamese culinary artist",
          emailConfirmed: true,
          createdAt: "2026-01-15T00:00:00Z",
          roles: ["Author"],
        }),
      });
    });

    // Seed sessionStorage with access token before loading page
    await page.addInitScript(() => {
      sessionStorage.setItem("culinary_access_token", "mock_access_token_123");
      sessionStorage.setItem("culinary_refresh_token", "mock_refresh_token_123");
    });

    await page.goto("/profile");

    // Verify rendered profile elements
    await expect(page.getByRole("heading", { name: "Master Chef Nam" })).toBeVisible();
    await expect(page.getByText("chef@example.com")).toBeVisible();
    await expect(page.getByText("Author")).toBeVisible();
    await expect(page.getByText("Passionate Vietnamese culinary artist")).toBeVisible();
    await expect(page.getByRole("button", { name: "Đăng xuất" })).toBeVisible();
  });

  test("should rotate token on 401 and retry successfully", async ({ page }) => {
    let meCallCount = 0;

    await page.route("**/api/v1/auth/me", async (route) => {
      meCallCount++;
      if (meCallCount === 1) {
        // First attempt: token expired
        await route.fulfill({
          status: 401,
          contentType: "application/json",
          body: JSON.stringify({
            title: "Unauthorized",
            detail: "Token expired",
          }),
        });
      } else {
        // Second attempt after refresh: success
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            id: "chef-123",
            email: "chef@example.com",
            displayName: "Master Chef Nam",
            avatarUrl: null,
            bio: "Refreshed session user",
            emailConfirmed: true,
            createdAt: "2026-01-15T00:00:00Z",
            roles: ["Author"],
          }),
        });
      }
    });

    await page.route("**/api/v1/auth/refresh", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          accessToken: "new_rotated_access_token",
          refreshToken: "new_rotated_refresh_token",
          expiresIn: 900,
        }),
      });
    });

    await page.addInitScript(() => {
      sessionStorage.setItem("culinary_access_token", "expired_access_token");
      sessionStorage.setItem("culinary_refresh_token", "valid_refresh_token");
    });

    await page.goto("/profile");

    // Profile should render after automatic refresh-and-retry
    await expect(page.getByRole("heading", { name: "Master Chef Nam" })).toBeVisible();
    await expect(page.getByText("Refreshed session user")).toBeVisible();
  });

  test("should logout and redirect to /auth/login", async ({ page }) => {
    await page.route("**/api/v1/auth/me", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          id: "chef-123",
          email: "chef@example.com",
          displayName: "Master Chef Nam",
          avatarUrl: null,
          bio: null,
          emailConfirmed: true,
          createdAt: "2026-01-15T00:00:00Z",
          roles: ["Author"],
        }),
      });
    });

    let logoutCalled = false;
    await page.route("**/api/v1/auth/logout", async (route) => {
      logoutCalled = true;
      await route.fulfill({
        status: 204,
      });
    });

    await page.addInitScript(() => {
      sessionStorage.setItem("culinary_access_token", "mock_access_token");
      sessionStorage.setItem("culinary_refresh_token", "mock_refresh_token");
    });

    await page.goto("/profile");

    // Click logout
    const logoutBtn = page.getByRole("button", { name: "Đăng xuất" });
    await expect(logoutBtn).toBeVisible();
    await logoutBtn.click();

    // Verify redirected to /auth/login and logout was called
    await expect(page).toHaveURL(/.*\/auth\/login/);
    expect(logoutCalled).toBe(true);

    // Verify tokens were cleared from sessionStorage
    const token = await page.evaluate(() => sessionStorage.getItem("culinary_access_token"));
    expect(token).toBeNull();
  });
});
