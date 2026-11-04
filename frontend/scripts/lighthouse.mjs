import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const frontendDir = path.resolve(__dirname, '..');
const cliPath = path.resolve(frontendDir, 'node_modules', 'lighthouse', 'cli', 'index.js');
const reportsDir = path.resolve(frontendDir, 'reports', 'lighthouse');

// 1. Biến môi trường
const baseUrl = (process.env.LH_BASE_URL || 'http://localhost:3000').replace(/\/+$/, '');
const recipeSlug = process.env.LH_RECIPE_SLUG;

if (!recipeSlug || recipeSlug.trim() === '') {
  console.error('[Lỗi] Biến môi trường LH_RECIPE_SLUG là BẮT BUỘC nhưng chưa được cung cấp.');
  console.error('Vui lòng cung cấp slug công thức hợp lệ trước khi chạy đo Lighthouse.');
  console.error('Ví dụ (PowerShell):');
  console.error('  $env:LH_RECIPE_SLUG="pho-bo-ha-noi"; npm --prefix frontend run lighthouse');
  console.error('Ví dụ (Bash):');
  console.error('  LH_RECIPE_SLUG="pho-bo-ha-noi" npm --prefix frontend run lighthouse');
  process.exit(1);
}

// 2. Kiểm tra kết nối tới LH_BASE_URL trước khi đo
try {
  await fetch(baseUrl);
} catch {
  console.error('Hãy chạy npm --prefix frontend run build rồi npm --prefix frontend run start');
  process.exit(1);
}

if (!fs.existsSync(cliPath)) {
  console.error(`[Lỗi] Không tìm thấy Lighthouse CLI tại ${cliPath}.`);
  console.error('Hãy chạy "npm --prefix frontend install -D lighthouse".');
  process.exit(1);
}

fs.mkdirSync(reportsDir, { recursive: true });

function getRouteFileSlug(routePath) {
  if (routePath === '/') return 'home';
  return routePath.replace(/^\//, '').replace(/[^a-zA-Z0-9_-]/g, '-');
}

// 3. 5 route và 2 chế độ
const routes = [
  '/',
  '/recipes',
  `/recipes/${recipeSlug.trim()}`,
  '/search',
  '/dashboard',
];

const modes = [
  { name: 'mobile', args: [] },
  { name: 'desktop', args: ['--preset=desktop'] },
];

const summaryList = [];

for (const route of routes) {
  const targetUrl = `${baseUrl}${route}`;
  const routeSlug = getRouteFileSlug(route);

  for (const mode of modes) {
    const outputPathPrefix = path.join(reportsDir, `${routeSlug}-${mode.name}`);

    const cliArgs = [
      cliPath,
      targetUrl,
      ...mode.args,
      '--output=json',
      '--output=html',
      `--output-path=${outputPathPrefix}`,
      '--chrome-flags=--headless=new',
      '--quiet',
    ];

    console.log(`Đang đo: ${targetUrl} [${mode.name}]...`);
    const runResult = spawnSync(process.execPath, cliArgs, {
      encoding: 'utf-8',
      stdio: 'pipe',
      env: process.env,
    });

    if (runResult.status !== 0) {
      const output = `${runResult.stdout || ''}\n${runResult.stderr || ''}`;
      const isChromeMissing =
        output.includes('No Chrome installations found') ||
        output.includes('CHROME_PATH') ||
        output.toLowerCase().includes('cannot find chrome') ||
        output.toLowerCase().includes('chrome-launcher');

      if (isChromeMissing) {
        console.error(`\n[Lỗi] Không tìm thấy trình duyệt Chrome khi đo ${route} (${mode.name}).`);
        console.error('Vui lòng cài đặt Google Chrome hoặc thiết lập biến môi trường CHROME_PATH:');
        console.error('  - Windows PowerShell: $env:CHROME_PATH = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"');
        console.error('  - Windows CMD:        set CHROME_PATH=C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe');
        console.error('  - Linux / macOS:      export CHROME_PATH="/usr/bin/google-chrome"');
      } else {
        console.error(`\n[Lỗi] Lighthouse gặp sự cố khi đo ${route} (${mode.name}) (exit code ${runResult.status}):`);
        console.error(output);
      }
      process.exit(runResult.status || 1);
    }

    const candidateJsonPaths = [
      `${outputPathPrefix}.report.json`,
      `${outputPathPrefix}.json`,
      outputPathPrefix,
    ];
    const jsonFilePath = candidateJsonPaths.find((p) => fs.existsSync(p));

    if (!jsonFilePath) {
      console.error(`[Lỗi] Không tìm thấy file JSON kết quả cho ${route} (${mode.name}) tại ${outputPathPrefix}.report.json`);
      process.exit(1);
    }

    const reportData = JSON.parse(fs.readFileSync(jsonFilePath, 'utf-8'));

    const performanceScore = reportData.categories?.performance?.score != null
      ? Math.round(reportData.categories.performance.score * 100)
      : null;

    const lcpNumeric = reportData.audits?.['largest-contentful-paint']?.numericValue;
    const lcp = lcpNumeric != null ? Number((lcpNumeric / 1000).toFixed(2)) : null;

    const clsNumeric = reportData.audits?.['cumulative-layout-shift']?.numericValue;
    const cls = clsNumeric != null ? Number(clsNumeric.toFixed(3)) : null;

    const tbtNumeric = reportData.audits?.['total-blocking-time']?.numericValue;
    const tbt = tbtNumeric != null ? Math.round(tbtNumeric) : null;

    summaryList.push({
      route,
      mode: mode.name,
      performance: performanceScore,
      lcp,
      cls,
      tbt,
    });
  }
}

// In bảng kết quả
console.log('\n| Route | Mode | Performance | LCP (s) | CLS | TBT (ms) |');
console.log('| :--- | :--- | :--- | :--- | :--- | :--- |');
for (const item of summaryList) {
  const perfStr = item.performance != null ? String(item.performance) : 'N/A';
  const lcpStr = item.lcp != null ? item.lcp.toFixed(2) : 'N/A';
  const clsStr = item.cls != null ? item.cls.toFixed(3) : 'N/A';
  const tbtStr = item.tbt != null ? String(item.tbt) : 'N/A';
  console.log(`| ${item.route} | ${item.mode} | ${perfStr} | ${lcpStr} | ${clsStr} | ${tbtStr} |`);
}

// Ghi summary.json
const summaryJsonPath = path.join(reportsDir, 'summary.json');
fs.writeFileSync(summaryJsonPath, JSON.stringify(summaryList, null, 2), 'utf-8');
console.log(`\nĐã lưu kết quả tóm tắt vào: ${summaryJsonPath}`);

