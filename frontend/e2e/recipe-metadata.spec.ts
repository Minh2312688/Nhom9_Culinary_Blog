import { expect, test } from "@playwright/test";

test("renders root layout default Open Graph and Twitter image metadata exactly once", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveTitle("Culinary Blog - Khám phá Ẩm thực");

  const ogImages = page.locator('meta[property="og:image"]');
  await expect(ogImages).toHaveCount(1);
  const ogImageUrl = await ogImages.first().getAttribute("content");
  expect(ogImageUrl).toBeTruthy();
  expect(ogImageUrl).toContain("/opengraph-image");

  const twitterImages = page.locator('meta[name="twitter:image"]');
  await expect(twitterImages).toHaveCount(1);
  const twitterImageUrl = await twitterImages.first().getAttribute("content");
  expect(twitterImageUrl).toBeTruthy();
  expect(twitterImageUrl).toContain("/twitter-image");

  const twitterCard = page.locator('meta[name="twitter:card"]');
  await expect(twitterCard).toHaveAttribute("content", "summary_large_image");

  // Root page alt text must be non-empty
  const ogImageAlt = page.locator('meta[property="og:image:alt"]');
  await expect(ogImageAlt).toHaveCount(1);
  const altValue = await ogImageAlt.first().getAttribute("content");
  expect(altValue).toBeTruthy();
  expect(altValue!.length).toBeGreaterThan(0);
});

test("renders recipe Open Graph metadata for recipe with image", async ({ page }) => {
  await page.goto("/recipes/pho-bo");

  await expect(page).toHaveTitle("Phở bò | Culinary Blog");

  const ogTitle = page.locator('meta[property="og:title"]');
  await expect(ogTitle).toHaveAttribute("content", "Phở bò");

  const ogDescription = page.locator('meta[property="og:description"]');
  await expect(ogDescription).toHaveAttribute("content", "Nước dùng đậm đà");

  const ogType = page.locator('meta[property="og:type"]');
  await expect(ogType).toHaveAttribute("content", "article");

  const ogImage = page.locator('meta[property="og:image"]');
  await expect(ogImage).toHaveCount(1);
  await expect(ogImage).toHaveAttribute("content", "https://storage.example.test/original.avif");

  // pho-bo mock has altText "Phở bò" → og:image:alt must match
  const ogImageAlt = page.locator('meta[property="og:image:alt"]');
  await expect(ogImageAlt).toHaveCount(1);
  await expect(ogImageAlt).toHaveAttribute("content", "Phở bò");
});

test("renders default Open Graph image for recipe without custom image", async ({ page }) => {
  await page.goto("/recipes/recipe-with-author");

  const ogImage = page.locator('meta[property="og:image"]');
  await expect(ogImage).toHaveCount(1);
  const ogImageUrl = await ogImage.first().getAttribute("content");
  expect(ogImageUrl).toBeTruthy();
  expect(ogImageUrl).toContain("/opengraph-image");
});

test("default opengraph-image and twitter-image are valid 1200x630 PNG", async ({ page }) => {
  const PNG_SIGNATURE = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

  for (const path of ["/opengraph-image", "/twitter-image"]) {
    const response = await page.request.get(path);
    expect(response.status(), `${path} status`).toBe(200);

    const contentType = response.headers()["content-type"] ?? "";
    expect(contentType, `${path} content-type`).toContain("image/png");

    const body = await response.body();
    // Verify PNG signature (first 8 bytes)
    for (let i = 0; i < 8; i++) {
      expect(body[i], `${path} PNG signature byte ${i}`).toBe(PNG_SIGNATURE[i]);
    }

    // Read IHDR width (bytes 16-19) and height (bytes 20-23), big-endian
    const width = body.readUInt32BE(16);
    const height = body.readUInt32BE(20);
    expect(width, `${path} width`).toBe(1200);
    expect(height, `${path} height`).toBe(630);
  }
});
