import assert from "node:assert/strict";
import test from "node:test";
import { getRecipeStepImageUrls } from "./recipe-step-images.ts";

test("groups stepN-M images by step and uses one image for stepN", () => {
  const images = [
    { originalUrl: "https://cdn.example.test/recipes/step1-1.webp" },
    { originalUrl: "https://cdn.example.test/recipes/step1-2.webp" },
    { originalUrl: "https://cdn.example.test/recipes/step2.webp" },
  ];

  assert.deepEqual(
    getRecipeStepImageUrls(
      { stepNumber: 1, title: null, description: "Mix", durationMinutes: null, imageUrl: images[0].originalUrl },
      0,
      images,
    ),
    [images[0].originalUrl, images[1].originalUrl],
  );
  assert.deepEqual(
    getRecipeStepImageUrls(
      { stepNumber: 2, title: null, description: "Cook", durationMinutes: null, imageUrl: null },
      1,
      images,
    ),
    [images[2].originalUrl],
  );
});

test("groups step images by persisted step number when storage URLs use generated names", () => {
  const firstImage = "https://cdn.example.test/recipes/a71f.webp";
  const secondImage = "https://cdn.example.test/recipes/b82a.webp";

  assert.deepEqual(
    getRecipeStepImageUrls(
      { stepNumber: 1, title: null, description: "Mix", durationMinutes: null, imageUrl: firstImage },
      0,
      [
        { originalUrl: firstImage, stepNumber: 1 },
        { originalUrl: secondImage, stepNumber: 1 },
      ],
    ),
    [firstImage, secondImage],
  );
});

test("includes a selected local image in the recipe review gallery", () => {
  const previewUrl = "blob:http://localhost:3001/step-preview";

  assert.deepEqual(
    getRecipeStepImageUrls(
      {
        stepNumber: 1,
        title: null,
        description: "Mix",
        durationMinutes: null,
        imageUrl: null,
        imagePreviewUrl: previewUrl,
      },
      0,
      [],
    ),
    [previewUrl],
  );
});
