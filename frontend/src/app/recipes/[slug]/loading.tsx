import { SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function RecipeDetailLoading() {
  return (
    <SitePage>
      <main className={styles.detail}>
        <p className={styles.notice} role="status">Đang tải công thức...</p>
      </main>
    </SitePage>
  );
}
