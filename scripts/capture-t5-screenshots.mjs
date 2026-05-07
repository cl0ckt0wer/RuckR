import { chromium } from 'playwright';
import { fileURLToPath } from 'url';
import path from 'path';
import fs from 'fs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BASE = 'https://localhost:7161';
const EMAIL = 'TestUserWithAVeryLongDisplayName@test.com';
const PASSWORD = 'TestPass123!';
const AFTER_DIR = path.join(__dirname, '..', 'docs', 'plan', '20260506-nav-menu-overlap-map-fix', 'screenshots', 'after');

fs.mkdirSync(AFTER_DIR, { recursive: true });

const browser = await chromium.launch({
  headless: true,
  args: ['--ignore-certificate-errors', '--ignore-certificate-errors-spki-list'],
});

const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1280, height: 720 },
});

const page = await context.newPage();

// ── STEP 1: Authenticate ──────────────────────────────────────
console.log('[1] Navigating to login page...');
await page.goto(`${BASE}/Identity/Account/Login`, { waitUntil: 'networkidle', timeout: 30000 });
await page.waitForTimeout(1000);

let content = await page.content();
console.log('[1] Page contains "Log in":', content.includes('Log in'));

console.log('[1a] Attempting login...');
await page.fill('input[name="Input.Email"]', EMAIL);
await page.fill('input[name="Input.Password"]', PASSWORD);
await page.locator('button[type="submit"]').first().click();
await page.waitForLoadState('networkidle', { timeout: 30000 });
await page.waitForTimeout(500);

content = await page.content();
console.log('[1b] After login attempt. URL:', page.url());

if (page.url().includes('/Login') || content.includes('Invalid login attempt')) {
  console.log('[1c] Login failed — registering new user...');
  await page.goto(`${BASE}/Identity/Account/Register`, { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(500);
  
  await page.fill('input[name="Input.Email"]', EMAIL);
  await page.fill('input[name="Input.Password"]', PASSWORD);
  await page.fill('input[name="Input.ConfirmPassword"]', PASSWORD);
  await page.locator('button[type="submit"]').first().click();
  await page.waitForLoadState('networkidle', { timeout: 30000 });
  await page.waitForTimeout(500);
  
  console.log('[1d] After registration. URL:', page.url());
  
  if (page.url().includes('/Login')) {
    console.log('[1e] Logging in after registration...');
    await page.fill('input[name="Input.Email"]', EMAIL);
    await page.fill('input[name="Input.Password"]', PASSWORD);
    await page.locator('button[type="submit"]').first().click();
    await page.waitForLoadState('networkidle', { timeout: 30000 });
    await page.waitForTimeout(500);
  }
}

console.log('[2] Authenticated. URL:', page.url());

// ── STEP 2: Navigate to /map (desktop viewport) ───────────────
console.log('[3] Navigating to /map at 1280x720...');
await page.goto(`${BASE}/map`, { waitUntil: 'networkidle', timeout: 30000 });
await page.waitForTimeout(2000);

// Dismiss onboarding banner
try {
  const ob = page.locator('[data-testid="onboarding-banner"] .btn-close').first();
  if (await ob.isVisible({ timeout: 2000 }).catch(() => false)) {
    console.log('[3a] Dismissing onboarding banner...');
    await ob.click({ force: true });
    await page.waitForTimeout(500);
  }
} catch (e) { /* ignore */ }

// Dismiss GPS banner
try {
  const gps = page.locator('.alert-warning .btn-close').first();
  if (await gps.isVisible({ timeout: 1000 }).catch(() => false)) {
    console.log('[3b] Dismissing GPS banner...');
    await gps.click({ force: true });
    await page.waitForTimeout(500);
  }
} catch (e) { /* ignore */ }

// Wait for map container to be present (even if hidden behind loading)
console.log('[4] Waiting for map container...');
try {
  await page.waitForSelector('#ruckr-map', { state: 'attached', timeout: 15000 });
  // Wait for loading to potentially finish
  await page.waitForTimeout(8000);
} catch (e) {
  console.log('[4] Map container issue:', e.message);
  await page.waitForTimeout(5000);
}

// Check current page state
const mapVisible = await page.locator('#ruckr-map').isVisible().catch(() => false);
const loadingVisible = await page.locator('[data-testid="map-loading"]').isVisible().catch(() => false);
console.log('[4] Map visible:', mapVisible, '| Loading visible:', loadingVisible);

// If map still showing loading spinner, take screenshot anyway — 
// the CSS layout is what we're testing, not map tile rendering.
if (!mapVisible && loadingVisible) {
  console.log('[4a] Map still loading — capturing layout state anyway.');
}

// ── STEP 3: Desktop screenshot (1280×720) ─────────────────────
console.log('[5] Capturing DESKTOP screenshot...');
const desktopPath = path.join(AFTER_DIR, 'desktop-map-nav-fixed.png');
await page.screenshot({ path: desktopPath, fullPage: true });
console.log('[5] Saved:', desktopPath);

// ── STEP 4: Mobile screenshot (375×667) ───────────────────────
// IMPORTANT: Don't re-navigate — just resize. Blazor WASM re-renders.
console.log('[6] Resizing to mobile viewport (375×667)...');
await page.setViewportSize({ width: 375, height: 667 });
await page.waitForTimeout(2000); // Let Blazor re-render + Leaflet resize

// Dismiss any popup overlays that appeared after resize
try {
  const ob = page.locator('[data-testid="onboarding-banner"] .btn-close').first();
  if (await ob.isVisible({ timeout: 1000 }).catch(() => false)) {
    await ob.click({ force: true });
    await page.waitForTimeout(500);
  }
} catch (e) { /* ignore */ }
try {
  const gps = page.locator('.alert-warning .btn-close').first();
  if (await gps.isVisible({ timeout: 1000 }).catch(() => false)) {
    await gps.click({ force: true });
    await page.waitForTimeout(500);
  }
} catch (e) { /* ignore */ }

// Expand nav menu
console.log('[7] Expanding navbar on mobile...');
try {
  const toggler = page.locator('.navbar-toggler').first();
  if (await toggler.isVisible({ timeout: 3000 }).catch(() => false)) {
    // Check if nav is already expanded
    const navContent = page.locator('#navbarNav .navbar-collapse, .collapse.navbar-collapse').first();
    const isExpanded = await navContent.isVisible().catch(() => false);
    
    if (!isExpanded) {
      await toggler.click({ force: true });
      await page.waitForTimeout(1500);
      console.log('[7] Toggler clicked, nav should be expanded');
    } else {
      console.log('[7] Nav already expanded');
    }
  } else {
    console.log('[7] Navbar toggler not visible (likely desktop nav style)');
  }
} catch (e) {
  console.log('[7] Toggler error:', e.message);
}

// Wait for layout to settle
await page.waitForTimeout(2000);

// Check map state on mobile
const mobileMapVisible = await page.locator('#ruckr-map').isVisible().catch(() => false);
const mobileLoadingVisible = await page.locator('[data-testid="map-loading"]').isVisible().catch(() => false);
console.log('[8] Mobile — map visible:', mobileMapVisible, '| loading:', mobileLoadingVisible);

// Capture mobile screenshot
console.log('[9] Capturing MOBILE screenshot...');
const mobilePath = path.join(AFTER_DIR, 'mobile-map-nav-fixed.png');
await page.screenshot({ path: mobilePath, fullPage: true });
console.log('[9] Saved:', mobilePath);

// ── STEP 5: Cleanup ───────────────────────────────────────────
await browser.close();
console.log('\n=== T5 Screenshot capture complete ===');
console.log('Desktop:', desktopPath);
console.log('Mobile:', mobilePath);
