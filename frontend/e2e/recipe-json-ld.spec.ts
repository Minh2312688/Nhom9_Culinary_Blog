import { expect, test } from "@playwright/test";

test("renders the published author and publication date in initial Recipe JSON-LD", async ({ page }) => {
  const response = await page.goto("/recipes/recipe-with-author");
  expect(response).not.toBeNull();
  const html = await response!.text();

  expect(html).toContain('"datePublished":"2026-10-01T09:30:00Z"');
  expect(html).toContain('"author":{"@type":"Person","name":"Linh Nguyen"}');

  const jsonLd = JSON.parse(await page.locator('script[type="application/ld+json"]').textContent() ?? "{}");
  expect(jsonLd.datePublished).toBe("2026-10-01T09:30:00Z");
  expect(jsonLd.author.name).toBe("Linh Nguyen");

  await page.getByRole("link", { name: "Linh Nguyen" }).click();
  await expect(page).toHaveURL(/\/authors\/author-1(?:\?|$)/);
  await expect(page.getByRole("heading", { name: "Linh Nguyen" })).toBeVisible();
});

test("omits the publication date for a historical recipe in UI and JSON-LD", async ({ page }) => {
  await page.goto("/recipes/historical-recipe");

  const jsonLd = JSON.parse(await page.locator('script[type="application/ld+json"]').textContent() ?? "{}");
  expect("datePublished" in jsonLd).toBe(false);
  await expect(page.locator("time")).toHaveCount(0);
});
