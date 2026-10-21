import { SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function AuthorProfileLoading() {
  return (
    <SitePage>
      <main className={styles.content}>
        <p className={styles.notice} role="status">Đang tải hồ sơ tác giả...</p>
      </main>
    </SitePage>
  );
}
