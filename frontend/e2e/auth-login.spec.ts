import { test, expect } from "@playwright/test";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, GET, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, Authorization",
};

test.describe("FR-AUTH-002: Login Flow & Redirect E2E", () => {
  test.beforeEach(async ({ page }) => {
    // Mock GIS Google library if loaded
    await page.addInitScript(() => {
      (window as any).google = {
        accounts: {
          id: {
            initialize: (config: any) => {
              (window as any).__googleGisConfig = config;
            },
            renderButton: () => { },
          },
        },
      };
    });
  });

  test("should login with email and password, save tokens, and redirect to homepage / with user name in navbar", async ({ page }) => {
    // Intercept backend login call
    await page.route("**/api/v1/auth/login", async (route) => {
      if (route.request().method() === "OPTIONS") {
        await route.fulfill({ status: 204, headers: CORS_HEADERS });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: CORS_HEADERS,
        contentType: "application/json",
        body: JSON.stringify({
          accessToken: "sample_e2e_jwt_access_token",
          refreshToken: "sample_e2e_refresh_token",
          expiresIn: 900,
        }),
      });
    });

    // Intercept profile call to return user details
    await page.route("**/api/v1/auth/me", async (route) => {
      if (route.request().method() === "OPTIONS") {
        await route.fulfill({ status: 204, headers: CORS_HEADERS });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: CORS_HEADERS,
        contentType: "application/json",
        body: JSON.stringify({
          id: "user-nam-123",
          email: "chef@example.com",
          displayName: "Nguyễn Phạm Phú Nam",
          avatarUrl: null,
          bio: "Đầu bếp ẩm thực Việt",
          emailConfirmed: true,
          createdAt: "2026-10-01T00:00:00Z",
          roles: ["Author"],
        }),
      });
    });

    // 1. Open /auth/login
    await page.goto("/auth/login");

    // Verify initial header & guest state
    await expect(page.getByRole("heading", { name: "Đăng nhập Culinary Blog" })).toBeVisible();

    // 2. Fill valid account
    await page.getByLabel("Địa chỉ Email").fill("chef@example.com");
    await page.getByLabel("Mật khẩu *", { exact: true }).fill("Password123!");

    // 3. Submit
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();

    // 4. Verify automatic redirection to homepage /
    await expect(page).toHaveURL("http://localhost:3000/", { timeout: 10000 });

    // 5. Verify tokens stored in sessionStorage
    const storedToken = await page.evaluate(() => sessionStorage.getItem("culinary_access_token"));
    expect(storedToken).toBe("sample_e2e_jwt_access_token");

    // 6. Verify navbar recognizes authenticated state and displays user name
    await expect(page.getByTestId("nav-profile")).toBeVisible();
    await expect(page.getByTestId("nav-profile")).toContainText("Nguyễn Phạm Phú Nam");
  });

  test("should display error message and stay on /auth/login when credentials are invalid", async ({ page }) => {
    await page.route("**/api/v1/auth/login", async (route) => {
      if (route.request().method() === "OPTIONS") {
        await route.fulfill({ status: 204, headers: CORS_HEADERS });
        return;
      }
      await route.fulfill({
        status: 401,
        headers: CORS_HEADERS,
        contentType: "application/json",
        body: JSON.stringify({
          status: 401,
          title: "Unauthorized",
          detail: "Email hoặc mật khẩu không chính xác.",
        }),
      });
    });

    await page.goto("/auth/login");
    await page.getByLabel("Địa chỉ Email").fill("wrong@example.com");
    await page.getByLabel("Mật khẩu *", { exact: true }).fill("WrongPassword");
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();

    // Must stay on /auth/login and show error
    await expect(page.getByText("Email hoặc mật khẩu không chính xác.")).toBeVisible();
    await expect(page).toHaveURL(/.*\/auth\/login/);

    // No token in sessionStorage
    const storedToken = await page.evaluate(() => sessionStorage.getItem("culinary_access_token"));
    expect(storedToken).toBeNull();
  });

  test("should not redirect if token saving fails in sessionStorage", async ({ page }) => {
    await page.route("**/api/v1/auth/login", async (route) => {
      if (route.request().method() === "OPTIONS") {
        await route.fulfill({ status: 204, headers: CORS_HEADERS });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: CORS_HEADERS,
        contentType: "application/json",
        body: JSON.stringify({
          accessToken: "sample_token",
          refreshToken: "sample_refresh",
          expiresIn: 900,
        }),
      });
    });

    // Simulate quota exceeded error in sessionStorage
    await page.addInitScript(() => {
      const origSetItem = Storage.prototype.setItem;
      Storage.prototype.setItem = function (key, value) {
        if (key === "culinary_refresh_token") {
          throw new DOMException("QuotaExceededError", "QuotaExceededError");
        }
        return origSetItem.apply(this, [key, value]);
      };
    });

    await page.goto("/auth/login");
    await page.getByLabel("Địa chỉ Email").fill("chef@example.com");
    await page.getByLabel("Mật khẩu *", { exact: true }).fill("Password123!");
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();

    // Must stay on login page and show error message
    await expect(page.getByText("Không thể lưu phiên đăng nhập")).toBeVisible();
    await expect(page).toHaveURL(/.*\/auth\/login/);

    // Rollback atomic: both tokens should be null
    const accessToken = await page.evaluate(() => sessionStorage.getItem("culinary_access_token"));
    const refreshToken = await page.evaluate(() => sessionStorage.getItem("culinary_refresh_token"));
    expect(accessToken).toBeNull();
    expect(refreshToken).toBeNull();
  });

  test("should login with Google callback, save tokens, and redirect to homepage / with user name in navbar", async ({ page }) => {
    // Intercept Google login API
    await page.route("**/api/v1/auth/google", async (route) => {
      if (route.request().method() === "OPTIONS") {
        await route.fulfill({ status: 204, headers: CORS_HEADERS });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: CORS_HEADERS,
        contentType: "application/json",
        body: JSON.stringify({
          accessToken: "google_jwt_access_token_xyz",
          refreshToken: "google_jwt_refresh_token_xyz",
          expiresIn: 900,
        }),
      });
    });

    // Intercept profile call for Google user
    await page.route("**/api/v1/auth/me", async (route) => {
      if (route.request().method() === "OPTIONS") {
        await route.fulfill({ status: 204, headers: CORS_HEADERS });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: CORS_HEADERS,
        contentType: "application/json",
        body: JSON.stringify({
          id: "google-nam-456",
          email: "2312695@dlu.edu.vn",
          displayName: "Nguyễn Phạm Phú Nam",
          avatarUrl: null,
          bio: null,
          emailConfirmed: true,
          createdAt: "2026-10-01T00:00:00Z",
          roles: ["Member"],
        }),
      });
    });

    await page.goto("/auth/login");

    // Wait for GIS configuration and trigger Google login callback
    await page.waitForFunction(() => Boolean((window as any).__googleGisConfig?.callback));
    await page.evaluate(async () => {
      await (window as any).__googleGisConfig.callback({ credential: "mock_google_id_token_123" });
    });

    // Verify automatic redirect to homepage /
    await expect(page).toHaveURL("http://localhost:3000/", { timeout: 10000 });

    // Verify tokens stored
    const storedToken = await page.evaluate(() => sessionStorage.getItem("culinary_access_token"));
    expect(storedToken).toBe("google_jwt_access_token_xyz");

    // Verify navbar displays actual user name
    await expect(page.getByTestId("nav-profile")).toBeVisible();
    await expect(page.getByTestId("nav-profile")).toContainText("Nguyễn Phạm Phú Nam");
  });
});
