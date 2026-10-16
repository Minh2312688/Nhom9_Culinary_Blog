import { createServer } from "node:http";

const author = {
  id: "author-1",
  displayName: "Linh Nguyen",
  avatarUrl: null,
  bio: "A home cook sharing practical recipes.",
  email: "linh@example.com",
  roles: ["Admin"],
};

function recipe(overrides = {}) {
  return {
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
      { id: "step-1", stepNumber: 1, title: null, description: "Roast the tomatoes.", durationMinutes: null, imageUrl: null },
    ],
    images: [],
    nutrition: null,
    author: { id: author.id, displayName: author.displayName, avatarUrl: author.avatarUrl },
    ...overrides,
  };
}

function paginated(items, page) {
  return {
    items,
    totalCount: 13,
    page,
    pageSize: 12,
    totalPages: 2,
    hasNextPage: page < 2,
    hasPreviousPage: page > 1,
  };
}

function sendJson(response, statusCode, payload) {
  response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(payload));
}

const server = createServer(async (request, response) => {
  const url = new URL(request.url ?? "/", "http://127.0.0.1");

  if (url.pathname === "/health") {
    response.writeHead(200);
    response.end("ok");
    return;
  }

  if (request.method === "GET" && url.pathname === "/api/v1/recipes/recipe-with-author") {
    sendJson(response, 200, recipe());
    return;
  }

  if (request.method === "GET" && url.pathname === "/api/v1/recipes/slow-recipe") {
    await new Promise((resolve) => setTimeout(resolve, 1200));
    sendJson(response, 200, recipe({ id: "recipe-slow", title: "Slow recipe", slug: "slow-recipe" }));
    return;
  }

  if (request.method === "GET" && url.pathname === "/api/v1/recipes/historical-recipe") {
    sendJson(response, 200, recipe({
      id: "recipe-2",
      title: "Historical tomato soup",
      slug: "historical-recipe",
      publishedAt: null,
    }));
    return;
  }

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
      recipes: paginated(items, page),
    });
    return;
  }

  sendJson(response, 404, { title: "Not Found", detail: "Mock API endpoint not found." });
});

server.listen(Number(process.env.MOCK_API_PORT ?? "5055"), "127.0.0.1");
