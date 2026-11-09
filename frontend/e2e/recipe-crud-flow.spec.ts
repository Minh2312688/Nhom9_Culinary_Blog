import { test, expect } from "@playwright/test";

test.describe("FR-RCP-010: E2E Recipe CRUD, Structured Steps, Nutrition & Image Management", () => {
  const MOCK_AUTH_TOKEN = "valid-test-access-token";

  test("1. Access Control: redirects unauthenticated user away from recipe creator to /auth/login", async ({
    page,
  }) => {
    // Navigate with empty sessionStorage
    await page.goto("/dashboard/recipes/new");
    await expect(page).toHaveURL(/.*\/auth\/login/);
  });

  test("2. Access Control: authenticated user can access recipe editor", async ({ page }) => {
    await page.addInitScript((tok) => {
      sessionStorage.setItem("culinary_access_token", tok);
    }, MOCK_AUTH_TOKEN);

    await page.goto("/dashboard/recipes/new");
    await expect(page).toHaveURL(/.*\/dashboard\/recipes\/new/);
    await expect(page.getByRole("heading", { name: "Tạo công thức mới" })).toBeVisible();
  });

  test("3. Recipe Creation with Structured Steps, Ingredients, and Nutrition", async ({ page }) => {
    await page.addInitScript((tok) => {
      sessionStorage.setItem("culinary_access_token", tok);
    }, MOCK_AUTH_TOKEN);

    await page.goto("/dashboard/recipes/new");
    await expect(page.getByRole("heading", { name: "Tạo công thức mới" })).toBeVisible();

    // Wait for categories to populate
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });

    // Fill basic details
    await page.fill("#title", "Canh Chua Cá Lóc Nam Bộ");
    await page.fill("#description", "Món canh chua cá lóc đậm đà vị miền Tây sông nước.");
    await page.selectOption("#categoryId", { label: "Món chính" });
    await page.selectOption("#difficulty", "Medium");
    await page.fill("#prepTimeMinutes", "15");
    await page.fill("#cookTimeMinutes", "25");
    await page.fill("#servings", "4");

    // Fill ingredients
    await page.fill(
      "#ingredients",
      "Cá lóc | 500 | g | cắt khúc\nCà chua | 2 | quả | bổ múi cau\nDứa | 0.5 | quả | thái lát",
    );

    // Add structured steps
    await page.click("#add-step-button");
    await page.fill("#step-title-0", "Sơ chế cá lóc");
    await page.fill("#step-duration-0", "10");
    await page.fill("#step-desc-0", "Làm sạch vảy cá, xát muối và chanh khử tanh rồi cắt khúc vừa ăn.");
    await page.fill("#step-image-0", "https://storage.example.test/ca-loc.jpg");

    await page.click("#add-step-button");
    await page.fill("#step-title-1", "Nấu nước dùng canh chua");
    await page.fill("#step-duration-1", "15");
    await page.fill("#step-desc-1", "Đun sôi nước me lọc, cho thơm và cà chua vào đun chín tới.");

    // Fill Nutrition information
    await page.check("#includeNutrition");
    await page.fill("#nutrition-calories", "285");
    await page.fill("#nutrition-protein", "34.5");
    await page.fill("#nutrition-carbohydrates", "12");
    await page.fill("#nutrition-fat", "7.5");
    await page.fill("#nutrition-fiber", "3.2");
    await page.fill("#nutrition-sodium", "620");

    // Submit form
    await expect(page.locator("button[type='submit']")).toBeEnabled();
    await page.click("button[type='submit']");

    // Redirect to dashboard recipes
    await expect(page).toHaveURL(/.*\/dashboard\/recipes/, { timeout: 10_000 });
  });

  test("3a. Recipe creation uploads a selected primary image after the recipe is saved", async ({ page }) => {
    await page.addInitScript((tok) => {
      sessionStorage.setItem("culinary_access_token", tok);
    }, MOCK_AUTH_TOKEN);
    let createCount = 0;
    let uploadBody = "";

    await page.route("**/api/v1/recipes/", async (route) => {
      if (route.request().method() === "POST") {
        createCount++;
        await route.fulfill({ status: 201, json: { id: "created-recipe-image" } });
        return;
      }
      await route.fallback();
    });
    await page.route("**/api/v1/recipes/created-recipe-image/images/**", async (route) => {
      uploadBody = route.request().postDataBuffer()?.toString("utf8") ?? "";
      await route.fulfill({
        status: 201,
        json: {
          imageId: "created-image-1",
          originalUrl: "https://storage.example.test/created-image.webp",
          altText: "Canh chua hoàn chỉnh",
          isPrimary: true,
        },
      });
    });

    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.fill("#title", "Canh chua ảnh đại diện");
    await page.selectOption("#categoryId", { label: "Món chính" });
    await page.fill("#imageAltText", "Canh chua hoàn chỉnh");
    await page.check("#imageSetPrimary");
    await page.locator("#imageFile").setInputFiles({
      name: "main.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });
    await page.getByRole("button", { name: "Lưu bản nháp" }).click();

    await expect(page).toHaveURL(/.*\/dashboard\/recipes$/, { timeout: 10_000 });
    expect(createCount).toBe(1);
    expect(uploadBody).toContain('name="file"');
    expect(uploadBody).toContain('name="isPrimary"\r\n\r\ntrue');
    expect(uploadBody).toContain("Canh chua hoàn chỉnh");
  });

  test("3b. Recipe image upload retry does not create a duplicate recipe", async ({ page }) => {
    await page.addInitScript((tok) => {
      sessionStorage.setItem("culinary_access_token", tok);
    }, MOCK_AUTH_TOKEN);
    let createCount = 0;
    let uploadCount = 0;

    await page.route("**/api/v1/recipes/", async (route) => {
      if (route.request().method() === "POST") {
        createCount++;
        await route.fulfill({ status: 201, json: { id: "created-recipe-image" } });
        return;
      }
      await route.fallback();
    });
    await page.route("**/api/v1/recipes/created-recipe-image/images/**", async (route) => {
      uploadCount++;
      await route.fulfill(uploadCount === 1
        ? { status: 503, contentType: "application/problem+json", json: { title: "Storage unavailable" } }
        : {
          status: 201,
          json: {
            imageId: "created-image-1",
            originalUrl: "https://storage.example.test/created-image.webp",
            altText: "Canh chua",
            isPrimary: true,
          },
        });
    });

    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.fill("#title", "Canh chua ảnh đại diện");
    await page.selectOption("#categoryId", { label: "Món chính" });
    await page.locator("#imageFile").setInputFiles({
      name: "main.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });
    await page.getByRole("button", { name: "Lưu bản nháp" }).click();

    await expect(page.locator("[data-error-summary]")).toContainText(/đã lưu.*ảnh chưa tải lên/i);
    await page.getByRole("button", { name: "Thử tải ảnh lại" }).click();
    await expect(page).toHaveURL(/.*\/dashboard\/recipes$/, { timeout: 10_000 });
    expect(createCount).toBe(1);
    expect(uploadCount).toBe(2);
  });

  test("4. Structured Steps Reordering (Move Up / Down / Delete)", async ({ page }) => {
    await page.addInitScript((tok) => {
      sessionStorage.setItem("culinary_access_token", tok);
    }, MOCK_AUTH_TOKEN);

    await page.goto("/dashboard/recipes/new");
    await expect(page.getByRole("heading", { name: "Tạo công thức mới" })).toBeVisible();

    // Add 3 steps
    await page.click("#add-step-button");
    await page.fill("#step-title-0", "Bước A: Chuẩn bị nguyên liệu");
    await page.fill("#step-desc-0", "Rửa sạch và cắt thái tất cả nguyên liệu.");

    await page.click("#add-step-button");
    await page.fill("#step-title-1", "Bước B: Nấu chín thực phẩm");
    await page.fill("#step-desc-1", "Đun lửa vừa và đảo đều tay.");

    await page.click("#add-step-button");
    await page.fill("#step-title-2", "Bước C: Trình bày và thưởng thức");
    await page.fill("#step-desc-2", "Múc ra đĩa và rắc tiêu thơm.");

    // Verify initial order
    await expect(page.locator("#step-title-0")).toHaveValue("Bước A: Chuẩn bị nguyên liệu");
    await expect(page.locator("#step-title-1")).toHaveValue("Bước B: Nấu chín thực phẩm");
    await expect(page.locator("#step-title-2")).toHaveValue("Bước C: Trình bày và thưởng thức");

    // Move Step 1 down (Bước A xuống vị trí 2)
    await page.click("[data-testid='step-move-down-0']");

    // Now Step 0 is Bước B, Step 1 is Bước A
    await expect(page.locator("#step-title-0")).toHaveValue("Bước B: Nấu chín thực phẩm");
    await expect(page.locator("#step-title-1")).toHaveValue("Bước A: Chuẩn bị nguyên liệu");

    // Move Step 2 up (Bước C lên vị trí 2)
    await page.click("[data-testid='step-move-up-2']");

    // Now Step 1 is Bước C, Step 2 is Bước A
    await expect(page.locator("#step-title-1")).toHaveValue("Bước C: Trình bày và thưởng thức");
    await expect(page.locator("#step-title-2")).toHaveValue("Bước A: Chuẩn bị nguyên liệu");

    // Delete middle step (Bước C)
    await page.click("[data-testid='step-delete-1']");

    // Verify 2 steps remain
    await expect(page.locator("[data-testid^='structured-step-']")).toHaveCount(2);
    await expect(page.locator("#step-title-0")).toHaveValue("Bước B: Nấu chín thực phẩm");
    await expect(page.locator("#step-title-1")).toHaveValue("Bước A: Chuẩn bị nguyên liệu");
  });

  test("5. Recipe Detail Page renders Structured Steps and Nutrition Info correctly", async ({
    page,
  }) => {
    await page.goto("/recipes/canh-chua-ca-loc");

    // Title and basics
    await expect(page.getByRole("heading", { name: "Canh Chua Cá Lóc Nam Bộ" })).toBeVisible();
    await expect(page.getByText("Chuẩn bị 15 phút")).toBeVisible();
    await expect(page.getByText("Nấu 25 phút")).toBeVisible();
    await expect(page.getByText("4 khẩu phần")).toBeVisible();

    // Ingredients
    await expect(page.getByText("500 g Cá lóc (cắt khúc)")).toBeVisible();

    // Structured steps with title & duration
    const step1 = page.locator("[data-testid='recipe-step-1']");
    await expect(step1).toBeVisible();
    await expect(step1.getByText("Bước 1: Sơ chế cá lóc")).toBeVisible();
    await expect(step1.getByText("⏱ 10 phút")).toBeVisible();
    await expect(step1.getByText("Làm sạch vảy cá, rửa sạch với chanh khử tanh.")).toBeVisible();

    const step2 = page.locator("[data-testid='recipe-step-2']");
    await expect(step2).toBeVisible();
    await expect(step2.getByText("Bước 2: Nấu nước dùng canh")).toBeVisible();
    await expect(step2.getByText("⏱ 15 phút")).toBeVisible();

    // Nutrition section verification
    const nutritionSec = page.locator("[data-testid='recipe-nutrition']");
    await expect(nutritionSec).toBeVisible();
    await expect(nutritionSec).toContainText(/ước tính để tham khảo.*mỗi khẩu phần/i);
    await expect(nutritionSec.getByText("285 kcal")).toBeVisible();
    await expect(nutritionSec.getByText("34.5 g")).toBeVisible();
    await expect(nutritionSec.getByText("12 g")).toBeVisible();
    await expect(nutritionSec.getByText("7.5 g")).toBeVisible();
    await expect(nutritionSec.getByText("3.2 g")).toBeVisible();
    await expect(nutritionSec.getByText("620 mg")).toBeVisible();
  });

  test("5a. Recipe detail places the primary image before recipe content and secondary images after nutrition", async ({ page }) => {
    await page.goto("/recipes/image-ordering");

    const title = page.getByRole("heading", { name: "Món có nhiều ảnh" });
    const primaryImage = page.locator("[data-testid='recipe-primary-image']");
    const nutrition = page.locator("[data-testid='recipe-nutrition']");
    const secondaryImages = page.locator("[data-testid='recipe-secondary-images']");
    await expect(primaryImage).toHaveAttribute("src", "https://storage.example.test/primary.webp");
    await expect(secondaryImages.locator("img")).toHaveCount(1);
    expect(await title.evaluate((node) =>
      Boolean(node.compareDocumentPosition(document.querySelector("[data-testid='recipe-primary-image']")!)
        & Node.DOCUMENT_POSITION_FOLLOWING))).toBeTruthy();
    expect(await primaryImage.evaluate((node) =>
      Boolean(node.compareDocumentPosition(document.querySelector("[data-testid='recipe-nutrition']")!)
        & Node.DOCUMENT_POSITION_FOLLOWING))).toBeTruthy();
    expect(await nutrition.evaluate((node) =>
      Boolean(node.compareDocumentPosition(document.querySelector("[data-testid='recipe-secondary-images']")!)
        & Node.DOCUMENT_POSITION_FOLLOWING))).toBeTruthy();
  });

  test("6. Recipe Image Gallery: Upload, Set Primary, and Delete Image", async ({ page }) => {
    await page.addInitScript((tok) => {
      sessionStorage.setItem("culinary_access_token", tok);
    }, MOCK_AUTH_TOKEN);

    // Navigate to edit existing recipe-1
    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.getByRole("heading", { name: "Chỉnh sửa công thức" })).toBeVisible();

    // Fill image info
    await page.fill("#imageAltText", "Hình ảnh bát súp thơm ngon");
    await page.check("#imageSetPrimary");

    // Set files directly on file input
    await page.locator("#imageFile").setInputFiles({
      name: "soup.png",
      mimeType: "image/png",
      buffer: Buffer.from("fake-png-image-content"),
    });

    // Wait for upload toast
    await expect(page.getByText("Ảnh đã được tải lên.")).toBeVisible({ timeout: 10_000 });

    // Verify image card exists with primary badge
    const badge = page.locator("[data-testid='badge-primary']");
    await expect(badge).toBeVisible();

    // Click delete image on the newly added image
    page.on("dialog", (dialog) => dialog.accept());
    const deleteBtn = page.locator("[data-testid^='btn-delete-image-']").first();
    await deleteBtn.click();

    // Verify deleted toast
    await expect(page.getByText("Đã xóa ảnh.")).toBeVisible({ timeout: 10_000 });
  });

  test("7. Access Control: Non-owner gets 403 Forbidden on update", async ({ page }) => {
    await page.addInitScript((tok) => {
      sessionStorage.setItem("culinary_access_token", tok);
    }, MOCK_AUTH_TOKEN);

    await page.route("**/api/v1/recipes/recipe-1*", async (route) => {
      if (route.request().method() === "PUT") {
        await route.fulfill({
          status: 403,
          contentType: "application/json",
          body: JSON.stringify({ detail: "Bạn không có quyền thực hiện thao tác này." }),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.getByRole("heading", { name: "Chỉnh sửa công thức" })).toBeVisible();

    // Wait for submit button to be enabled
    await expect(page.locator("button[type='submit']")).toBeEnabled();

    // Attempt to submit
    await page.click("button[type='submit']");

    // Error notification must appear
    await expect(
      page.getByText("Bạn không có quyền thực hiện thao tác này.").first(),
    ).toBeVisible();
  });
});
