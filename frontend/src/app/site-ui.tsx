"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { ChefHat } from "lucide-react";
import { useAuth, setStoredUser } from "@/lib/auth-store";
import { getProfileApi } from "@/lib/auth-api";
import styles from "./site-ui.module.css";

export function BrandLink() {
  return (
    <Link className={styles.brand} href="/" aria-label="Culinary Blog, trang chủ">
      <span className={styles.brandMark}><ChefHat size={18} aria-hidden="true" /></span>
      <span>Culinary <strong>Blog</strong></span>
    </Link>
  );
}

export function SiteHeader() {
  const { isAuthenticated, accessToken, user } = useAuth();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  // Tự động tải profile đầy đủ (tên thật, avatar) từ API nếu đã đăng nhập
  useEffect(() => {
    if (!mounted || !isAuthenticated || !accessToken) return;

    // Nếu đã có displayName đầy đủ (không phải chỉ email username), không cần fetch lại liên tục
    if (user?.displayName && user.displayName !== user.email?.split("@")[0]) return;

    let cancelled = false;
    getProfileApi(accessToken)
      .then((data) => {
        if (!cancelled && data) {
          setStoredUser({
            displayName: data.displayName,
            email: data.email,
            avatarUrl: data.avatarUrl,
          });
        }
      })
      .catch(() => {
        // Fallback: giữ thông tin từ JWT / state hiện tại
      });

    return () => {
      cancelled = true;
    };
  }, [mounted, isAuthenticated, accessToken, user?.displayName, user?.email]);

  const loggedIn = mounted && isAuthenticated;
  const displayName = user?.displayName || user?.email || "Tài khoản";

  return (
    <header className={styles.header}>
      <BrandLink />
      <nav className={styles.nav} aria-label="Điều hướng chính">
        <Link href="/recipes">Công thức</Link>
        <Link href="/categories">Danh mục</Link>
        <Link href="/search">Tìm kiếm</Link>
        {loggedIn ? (
          <Link
            href="/profile"
            data-testid="nav-profile"
            className="inline-flex items-center gap-1.5 py-1 px-2.5 rounded-full bg-orange-50/80 hover:bg-orange-100/90 border border-orange-200/60 transition group max-w-[200px]"
            title={`Tài khoản: ${displayName}`}
          >
            {user?.avatarUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={user.avatarUrl}
                alt=""
                className="w-5 h-5 rounded-full object-cover shrink-0 border border-orange-300"
              />
            ) : (
              <span className="w-5 h-5 rounded-full bg-orange-200 text-orange-800 flex items-center justify-center text-[10px] font-bold shrink-0">
                {displayName.charAt(0).toUpperCase()}
              </span>
            )}
            <span className="truncate max-w-[120px] font-medium text-gray-800 group-hover:text-orange-700 text-xs">
              {displayName}
            </span>
          </Link>
        ) : (
          <Link href="/auth/login" data-testid="nav-login">
            Đăng nhập
          </Link>
        )}
      </nav>
    </header>
  );
}

export function SiteFooter({ message }: Readonly<{ message?: string }>) {
  return (
    <footer className={styles.footer}>
      <span>CULINARY BLOG</span>
      <span>{message ?? "Chia sẻ hương vị, kết nối đam mê."}</span>
    </footer>
  );
}

export function SitePage({
  children,
  className,
  footerMessage,
}: Readonly<{ children: React.ReactNode; className?: string; footerMessage?: string }>) {
  return (
    <div className={[styles.page, className].filter(Boolean).join(" ")}>
      <SiteHeader />
      {children}
      <SiteFooter message={footerMessage} />
    </div>
  );
}

export function SectionTitle({
  eyebrow,
  title,
  description,
}: {
  eyebrow: string;
  title: string;
  description?: string;
}) {
  return (
    <div className={styles.sectionTitle}>
      <p className={styles.eyebrow}>{eyebrow}</p>
      <h1>{title}</h1>
      {description && <p className={styles.description}>{description}</p>}
    </div>
  );
}

export function ErrorNotice({ children }: Readonly<{ children: React.ReactNode }>) {
  return <p className={styles.error} role="alert">{children}</p>;
}

export function LoadingNotice() {
  return <p className={styles.notice} role="status">Đang tải dữ liệu...</p>;
}

export function LoadingSkeleton({ variant }: Readonly<{ variant: "cards" | "detail" | "form" }>) {
  const labels = {
    cards: "Đang tải danh sách công thức",
    detail: "Đang tải nội dung công thức",
    form: "Đang tải biểu mẫu",
  };
  const itemCount = variant === "form" ? 6 : variant === "detail" ? 4 : 3;

  return (
    <div className={`${styles.skeleton} ${styles[`skeleton-${variant}`]}`} role="status" aria-busy="true" aria-label={labels[variant]}>
      {Array.from({ length: itemCount }, (_, index) => (
        <span className={styles.skeletonItem} key={index} aria-hidden="true" />
      ))}
    </div>
  );
}

export function Pagination({
  page,
  totalPages,
  hasNextPage,
  hasPreviousPage,
  onPageChange,
  label,
}: {
  page: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
  onPageChange: (page: number) => void;
  label: string;
}) {
  if (totalPages <= 1) return null;

  return (
    <nav className={styles.pagination} aria-label={label}>
      <button type="button" className={styles.buttonSecondary} disabled={!hasPreviousPage}
        onClick={() => onPageChange(page - 1)}>Trang trước</button>
      <span aria-live="polite">Trang {page} / {totalPages}</span>
      <button type="button" className={styles.buttonSecondary} disabled={!hasNextPage}
        onClick={() => onPageChange(page + 1)}>Trang sau</button>
    </nav>
  );
}
