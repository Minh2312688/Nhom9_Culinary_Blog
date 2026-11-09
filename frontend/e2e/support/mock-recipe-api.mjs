import { createServer } from "node:http";

const author = {
  id: "author-1",
  displayName: "Linh Nguyen",
  avatarUrl: null,
  bio: "A home cook sharing practical recipes.",
  email: "linh@example.com",
  roles: ["Admin"],
};

const categories = [
  { id: "category-1", name: "Món chính", slug: "mon-chinh" },
  { id: "category-2", name: "Món khai vị", slug: "mon-khai-vi" },
  { id: "category-3", name: "Tráng miệng", slug: "trang-mieng" },
];

const recipesDb = new Map();

function createInitialRecipe(overrides = {}) {
  const r = {
    id: overrides.id || "recipe-1",
    title: overrides.title || "Roasted tomato soup",
    slug: overrides.slug || "recipe-with-author",
    description: overrides.description || "A simple soup.",
    prepTimeMinutes: overrides.prepTimeMinutes ?? 10,
    cookTimeMinutes: overrides.cookTimeMinutes ?? 30,
    servings: overrides.servings ?? 4,
    difficulty: overrides.difficulty || "Easy",
    status: overrides.status || "Published",
    categoryId: overrides.categoryId || "category-1",
    authorId: overrides.authorId || "author-1",
    publishedAt: overrides.publishedAt !== undefined ? overrides.publishedAt : "2026-10-01T09:30:00Z",
    rowVersion: overrides.rowVersion || "v1",
    ingredients: overrides.ingredients || [
      { id: "ingredient-1", name: "Tomatoes", quantity: 4, unit: null, notes: null, orderIndex: 0 },
    ],
    steps: overrides.steps || [
      { id: "step-1", stepNumber: 1, title: null, description: "Roast the tomatoes.", durationMinutes: null, imageUrl: null },
    ],
    images: overrides.images || [],
    nutrition: overrides.nutrition || null,
    author: overrides.author !== undefined ? overrides.author : { id: author.id, displayName: author.displayName, avatarUrl: author.avatarUrl },
  };
  return r;
}

// Seed initial recipes
const initialRecipes = [
  createInitialRecipe(),
  createInitialRecipe({
    id: "00000000-0000-0000-0000-000000000001",
    title: "Phở bò",
    slug: "pho-bo",
    description: "Nước dùng đậm đà",
    prepTimeMinutes: 20,
    cookTimeMinutes: 90,
    servings: 4,
    difficulty: "Medium",
    ingredients: [{
      id: "ingredient-1",
      name: "Thịt bò",
      quantity: 300,
      unit: "g",
      notes: null,
      orderIndex: 0,
    }],
    steps: [{
      id: "step-1",
      stepNumber: 1,
      title: null,
      description: "Nấu nước dùng.",
      durationMinutes: 10,
      imageUrl: null,
    }],
    images: [{
      id: "image-1",
      originalUrl: "https://storage.example.test/original.avif",
      mediumUrl: null,
      thumbnailUrl: null,
      altText: "Phở bò",
      isPrimary: true,
      orderIndex: 0,
    }],
    author: null,
  }),
  createInitialRecipe({ id: "recipe-slow", title: "Slow recipe", slug: "slow-recipe" }),
  createInitialRecipe({
    id: "recipe-2",
    title: "Historical tomato soup",
    slug: "historical-recipe",
    publishedAt: null,
  }),
  createInitialRecipe({
    id: "recipe-canh-chua",
    title: "Canh Chua Cá Lóc Nam Bộ",
    slug: "canh-chua-ca-loc",
    description: "Món canh chua cá lóc đậm đà vị miền Tây sông nước.",
    prepTimeMinutes: 15,
    cookTimeMinutes: 25,
    servings: 4,
    difficulty: "Medium",
    status: "Published",
    categoryId: "category-1",
    authorId: "author-1",
    publishedAt: "2026-10-28T00:00:00Z",
    rowVersion: "v1",
    ingredients: [
      { id: "ing-1", name: "Cá lóc", quantity: 500, unit: "g", notes: "cắt khúc", orderIndex: 0 },
      { id: "ing-2", name: "Cà chua", quantity: 2, unit: "quả", notes: null, orderIndex: 1 },
    ],
    steps: [
      {
        id: "st-1",
        stepNumber: 1,
        title: "Sơ chế cá lóc",
        description: "Làm sạch vảy cá, rửa sạch với chanh khử tanh.",
        durationMinutes: 10,
        imageUrl: "https://storage.example.test/step1.jpg",
      },
      {
        id: "st-2",
        stepNumber: 2,
        title: "Nấu nước dùng canh",
        description: "Đun sôi nước me lọc và cho cà chua vào.",
        durationMinutes: 15,
        imageUrl: null,
      },
    ],
    images: [],
    nutrition: {
      calories: 285,
      protein: 34.5,
      carbohydrates: 12,
      fat: 7.5,
      fiber: 3.2,
      sodium: 620,
    },
    author: { id: "author-1", displayName: "Chef Minh", avatarUrl: null },
  }),
];

for (const r of initialRecipes) {
  recipesDb.set(r.id, r);
}

function paginated(items, page, pageSize = 12, totalCountOverride) {
  const totalCount = totalCountOverride !== undefined ? totalCountOverride : items.length;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;
  return {
    items,
    totalCount,
    page,
    pageSize,
    totalPages,
    hasNextPage: page < totalPages,
    hasPreviousPage: page > 1,
  };
}

function sendJson(response, statusCode, payload) {
  response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(payload));
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    request.on("data", (chunk) => chunks.push(chunk));
    request.on("end", () => resolve(Buffer.concat(chunks)));
    request.on("error", reject);
  });
}

function slugify(text) {
  return text
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[đĐ]/g, "d")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

const server = createServer(async (request, response) => {
  response.setHeader("Access-Control-Allow-Origin", "*");
  response.setHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
  response.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

  if (request.method === "OPTIONS") {
    response.writeHead(204);
    response.end();
    return;
  }

  const url = new URL(request.url ?? "/", "http://127.0.0.1");

  if (url.pathname === "/health") {
    response.writeHead(200);
    response.end("ok");
    return;
  }

  // Categories list (supports /api/v1/categories, /api/v1/categories/, and /api/v1/recipes/categories)
  if (request.method === "GET" && (url.pathname.startsWith("/api/v1/categories") || url.pathname === "/api/v1/recipes/categories")) {
    sendJson(response, 200, paginated(categories, 1, 100));
    return;
  }

  // Authors endpoint
  if (request.method === "GET" &&
      (url.pathname === "/api/v1/authors/author-1" || url.pathname === "/api/v1/authors/slow-author")) {
    if (url.pathname.endsWith("/slow-author")) {
      await new Promise((resolve) => setTimeout(resolve, 1200));
    }
    const page = Number(url.searchParams.get("page") ?? "1");
    const items = page === 1
      ? [{
          id: "recipe-1",
          title: "Roasted tomato soup",
          slug: "recipe-with-author",
          description: "A simple soup.",
          difficulty: "Easy",
          cookTimeMinutes: 30,
          servings: 4,
          status: "Published",
          categoryId: "category-1",
          authorId: "author-1",
          publishedAt: "2026-10-01T09:30:00Z",
          rowVersion: "v1",
        }]
      : [{
          id: "recipe-3",
          title: "Citrus lentil salad",
          slug: "citrus-lentil-salad",
          description: "Bright and filling.",
          difficulty: "Easy",
          cookTimeMinutes: 25,
          servings: 3,
          status: "Published",
          categoryId: "category-1",
          authorId: "author-1",
          publishedAt: "2026-09-20T12:00:00Z",
          rowVersion: "v1",
        }];

    sendJson(response, 200, {
      id: author.id,
      displayName: author.displayName,
      avatarUrl: author.avatarUrl,
      bio: author.bio,
      recipes: paginated(items, page, 12, 13),
    });
    return;
  }

  // List recipes
  if (request.method === "GET" && (url.pathname === "/api/v1/recipes" || url.pathname === "/api/v1/recipes/")) {
    const all = Array.from(recipesDb.values()).map((r) => ({
      id: r.id,
      title: r.title,
      slug: r.slug,
      description: r.description,
      difficulty: r.difficulty,
      cookTimeMinutes: r.cookTimeMinutes,
      servings: r.servings,
      status: r.status,
      categoryId: r.categoryId,
      authorId: r.authorId,
      publishedAt: r.publishedAt,
      rowVersion: r.rowVersion,
    }));
    sendJson(response, 200, paginated(all, 1, 50));
    return;
  }

  // Create recipe
  if (request.method === "POST" && (url.pathname === "/api/v1/recipes" || url.pathname === "/api/v1/recipes/")) {
    const raw = await readBody(request);
    const body = JSON.parse(raw.toString("utf-8"));
    const id = `recipe-${Date.now()}`;
    const slug = slugify(body.title || "new-recipe") + "-" + Math.floor(Math.random() * 1000);
    const newRecipe = {
      id,
      slug,
      title: body.title,
      description: body.description ?? null,
      categoryId: body.categoryId,
      prepTimeMinutes: body.prepTimeMinutes ?? 0,
      cookTimeMinutes: body.cookTimeMinutes ?? 0,
      servings: body.servings ?? 1,
      difficulty: body.difficulty || "Easy",
      status: "Draft",
      authorId: "author-1",
      publishedAt: null,
      rowVersion: "v1",
      ingredients: (body.ingredients || []).map((ing, i) => ({
        id: `ing-${Date.now()}-${i}`,
        ...ing,
      })),
      steps: (body.steps || []).map((st, i) => ({
        id: `step-${Date.now()}-${i}`,
        stepNumber: i + 1,
        ...st,
      })),
      images: [],
      nutrition: body.nutrition ?? null,
      author: { id: author.id, displayName: author.displayName, avatarUrl: author.avatarUrl },
    };
    recipesDb.set(id, newRecipe);
    sendJson(response, 201, newRecipe);
    return;
  }

  // Image upload: /api/v1/recipes/:id/images or /api/v1/recipes/:id/images/
  const uploadMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/images\/?$/);
  if (request.method === "POST" && uploadMatch) {
    const recipeId = uploadMatch[1];
    const r = recipesDb.get(recipeId);
    if (!r) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    await readBody(request);
    const imageId = `img-${Date.now()}`;
    const newImage = {
      id: imageId,
      originalUrl: `https://storage.example.test/${imageId}.avif`,
      mediumUrl: `https://storage.example.test/${imageId}-med.webp`,
      thumbnailUrl: `https://storage.example.test/${imageId}-thumb.webp`,
      altText: "Ảnh công thức",
      isPrimary: true,
      orderIndex: r.images.length,
    };
    r.images.forEach((img) => (img.isPrimary = false));
    r.images.push(newImage);
    sendJson(response, 201, {
      imageId: newImage.id,
      originalUrl: newImage.originalUrl,
      thumbnailUrl: newImage.thumbnailUrl,
      mediumUrl: newImage.mediumUrl,
      altText: newImage.altText,
      isPrimary: newImage.isPrimary,
    });
    return;
  }

  // Set primary image: /api/v1/recipes/:id/images/:imageId/primary
  const primaryMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/images\/([^/]+)\/primary\/?$/);
  if (request.method === "PUT" && primaryMatch) {
    const [, recipeId, imageId] = primaryMatch;
    const r = recipesDb.get(recipeId);
    if (!r) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    r.images.forEach((img) => {
      img.isPrimary = img.id === imageId;
    });
    sendJson(response, 200, { success: true });
    return;
  }

  // Delete image: /api/v1/recipes/:id/images/:imageId
  const deleteImageMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/images\/([^/]+)\/?$/);
  if (request.method === "DELETE" && deleteImageMatch) {
    const [, recipeId, imageId] = deleteImageMatch;
    const r = recipesDb.get(recipeId);
    if (!r) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    r.images = r.images.filter((img) => img.id !== imageId);
    sendJson(response, 204, null);
    return;
  }

  // Update recipe: /api/v1/recipes/:id
  const updateMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/?$/);
  if (request.method === "PUT" && updateMatch) {
    const recipeId = updateMatch[1];
    const r = recipesDb.get(recipeId);
    if (!r) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    const raw = await readBody(request);
    const body = JSON.parse(raw.toString("utf-8"));
    r.title = body.title ?? r.title;
    r.description = body.description !== undefined ? body.description : r.description;
    r.categoryId = body.categoryId ?? r.categoryId;
    r.prepTimeMinutes = body.prepTimeMinutes ?? r.prepTimeMinutes;
    r.cookTimeMinutes = body.cookTimeMinutes ?? r.cookTimeMinutes;
    r.servings = body.servings ?? r.servings;
    r.difficulty = body.difficulty ?? r.difficulty;
    if (body.ingredients) {
      r.ingredients = body.ingredients.map((ing, i) => ({
        id: ing.id || `ing-${Date.now()}-${i}`,
        ...ing,
      }));
    }
    if (body.steps) {
      r.steps = body.steps.map((st, i) => ({
        id: st.id || `step-${Date.now()}-${i}`,
        stepNumber: i + 1,
        ...st,
      }));
    }
    if (body.nutrition !== undefined) {
      r.nutrition = body.nutrition;
    }
    r.rowVersion = `v-${Date.now()}`;
    sendJson(response, 200, r);
    return;
  }

  // Delete recipe
  if (request.method === "DELETE" && updateMatch) {
    const recipeId = updateMatch[1];
    recipesDb.delete(recipeId);
    sendJson(response, 204, null);
    return;
  }

  // Get recipe by slug or ID
  const getMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/?$/);
  if (request.method === "GET" && getMatch) {
    const param = getMatch[1];
    if (param === "slow-recipe") {
      await new Promise((resolve) => setTimeout(resolve, 1200));
    }
    const found = Array.from(recipesDb.values()).find((r) => r.slug === param || r.id === param);
    if (found) {
      sendJson(response, 200, found);
      return;
    }
  }

  sendJson(response, 404, { title: "Not Found", detail: "Mock API endpoint not found." });
});

server.listen(Number(process.env.MOCK_API_PORT ?? "5055"), "127.0.0.1");
