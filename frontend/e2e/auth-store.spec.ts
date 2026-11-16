import { expect, test } from "@playwright/test";
import { signInAs } from "./support/auth";

test.beforeEach(async ({ page }) => {
  const reset = await page.context().request.post("http://127.0.0.1:5055/__test/auth/reset");
  expect(reset.status()).toBe(204);
});

test("UI sign-in keeps backend tokens out of browser storage and the Session response", async ({ page }) => {
  await signInAs(page);

  const sessionResponse = await page.context().request.get("/api/auth/session");
  expect(sessionResponse.ok()).toBe(true);
  const session = await sessionResponse.json();
  expect(session.user).toMatchObject({
    email: "chef@example.com",
    name: "Chef Example",
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
