import { chromium } from 'playwright';
import { fileURLToPath } from 'url';
import path from 'path';

const BASE = 'https://localhost:7161';
const EMAIL = 'TestUserWithAVeryLongDisplayName@test.com';
const PASSWORD = 'TestPass123!';

const browser = await chromium.launch({
  headless: true,
  args: ['--ignore-certificate-errors'],
});

const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1280, height: 720 },
});

const page = await context.newPage();

// Login
console.log('[1] Logging in...');
await page.goto(`${BASE}/Identity/Account/Login`, { waitUntil: 'networkidle', timeout: 30000 });
await page.fill('input[name="Input.Email"]', EMAIL);
await page.fill('input[name="Input.Password"]', PASSWORD);
await page.locator('button[type="submit"]').first().click();
await page.waitForLoadState('networkidle', { timeout: 30000 });
await page.waitForTimeout(500);

// Navigate to /map
console.log('[2] Navigating to /map...');
await page.goto(`${BASE}/map`, { waitUntil: 'networkidle', timeout: 30000 });
await page.waitForTimeout(6000);

// Dismiss banners
try {
  const ob = page.locator('[data-testid="onboarding-banner"] .btn-close').first();
  if (await ob.isVisible({ timeout: 1000 }).catch(() => false)) {
    await ob.click({ force: true });
    await page.waitForTimeout(300);
  }
} catch (e) { /* ignore */ }

// ── DESKTOP CSS CHECKS ────────────────────────────────────────
console.log('\n══════ DESKTOP CSS VERIFICATION (1280×720) ══════');

// 1. Verify .sidebar has overflow-x:hidden and z-index
const sidebarStyles = await page.evaluate(() => {
  const s = document.querySelector('.sidebar');
  if (!s) return { error: '.sidebar not found' };
  const cs = getComputedStyle(s);
  return {
    width: cs.width,
    overflowX: cs.overflowX,
    zIndex: cs.zIndex,
    position: cs.position,
  };
});
console.log('  .sidebar:', JSON.stringify(sidebarStyles, null, 2));

// 2. Verify article.content has height:100%
const articleStyles = await page.evaluate(() => {
  const a = document.querySelector('article.content');
  if (!a) return { error: 'article.content not found' };
  const cs = getComputedStyle(a);
  return {
    height: cs.height,
    overflow: cs.overflow,
  };
});
console.log('  article.content:', JSON.stringify(articleStyles, null, 2));

// 3. Verify nav links have text-overflow:ellipsis
const navLinkStyles = await page.evaluate(() => {
  const links = document.querySelectorAll('.nav-item a, .nav-item .nav-link');
  const results = [];
  for (const l of links) {
    const cs = getComputedStyle(l);
    results.push({
      text: l.textContent.trim().substring(0, 40),
      overflow: cs.overflow,
      textOverflow: cs.textOverflow,
      whiteSpace: cs.whiteSpace,
    });
  }
  return results;
});
console.log('  Nav links:');
for (const n of navLinkStyles) {
  console.log(`    "${n.text}" → overflow:${n.overflow}, text-overflow:${n.textOverflow}, white-space:${n.whiteSpace}`);
}

// 4. Verify #ruckr-map has height:100%
const mapStyles = await page.evaluate(() => {
  const m = document.querySelector('#ruckr-map');
  if (!m) return { error: '#ruckr-map not found' };
  const cs = getComputedStyle(m);
  return {
    height: cs.height,
    width: cs.width,
    zIndex: cs.zIndex,
  };
});
console.log('  #ruckr-map:', JSON.stringify(mapStyles, null, 2));

// 5. Check if sidebar content overflows into map area
const overflowCheck = await page.evaluate(() => {
  const sidebar = document.querySelector('.sidebar');
  const map = document.querySelector('#ruckr-map');
  if (!sidebar || !map) return { error: 'Elements not found' };
  const sidebarRect = sidebar.getBoundingClientRect();
  const mapRect = map.getBoundingClientRect();
  return {
    sidebarRight: sidebarRect.right,
    mapLeft: mapRect.left,
    overlap: mapRect.left < sidebarRect.right,
    sidebarWidth: sidebarRect.width,
    gap: mapRect.left - sidebarRect.right,
  };
});
console.log('  Layout overlap check:', JSON.stringify(overflowCheck, null, 2));

// ── MOBILE CSS CHECKS ────────────────────────────────────────
console.log('\n══════ MOBILE CSS VERIFICATION (375×667) ══════');

await page.setViewportSize({ width: 375, height: 667 });
await page.waitForTimeout(2000);

// Expand nav
try {
  const toggler = page.locator('.navbar-toggler').first();
  if (await toggler.isVisible({ timeout: 2000 }).catch(() => false)) {
    await toggler.click({ force: true });
    await page.waitForTimeout(1500);
    console.log('  Nav expanded');
  }
} catch (e) {
  console.log('  Nav toggler not available');
}
await page.waitForTimeout(1000);

// Check map dimensions on mobile
const mobileMapStyles = await page.evaluate(() => {
  const m = document.querySelector('#ruckr-map');
  if (!m) return { error: '#ruckr-map not found' };
  const cs = getComputedStyle(m);
  const rect = m.getBoundingClientRect();
  const viewportH = window.innerHeight;
  return {
    computedHeight: cs.height,
    rectHeight: rect.height,
    rectTop: rect.top,
    rectBottom: rect.bottom,
    viewportHeight: viewportH,
    visible: rect.bottom <= viewportH && rect.height > 0,
    fillsRemaining: `rect(${Math.round(rect.top)}-${Math.round(rect.bottom)}) in viewport(${viewportH})`,
  };
});
console.log('  Mobile #ruckr-map:', JSON.stringify(mobileMapStyles, null, 2));

// Check if page is scrollable
const scrollInfo = await page.evaluate(() => {
  return {
    scrollHeight: document.documentElement.scrollHeight,
    clientHeight: document.documentElement.clientHeight,
    bodyScrollHeight: document.body.scrollHeight,
    hasVerticalScroll: document.documentElement.scrollHeight > document.documentElement.clientHeight,
  };
});
console.log('  Scroll info:', JSON.stringify(scrollInfo, null, 2));

// Check nav menu position vs map
const mobileLayout = await page.evaluate(() => {
  const nav = document.querySelector('.navbar-collapse, .collapse');
  const map = document.querySelector('#ruckr-map');
  const main = document.querySelector('main');
  const article = document.querySelector('article.content');
  
  const result = {};
  if (nav) {
    const nr = nav.getBoundingClientRect();
    result.navBottom = Math.round(nr.bottom);
  }
  if (map) {
    const mr = map.getBoundingClientRect();
    result.mapTop = Math.round(mr.top);
    result.mapHeight = Math.round(mr.height);
  }
  if (article) {
    const ar = article.getBoundingClientRect();
    result.articleHeight = Math.round(ar.height);
  }
  if (main) {
    const mr = main.getBoundingClientRect();
    result.mainHeight = Math.round(mr.height);
  }
  result.viewportHeight = window.innerHeight;
  return result;
});
console.log('  Mobile layout:', JSON.stringify(mobileLayout, null, 2));

await browser.close();
console.log('\n=== CSS verification complete ===');
