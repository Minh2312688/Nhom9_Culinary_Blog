# Recipe, Category Media, and Nutrition Design

## Goal

Use the existing MinIO-backed image storage for seeded category images and user
recipe images, improve image placement in recipe/category views, and provide
estimated per-serving nutrition values for all 20 sample recipes.

## Existing constraints

- `Category.ImageUrl` and its API DTO already exist; there is no category image
  upload endpoint or form control.
- Recipe image upload already exists for saved recipes. The recipe editor does
  not accept a file while creating a new recipe.
- Recipe nutrition already exists in the domain, API, and editor, but the
  sample JSON has no nutrition values and the seed process does not update
  existing sample recipes.
- Category assets are in `tools/sample-data/images/danhmuc`. Their filename
  stems correspond to category slugs. Some seeded categories have no matching
  file.
- Compose mounts `tools/sample-data` read-only into the API container and uses
  MinIO for persistent image storage.

## Data and API

### Category image seeding

- Match a category asset by exact slug filename stem, allowing the existing
  supported image extensions (`.webp`, `.jpg`, `.jpeg`, `.png`).
- Upload matching category assets to MinIO and persist the resulting public URL
  in the existing `Category.ImageUrl` field.
- Seed existing categories only when `ImageUrl` is empty. Do not replace an
  image set by an administrator.
- A category without a matching asset remains without an image; the public UI
  displays its normal placeholder.
- Add an authenticated, admin-only multipart upload endpoint for category
  images. It replaces the category image URL only after a successful storage
  upload and database update, and cleans up the new object if persistence fails.
- Invalidate category list/detail cache after a successful image update.

### Recipe image entry

- Reuse the current recipe image upload endpoint and MinIO storage path.
- The create form stages the selected file and sends it after recipe creation.
  Existing edit flows continue to upload immediately against the saved recipe.
- Keep the selected file and report partial completion if recipe creation
  succeeded but image upload failed; provide a retry path without creating a
  duplicate recipe.
- Preserve existing image count, file size, primary image, alt-text, and
  authorization rules.

### Sample recipe nutrition

- Add all six existing nutrition fields to each of the 20 sample recipe
  entries: calories, protein, carbohydrates, fat, fiber, and sodium.
- Values are estimates per serving derived from the recipe ingredients and
  serving count, not verified laboratory values.
- During seed, fill missing nutrition on existing sample recipes and leave
  existing nutrition untouched. Newly seeded recipes receive the same data.
- No schema migration is required.

## User interface

- Add image previews and upload inputs to category create/edit administration.
- Add staged image selection, primary-image choice, and alt text to recipe
  creation; preserve the existing edit/gallery controls.
- Category cards display `ImageUrl` where available and a placeholder otherwise.
- On the recipe detail page, show the primary image prominently near the title
  before the recipe information. Show secondary images after the nutrition
  section rather than before the recipe content.
- Label sample nutrition values as estimates/reference values per serving.
- Keep layouts responsive and image alt text meaningful.

## Error handling and data safety

- Surface upload and persistence errors in the existing form feedback pattern.
- Do not silently report successful image save if storage or database persistence
  failed.
- Do not overwrite populated category images or recipe nutrition during seeding.
- Do not remove PostgreSQL, Redis, MinIO, or Seq volumes during container
  rebuild/recreation.

## Verification

- Add tests for slug-to-category-image mapping, seeding category images and
  missing nutrition without overwriting existing values, and category upload
  authorization/persistence behavior.
- Add frontend tests for create/edit image selection, category previews, detail
  image ordering, and nutrition reference labeling.
- Run backend tests, frontend production build, and the focused browser tests.
- Once Docker is available, rebuild API and frontend images, start Compose
  without `down -v`, and verify service health, sample records, and image
  delivery from MinIO.
- Remove only stale/dangling Culinary Blog project images after the rebuilt
  stack is healthy. Keep active container images, third-party images, and all
  volumes; do not run a global prune.
