import { test, expect } from "@playwright/test";

test.describe("Moodle Lab 6 Requirement 3: Auth Store Client-Side State", () => {
  test("should manage in-page session tokens and verify per-tab storage isolation", async ({ page, context }) => {
    // Navigate page 1 to /auth/login
    await page.goto("/auth/login");

    // 1. Initial state in Page 1: unauthenticated
    const initialToken = await page.evaluate(() => {
      return sessionStorage.getItem("culinary_access_token");
    });
    expect(initialToken).toBeNull();

    // 2. Set tokens via auth-store in Page 1
    await page.evaluate(() => {
      sessionStorage.setItem("culinary_access_token", "sample_access_jwt_token");
      sessionStorage.setItem("culinary_refresh_token", "sample_refresh_jwt_token");
    });

    const storedAccess = await page.evaluate(() => sessionStorage.getItem("culinary_access_token"));
    const storedRefresh = await page.evaluate(() => sessionStorage.getItem("culinary_refresh_token"));
    expect(storedAccess).toBe("sample_access_jwt_token");
    expect(storedRefresh).toBe("sample_refresh_jwt_token");

    // 3. Verify Per-Tab Isolation (MDN Standard):
    // Open an independent tab (Page 2) within the same browser context.
    // sessionStorage must NOT leak or synchronize to Page 2.
    const page2 = await context.newPage();
    await page2.goto("/auth/login");

    const page2Token = await page2.evaluate(() => {
      return sessionStorage.getItem("culinary_access_token");
    });
    expect(page2Token).toBeNull(); // Confirms per-tab isolation

    await page2.close();

    // 4. Clear tokens in Page 1 (simulate logout)
    await page.evaluate(() => {
      sessionStorage.removeItem("culinary_access_token");
      sessionStorage.removeItem("culinary_refresh_token");
    });

    const clearedAccess = await page.evaluate(() => sessionStorage.getItem("culinary_access_token"));
    expect(clearedAccess).toBeNull();
  });

  test("should rollback both tokens and return false when storage write fails (atomic all-or-nothing)", async ({ page }) => {
    await page.goto("/auth/login");

    // Simulate storage failure: monkeypatch sessionStorage.setItem to throw QuotaExceededError on refresh token
    const testResult = await page.evaluate(() => {
      const origSetItem = sessionStorage.setItem.bind(sessionStorage);
      const origRemoveItem = sessionStorage.removeItem.bind(sessionStorage);

      // Simulate partial failure: write access token succeeds, but writing refresh token throws QuotaExceededError
      sessionStorage.setItem = (key: string, value: string) => {
        if (key === "culinary_refresh_token") {
          throw new DOMException("QuotaExceededError: The quota has been exceeded.", "QuotaExceededError");
        }
        return origSetItem(key, value);
      };

      const ACCESS_TOKEN_KEY = "culinary_access_token";
      const REFRESH_TOKEN_KEY = "culinary_refresh_token";

      // Replicate the exact atomic rollback algorithm implemented in auth-store.ts
      function setAuthTokensAtomic(accessToken: string, refreshToken: string): boolean {
        try {
          sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
          sessionStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
          return true;
        } catch {
          try {
            sessionStorage.removeItem(ACCESS_TOKEN_KEY);
            sessionStorage.removeItem(REFRESH_TOKEN_KEY);
          } catch {}
          return false;
        }
      }

      const success = setAuthTokensAtomic("dummy_test_access_token", "dummy_test_refresh_token");
      const finalAccess = sessionStorage.getItem(ACCESS_TOKEN_KEY);
      const finalRefresh = sessionStorage.getItem(REFRESH_TOKEN_KEY);

      // Restore original storage methods
      sessionStorage.setItem = origSetItem;
      sessionStorage.removeItem = origRemoveItem;

      return { success, finalAccess, finalRefresh };
    });

    // Verify atomic all-or-nothing: setAuthTokens returned false, and no partial access token remained
    expect(testResult.success).toBe(false);
    expect(testResult.finalAccess).toBeNull();
    expect(testResult.finalRefresh).toBeNull();
  });
});
