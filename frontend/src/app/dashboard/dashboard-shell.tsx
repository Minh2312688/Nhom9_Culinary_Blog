"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { BrandLink } from "../site-ui";
import styles from "./dashboard.module.css";

const navigation = [
  { href: "/dashboard", label: "Tổng quan" },
  { href: "/dashboard/recipes", label: "Công thức" },
  { href: "/dashboard/categories", label: "Danh mục" },
];

export function DashboardShell({ children }: Readonly<{ children: React.ReactNode }>) {
  const pathname = usePathname();

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <BrandLink />
        <nav className={styles.navigation} aria-label="Điều hướng bảng điều khiển">
          {navigation.map(({ href, label }) => (
            <Link
              href={href}
              key={href}
              aria-current={pathname === href || (href !== "/dashboard" && pathname.startsWith(`${href}/`)) ? "page" : undefined}
            >
              {label}
            </Link>
          ))}
        </nav>
        <Link className={styles.returnLink} href="/">Về trang chủ</Link>
      </header>
      <div className={styles.content}>{children}</div>
    </div>
  );
}
