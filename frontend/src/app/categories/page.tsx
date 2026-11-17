"use client";

import Link from "next/link";
import { useCategories } from "@/lib/queries/categories";
import { ErrorNotice, LoadingNotice, SectionTitle, SitePage } from "../site-ui";
import styles from "../site-ui.module.css";

export default function CategoriesPage() {
  const categoriesQuery = useCategories();
  const categories = categoriesQuery.data ?? [];

  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle eyebrow="KHÁM PHÁ THEO CHỦ ĐỀ" title="Danh mục món ăn" description="Chọn một chủ đề để tìm công thức phù hợp với khẩu vị." />
        {categoriesQuery.isPending && <LoadingNotice />}
        {categoriesQuery.error && <ErrorNotice>{categoriesQuery.error.message || "Không thể tải danh mục."}</ErrorNotice>}
        {!categoriesQuery.isPending && !categoriesQuery.error && categories.length === 0 && <p className={styles.notice}>Chưa có danh mục.</p>}
        <div className={styles.grid}>
          {categories.map((category) => (
            <Link className={styles.card} href={`/categories/${category.slug}`} key={category.id}>
              {category.imageUrl
                ? <img className={styles.cardImage} src={category.imageUrl} alt={category.name} />
                : <div className={styles.cardImagePlaceholder} role="img" aria-label={`${category.name} — chưa có ảnh`}>
                    <span>Chưa có ảnh</span>
                  </div>}
              <p className={styles.cardLabel}>DANH MỤC</p>
              <h2>{category.name}</h2>
              {category.description && <p className={styles.cardText}>{category.description}</p>}
              <div className={styles.cardMeta}><span>{category.recipeCount ?? 0} công thức</span></div>
            </Link>
          ))}
        </div>
      </main>
    </SitePage>
  );
}
