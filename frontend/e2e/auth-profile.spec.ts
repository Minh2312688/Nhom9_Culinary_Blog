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

  test("should allow editing profile and save changes successfully (FR-AUTH-007)", async ({ page }) => {
    let currentProfile = {
      id: "chef-123",
      email: "chef@example.com",
      displayName: "Master Chef Nam",
      avatarUrl: "https://example.com/initial-avatar.jpg",
      bio: "Initial culinary bio",
      emailConfirmed: true,
      createdAt: "2026-01-15T00:00:00Z",
      roles: ["Author"],
    };

    await page.route("**/api/v1/auth/me", async (route) => {
      if (route.request().method() === "GET") {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(currentProfile),
        });
      } else if (route.request().method() === "PATCH") {
        const payload = route.request().postDataJSON();
        currentProfile = {
          ...currentProfile,
          displayName: payload.displayName || currentProfile.displayName,
          avatarUrl: payload.avatarUrl !== undefined ? payload.avatarUrl : currentProfile.avatarUrl,
          bio: payload.bio !== undefined ? payload.bio : currentProfile.bio,
        };
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(currentProfile),
        });
      }
    });

    await page.addInitScript(() => {
      sessionStorage.setItem("culinary_access_token", "mock_access_token");
      sessionStorage.setItem("culinary_refresh_token", "mock_refresh_token");
    });

    await page.goto("/profile");

    // 1. Enter edit mode
    const editBtn = page.getByRole("button", { name: "Chỉnh sửa" });
    await expect(editBtn).toBeVisible();
    await editBtn.click();

    // 2. Form should be visible and prefilled
    const displayNameInput = page.locator("#edit-display-name");
    await expect(displayNameInput).toHaveValue("Master Chef Nam");

    // 3. Edit DisplayName and Bio
    await displayNameInput.fill("Master Chef Phu Nam");
    const bioInput = page.locator("#edit-bio");
    await bioInput.fill("Refined master of traditional Vietnamese spices.");

    // 4. Submit save
    const saveBtn = page.locator("#save-profile-button");
    await saveBtn.click();

    // 5. Verify success feedback and updated profile content rendered
    await expect(page.getByText("Cập nhật hồ sơ thành công!")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Master Chef Phu Nam" })).toBeVisible();
    await expect(page.getByText("Refined master of traditional Vietnamese spices.")).toBeVisible();
  });

  test("should show validation error when invalid avatar URL is entered (FR-AUTH-007)", async ({ page }) => {
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

    await page.addInitScript(() => {
      sessionStorage.setItem("culinary_access_token", "mock_access_token");
      sessionStorage.setItem("culinary_refresh_token", "mock_refresh_token");
    });

    await page.goto("/profile");
    await page.getByRole("button", { name: "Chỉnh sửa" }).click();

    // Fill invalid avatar URL
    await page.locator("#edit-avatar-url").fill("ftp://invalid-avatar-url.jpg");
    await page.locator("#save-profile-button").click();

    // Must show validation error and stay in edit form
    await expect(page.getByText(/URL ảnh đại diện phải có giao thức http:\/\/ hoặc https:\/\//)).toBeVisible();
    await expect(page.locator("#save-profile-button")).toBeVisible();
  });

  test("should cancel edit mode and retain original values without saving (FR-AUTH-007)", async ({ page }) => {
    await page.route("**/api/v1/auth/me", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          id: "chef-123",
          email: "chef@example.com",
          displayName: "Original Chef Name",
          avatarUrl: null,
          bio: "Original Bio",
          emailConfirmed: true,
          createdAt: "2026-01-15T00:00:00Z",
          roles: ["Author"],
        }),
      });
    });

    await page.addInitScript(() => {
      sessionStorage.setItem("culinary_access_token", "mock_access_token");
      sessionStorage.setItem("culinary_refresh_token", "mock_refresh_token");
    });

    await page.goto("/profile");
    await page.getByRole("button", { name: "Chỉnh sửa" }).click();

    // Modify field then cancel
    await page.locator("#edit-display-name").fill("Discarded Name");
    await page.locator("#cancel-edit-button").click();

    // Edit form should close and original display name remains
    await expect(page.locator("#save-profile-button")).not.toBeVisible();
    await expect(page.getByRole("heading", { name: "Original Chef Name" })).toBeVisible();
  });
});
