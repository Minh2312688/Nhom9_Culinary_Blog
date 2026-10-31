import { expect, test } from "@playwright/test";

const viewports = [
  { name: "Desktop 1280x800", width: 1280, height: 800 },
  { name: "Tablet 768x1024", width: 768, height: 1024 },
  { name: "Mobile 375x667", width: 375, height: 667 },
];

const routes = ["/", "/recipes", "/recipes/pho-bo", "/search", "/dashboard"];

test.describe("Responsive – no horizontal overflow", () => {
  for (const vp of viewports) {
    for (const route of routes) {
      test(`${route} @ ${vp.name}`, async ({ page }) => {
        await page.setViewportSize({ width: vp.width, height: vp.height });
        await page.goto(route);
        await page.locator("main").waitFor({ state: "visible" });
        await page.waitForLoadState("domcontentloaded");

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

        await expect(page.locator("main")).toBeVisible();

        const h1 = page.locator("h1");
        if ((await h1.count()) > 0) {
          await expect(h1.first()).toBeVisible();
        }
      });
    }
  }
});

