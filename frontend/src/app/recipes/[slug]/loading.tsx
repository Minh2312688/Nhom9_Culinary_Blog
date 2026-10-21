import { LoadingSkeleton, SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function RecipeDetailLoading() {
  return (
    <SitePage>
      <main className={styles.detail}>
        <LoadingSkeleton variant="detail" />
      </main>
    </SitePage>
  );
}
