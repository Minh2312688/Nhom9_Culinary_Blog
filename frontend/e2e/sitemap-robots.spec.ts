import { expect, test } from "@playwright/test";

test("sitemap.xml returns valid XML containing public static and published recipe routes", async ({ request }) => {
  const response = await request.get("/sitemap.xml");
  expect(response.status()).toBe(200);

  const body = await response.text();

  // Published recipe routes from mock API
  expect(body).toContain("/recipes/recipe-with-author");
  expect(body).toContain("/recipes/citrus-lentil-salad");

  // Public static routes
  expect(body).toContain("/recipes");
  expect(body).toContain("/categories");

  // Private routes must NOT appear in sitemap
  expect(body).not.toContain("/dashboard");
  expect(body).not.toContain("/auth");
  expect(body).not.toContain("/profile");
  expect(body).not.toContain("/api");

  // Required XML tags
  expect(body).toContain("<lastmod>");
  expect(body).toContain("<changefreq>");
  expect(body).toContain("<priority>");
});

test("robots.txt disallows private routes and references sitemap.xml", async ({ request }) => {
  const response = await request.get("/robots.txt");
  expect(response.status()).toBe(200);

  const body = await response.text();

  expect(body).toMatch(/User-Agent:\s*\*/i);
  expect(body).toContain("Allow: /");
  expect(body).toContain("Disallow: /auth");
  expect(body).toContain("Disallow: /dashboard");
  expect(body).toContain("Disallow: /profile");
  expect(body).toContain("Disallow: /api");
  expect(body).toMatch(/Sitemap:\s*https?:\/\/.*\/sitemap\.xml/);
});

