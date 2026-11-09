"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ArrowRight } from "lucide-react";
import { getRecipes, type RecipeSummary } from "@/lib/recipes-api";
import { BrandMotion } from "./brand-motion";
import { ErrorNotice, LoadingNotice, RecipeCard, SectionTitle, SitePage } from "./site-ui";
import styles from "./site-ui.module.css";

export default function HomePage() {
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    getRecipes(controller.signal, 6).then(({ items }) => setRecipes(items)).catch((reason: unknown) => {
      if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải công thức.");
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, []);

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
          {loading && <LoadingNotice />}
          {error && <ErrorNotice>{error}</ErrorNotice>}
          {!loading && !error && recipes.length === 0 && <p className={styles.notice}>Chưa có công thức được chia sẻ.</p>}
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
