"use client";

import Link from "next/link";
import { SitePage, SectionTitle } from "../site-ui";
import styles from "../site-ui.module.css";

export default function DashboardPage() {
  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle eyebrow="KHÔNG GIAN TÁC GIẢ" title="Bảng điều khiển" description="Quản lý công thức và danh mục của Culinary Table." />
        <div className={styles.grid}>
          <Link className={styles.card} href="/dashboard/recipes">
            <p className={styles.cardLabel}>NỘI DUNG</p><h2>Công thức</h2>
            <p className={styles.cardText}>Tạo, cập nhật và xuất bản công thức của bạn.</p>
          </Link>
          <Link className={styles.card} href="/dashboard/categories">
            <p className={styles.cardLabel}>QUẢN TRỊ</p><h2>Danh mục</h2>
            <p className={styles.cardText}>Quản lý danh mục dùng để phân loại công thức.</p>
          </Link>
          <Link className={styles.card} href="/dashboard/recipes/new">
            <p className={styles.cardLabel}>BẮT ĐẦU VIẾT</p><h2>Tạo công thức</h2>
            <p className={styles.cardText}>Ghi lại món ăn tiếp theo bạn muốn chia sẻ.</p>
          </Link>
        </div>
      </main>
    </SitePage>
  );
}
