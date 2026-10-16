import { expect, test } from "@playwright/test";

const brandedRoutes = [
  "/",
  "/recipes",
  "/recipes/recipe-with-author",
  "/categories",
  "/categories/category-1",
  "/search",
  "/authors/author-1",
  "/auth/login",
  "/auth/register",
  "/profile",
  "/dashboard",
  "/dashboard/recipes",
  "/dashboard/recipes/new",
  "/dashboard/recipes/recipe-1/edit",
  "/dashboard/categories",
];

test("uses the Culinary Blog brand across existing app routes", async ({ page }) => {
  test.setTimeout(60_000);
  await page.route("https://accounts.google.com/**", (route) => route.abort());
  await page.addInitScript(() => {
    sessionStorage.setItem("culinary_access_token", "brand-acceptance-token");
  });

  for (const route of brandedRoutes) {
    await page.goto(route, { waitUntil: "domcontentloaded" });
    await expect(page).toHaveTitle(/Culinary Blog/);
    await expect(page.getByRole("link", { name: "Culinary Blog, trang chủ" })).toBeVisible();
  }
});

test("keeps the homepage hero visible before Motion hydrates", async ({ page }) => {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  const heading = page.getByRole("heading", { name: "Mỗi món ngon bắt đầu từ một câu chuyện." });
  await expect(heading).toBeAttached();
  const opacity = await heading.locator("xpath=../..").evaluate((element) =>
    Number.parseFloat(getComputedStyle(element).opacity),
  );

  expect(opacity).toBeGreaterThanOrEqual(1);
});

test("keeps the shared brand color pairs readable", async ({ page }) => {
  await page.goto("/");
  const contrastRatios = await page.evaluate(() => {
    const styles = getComputedStyle(document.documentElement);
    const parse = (value: string) => {
      const match = value.match(/^#([0-9a-f]{6})$/i);
      if (!match) return null;
      const channels = match[1].match(/.{2}/g)?.map((channel) => parseInt(channel, 16) / 255);
      return channels?.map((channel) =>
        channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4,
      ) ?? null;
    };
    const ratio = (foreground: string, background: string) => {
      const fg = parse(foreground);
      const bg = parse(background);
      if (!fg || !bg) return 0;
      const luminance = (rgb: number[]) => 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
      const [lighter, darker] = [luminance(fg), luminance(bg)].sort((a, b) => b - a);
      return (lighter + 0.05) / (darker + 0.05);
    };
    const token = (name: string) => styles.getPropertyValue(name).trim();
    return [
      ratio(token("--color-ink"), token("--color-cream")),
      ratio(token("--color-teal"), token("--color-cream")),
      ratio(token("--color-on-coral"), token("--color-coral")),
      ratio(token("--color-teal"), token("--color-turmeric")),
    ];
  });

  expect(contrastRatios[0]).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatios[1]).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatios[2]).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatios[3]).toBeGreaterThanOrEqual(4.5);
});

test("reduces card transitions when reduced motion is requested", async ({ page }) => {
  await page.goto("/authors/author-1");
  const recipeCard = page.getByRole("link", { name: "Roasted tomato soup" });
  await expect(recipeCard).toBeVisible();
  const normalDuration = await recipeCard.evaluate((element) =>
    getComputedStyle(element).transitionDuration.split(",")[0],
  );
  expect(Number.parseFloat(normalDuration)).toBeGreaterThan(0.1);

  await page.emulateMedia({ reducedMotion: "reduce" });
  const reducedDuration = await recipeCard.evaluate((element) =>
    getComputedStyle(element).transitionDuration.split(",")[0],
  );
  expect(Number.parseFloat(reducedDuration)).toBeLessThanOrEqual(0.001);
});

test("avoids horizontal overflow at supported viewport widths", async ({ page }) => {
  for (const route of ["/", "/search", "/dashboard/recipes/new"]) {
    await page.goto(route);
    for (const width of [375, 768, 1024, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
      expect(scrollWidth, `${route} at ${width}px`).toBeLessThanOrEqual(width);
    }
  }
});
