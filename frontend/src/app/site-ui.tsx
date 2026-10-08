import Link from "next/link";
import { ChefHat } from "lucide-react";
import styles from "./site-ui.module.css";

export function SiteHeader() {
  return (
    <header className={styles.header}>
      <Link className={styles.brand} href="/" aria-label="Culinary Table, trang chủ">
        <span className={styles.brandMark}><ChefHat size={18} /></span>
        <span>Culinary <strong>Table</strong></span>
      </Link>
      <nav className={styles.nav} aria-label="Điều hướng chính">
        <Link href="/recipes">Công thức</Link>
        <Link href="/categories">Danh mục</Link>
        <Link href="/search">Tìm kiếm</Link>
        <Link href="/profile">Tài khoản</Link>
      </nav>
    </header>
  );
}

export function SiteFooter() {
  return (
    <footer className={styles.footer}>
      <span>CULINARY TABLE</span>
      <span>Chia sẻ hương vị, kết nối đam mê.</span>
    </footer>
  );
}

export function SitePage({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <div className={styles.page}>
      <SiteHeader />
      {children}
      <SiteFooter />
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
