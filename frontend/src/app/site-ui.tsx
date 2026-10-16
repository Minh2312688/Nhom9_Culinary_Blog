import Link from "next/link";
import { ChefHat } from "lucide-react";
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
  return (
    <header className={styles.header}>
      <BrandLink />
      <nav className={styles.nav} aria-label="Điều hướng chính">
        <Link href="/recipes">Công thức</Link>
        <Link href="/categories">Danh mục</Link>
        <Link href="/search">Tìm kiếm</Link>
        <Link href="/profile">Tài khoản</Link>
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
