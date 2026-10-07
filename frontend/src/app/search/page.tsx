"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeft, ArrowRight, ChefHat, Clock3, Search, SlidersHorizontal, Users, X } from "lucide-react";
import { getRecipeCategories, searchRecipes, type PaginatedResult, type RecipeCategory, type RecipeSummary } from "@/lib/recipes-api";
import styles from "./search.module.css";

interface SearchFilters {
  search: string;
  categoryId: string;
  difficulty: string;
  maxCookTime: string;
  minServings: string;
  sortBy: "relevance" | "createdAt" | "cookTime" | "prepTime" | "title";
  sortOrder: "asc" | "desc";
  page: number;
}

const emptyFilters: SearchFilters = {
  search: "",
  categoryId: "",
  difficulty: "",
  maxCookTime: "",
  minServings: "",
  sortBy: "relevance",
  sortOrder: "desc",
  page: 1,
};

function readFiltersFromUrl(): SearchFilters {
  const params = new URLSearchParams(window.location.search);
  const page = Number(params.get("page"));
  return {
    search: params.get("q") ?? params.get("search") ?? "",
    categoryId: params.get("categoryId") ?? "",
    difficulty: params.get("difficulty") ?? "",
    maxCookTime: params.get("maxCookTime") ?? "",
    minServings: params.get("minServings") ?? "",
    sortBy: ["createdAt", "cookTime", "prepTime", "title"].includes(params.get("sortBy") ?? "")
      ? params.get("sortBy") as SearchFilters["sortBy"]
      : "relevance",
    sortOrder: params.get("sortOrder")?.toLowerCase() === "asc" ? "asc" : "desc",
    page: Number.isInteger(page) && page > 0 ? page : 1,
  };
}

function buildQuery(filters: SearchFilters): URLSearchParams {
  const query = new URLSearchParams({ page: String(filters.page), pageSize: "12" });
  if (filters.search.trim()) query.set("q", filters.search.trim());
  if (filters.categoryId) query.set("categoryId", filters.categoryId);
  if (filters.difficulty) query.set("difficulty", filters.difficulty);
  if (filters.maxCookTime) query.set("maxCookTime", filters.maxCookTime);
  if (filters.minServings) query.set("minServings", filters.minServings);
  if (filters.sortBy !== "relevance") {
    query.set("sortBy", filters.sortBy);
    query.set("sortOrder", filters.sortOrder);
  }
  return query;
}

export default function RecipeSearchPage() {
  const [filters, setFilters] = useState(emptyFilters);
  const [initialized, setInitialized] = useState(false);
  const [categories, setCategories] = useState<RecipeCategory[]>([]);
  const [result, setResult] = useState<PaginatedResult<RecipeSummary> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setFilters(readFiltersFromUrl());
    setInitialized(true);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    getRecipeCategories(controller.signal).then(setCategories).catch(() => {
      if (!controller.signal.aborted) setCategories([]);
    });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!initialized) return;
    const query = buildQuery(filters);
    const search = query.toString();
    window.history.replaceState(null, "", search ? `/search?${search}` : "/search");

    const controller = new AbortController();
    const timeout = window.setTimeout(async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await searchRecipes(query, controller.signal);
        setResult(data);
      } catch (requestError) {
        if (!controller.signal.aborted) {
          setError(requestError instanceof Error ? requestError.message : "Không thể tải công thức.");
          setResult(null);
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }, 300);

    return () => {
      window.clearTimeout(timeout);
      controller.abort();
    };
  }, [filters, initialized]);

  function updateFilter<K extends keyof SearchFilters>(key: K, value: SearchFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value, page: key === "page" ? value as number : 1 }));
  }

  function clearFilters() {
    setFilters(emptyFilters);
  }

  const hasActiveFilters = Boolean(
    filters.categoryId || filters.difficulty || filters.maxCookTime || filters.minServings,
  );

  return (
    <div className={styles.page}>
      <header className={styles.topbar}>
        <Link className={styles.brand} href="/" aria-label="Culinary Blog, trang chủ">
          <span className={styles.brandMark}><ChefHat size={19} /></span>
          <span>Culinary <strong>Table</strong></span>
        </Link>
        <span className={styles.topbarNote}>CÔNG THỨC ĐƯỢC CHIA SẺ BỞI CỘNG ĐỒNG</span>
      </header>

      <main>
        <section className={styles.searchBand} aria-labelledby="page-title">
          <div className={styles.searchInner}>
            <p className={styles.eyebrow}>KHÁM PHÁ MÓN NGON</p>
            <h1 id="page-title">Tìm món cho hôm nay.</h1>
            <p className={styles.intro}>Tìm theo tên món, nguyên liệu hoặc mô tả công thức.</p>
            <label className={styles.searchBox} htmlFor="recipe-search">
              <Search size={21} aria-hidden="true" />
              <input
                id="recipe-search"
                type="search"
                maxLength={100}
                value={filters.search}
                onChange={(event) => updateFilter("search", event.target.value)}
                placeholder="Ví dụ: phở bò, bánh mì..."
                autoComplete="off"
              />
              {filters.search && (
                <button className={styles.clearSearch} type="button" aria-label="Xóa từ khóa" onClick={() => updateFilter("search", "")}>
                  <X size={17} />
                </button>
              )}
              <span className={styles.searchHint}>TỐI ĐA 100 KÝ TỰ</span>
            </label>
          </div>
        </section>

        <section className={styles.content} aria-label="Kết quả và bộ lọc công thức">
          <div className={styles.filterHeading}>
            <div className={styles.sectionLabel}><SlidersHorizontal size={17} /> <h2>Bộ lọc</h2></div>
            {hasActiveFilters && (
              <button type="button" className={styles.resetButton} onClick={clearFilters}>
                <X size={15} /> Xóa bộ lọc
              </button>
            )}
          </div>
          <div className={styles.filters}>
            <label className={styles.filterField}>
              <span>Danh mục</span>
              <select value={filters.categoryId} onChange={(event) => updateFilter("categoryId", event.target.value)}>
                <option value="">Tất cả danh mục</option>
                {categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
              </select>
            </label>
            <label className={styles.filterField}>
              <span>Độ khó</span>
              <select value={filters.difficulty} onChange={(event) => updateFilter("difficulty", event.target.value)}>
                <option value="">Mọi mức độ</option>
                <option value="Easy">Dễ</option>
                <option value="Medium">Trung bình</option>
                <option value="Hard">Khó</option>
              </select>
            </label>
            <label className={styles.filterField}>
              <span>Nấu tối đa (phút)</span>
              <input type="number" min={1} step={1} inputMode="numeric" placeholder="Ví dụ: 30" value={filters.maxCookTime}
                onChange={(event) => updateFilter("maxCookTime", event.target.value)} />
            </label>
            <label className={styles.filterField}>
              <span>Khẩu phần từ</span>
              <input type="number" min={1} step={1} inputMode="numeric" placeholder="Ví dụ: 2" value={filters.minServings}
                onChange={(event) => updateFilter("minServings", event.target.value)} />
            </label>
            <label className={styles.filterField}>
              <span>Sắp xếp theo</span>
              <select value={filters.sortBy} onChange={(event) => updateFilter("sortBy", event.target.value as SearchFilters["sortBy"])}>
                <option value="relevance">Độ liên quan</option>
                <option value="createdAt">Ngày tạo</option>
                <option value="cookTime">Thời gian nấu</option>
                <option value="prepTime">Thời gian chuẩn bị</option>
                <option value="title">Tiêu đề</option>
              </select>
            </label>
            <label className={styles.filterField}>
              <span>Thứ tự</span>
              <select value={filters.sortOrder} disabled={filters.sortBy === "relevance"}
                onChange={(event) => updateFilter("sortOrder", event.target.value as SearchFilters["sortOrder"])}>
                <option value="asc">Tăng dần</option>
                <option value="desc">Giảm dần</option>
              </select>
            </label>
          </div>

          <div className={styles.resultsHeader}>
            <div>
              <p className={styles.eyebrowDark}>THƯ VIỆN CÔNG THỨC</p>
              <h2>{loading ? "Đang tìm công thức..." : result ? `${result.totalCount} công thức` : "Công thức"}</h2>
            </div>
            {result && <p className={styles.pageMeta}>Trang {result.page} / {Math.max(result.totalPages, 1)}</p>}
          </div>

          {error && <div className={styles.error} role="alert">{error}</div>}
          {!error && !loading && result?.items.length === 0 && (
            <div className={styles.empty} role="status">
              <span className={styles.emptyMark}><Search size={22} /></span>
              <h3>Chưa tìm thấy công thức phù hợp</h3>
              <p>Thử từ khóa khác hoặc nới lỏng một vài bộ lọc.</p>
            </div>
          )}

          {result && result.items.length > 0 && (
            <div className={styles.recipeList} aria-live="polite" aria-busy={loading}>
              {result.items.map((recipe, index) => (
                <article className={styles.recipe} key={recipe.id}>
                  <div className={styles.recipeIndex}>{String((result.page - 1) * result.pageSize + index + 1).padStart(2, "0")}</div>
                  <div className={styles.recipeBody}>
                    <div className={styles.recipeTopline}>
                      <span className={styles.difficulty}>{recipe.difficulty}</span>
                      <span className={styles.recipeStatus}>
                        {recipe.status === "Published" || recipe.status === 1 ? "Đã xuất bản" : String(recipe.status)}
                      </span>
                    </div>
                    <h3>{recipe.title}</h3>
                    {recipe.description && <p className={styles.description}>{recipe.description}</p>}
                    <div className={styles.recipeMeta}>
                      <span><Clock3 size={15} /> {recipe.cookTimeMinutes} phút</span>
                      <span><Users size={15} /> {recipe.servings} khẩu phần</span>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}

          {result && result.totalPages > 1 && (
            <nav className={styles.pagination} aria-label="Phân trang công thức">
              <button type="button" aria-label="Trang trước" title="Trang trước" disabled={!result.hasPreviousPage || loading}
                onClick={() => updateFilter("page", result.page - 1)}><ArrowLeft size={18} /></button>
              <span>{result.page} <i>/</i> {result.totalPages}</span>
              <button type="button" aria-label="Trang sau" title="Trang sau" disabled={!result.hasNextPage || loading}
                onClick={() => updateFilter("page", result.page + 1)}><ArrowRight size={18} /></button>
            </nav>
          )}
        </section>
      </main>
      <footer className={styles.footer}><span>CULINARY TABLE</span><span>Một công thức hay bắt đầu từ nguyên liệu đúng.</span></footer>
    </div>
  );
}
