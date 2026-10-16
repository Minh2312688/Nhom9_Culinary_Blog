"use client";

import Link from "next/link";
import Image from "next/image";
import type { AuthorProfilePage } from "@/lib/recipes-api";
import { SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export function AuthorProfileClient({
  author,
}: Readonly<{ author: AuthorProfilePage }>) {
  const { recipes } = author;

  return (
    <SitePage>
      <main className={styles.content}>
        <section
          aria-labelledby="author-name"
          style={{ display: "flex", alignItems: "center", gap: 20, marginBottom: 28 }}
        >
          {author.avatarUrl
            ? <Image
                src={author.avatarUrl}
                alt={`Ảnh đại diện của ${author.displayName}`}
                width={96}
                height={96}
                unoptimized
                style={{ width: 96, height: 96, borderRadius: "50%", objectFit: "cover" }}
              />
            : <span
                aria-hidden="true"
                style={{
                  width: 96,
                  height: 96,
                  display: "grid",
                  placeItems: "center",
                  flexShrink: 0,
                  borderRadius: "50%",
                  color: "white",
                  background: "#173d31",
                  fontSize: 30,
                }}
              >
                {author.displayName.trim().charAt(0).toLocaleUpperCase()}
              </span>}
          <div className={styles.sectionTitle} style={{ marginBottom: 0 }}>
            <p className={styles.eyebrow}>HỒ SƠ TÁC GIẢ</p>
            <h1 id="author-name">{author.displayName}</h1>
            {author.bio && <p className={styles.description}>{author.bio}</p>}
          </div>
        </section>

        <section aria-labelledby="author-recipes">
          <div className={styles.toolbar}>
            <h2 id="author-recipes">Công thức đã xuất bản</h2>
          </div>
          {recipes.totalCount === 0
            ? <p className={styles.notice}>Tác giả chưa có công thức công khai.</p>
            : recipes.items.length === 0
              ? <p className={styles.notice}>Không có công thức trên trang này.</p>
              : (
                <div className={styles.grid}>
                  {recipes.items.map((recipe) => (
                    <Link
                      className={styles.card}
                      href={`/recipes/${encodeURIComponent(recipe.slug)}`}
                      key={recipe.id}
                    >
                      <p className={styles.cardLabel}>{recipe.difficulty}</p>
                      <h2>{recipe.title}</h2>
                      {recipe.description && <p className={styles.cardText}>{recipe.description}</p>}
                      <div className={styles.cardMeta}>
                        <span>{recipe.cookTimeMinutes} phút</span>
                        <span>{recipe.servings} khẩu phần</span>
                      </div>
                    </Link>
                  ))}
                </div>
              )}
        </section>

        {recipes.totalPages > 1 && (
          <nav className={styles.pagination} aria-label="Phân trang công thức tác giả">
            {recipes.hasPreviousPage
              ? <Link
                  className={styles.buttonSecondary}
                  href={`/authors/${encodeURIComponent(author.id)}?page=${recipes.page - 1}`}
                >
                  Trang trước
                </Link>
              : <span aria-disabled="true">Trang trước</span>}
            <span aria-live="polite">Trang {recipes.page} / {recipes.totalPages}</span>
            {recipes.hasNextPage
              ? <Link
                  className={styles.buttonSecondary}
                  href={`/authors/${encodeURIComponent(author.id)}?page=${recipes.page + 1}`}
                >
                  Trang sau
                </Link>
              : <span aria-disabled="true">Trang sau</span>}
          </nav>
        )}
      </main>
    </SitePage>
  );
}
