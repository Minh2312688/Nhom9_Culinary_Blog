import { expect, test } from "@playwright/test";

test("shows loading feedback while the author server fetch is pending", async ({ page }) => {
  await page.goto("/authors/slow-author", { waitUntil: "commit" });
  await expect(page.getByRole("status")).toHaveText("Đang tải hồ sơ tác giả...");
  await expect(page.getByRole("heading", { name: "Linh Nguyen" })).toBeVisible();
});

test("renders public author information and paginates only published recipes", async ({ page }) => {
  const response = await page.goto("/authors/author-1?page=1");
  expect(response).not.toBeNull();
  const html = await response!.text();
  expect(html).not.toContain("linh@example.com");

  await expect(page.getByRole("heading", { name: "Linh Nguyen" })).toBeVisible();
  await expect(page.getByText("A home cook sharing practical recipes.")).toBeVisible();
  await expect(page.getByRole("link", { name: "Roasted tomato soup" })).toBeVisible();
  await expect(page.getByText("linh@example.com")).toHaveCount(0);

  await page.getByRole("link", { name: "Trang sau" }).click();
  await expect(page).toHaveURL(/\/authors\/author-1\?page=2/);
  await expect(page.getByRole("link", { name: "Citrus lentil salad" })).toBeVisible();
});
