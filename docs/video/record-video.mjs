// Records the four segments of the Mathesis demo as .webm clips via Playwright.
// Run from docs/video:  node record-video.mjs
import { chromium } from 'playwright';
import { fileURLToPath } from 'url';
import path from 'path';

const here = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(here, 'clips');
const fileUrl = (p) => 'file:///' + p.replace(/\\/g, '/');

const browser = await chromium.launch();

async function record(name, fn) {
  const context = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    recordVideo: { dir: out, size: { width: 1280, height: 720 } }
  });
  const page = await context.newPage();
  await fn(page);
  const video = page.video();
  await context.close();
  await video.saveAs(path.join(out, `${name}.webm`));
  console.log(`recorded ${name}`);
}

// 1 — intro: cover card
await record('1-intro', async (page) => {
  await page.goto(fileUrl(path.join(here, '..', 'logo', 'mathesis-cover.html')));
  await page.waitForTimeout(6000);
});

// 2 — terminal replay (real captured output, typed)
await record('2-terminal', async (page) => {
  await page.goto(fileUrl(path.join(here, 'terminal-replay.html')));
  await page.waitForFunction(() => document.title === 'REPLAY-DONE', null, { timeout: 90000 });
  await page.waitForTimeout(600);
});

// 3 — live dashboard with injected caption bar
await record('3-dashboard', async (page) => {
  await page.goto('http://localhost:5221', { waitUntil: 'networkidle' });
  await page.waitForTimeout(3500);

  await page.evaluate(() => {
    const cap = document.createElement('div');
    cap.id = 'vidcap';
    cap.style.cssText = 'position:fixed;left:0;right:0;bottom:0;height:56px;background:rgba(13,27,42,.93);' +
      'border-top:1px solid #2e4a66;display:flex;align-items:center;justify-content:center;' +
      'font-family:Segoe UI,sans-serif;font-size:21px;color:#7fd8d8;z-index:9999;text-align:center;';
    cap.textContent = 'The manager dashboard: team readiness, capacity flags — and the approval queue';
    document.body.appendChild(cap);
  });
  await page.waitForTimeout(5000);

  // scroll to the approval queue
  await page.evaluate(() => document.querySelector('details')?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
  await page.waitForTimeout(2500);

  // expand the agent rationale
  await page.locator('details summary').first().click();
  await page.evaluate(() => { document.getElementById('vidcap').textContent =
    'The agent’s rationale: work signals, historical outcomes, and citations from Foundry IQ'; });
  await page.waitForTimeout(6000);

  // the money click — human gate #2
  await page.evaluate(() => { document.getElementById('vidcap').textContent =
    'Human gate #2: agents have no approve tool — this button is the only way a plan activates'; });
  await page.locator('button:has-text("Approve plan")').first().scrollIntoViewIfNeeded();
  await page.waitForTimeout(3000);
  await page.locator('button:has-text("Approve plan")').first().click();
  await page.waitForTimeout(2500);

  // show the audit trail
  await page.evaluate(() => {
    const rows = [...document.querySelectorAll('h5')];
    rows.find(h => h.textContent.includes('Recently Decided'))?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    document.getElementById('vidcap').textContent = 'Approved — recorded with notes and timestamp. The queue is the audit log.';
  });
  await page.waitForTimeout(4500);
});

// 4 — outro: architecture + repo
await record('4-outro', async (page) => {
  await page.goto(fileUrl(path.join(here, 'outro.html')));
  await page.waitForTimeout(9000);
});

await browser.close();
console.log('all clips recorded');
