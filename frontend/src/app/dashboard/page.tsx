"use client";

import Link from "next/link";
import { useSession } from "next-auth/react";
import { SectionTitle } from "../site-ui";
import styles from "../site-ui.module.css";

export default function DashboardPage() {
  const { data: session } = useSession();
  const isAdmin = session?.user.roles?.includes("Admin") ?? false;

  return (
    <main className={styles.content}>
      <SectionTitle
        eyebrow={isAdmin ? "QUẢN TRỊ HỆ THỐNG" : "KHÔNG GIAN TÁC GIẢ"}
        title="Bảng điều khiển"
        description={isAdmin
          ? "Quản lý công thức, danh mục và tài khoản của Culinary Blog."
          : "Quản lý công thức và danh mục của Culinary Blog."}
      />
      <div className={styles.grid}>
        <Link className={styles.card} href="/dashboard/recipes">
          <p className={styles.cardLabel}>NỘI DUNG</p><h2>Công thức</h2>
          <p className={styles.cardText}>
            {isAdmin ? "Xem và quản lý công thức trên Culinary Blog." : "Tạo, cập nhật và xuất bản công thức của bạn."}
          </p>
        </Link>
        {isAdmin && (
          <>
            <Link className={styles.card} href="/dashboard/categories">
              <p className={styles.cardLabel}>QUẢN TRỊ</p><h2>Danh mục</h2>
              <p className={styles.cardText}>Quản lý danh mục dùng để phân loại công thức.</p>
            </Link>
            <Link className={styles.card} href="/dashboard/users">
              <p className={styles.cardLabel}>QUẢN TRỊ</p><h2>Người dùng</h2>
              <p className={styles.cardText}>Xem tài khoản và vai trò của người dùng.</p>
            </Link>
          </>
        )}
        {!isAdmin && (
          <Link className={styles.card} href="/dashboard/recipes/new">
            <p className={styles.cardLabel}>BẮT ĐẦU VIẾT</p><h2>Tạo công thức</h2>
            <p className={styles.cardText}>Ghi lại món ăn tiếp theo bạn muốn chia sẻ.</p>
          </Link>
        )}
      </div>
    </main>
  );
}
