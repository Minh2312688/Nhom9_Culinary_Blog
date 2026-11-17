"use client";

import { useSession } from "next-auth/react";
import { useAdminUsers } from "@/lib/queries/auth";
import { ErrorNotice, LoadingNotice, SectionTitle } from "../../site-ui";
import styles from "../../site-ui.module.css";

function roleLabel(role: string): string {
  if (role === "Admin") return "Quản trị viên";
  if (role === "Author") return "Tác giả";
  if (role === "Reader") return "Độc giả";
  return role;
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat("vi-VN", { dateStyle: "medium" }).format(new Date(value));
}

export default function DashboardUsersPage() {
  const { status: sessionStatus } = useSession();
  const usersQuery = useAdminUsers(sessionStatus === "authenticated");
  const users = usersQuery.data ?? [];
  const error = usersQuery.error
    ? usersQuery.error instanceof Error ? usersQuery.error.message : "Không thể tải danh sách người dùng."
    : null;
  const loading = sessionStatus === "loading" || usersQuery.isFetching;

  return (
    <main className={styles.content}>
      <SectionTitle
        eyebrow="QUẢN TRỊ HỆ THỐNG"
        title="Quản lý người dùng"
        description="Xem tất cả tài khoản và vai trò đang có trong Culinary Blog."
      />
      {error && <ErrorNotice><span data-error-summary>{error}</span></ErrorNotice>}
      {loading && <LoadingNotice />}
      {!loading && !error && users.length === 0 && (
        <section className={`${styles.notice} ${styles.emptyState}`} aria-label="Chưa có người dùng">
          <p>Chưa có tài khoản nào.</p>
        </section>
      )}
      {!loading && !error && users.length > 0 && (
        <>
          <div className={styles.toolbar}>
            <h2>{users.length} tài khoản</h2>
          </div>
          <div style={{ overflowX: "auto" }}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Tên hiển thị</th>
                  <th>Email</th>
                  <th>Vai trò</th>
                  <th>Trạng thái</th>
                  <th>Ngày tạo</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td><strong>{user.displayName}</strong></td>
                    <td>{user.email}</td>
                    <td>{user.roles.length > 0 ? user.roles.map(roleLabel).join(", ") : "Chưa gán"}</td>
                    <td>{user.isLockedOut ? "Đang bị khóa" : user.isActive ? "Đang hoạt động" : "Đã vô hiệu hóa"}</td>
                    <td>{formatDate(user.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </main>
  );
}
