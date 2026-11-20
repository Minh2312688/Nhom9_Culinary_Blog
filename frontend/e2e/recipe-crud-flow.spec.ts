import { test, expect } from "@playwright/test";
import { signInAsByApi } from "./support/auth";
import {
  clearMockApiRequests,
  getMockApiRequests,
  queueMockApiResponse,
} from "./support/mock-api";

const mockApiUrl = "http://127.0.0.1:5055";

async function signInForRecipeTest(page: import("@playwright/test").Page) {
  await signInAsByApi(page);
  await clearMockApiRequests(page);
}

test.describe("FR-RCP-010: E2E Recipe CRUD, Structured Steps, Nutrition & Image Management", () => {
  test.beforeEach(async ({ page }) => {
    const reset = await page.context().request.post(`${mockApiUrl}/__test/auth/reset`);
    expect(reset.status()).toBe(204);
  });

  test("wizard navigation actions are centered", async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");

    const actions = page.locator("form > div[class*='actions']");
    await expect(actions).toHaveCount(1);
    const bounds = await actions.boundingBox();
    expect(bounds).not.toBeNull();
    const viewportWidth = await page.evaluate(() => document.documentElement.clientWidth);
    expect(Math.abs(bounds!.x + bounds!.width / 2 - viewportWidth / 2)).toBeLessThan(3);
  });

  test("wizard next action stays visible in the viewport on a long step", async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 700 });
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Công thức kiểm tra nút tiếp theo");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });

    const nextButton = page.getByRole("button", { name: "Tiếp theo" });
    await nextButton.scrollIntoViewIfNeeded();
    await expect(nextButton).toBeVisible();
    const bounds = await nextButton.boundingBox();
    expect(bounds).not.toBeNull();
    expect(bounds!.y).toBeGreaterThanOrEqual(0);
    expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(700);
  });

  test("wizard next and back actions have visible colors", async ({ page }) => {
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Công thức kiểm tra màu wizard");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });

    const nextButton = page.getByRole("button", { name: "Tiếp theo" });
    await expect(nextButton).toHaveCSS("background-color", "rgb(183, 62, 45)");
    await expect(nextButton).toHaveCSS("color", "rgb(255, 253, 248)");
    await nextButton.click();

    const backButton = page.getByRole("button", { name: "Quay lại" });
    await expect(backButton).toHaveCSS("background-color", "rgb(255, 253, 248)");
    await expect(backButton).toHaveCSS("color", "rgb(23, 74, 73)");
  });

  test("wizard progress follows next and back navigation", async ({ page }) => {
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });

    const progress = page.getByRole("progressbar", { name: "Tiến độ công thức" });
    await expect(progress).toHaveAttribute("aria-valuenow", "1");
    await expect(page.getByText("Bước 1 / 4: Thông tin chung")).toBeVisible();
    await page.locator("#title").fill("Công thức theo dõi tiến độ");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });
    await page.getByRole("button", { name: "Tiếp theo" }).click();

    await expect(progress).toHaveAttribute("aria-valuenow", "2");
    await expect(page.getByText("Bước 2 / 4: Nguyên liệu")).toBeVisible();
    await expect(page.getByTestId("recipe-wizard-step-general")).toHaveAttribute("data-completed", "true");
    await page.getByRole("button", { name: "Quay lại" }).click();

    await expect(progress).toHaveAttribute("aria-valuenow", "1");
    await expect(page.getByText("Bước 1 / 4: Thông tin chung")).toBeVisible();
  });

  test("new recipe cover image preview stays compact in the editor and review", async ({ page }) => {
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Công thức ảnh đại diện");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });
    await page.locator("#imageFile").setInputFiles({
      name: "cover.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });

    const editorPreview = page.getByRole("img", { name: "Công thức ảnh đại diện" });
    await expect(editorPreview).toHaveCSS("width", "320px");
    await expect(editorPreview).toHaveCSS("height", "200px");

    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
    await page.getByRole("textbox", { name: "Tên nguyên liệu 1" }).fill("Cà chua");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "+ Thêm bước nấu" }).click();
    await page.locator("#step-desc-0").fill("Nấu chín nguyên liệu.");
    await page.getByRole("button", { name: "Xem lại" }).click();

    const reviewPreview = page.locator("#review-image-heading").locator("..").locator("img");
    await expect(reviewPreview).toHaveCSS("width", "320px");
    await expect(reviewPreview).toHaveCSS("height", "200px");
  });

  test("failed recipe detail loading does not show a blank editor or create a recipe", async ({ page }) => {
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "GET",
      path: "/api/v1/recipes/recipe-1",
      status: 404,
      body: { title: "Not Found", detail: "Recipe not found." },
    });
    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.locator("[data-error-summary]")).toContainText(
      /not found|không tìm thấy/i,
      { timeout: 15_000 },
    );
    await expect(page.locator("#title")).toHaveCount(0);
    const requests = await getMockApiRequests(page);
    expect(requests.some((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes",
    )).toBe(false);
  });

  test("step images can be selected in batches and individually removed before creation", async ({ page }) => {
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Công thức có ảnh bước nấu");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
    await page.getByRole("textbox", { name: "Tên nguyên liệu 1" }).fill("Cà chua");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "+ Thêm bước nấu" }).click();
    await page.locator("#step-desc-0").fill("Trộn các nguyên liệu.");
    const stepImageInput = page.locator("#step-image-file-0");
    await expect(stepImageInput).toHaveAttribute("multiple", "");
    await stepImageInput.setInputFiles([
      {
        name: "buoc-nau-1.webp",
        mimeType: "image/webp",
        buffer: Buffer.from("sample-image-1"),
      },
      {
        name: "buoc-nau-2.webp",
        mimeType: "image/webp",
        buffer: Buffer.from("sample-image-2"),
      },
    ]);

    await expect(page.locator("#step-image-0")).toHaveCount(0);
    const selectedStepImages = page.getByTestId("step-image-files-0");
    await expect(selectedStepImages.locator("img")).toHaveCount(2);
    await expect(selectedStepImages).toHaveCSS("display", "flex");
    await expect(selectedStepImages).toHaveCSS("flex-wrap", "wrap");
    await expect(selectedStepImages.locator("img").first()).toHaveCSS("width", "120px");
    await expect(selectedStepImages.locator("img").first()).toHaveCSS("height", "90px");
    await selectedStepImages.getByRole("button", { name: "Xóa ảnh bước nấu 1 đã chọn 1" }).click();
    await expect(selectedStepImages.locator("img")).toHaveCount(1);
    await page.getByRole("button", { name: "Xem lại" }).click();
    await expect(page.locator("#review-steps-heading").locator("..").locator("[class*='stepImageGallery'] img"))
      .toHaveCount(1);
    expect((await getMockApiRequests(page)).some((request) =>
      request.method === "POST" && request.path.endsWith("/images"),
    )).toBe(false);
    await page.getByRole("button", { name: "Tạo bản nháp" }).click();
    await expect(page).toHaveURL(/\/dashboard\/recipes$/, { timeout: 10_000 });

    const requests = await getMockApiRequests(page);
    const uploads = requests.filter((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes/created-recipe-1/images",
    );
    expect(uploads).toHaveLength(1);
  });

  test("recipe detail groups multiple and single step images under the correct step", async ({ page }) => {
    await page.goto("/recipes/step-image-gallery");

    await expect(page.getByTestId("recipe-step-1").locator("img")).toHaveCount(2);
    await expect(page.getByTestId("recipe-step-2").locator("img")).toHaveCount(1);
    await expect(page.getByTestId("recipe-secondary-images")).toHaveCount(0);
    const gallery = page.getByTestId("recipe-step-1").locator("[class*='stepImageGallery']");
    await expect(gallery).toHaveCSS("display", "flex");
    await expect(gallery).toHaveCSS("flex-wrap", "wrap");
    await expect(gallery.locator("img").first()).toHaveCSS("width", "120px");
    await expect(gallery.locator("img").first()).toHaveCSS("height", "90px");
  });

  test("editing a recipe uploads a selected step image after the update", async ({ page }) => {
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "GET",
      path: "/api/v1/recipes/recipe-1",
      status: 200,
      body: {
        id: "recipe-1",
        title: "Roasted tomato soup",
        slug: "recipe-with-author",
        description: "A simple soup.",
        prepTimeMinutes: 10,
        cookTimeMinutes: 30,
        servings: 4,
        difficulty: "Easy",
        status: "Published",
        categoryId: "category-1",
        authorId: "author-1",
        publishedAt: "2026-10-01T09:30:00Z",
        rowVersion: "v1",
        ingredients: [
          { id: "ingredient-1", name: "Tomatoes", quantity: 4, unit: null, notes: null, orderIndex: 0 },
        ],
        steps: [
          {
            id: "step-1",
            stepNumber: 1,
            title: null,
            description: "Roast the tomatoes.",
            durationMinutes: null,
            imageUrl: "https://storage.example.test/saved-step.webp",
          },
        ],
        images: [
          {
            id: "image-step-1",
            originalUrl: "https://storage.example.test/saved-step-gallery.webp",
            mediumUrl: null,
            thumbnailUrl: null,
            altText: null,
            isPrimary: false,
            orderIndex: 1,
            stepNumber: 1,
          },
        ],
        nutrition: null,
        author: { id: "author-1", displayName: "Recipe Author", avatarUrl: null },
      },
    });
    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.locator("#title")).toHaveValue("Roasted tomato soup");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    const savedStepImages = page.getByTestId("step-image-files-0");
    await expect(savedStepImages.locator("img")).toHaveCount(2);
    await expect(savedStepImages).toHaveCSS("display", "flex");
    await expect(savedStepImages.locator("img").first()).toHaveCSS("width", "120px");
    await expect(savedStepImages.locator("img").first()).toHaveCSS("height", "90px");
    const stepImageInput = page.locator("#step-image-file-0");
    await expect(stepImageInput).toHaveAttribute("multiple", "");
    await stepImageInput.setInputFiles([
      {
        name: "step-update-1.webp",
        mimeType: "image/webp",
        buffer: Buffer.from("sample-image-1"),
      },
      {
        name: "step-update-2.webp",
        mimeType: "image/webp",
        buffer: Buffer.from("sample-image-2"),
      },
    ]);
    await page.getByRole("button", { name: "Xem lại" }).click();
    await expect(page.locator("#review-steps-heading").locator("..").locator("[class*='stepImageGallery'] img"))
      .toHaveCount(4);
    await page.getByRole("button", { name: "Cập nhật công thức" }).click();

    await expect(page).toHaveURL(/\/dashboard\/recipes$/, { timeout: 10_000 });
    const requests = await getMockApiRequests(page);
    expect(requests.find((request) =>
      request.method === "PUT" && request.path === "/api/v1/recipes/recipe-1",
    )).toBeDefined();
    const uploads = requests.filter((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes/recipe-1/images",
    );
    expect(uploads).toHaveLength(2);
    expect(uploads[0]).toMatchObject({
      authorizationPresent: true,
      contentType: expect.stringContaining("multipart/form-data; boundary="),
    });
  });

  test("editing a recipe can mark a saved step image for deletion", async ({ page }) => {
    page.on("dialog", (dialog) => void dialog.accept());
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "GET",
      path: "/api/v1/recipes/recipe-1",
      status: 200,
      body: {
        id: "recipe-1",
        title: "Roasted tomato soup",
        slug: "recipe-with-author",
        description: "A simple soup.",
        prepTimeMinutes: 10,
        cookTimeMinutes: 30,
        servings: 4,
        difficulty: "Easy",
        status: "Published",
        categoryId: "category-1",
        authorId: "author-1",
        publishedAt: "2026-10-01T09:30:00Z",
        rowVersion: "v1",
        ingredients: [
          { id: "ingredient-1", name: "Tomatoes", quantity: 4, unit: null, notes: null, orderIndex: 0 },
        ],
        steps: [
          {
            id: "step-1",
            stepNumber: 1,
            title: null,
            description: "Roast the tomatoes.",
            durationMinutes: null,
            imageUrl: "https://storage.example.test/saved-step.webp",
          },
        ],
        images: [
          {
            id: "image-step-1",
            originalUrl: "https://storage.example.test/saved-step-gallery.webp",
            mediumUrl: null,
            thumbnailUrl: null,
            altText: null,
            isPrimary: false,
            orderIndex: 1,
            stepNumber: 1,
          },
        ],
        nutrition: null,
        author: { id: "author-1", displayName: "Recipe Author", avatarUrl: null },
      },
    });
    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.locator("#title")).toHaveValue("Roasted tomato soup");
    await page.getByRole("button", { name: /theo/i }).click();
    await page.getByRole("button", { name: /theo/i }).click();

    const savedStepImages = page.getByTestId("step-image-files-0");
    await expect(savedStepImages.locator("img")).toHaveCount(2);
    await expect(savedStepImages.getByTestId("btn-delete-step-image-image-step-1")).toHaveCount(1);
    await savedStepImages.getByTestId("btn-delete-step-image-image-step-1").click();
    await expect(savedStepImages.locator("img")).toHaveCount(1);

    await page.getByRole("button", { name: /xem/i }).click();
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(/\/dashboard\/recipes$/, { timeout: 10_000 });

    const requests = await getMockApiRequests(page);
    expect(requests.some((request) =>
      request.method === "DELETE" && request.path === "/api/v1/recipes/recipe-1/images/image-step-1",
    )).toBe(true);
  });

  test("canceling a recipe with selected step images does not save the recipe or images", async ({ page }) => {
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Công thức hủy khi chọn ảnh");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
    await page.getByRole("textbox", { name: "Tên nguyên liệu 1" }).fill("Cà chua");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "+ Thêm bước nấu" }).click();
    await page.locator("#step-desc-0").fill("Trộn các nguyên liệu.");
    await page.locator("#step-image-file-0").setInputFiles({
      name: "step-cancel.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });

    await page.getByRole("link", { name: "Hủy" }).click();
    await expect(page).toHaveURL(/\/dashboard\/recipes$/);
    const requests = await getMockApiRequests(page);
    expect(requests.some((request) =>
      request.method === "POST" &&
      (request.path === "/api/v1/recipes" || request.path.endsWith("/images")),
    )).toBe(false);
  });

  test("recipe row edit, publish, and delete actions stay centered on one line", async ({ page }) => {
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "GET",
      path: "/api/v1/recipes/mine",
      status: 200,
      body: {
        items: [
          {
            id: "draft-recipe",
            title: "Draft recipe for layout test",
            slug: "draft-recipe-for-layout-test",
            description: null,
            difficulty: "Easy",
            cookTimeMinutes: 10,
            servings: 2,
            status: "Draft",
            categoryId: "category-1",
            authorId: "author-1",
            publishedAt: null,
            rowVersion: "v1",
          },
          {
            id: "published-recipe",
            title: "Published recipe for layout test",
            slug: "published-recipe-for-layout-test",
            description: null,
            difficulty: "Easy",
            cookTimeMinutes: 10,
            servings: 2,
            status: "Published",
            categoryId: "category-1",
            authorId: "author-1",
            publishedAt: "2026-01-01T00:00:00Z",
            rowVersion: "v1",
          },
        ],
        totalCount: 2,
        page: 1,
        pageSize: 12,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      },
    });

    await page.goto("/dashboard/recipes");
    const row = page.locator("tbody tr").filter({ hasText: "Draft recipe for layout test" });
    const buttons = [
      row.getByRole("link", { name: "Sửa" }),
      row.getByRole("button", { name: "Xuất bản" }),
      row.getByRole("button", { name: "Xóa" }),
    ];
    const bounds = await Promise.all(buttons.map((button) => button.boundingBox()));
    expect(bounds.every(Boolean)).toBe(true);
    const yPositions = bounds.map((box) => box!.y);
    expect(Math.max(...yPositions) - Math.min(...yPositions)).toBeLessThan(3);

    for (const recipeTitle of [
      "Draft recipe for layout test",
      "Published recipe for layout test",
    ]) {
      const currentRow = page.locator("tbody tr").filter({ hasText: recipeTitle });
      const cellBounds = await currentRow.locator("td").last().boundingBox();
      const actionBounds = await currentRow.locator("td").last().locator('[class*="tableActions"] > *')
        .evaluateAll((elements) => elements.map((element) => {
          const bounds = element.getBoundingClientRect();
          const range = document.createRange();
          range.selectNodeContents(element);
          const textBounds = range.getBoundingClientRect();
          return {
            x: bounds.x,
            width: bounds.width,
            textCenterOffset: Math.abs(
              textBounds.x + textBounds.width / 2 - (bounds.x + bounds.width / 2),
            ),
          };
        }));
      expect(cellBounds).not.toBeNull();
      expect(actionBounds.every((box) => box.width >= 76 && box.textCenterOffset < 3)).toBe(true);
      const actionLeft = Math.min(...actionBounds.map((box) => box.x));
      const actionRight = Math.max(...actionBounds.map((box) => box.x + box.width));
      expect(Math.abs(
        (actionLeft + actionRight) / 2 - (cellBounds!.x + cellBounds!.width / 2),
      )).toBeLessThan(3);
    }
  });

  test("ingredient fields stay in one desktop row and action buttons are easy to tap", async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Bún bò Huế kiểm tra giao diện");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();

    const ingredientFields = [
      page.getByRole("textbox", { name: "Tên nguyên liệu 1" }),
      page.getByRole("textbox", { name: "Số lượng nguyên liệu 1" }),
      page.getByRole("textbox", { name: "Đơn vị nguyên liệu 1" }),
      page.getByRole("textbox", { name: "Ghi chú nguyên liệu 1" }),
    ];
    const fieldBounds = await Promise.all(ingredientFields.map((field) => field.boundingBox()));
    expect(fieldBounds.every(Boolean)).toBe(true);
    const yPositions = fieldBounds.map((bounds) => bounds!.y);
    expect(Math.max(...yPositions) - Math.min(...yPositions)).toBeLessThan(3);

    const addButton = await page.getByRole("button", { name: "Thêm nguyên liệu" }).boundingBox();
    const removeButton = await page.getByRole("button", { name: "Xóa nguyên liệu 1" }).boundingBox();
    expect(addButton?.height).toBeGreaterThanOrEqual(44);
    expect(removeButton?.height).toBeGreaterThanOrEqual(44);
  });

  test("recipe step add and delete controls have large tap targets", async ({ page }) => {
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Bún bò Huế thao tác bước");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
    await page.getByRole("textbox", { name: "Tên nguyên liệu 1" }).fill("Thịt bò");
    await page.getByRole("textbox", { name: "Số lượng nguyên liệu 1" }).fill("300");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "+ Thêm bước nấu" }).click();

    const addButton = await page.getByRole("button", { name: "+ Thêm bước nấu" }).boundingBox();
    const deleteButton = await page.getByRole("button", { name: "Xóa bước 1" }).boundingBox();
    expect(addButton?.height).toBeGreaterThanOrEqual(44);
    expect(deleteButton?.height).toBeGreaterThanOrEqual(44);
  });

  test("wizard validates each step and retains values when moving backward", async ({ page }) => {
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });

    await page.locator("#title").fill("Bad");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.locator("#title-error")).toContainText("ít nhất 5 ký tự");

    await page.locator("#title").fill("Bún bò Huế đầy đủ");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByRole("heading", { name: "Nguyên liệu" })).toBeVisible();

    await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByText("Vui lòng nhập tên nguyên liệu.")).toBeVisible();

    await page.getByRole("textbox", { name: "Tên nguyên liệu 1" }).fill("Thịt bò");
    await page.getByRole("textbox", { name: "Số lượng nguyên liệu 1" }).fill("300");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByRole("heading", { name: "Các bước chế biến" })).toBeVisible();
    await page.getByRole("button", { name: "Quay lại" }).click();
    await expect(page.getByRole("textbox", { name: "Tên nguyên liệu 1" })).toHaveValue("Thịt bò");
    await page.getByRole("button", { name: "Tiếp theo" }).click();

    await page.getByRole("button", { name: "+ Thêm bước nấu" }).click();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await expect(page.getByText("Vui lòng nhập mô tả cho từng bước.")).toBeVisible();
    await expect(page.locator("#step-desc-0")).toBeFocused();
  });

  test("wizard review shows recipe details and creates exactly one Draft recipe", async ({ page }) => {
    await signInForRecipeTest(page);
    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });

    await page.locator("#title").fill("Bún bò Huế Review");
    await page.locator("#description").fill("Nước dùng đậm đà.");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });
    await page.locator("#difficulty").selectOption("Medium");
    await page.locator("#prepTimeMinutes").fill("20");
    await page.locator("#cookTimeMinutes").fill("90");
    await page.locator("#servings").fill("4");
    await page.locator("#includeNutrition").check();
    await page.locator("#nutrition-calories").fill("285");
    await page.locator("#nutrition-protein").fill("34.5");
    await page.locator("#imageAltText").fill("Tô bún bò Huế");
    await page.locator("#imageFile").setInputFiles({
      name: "bun-bo.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });

    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
    await page.getByRole("textbox", { name: "Tên nguyên liệu 1" }).fill("Thịt bò");
    await page.getByRole("textbox", { name: "Số lượng nguyên liệu 1" }).fill("300");
    await page.getByRole("textbox", { name: "Đơn vị nguyên liệu 1" }).fill("g");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "+ Thêm bước nấu" }).click();
    await page.locator("#step-title-0").fill("Nấu nước dùng");
    await page.locator("#step-desc-0").fill("Ninh xương bò.");
    await page.getByRole("button", { name: "+ Thêm bước nấu" }).click();
    await page.locator("#step-title-1").fill("Hoàn thành");
    await page.locator("#step-desc-1").fill("Chan nước dùng lên bún.");
    await page.getByRole("button", { name: "Xem lại" }).click();

    await expect(page.getByRole("heading", { name: "Xem lại công thức" })).toBeVisible();
    await expect(page.getByText("Bún bò Huế Review")).toBeVisible();
    await expect(page.getByText("Thịt bò")).toBeVisible();
    await expect(page.getByText("Ninh xương bò.")).toBeVisible();
    await expect(page.getByText("Chan nước dùng lên bún.")).toBeVisible();
    await expect(page.getByText(/285 kcal/)).toBeVisible();
    await expect(page.getByRole("img", { name: "Tô bún bò Huế" })).toBeVisible();

    await page.getByRole("button", { name: "Tạo bản nháp" }).click();
    await expect(page).toHaveURL(/.*\/dashboard\/recipes$/, { timeout: 10_000 });
    const requests = await getMockApiRequests(page);
    const createRequests = requests.filter((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes",
    );
    expect(createRequests).toHaveLength(1);
    expect(createRequests[0].body).toMatchObject({
      title: "Bún bò Huế Review",
      ingredients: [{ name: "Thịt bò", quantity: 300, unit: "g", orderIndex: 0 }],
      steps: [
        { stepNumber: 1, title: "Nấu nước dùng", description: "Ninh xương bò." },
        { stepNumber: 2, title: "Hoàn thành", description: "Chan nước dùng lên bún." },
      ],
      nutrition: { calories: 285, protein: 34.5 },
    });
    expect(createRequests[0].body).not.toHaveProperty("status");
    expect(requests.some((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes/created-recipe-1/images",
    )).toBe(true);
  });

  test("wizard image retry does not create a duplicate recipe", async ({ page }) => {
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "POST",
      path: "/api/v1/recipes/created-recipe-1/images",
      status: 503,
      body: { title: "Storage unavailable" },
    });

    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Canh chua ảnh đại diện");
    await page.locator("#categoryId").selectOption({ label: "Món chính" });
    await page.locator("#imageFile").setInputFiles({
      name: "main.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await page.getByRole("button", { name: "Tạo bản nháp" }).click();

    await expect(page.locator("[data-error-summary]")).toContainText(/đã lưu.*ảnh chưa tải lên/i);
    await page.getByRole("button", { name: "Thử tải ảnh lại" }).click();
    await expect(page).toHaveURL(/.*\/dashboard\/recipes$/, { timeout: 10_000 });
    const requests = await getMockApiRequests(page);
    expect(requests.filter((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes",
    )).toHaveLength(1);
    expect(requests.filter((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes/created-recipe-1/images",
    )).toHaveLength(2);
  });

  test("wizard edit preserves RowVersion and entered values after a conflict", async ({ page }) => {
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "PUT",
      path: "/api/v1/recipes/recipe-1",
      status: 409,
      body: { title: "Conflict", detail: "Recipe changed." },
    });

    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.getByRole("heading", { name: "Chỉnh sửa công thức" })).toBeVisible();
    await expect(page.locator("#title")).toHaveValue("Roasted tomato soup");
    await page.locator("#title").fill("Roasted tomato soup updated");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByRole("heading", { name: "Nguyên liệu" })).toBeVisible();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByRole("heading", { name: "Các bước chế biến" })).toBeVisible();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await page.getByRole("button", { name: "Cập nhật công thức" }).click();

    await expect(page.getByRole("button", { name: "Tải lại dữ liệu" })).toBeVisible();
    const updateRequest = (await getMockApiRequests(page)).find((request) =>
      request.method === "PUT" && request.path === "/api/v1/recipes/recipe-1",
    );
    expect(updateRequest?.body).toMatchObject({
      title: "Roasted tomato soup updated",
      rowVersion: "v1",
    });

    await page.getByRole("button", { name: "Quay lại" }).click();
    await page.getByRole("button", { name: "Quay lại" }).click();
    await page.getByRole("button", { name: "Quay lại" }).click();
    await expect(page.locator("#title")).toHaveValue("Roasted tomato soup updated");
  });

  test("wizard reloads the latest RowVersion and can save after a conflict", async ({ page }) => {
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "PUT",
      path: "/api/v1/recipes/recipe-1",
      status: 409,
      body: { title: "Conflict", detail: "Recipe changed." },
    });

    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.locator("#title")).toHaveValue("Roasted tomato soup");
    await page.locator("#title").fill("My edit before conflict");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await page.getByRole("button", { name: "Cập nhật công thức" }).click();
    await expect(page.getByRole("button", { name: "Tải lại dữ liệu" })).toBeVisible();

    const refreshedRecipe = {
      id: "recipe-1",
      title: "Recipe changed elsewhere",
      slug: "recipe-with-author",
      description: "A simple soup.",
      prepTimeMinutes: 10,
      cookTimeMinutes: 30,
      servings: 4,
      difficulty: "Easy",
      status: "Published",
      categoryId: "category-1",
      authorId: "author-1",
      publishedAt: "2026-10-01T09:30:00Z",
      rowVersion: "v2",
      ingredients: [
        { id: "ingredient-1", name: "Tomatoes", quantity: 4, unit: null, notes: null, orderIndex: 0 },
      ],
      steps: [
        { id: "step-1", stepNumber: 1, title: null, description: "Roast the tomatoes.", durationMinutes: null, imageUrl: null },
      ],
      images: [],
      nutrition: null,
      author: { id: "author-1", displayName: "Linh Nguyen", avatarUrl: null },
    };
    const savedRecipe = { ...refreshedRecipe, title: "My edit after reload", rowVersion: "v3" };
    await queueMockApiResponse(page, {
      method: "GET",
      path: "/api/v1/recipes/recipe-1",
      status: 200,
      body: refreshedRecipe,
    });
    await queueMockApiResponse(page, {
      method: "PUT",
      path: "/api/v1/recipes/recipe-1",
      status: 200,
      body: savedRecipe,
    });

    await page.getByRole("button", { name: "Tải lại dữ liệu" }).click();
    await expect(page.getByRole("button", { name: "Tải lại dữ liệu" })).not.toBeVisible();
    await page.getByRole("button", { name: "Quay lại" }).click();
    await page.getByRole("button", { name: "Quay lại" }).click();
    await page.getByRole("button", { name: "Quay lại" }).click();
    await expect(page.locator("#title")).toHaveValue("Recipe changed elsewhere");

    await page.locator("#title").fill("My edit after reload");
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await page.getByRole("button", { name: "Cập nhật công thức" }).click();
    await expect(page).toHaveURL(/\/dashboard\/recipes$/, { timeout: 10_000 });

    const updateRequests = (await getMockApiRequests(page)).filter((request) =>
      request.method === "PUT" && request.path === "/api/v1/recipes/recipe-1",
    );
    expect(updateRequests).toHaveLength(2);
    expect(updateRequests[0].body).toMatchObject({
      title: "My edit before conflict",
      rowVersion: "v1",
    });
    expect(updateRequests[1].body).toMatchObject({
      title: "My edit after reload",
      rowVersion: "v2",
    });
  });

  test("1. Access Control: redirects unauthenticated user away from recipe creator to /auth/login", async ({
    page,
  }) => {
    // Navigate with empty sessionStorage
    await page.goto("/dashboard/recipes/new");
    await expect(page).toHaveURL(/.*\/auth\/login/);
  });

  test("2. Access Control: authenticated user can access recipe editor", async ({ page }) => {
    await signInAsByApi(page);

    await page.goto("/dashboard/recipes/new");
    await expect(page).toHaveURL(/.*\/dashboard\/recipes\/new/);
    await expect(page.getByRole("heading", { name: "Tạo công thức mới" })).toBeVisible();
  });

  test("Recipe Editor validates required fields before calling the API", async ({ page }) => {
    await signInForRecipeTest(page);

    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.locator("#title").fill("Bad");
    await page.getByRole("button", { name: "Tiếp theo" }).click();

    await expect(page.locator("#title-error")).toContainText("ít nhất 5 ký tự");
    const requests = await getMockApiRequests(page);
    expect(requests.some((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes",
    )).toBe(false);
  });

  test("3. Recipe Creation with Structured Steps, Ingredients, and Nutrition", async ({ page }) => {
    await signInForRecipeTest(page);

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
    await page.check("#includeNutrition");
    await page.fill("#nutrition-calories", "285");
    await page.fill("#nutrition-protein", "34.5");
    await page.fill("#nutrition-carbohydrates", "12");
    await page.fill("#nutrition-fat", "7.5");
    await page.fill("#nutrition-fiber", "3.2");
    await page.fill("#nutrition-sodium", "620");
    await page.getByRole("button", { name: "Tiếp theo" }).click();

    // Fill ingredients
    for (const [index, [name, quantity, unit, notes]] of [
      ["Cá lóc", "500", "g", "cắt khúc"],
      ["Cà chua", "2", "quả", "bổ múi cau"],
      ["Dứa", "0.5", "quả", "thái lát"],
    ].entries()) {
      await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
      await page.getByRole("textbox", { name: `Tên nguyên liệu ${index + 1}` }).fill(name);
      await page.getByRole("textbox", { name: `Số lượng nguyên liệu ${index + 1}` }).fill(quantity);
      await page.getByRole("textbox", { name: `Đơn vị nguyên liệu ${index + 1}` }).fill(unit);
      await page.getByRole("textbox", { name: `Ghi chú nguyên liệu ${index + 1}` }).fill(notes);
    }

    await expect(page.locator("[data-testid^='ingredient-row-']")).toHaveCount(3);
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByRole("heading", { name: "Các bước chế biến" })).toBeVisible();

    // Add structured steps
    await page.locator("#add-step-button").scrollIntoViewIfNeeded();
    await page.click("#add-step-button");
    await page.fill("#step-title-0", "Sơ chế cá lóc");
    await page.fill("#step-duration-0", "10");
    await page.fill("#step-desc-0", "Làm sạch vảy cá, xát muối và chanh khử tanh rồi cắt khúc vừa ăn.");
    await page.locator("#step-image-file-0").setInputFiles({
      name: "ca-loc.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });

    await page.click("#add-step-button");
    await page.fill("#step-title-1", "Nấu nước dùng canh chua");
    await page.fill("#step-duration-1", "15");
    await page.fill("#step-desc-1", "Đun sôi nước me lọc, cho thơm và cà chua vào đun chín tới.");

    // Review and submit form
    await page.getByRole("button", { name: "Xem lại" }).click();
    await expect(page.locator("button[type='submit']")).toBeEnabled();
    await page.click("button[type='submit']");

    // Redirect to dashboard recipes
    await expect(page).toHaveURL(/\/dashboard\/recipes$/, { timeout: 10_000 });
    const requests = await getMockApiRequests(page);
    const createRequest = requests.find((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes",
    );
    const createBody = createRequest?.body as { steps?: Array<{ imageUrl?: string | null }> } | undefined;
    expect(createBody?.steps?.[0]?.imageUrl ?? null).toBeNull();
    expect(requests.find((request) =>
      request.method === "POST" &&
      request.path === "/api/v1/recipes/created-recipe-1/images",
    )).toMatchObject({
      authorizationPresent: true,
      contentType: expect.stringContaining("multipart/form-data; boundary="),
    });
  });

  test("Recipe ingredients use structured fields and submit typed values", async ({ page }) => {
    await signInForRecipeTest(page);

    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await expect(page.locator("#ingredients")).toHaveCount(0);

    await page.fill("#title", "Canh chua cá lóc");
    await page.selectOption("#categoryId", { label: "Món chính" });
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
    await page.getByRole("textbox", { name: "Tên nguyên liệu 1" }).fill("Cá lóc");
    await page.getByRole("textbox", { name: "Số lượng nguyên liệu 1" }).fill("0.5");
    await page.getByRole("textbox", { name: "Đơn vị nguyên liệu 1" }).fill("kg");
    await page.getByRole("textbox", { name: "Ghi chú nguyên liệu 1" }).fill("cắt khúc");

    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByRole("heading", { name: "Các bước chế biến" })).toBeVisible();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await page.getByRole("button", { name: "Tạo bản nháp" }).click();
    await expect(page).toHaveURL(/.*\/dashboard\/recipes$/, { timeout: 10_000 });

    const createRequest = (await getMockApiRequests(page)).find((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes",
    );
    expect(createRequest?.body).toMatchObject({
      ingredients: [{
        name: "Cá lóc",
        quantity: 0.5,
        unit: "kg",
        notes: "cắt khúc",
        orderIndex: 0,
      }],
    });
  });

  test("3a. Recipe creation uploads a selected primary image after the recipe is saved", async ({ page }) => {
    await signInForRecipeTest(page);

    await page.goto("/dashboard/recipes/new");
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await expect(page.getByLabel("Mô tả ảnh (Alt text) (không bắt buộc)")).toBeVisible();
    await expect(page.getByText("Mô tả nội dung ảnh cho người dùng trình đọc màn hình.")).toBeVisible();
    await page.fill("#title", "Canh chua ảnh đại diện");
    await page.selectOption("#categoryId", { label: "Món chính" });
    await page.fill("#imageAltText", "Canh chua hoàn chỉnh");
    await page.check("#imageSetPrimary");
    await page.locator("#imageFile").setInputFiles({
      name: "main.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await page.getByRole("button", { name: "Tạo bản nháp" }).click();

    await expect(page).toHaveURL(/.*\/dashboard\/recipes$/, { timeout: 10_000 });
    const requests = await getMockApiRequests(page);
    expect(requests.find((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes",
    )).toMatchObject({
      authorizationPresent: true,
      body: { title: "Canh chua ảnh đại diện" },
    });
    expect(requests.find((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes/created-recipe-1/images",
    )).toMatchObject({
      authorizationPresent: true,
      contentType: expect.stringContaining("multipart/form-data; boundary="),
    });
  });

  test("3b. Recipe image upload retry does not create a duplicate recipe", async ({ page }) => {
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "POST",
      path: "/api/v1/recipes/created-recipe-1/images",
      status: 503,
      body: { title: "Storage unavailable" },
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
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await page.getByRole("button", { name: "Tạo bản nháp" }).click();

    await expect(page.locator("[data-error-summary]")).toContainText(/đã lưu.*ảnh chưa tải lên/i);
    await page.getByRole("button", { name: "Thử tải ảnh lại" }).click();
    await expect(page).toHaveURL(/.*\/dashboard\/recipes$/, { timeout: 10_000 });
    const requests = await getMockApiRequests(page);
    expect(requests.filter((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes",
    )).toHaveLength(1);
    expect(requests.filter((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes/created-recipe-1/images",
    )).toHaveLength(2);
  });

  test("4. Structured Steps Reordering (Move Up / Down / Delete)", async ({ page }) => {
    await signInForRecipeTest(page);

    await page.goto("/dashboard/recipes/new");
    await expect(page.getByRole("heading", { name: "Tạo công thức mới" })).toBeVisible();
    await expect(page.locator("#categoryId option")).toHaveCount(4, { timeout: 10_000 });
    await page.fill("#title", "Công thức sắp xếp bước");
    await page.selectOption("#categoryId", { label: "Món chính" });
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByRole("heading", { name: "Nguyên liệu" })).toBeVisible();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await expect(page.getByRole("heading", { name: "Các bước chế biến" })).toBeVisible();

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
    const main = page.locator("main");
    await expect(main).toHaveCount(1);

    // Title and basics
    await expect(main.getByRole("heading", { name: "Canh Chua Cá Lóc Nam Bộ" })).toBeVisible();
    await expect(main.getByText("Chuẩn bị 15 phút")).toBeVisible();
    await expect(main.getByText("Nấu 25 phút")).toBeVisible();
    await expect(main.getByText("4 khẩu phần")).toBeVisible();

    // Ingredients
    await expect(main.getByText("500 g Cá lóc (cắt khúc)")).toBeVisible();

    // Structured steps with title & duration
    const step1 = main.locator("[data-testid='recipe-step-1']");
    await expect(step1).toBeVisible();
    await expect(step1.getByText("Bước 1: Sơ chế cá lóc")).toBeVisible();
    await expect(step1.getByText("⏱ 10 phút")).toBeVisible();
    await expect(step1.getByText("Làm sạch vảy cá, rửa sạch với chanh khử tanh.")).toBeVisible();

    const step2 = main.locator("[data-testid='recipe-step-2']");
    await expect(step2).toBeVisible();
    await expect(step2.getByText("Bước 2: Nấu nước dùng canh")).toBeVisible();
    await expect(step2.getByText("⏱ 15 phút")).toBeVisible();

    // Nutrition section verification
    const nutritionSec = main.locator("[data-testid='recipe-nutrition']");
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

    const main = page.locator("main");
    await expect(main).toHaveCount(1);
    const title = main.getByRole("heading", { name: "Món có nhiều ảnh" });
    const primaryImage = main.locator("[data-testid='recipe-primary-image']");
    const nutrition = main.locator("[data-testid='recipe-nutrition']");
    const secondaryImages = main.locator("[data-testid='recipe-secondary-images']");
    await expect(primaryImage).toHaveAttribute("src", "https://storage.example.test/primary.webp");
    await expect(primaryImage).toHaveCSS("width", "720px");
    await expect(primaryImage).toHaveCSS("height", "420px");
    await expect(secondaryImages.locator("img")).toHaveCount(1);
    expect(await title.evaluate((node) =>
      Boolean(node.compareDocumentPosition(document.querySelector("main [data-testid='recipe-primary-image']")!)
        & Node.DOCUMENT_POSITION_FOLLOWING))).toBeTruthy();
    expect(await primaryImage.evaluate((node) =>
      Boolean(node.compareDocumentPosition(document.querySelector("main [data-testid='recipe-nutrition']")!)
        & Node.DOCUMENT_POSITION_FOLLOWING))).toBeTruthy();
    expect(await nutrition.evaluate((node) =>
      Boolean(node.compareDocumentPosition(document.querySelector("main [data-testid='recipe-secondary-images']")!)
        & Node.DOCUMENT_POSITION_FOLLOWING))).toBeTruthy();
  });

  test("6. Recipe image changes are applied only after recipe update succeeds", async ({ page }) => {
    test.setTimeout(120_000);
    await signInForRecipeTest(page);

    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.getByRole("heading", { name: "Chỉnh sửa công thức" })).toBeVisible();
    await expect(page.locator("#imageAltText")).toBeVisible();
    await expect(page.getByLabel("Mô tả ảnh (Alt text) cho ảnh sắp tải lên")).toBeVisible();
    await expect(page.getByText("Mô tả nội dung ảnh cho người dùng trình đọc màn hình.")).toBeVisible();

    const saveRecipe = async () => {
      await page.getByRole("button", { name: "Tiếp theo" }).click();
      await page.getByRole("button", { name: "Tiếp theo" }).click();
      await page.getByRole("button", { name: "Xem lại" }).click();
      await page.getByRole("button", { name: "Cập nhật công thức" }).click();
      await expect(page).toHaveURL(/.*\/dashboard\/recipes$/, { timeout: 10_000 });
    };
    const imageRequests = async () => (await getMockApiRequests(page)).filter((request) =>
      request.path.includes("/images") &&
      ["POST", "PATCH", "DELETE"].includes(request.method),
    );

    await page.fill("#imageAltText", "Hình ảnh bát súp thơm ngon");
    await page.check("#imageSetPrimary");
    await page.locator("#imageFile").setInputFiles({
      name: "soup.png",
      mimeType: "image/png",
      buffer: Buffer.from("fake-png-image-content"),
    });
    await page.uncheck("#imageSetPrimary");
    await page.locator("#imageFile").setInputFiles({
      name: "soup-detail.png",
      mimeType: "image/png",
      buffer: Buffer.from("fake-png-image-content"),
    });
    await expect(page.getByRole("radio")).toHaveCount(2);
    await expect(page.getByRole("radio", { name: "Đặt ảnh 1 làm ảnh chính" })).toBeChecked();
    await page.getByRole("radio", { name: "Đặt ảnh 2 làm ảnh chính" }).click();
    await expect(page.getByRole("radio", { name: "Đặt ảnh 2 làm ảnh chính" })).toBeChecked();
    await expect(page.getByRole("radio", { name: "Đặt ảnh 1 làm ảnh chính" })).not.toBeChecked();
    expect(await imageRequests()).toHaveLength(0);

    await saveRecipe();
    const firstSaveRequests = await getMockApiRequests(page);
    const recipeUpdateIndex = firstSaveRequests.findIndex((request) =>
      request.method === "PUT" && request.path === "/api/v1/recipes/recipe-1",
    );
    const firstImageUploadIndex = firstSaveRequests.findIndex((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes/recipe-1/images",
    );
    expect(recipeUpdateIndex).toBeGreaterThanOrEqual(0);
    expect(firstImageUploadIndex).toBeGreaterThan(recipeUpdateIndex);
    expect(firstSaveRequests.filter((request) =>
      request.method === "POST" && request.path === "/api/v1/recipes/recipe-1/images",
    )).toHaveLength(2);

    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.getByRole("radio")).toHaveCount(2);
    await clearMockApiRequests(page);
    await page.getByRole("radio", { name: "Đặt ảnh 1 làm ảnh chính" }).click();
    page.on("dialog", (dialog) => dialog.accept());
    await page.locator("[data-testid^='btn-delete-image-']").nth(1).click();
    expect(await imageRequests()).toHaveLength(0);

    await saveRecipe();
    const secondSaveRequests = await getMockApiRequests(page);
    const secondUpdateIndex = secondSaveRequests.findIndex((request) => request.method === "PUT");
    const deletePrimaryIndex = secondSaveRequests.findIndex((request) => request.method === "DELETE");
    const setPrimaryIndex = secondSaveRequests.findIndex((request) => request.method === "PATCH");
    expect(deletePrimaryIndex).toBeGreaterThan(secondUpdateIndex);
    expect(setPrimaryIndex).toBe(-1);

    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.getByRole("radio")).toHaveCount(1);
    await expect(page.getByRole("radio", { name: "Đặt ảnh 1 làm ảnh chính" })).toBeChecked();
    await clearMockApiRequests(page);
    await page.locator("[data-testid^='btn-delete-image-']").click();
    expect(await imageRequests()).toHaveLength(0);

    await saveRecipe();
    expect(await imageRequests()).toHaveLength(1);
    await expect(page.getByText("Đã xóa ảnh cuối; công thức hiện không có ảnh.")).toBeVisible();
  });

  test("image changes stay pending when recipe update fails", async ({ page }) => {
    test.setTimeout(60_000);
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "PUT",
      path: "/api/v1/recipes/recipe-1",
      status: 503,
      body: { detail: "Recipe update unavailable" },
    });

    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.getByRole("heading", { name: "Chỉnh sửa công thức" })).toBeVisible();
    await page.locator("#imageFile").setInputFiles({
      name: "pending.webp",
      mimeType: "image/webp",
      buffer: Buffer.from("sample-image"),
    });

    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Xem lại" }).click();
    await page.getByRole("button", { name: "Cập nhật công thức" }).click();

    await expect(page.getByText("Recipe update unavailable").first()).toBeVisible();
    const requests = await getMockApiRequests(page);
    expect(requests.some((request) =>
      request.path.includes("/images") &&
      ["POST", "PATCH", "DELETE"].includes(request.method),
    )).toBe(false);
  });

  test("7. Access Control: Non-owner gets 403 Forbidden on update", async ({ page }) => {
    await signInForRecipeTest(page);
    await queueMockApiResponse(page, {
      method: "PUT",
      path: "/api/v1/recipes/recipe-1",
      status: 403,
      body: { detail: "Bạn không có quyền thực hiện thao tác này." },
    });

    await page.goto("/dashboard/recipes/recipe-1/edit");
    await expect(page.getByRole("heading", { name: "Chỉnh sửa công thức" })).toBeVisible();

    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Tiếp theo" }).click();
    await page.getByRole("button", { name: "Xem lại" }).click();

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
