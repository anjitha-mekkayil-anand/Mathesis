// Retakes the three dashboard screenshots (dark theme) headlessly.
import { chromium } from 'playwright';
import { fileURLToPath } from 'url';
import path from 'path';

const here = path.dirname(fileURLToPath(import.meta.url));
const shots = path.join(here, '..', 'screenshots');

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });
await page.goto('http://localhost:5221', { waitUntil: 'networkidle' });
await page.waitForTimeout(3000);

await page.screenshot({ path: path.join(shots, 'dashboard-readiness-and-queue.png') });

await page.locator('details summary').first().click();
await page.locator('details').first().scrollIntoViewIfNeeded();
await page.waitForTimeout(800);
await page.screenshot({ path: path.join(shots, 'plan-rationale-grounded.png') });

await page.locator('button:has-text("Approve plan")').first().click();
await page.waitForTimeout(1500);
await page.evaluate(() => {
  [...document.querySelectorAll('h5')].find(h => h.textContent.includes('Recently Decided'))
    ?.scrollIntoView({ block: 'center' });
});
await page.waitForTimeout(800);
await page.screenshot({ path: path.join(shots, 'approval-gate-decided.png') });

await browser.close();
console.log('stills done');
