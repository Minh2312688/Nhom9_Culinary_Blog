import { expect, test } from "@playwright/test";

test("site typography loads the Vietnamese font for body text and headings", async ({ page }) => {
  await page.goto("/");

  const typography = await page.evaluate(async () => {
    await document.fonts.ready;
    return {
      body: getComputedStyle(document.body).fontFamily,
      heading: getComputedStyle(document.querySelector("h1")!).fontFamily,
      vietnameseFontLoaded: Array.from(document.fonts).some(
        (font) => font.family === "Be Vietnam Pro" && font.status === "loaded",
      ),
      vietnameseGlyphsAvailable: document.fonts.check('16px "Be Vietnam Pro"', "bắt"),
    };
  });
  expect(typography.body).toContain("Be Vietnam Pro");
  expect(typography.heading).toContain("Be Vietnam Pro");
  expect(typography.vietnameseFontLoaded).toBe(true);
  expect(typography.vietnameseGlyphsAvailable).toBe(true);
});
