import { expect, test } from "@playwright/test";
import { signInAsByApi } from "./support/auth";

const viewports = [
  { name: "Mobile minimum 320x667", width: 320, height: 667 },
  { name: "Mobile maximum 767x900", width: 767, height: 900 },
  { name: "Tablet minimum 768x1024", width: 768, height: 1024 },
  { name: "Tablet maximum 1199x900", width: 1199, height: 900 },
  { name: "Desktop minimum 1200x900", width: 1200, height: 900 },
];

const routes = [
  "/",
  "/recipes",
  "/recipes/pho-bo",
  "/categories",
  "/categories/mon-chinh",
  "/search",
  "/authors/author-1",
  "/dashboard",
  "/dashboard/recipes",
  "/dashboard/recipes/new",
  "/dashboard/recipes/00000000-0000-0000-0000-000000000001/edit",
  "/dashboard/categories",
];

const protectedRoutes = new Set([
  "/dashboard",
  "/dashboard/recipes",
  "/dashboard/recipes/new",
  "/dashboard/recipes/00000000-0000-0000-0000-000000000001/edit",
  "/dashboard/categories",
]);

test.describe("Responsive – no horizontal overflow", () => {
  for (const vp of viewports) {
    for (const route of routes) {
      test(`${route} @ ${vp.name}`, async ({ page }) => {
        await page.setViewportSize({ width: vp.width, height: vp.height });
        if (protectedRoutes.has(route)) {
          const email = route.startsWith("/dashboard")
            ? "admin@example.com"
            : "chef@example.com";
          await signInAsByApi(page, email);
        }
        await page.goto(route, { waitUntil: "domcontentloaded" });
        const main = page.locator("main").first();
        await main.waitFor({ state: "visible" });

        const { scrollWidth, clientWidth } = await page.evaluate(() => ({
          scrollWidth: document.documentElement.scrollWidth,
          clientWidth: document.documentElement.clientWidth,
        }));

        const hasOverflow = scrollWidth > clientWidth + 1;
        const result = hasOverflow ? "OVERFLOW" : "OK";
        console.log(`${route} | ${vp.name} | scrollWidth=${scrollWidth} | clientWidth=${clientWidth} | ${result}`);

        expect(
          scrollWidth,
          `Route ${route} @ ${vp.name}: scrollWidth (${scrollWidth}) phải <= clientWidth (${clientWidth}) + 1px`
        ).toBeLessThanOrEqual(clientWidth + 1);

        await expect(main).toBeVisible();

        const h1 = page.locator("h1");
        if ((await h1.count()) > 0) {
          await expect(h1.first()).toBeVisible();
        }
      });
    }
  }
});

