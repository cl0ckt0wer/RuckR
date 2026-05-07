import { chromium } from 'playwright';
import { mkdirSync } from 'fs';

const BASE = 'https://localhost:7161';
const SCREENSHOT_DIR = 'C:\\Users\\clock\\source\\repos\\RuckR\\docs\\plan\\20260506-nav-menu-overlap-map-fix\\screenshots\\after';
mkdirSync(SCREENSHOT_DIR, { recursive: true });

const results = {
  desktop: { height_px: 0, verdict: 'FAIL', details: [] },
  mobile: { height_px: 0, verdict: 'FAIL', details: [] }
};

async function main() {
  const browser = await chromium.launch({ headless: true, ignoreHTTPSErrors: true });

  try {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();

    // Collect console errors
    const consoleErrors = [];
    page.on('console', msg => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });

    // Step 1: Register/Login
    console.log('[LOGIN] Navigating to login/register...');
    try {
      await page.goto(`${BASE}/Identity/Account/Login`, { waitUntil: 'networkidle', timeout: 30000 });
      await page.waitForTimeout(2000);

      const emailInput = page.locator('#Input_Email').or(page.locator('#Input_Username'));
      const passwordInput = page.locator('#Input_Password');
      const loginBtn = page.locator('button[type="submit"]').first();

      if (await emailInput.isVisible({ timeout: 3000 }).catch(() => false)) {
        await emailInput.fill('TestUserWithAVeryLongDisplayName');
        if (await passwordInput.isVisible({ timeout: 1000 }).catch(() => false)) {
          await passwordInput.fill('TestPass123!');
        }
        await loginBtn.click();
        await page.waitForTimeout(3000);
        console.log('[LOGIN] Submitted. Current URL:', page.url());
      } else {
        console.log('[LOGIN] Login form not found, URL:', page.url());
      }
    } catch (e) {
      console.log('[LOGIN] Error:', e.message);
    }

    // ====================
    // Step 2: Desktop (1280x720)
    // ====================
    console.log('\n--- DESKTOP VERIFICATION (1280x720) ---');
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.goto(`${BASE}/map`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(6000); // Wait for map tiles + Blazor rendering

    // Dismiss onboarding if visible
    try {
      const onboardingClose = page.locator('#ruckr-map').page().locator('.alert-info .btn-close').first();
      if (await onboardingClose.isVisible({ timeout: 2000 }).catch(() => false)) {
        await onboardingClose.click();
        await page.waitForTimeout(1000);
        console.log('[DESKTOP] Onboarding dismissed.');
      }
    } catch (e) { /* no onboarding */ }

    // Full DOM diagnostics
    const desktopDiag = await page.evaluate(() => {
      const map = document.querySelector('#ruckr-map');
      const article = document.querySelector('article.content');
      const main = document.querySelector('main');
      const pageEl = document.querySelector('.page');
      const sidebar = document.querySelector('.sidebar');
      
      const getLayout = (el) => {
        if (!el) return null;
        const cs = window.getComputedStyle(el);
        return {
          tag: el.tagName,
          offsetHeight: el.offsetHeight,
          clientHeight: el.clientHeight,
          display: cs.display,
          flex: cs.flex,
          flexDirection: cs.flexDirection,
          overflow: cs.overflow,
          position: cs.position
        };
      };

      return {
        map: getLayout(map),
        article: getLayout(article),
        main: getLayout(main),
        page: getLayout(pageEl),
        sidebar: getLayout(sidebar),
        windowInnerHeight: window.innerHeight
      };
    });
    console.log('[DESKTOP] Layout diagnostics:', JSON.stringify(desktopDiag, null, 2));

    results.desktop.height_px = desktopDiag.map?.offsetHeight || 0;

    // Check sidebar overflow-x
    const sidebarOverflow = await page.evaluate(() => {
      const s = document.querySelector('.sidebar');
      return s ? window.getComputedStyle(s).overflowX : 'no .sidebar';
    });
    console.log('[DESKTOP] .sidebar overflow-x:', sidebarOverflow);

    // Check for nav text in map area
    const navInMap = await page.evaluate(() => {
      const map = document.querySelector('#ruckr-map');
      if (!map) return 'no map';
      const texts = map.querySelectorAll('a, span, .nav-link, .navbar-brand');
      return Array.from(texts).map(t => t.textContent?.trim()).filter(Boolean);
    });
    console.log('[DESKTOP] Nav text in map:', JSON.stringify(navInMap));

    await page.screenshot({ path: `${SCREENSHOT_DIR}\\desktop-map-nav-fixed.png`, fullPage: false });
    console.log('[DESKTOP] Screenshot saved.');

    results.desktop.verdict = results.desktop.height_px > 0 ? 'PASS' : 'FAIL';
    results.desktop.details.push(
      `height=${results.desktop.height_px}px`,
      `sidebar overflow-x=${sidebarOverflow}`,
      `nav text in map: ${navInMap.length} items`
    );

    // ====================
    // Step 3: Mobile (375x667)
    // ====================
    console.log('\n--- MOBILE VERIFICATION (375x667) ---');
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto(`${BASE}/map`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(5000); // Wait for map + rendering

    // Dismiss onboarding FIRST
    try {
      const mobileOnboardingClose = page.locator('.alert-info .btn-close').first();
      if (await mobileOnboardingClose.isVisible({ timeout: 3000 }).catch(() => false)) {
        console.log('[MOBILE] Dismissing onboarding banner...');
        await mobileOnboardingClose.click({ force: true });
        await page.waitForTimeout(2000);
        console.log('[MOBILE] Onboarding dismissed.');
      }
    } catch (e) {
      console.log('[MOBILE] No onboarding to dismiss:', e.message);
    }

    // Try clicking navbar toggler
    try {
      const toggler = page.locator('.navbar-toggler');
      const togglerVisible = await toggler.isVisible({ timeout: 3000 }).catch(() => false);
      console.log(`[MOBILE] .navbar-toggler visible: ${togglerVisible}`);

      if (togglerVisible) {
        // Use force click to bypass any overlay
        await toggler.click({ force: true });
        await page.waitForTimeout(3000);
        console.log('[MOBILE] Navbar toggler clicked (force).');
      }
    } catch (e) {
      console.log('[MOBILE] Toggler click failed:', e.message);
    }

    // Get mobile map height
    const mobileDiag = await page.evaluate(() => {
      const map = document.querySelector('#ruckr-map');
      const article = document.querySelector('article.content');
      const sidebar = document.querySelector('.sidebar');
      const navCollapse = document.querySelector('.nav-scrollable, .navbar-collapse');
      
      const getLayout = (el) => {
        if (!el) return null;
        const cs = window.getComputedStyle(el);
        return {
          tag: el.tagName,
          offsetHeight: el.offsetHeight,
          clientHeight: el.clientHeight,
          display: cs.display,
          flex: cs.flex,
          flexDirection: cs.flexDirection,
          overflow: cs.overflow,
          position: cs.position
        };
      };

      return {
        map: getLayout(map),
        article: getLayout(article),
        sidebar: getLayout(sidebar),
        navCollapse: getLayout(navCollapse),
        windowInnerHeight: window.innerHeight,
        leafletContainer: map ? map.querySelector('.leaflet-container') !== null : false
      };
    });
    console.log('[MOBILE] Layout diagnostics:', JSON.stringify(mobileDiag, null, 2));

    results.mobile.height_px = mobileDiag.map?.offsetHeight || 0;

    // Check map tiles
    const mapTiles = await page.evaluate(() => {
      const tiles = document.querySelectorAll('.leaflet-tile-loaded');
      return tiles.length;
    });
    console.log(`[MOBILE] Loaded map tiles: ${mapTiles}`);

    await page.screenshot({ path: `${SCREENSHOT_DIR}\\mobile-map-nav-fixed.png`, fullPage: false });
    console.log('[MOBILE] Screenshot saved.');

    results.mobile.verdict = results.mobile.height_px > 0 ? 'PASS' : 'FAIL';
    results.mobile.details.push(
      `height=${results.mobile.height_px}px`,
      `map tiles: ${mapTiles}`,
      `nav toggler visible: ${mobileDiag.sidebar?.display !== 'none'}`
    );

    // Console errors
    console.log('\n[CONSOLE ERRORS]:', consoleErrors.length);
    consoleErrors.forEach((e, i) => console.log(`  [${i}] ${e.substring(0, 200)}`));

    await page.close();
    await context.close();
  } catch (e) {
    console.error('FATAL:', e.message);
  } finally {
    await browser.close();
  }

  // Final output
  console.log('\n========== RESULTS ==========');
  console.log(`Desktop: ${results.desktop.height_px}px - ${results.desktop.verdict}`);
  console.log(`Mobile:  ${results.mobile.height_px}px - ${results.mobile.verdict}`);
  console.log(JSON.stringify(results, null, 2));
}

main().catch(e => { console.error(e); process.exit(1); });
