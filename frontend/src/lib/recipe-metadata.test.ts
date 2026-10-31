import assert from "node:assert/strict";
import test from "node:test";
import type { RecipeDetail } from "./recipes-api";
import { buildRecipeMetadata } from "./recipe-metadata.ts";

function recipe(overrides: Partial<RecipeDetail> = {}): RecipeDetail {
  return {
    id: "recipe-1",
    title: "Roasted tomato soup",
    slug: "roasted-tomato-soup",
    description: "A simple soup.",
    difficulty: "Easy",
    cookTimeMinutes: 30,
    prepTimeMinutes: 10,
    servings: 4,
    status: "Published",
    categoryId: "category-1",
    authorId: "author-1",
    rowVersion: "v1",
    publishedAt: "2026-10-01T09:30:00Z",
    author: { id: "author-1", displayName: "Linh Nguyen", avatarUrl: null },
    ingredients: [],
    steps: [],
    images: [],
    nutrition: null,
    ...overrides,
  };
}

test("openGraph đủ trường khi không có ảnh", () => {
  const meta = buildRecipeMetadata(recipe(), "https://example.com");

  assert.equal(meta.title, "Roasted tomato soup");
  assert.equal(meta.description, "A simple soup.");

  const og = meta.openGraph as Record<string, unknown>;
  assert.equal(og.siteName, "Culinary Blog");
  assert.equal(og.locale, "vi_VN");
  assert.equal(og.type, "article");
  assert.equal(og.url, "https://example.com/recipes/roasted-tomato-soup");
  assert.equal(og.title, "Roasted tomato soup");
  assert.equal(og.description, "A simple soup.");
  assert.equal(og.images, undefined);

  const twitter = meta.twitter as Record<string, unknown>;
  assert.equal(twitter.card, "summary_large_image");
  assert.equal(twitter.title, "Roasted tomato soup");
  assert.equal(twitter.description, "A simple soup.");
  assert.equal(twitter.images, undefined);
});

test("có ảnh: đặt ảnh vào openGraph và twitter", () => {
  const meta = buildRecipeMetadata(
    recipe({
      images: [
        {
          id: "img-1",
          originalUrl: "https://example.com/photo.jpg",
          mediumUrl: null,
          thumbnailUrl: null,
          altText: null,
          isPrimary: false,
          orderIndex: 0,
        },
      ],
    }),
    "https://example.com",
  );

  const og = meta.openGraph as Record<string, unknown>;
  assert.deepEqual(og.images, [{ url: "https://example.com/photo.jpg" }]);

  const twitter = meta.twitter as Record<string, unknown>;
  assert.deepEqual(twitter.images, [{ url: "https://example.com/photo.jpg" }]);
});

test("ưu tiên ảnh isPrimary trước ảnh đầu tiên", () => {
  const meta = buildRecipeMetadata(
    recipe({
      images: [
        {
          id: "img-first",
          originalUrl: "https://example.com/first.jpg",
          mediumUrl: null,
          thumbnailUrl: null,
          altText: "First",
          isPrimary: false,
          orderIndex: 0,
        },
        {
          id: "img-primary",
          originalUrl: "https://example.com/primary.jpg",
          mediumUrl: null,
          thumbnailUrl: null,
          altText: "Primary",
          isPrimary: true,
          orderIndex: 1,
        },
      ],
    }),
    "https://example.com",
  );

  const og = meta.openGraph as Record<string, unknown>;
  assert.deepEqual(og.images, [
    { url: "https://example.com/primary.jpg", alt: "Primary" },
  ]);
});

test("dùng altText cho ảnh nếu có", () => {
  const meta = buildRecipeMetadata(
    recipe({
      images: [
        {
          id: "img-1",
          originalUrl: "https://example.com/pho.jpg",
          mediumUrl: null,
          thumbnailUrl: null,
          altText: "Tô phở bò nóng hổi",
          isPrimary: true,
          orderIndex: 0,
        },
      ],
    }),
    "https://example.com/",
  );

  const og = meta.openGraph as Record<string, unknown>;
  assert.deepEqual(og.images, [
    { url: "https://example.com/pho.jpg", alt: "Tô phở bò nóng hổi" },
  ]);

  const twitter = meta.twitter as Record<string, unknown>;
  assert.deepEqual(twitter.images, [
    { url: "https://example.com/pho.jpg", alt: "Tô phở bò nóng hổi" },
  ]);
});
