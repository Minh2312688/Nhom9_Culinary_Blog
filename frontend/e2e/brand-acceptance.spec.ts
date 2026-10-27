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
      const hex = value.match(/^#([0-9a-f]{6})$/i);
      const rgb = value.match(/^rgba?\(([^)]+)\)$/i);
      const channels = hex
        ? hex[1].match(/.{2}/g)?.map((channel) => parseInt(channel, 16) / 255)
        : rgb?.[1].match(/[\d.]+/g)?.slice(0, 3).map((channel) => Number(channel) / 255);
      if (!channels) return null;
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
    const primary = document.querySelector('main a[href="/recipes"]');
    const secondary = document.querySelector('main a[href="/categories"]');
    const primaryStyle = primary ? getComputedStyle(primary) : null;
    const secondaryStyle = secondary ? getComputedStyle(secondary) : null;
    return [
      ratio(token("--color-ink"), token("--color-cream")),
      ratio(token("--color-teal"), token("--color-cream")),
      ratio(token("--color-on-coral"), token("--color-coral")),
      ratio(token("--color-teal"), token("--color-turmeric")),
      primaryStyle ? ratio(primaryStyle.color, primaryStyle.backgroundColor) : 0,
      secondaryStyle ? ratio(secondaryStyle.color, secondaryStyle.backgroundColor) : 0,
    ];
  });

  expect(contrastRatios[0]).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatios[1]).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatios[2]).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatios[3]).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatios[4]).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatios[5]).toBeGreaterThanOrEqual(4.5);
});

test("renders the profile's alpha-tinted brand gradient", async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem("culinary_access_token", "brand-acceptance-token");
  });
  await page.goto("/profile", { waitUntil: "domcontentloaded" });
  const backgroundImage = await page.locator(".bg-gradient-to-br").evaluate((element) =>
    getComputedStyle(element).backgroundImage,
  );

  expect(backgroundImage).toContain("linear-gradient");
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

test("disables the homepage hero entrance motion for reduced motion", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");

  const heading = page.getByRole("heading", { name: "Mỗi món ngon bắt đầu từ một câu chuyện." });
  await expect(heading).toBeVisible();
  const heroMotion = heading.locator("xpath=../..");
  const motionStyles = await heroMotion.evaluate((element) => {
    const styles = getComputedStyle(element);
    return { opacity: styles.opacity, transform: styles.transform, transitionDuration: styles.transitionDuration };
  });

  expect(motionStyles.opacity).toBe("1");
  expect(motionStyles.transform).toMatch(/^(none|matrix\(1, 0, 0, 1, 0, 0\))$/);
  expect(Number.parseFloat(motionStyles.transitionDuration.split(",")[0])).toBeLessThanOrEqual(0.001);
});

test("avoids horizontal overflow at supported viewport widths", async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem("culinary_access_token", "brand-acceptance-token");
  });

  for (const route of [
    "/",
    "/recipes/recipe-with-author",
    "/authors/author-1",
    "/search",
    "/profile",
    "/auth/login",
    "/dashboard/recipes/new",
  ]) {
    await page.goto(route);
    for (const width of [375, 768, 1024, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
      expect(scrollWidth, `${route} at ${width}px`).toBeLessThanOrEqual(width);
    }
  }
});
