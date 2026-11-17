"use client";

import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { useRecipes } from "@/lib/queries/recipes";
import { BrandMotion } from "./brand-motion";
import { ErrorNotice, LoadingNotice, RecipeCard, SectionTitle, SitePage } from "./site-ui";
import styles from "./site-ui.module.css";

export default function HomePage() {
  const recipesQuery = useRecipes({ page: 1, pageSize: 6 });
  const recipes = recipesQuery.data?.items ?? [];

  return (
    <SitePage>
      <main>
        <section className={styles.hero}>
          <BrandMotion>
            <div className={styles.heroInner}>
              <p className={styles.eyebrow}>BẾP NHÀ, CÂU CHUYỆN CỦA BẠN</p>
              <h1>Mỗi món ngon bắt đầu từ một câu chuyện.</h1>
              <p className={styles.heroText}>Khám phá công thức được cộng đồng yêu bếp chia sẻ, lưu lại cảm hứng và cùng nhau nấu những bữa ăn đáng nhớ.</p>
              <div className={styles.actions}>
                <Link className={styles.button} href="/recipes">Khám phá công thức <ArrowRight size={16} /></Link>
                <Link className={styles.buttonSecondary} href="/categories">Xem danh mục</Link>
              </div>
            </div>
          </BrandMotion>
        </section>
        <section className={styles.content}>
          <div className={styles.sectionTitle}>
            <p className={styles.eyebrow}>TỪ CỘNG ĐỒNG</p>
            <h2>Công thức mới nhất</h2>
            <p className={styles.description}>Những món ăn đang chờ bạn khám phá trong căn bếp hôm nay.</p>
          </div>
          {recipesQuery.isPending && <LoadingNotice />}
          {recipesQuery.error && <ErrorNotice>{recipesQuery.error.message || "Không thể tải công thức."}</ErrorNotice>}
          {!recipesQuery.isPending && !recipesQuery.error && recipes.length === 0 && <p className={styles.notice}>Chưa có công thức được chia sẻ.</p>}
          <div className={styles.grid}>
            {recipes.map((recipe) => (
              <RecipeCard recipe={recipe} key={recipe.id} />
            ))}
          </div>
        </section>
      </main>
    </SitePage>
  );
}
