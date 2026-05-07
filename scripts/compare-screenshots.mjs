import fs from 'fs';
import { PNG } from 'pngjs';
import pixelmatch from 'pixelmatch';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SCREENSHOTS_DIR = path.join(__dirname, '..', 'docs', 'plan', '20260506-nav-menu-overlap-map-fix', 'screenshots');

// Extract a region from a PNG as a new PNG buffer
function extractRegion(png, x, y, w, h) {
  const region = new PNG({ width: w, height: h });
  for (let row = 0; row < h; row++) {
    const srcStart = ((y + row) * png.width + x) * 4;
    const dstStart = row * w * 4;
    region.data.set(png.data.subarray(srcStart, srcStart + w * 4), dstStart);
  }
  return region;
}

function compareImages(beforePath, afterPath, diffPath, label) {
  console.log(`\n=== Comparing ${label} ===`);
  console.log(`  Before: ${path.basename(beforePath)}`);
  console.log(`  After:  ${path.basename(afterPath)}`);

  if (!fs.existsSync(beforePath)) {
    console.log(`  SKIP: Before image not found`);
    return null;
  }
  if (!fs.existsSync(afterPath)) {
    console.log(`  SKIP: After image not found`);
    return null;
  }

  const before = PNG.sync.read(fs.readFileSync(beforePath));
  const after = PNG.sync.read(fs.readFileSync(afterPath));

  console.log(`  Before: ${before.width}x${before.height}`);
  console.log(`  After:  ${after.width}x${after.height}`);

  // Compare only the overlapping region
  const cmpWidth = Math.min(before.width, after.width);
  const cmpHeight = Math.min(before.height, after.height);

  // Extract overlapping regions from both images
  const bRegion = extractRegion(before, 0, 0, cmpWidth, cmpHeight);
  const aRegion = extractRegion(after, 0, 0, cmpWidth, cmpHeight);

  const diff = new PNG({ width: cmpWidth, height: cmpHeight });
  const diffPixels = pixelmatch(
    bRegion.data, aRegion.data,
    diff.data,
    cmpWidth, cmpHeight,
    {
      threshold: 0.1,
      includeAA: false,
      alpha: 0.3,
      diffColor: [255, 0, 0],
    }
  );

  const totalPixels = cmpWidth * cmpHeight;
  const diffPercent = ((diffPixels / totalPixels) * 100).toFixed(2);

  if (diffPath) {
    fs.writeFileSync(diffPath, PNG.sync.write(diff));
    console.log(`  Diff saved: ${diffPath}`);
  }

  console.log(`  Overlap region: ${cmpWidth}x${cmpHeight}`);
  console.log(`  Different pixels: ${diffPixels.toLocaleString()} / ${totalPixels.toLocaleString()} (${diffPercent}%)`);

  // For desktop: analyze sidebar region (x:0-249) separately
  if (label.includes('desktop') && cmpWidth >= 250) {
    const sbW = 250;
    const sbBefore = extractRegion(before, 0, 0, sbW, cmpHeight);
    const sbAfter = extractRegion(after, 0, 0, sbW, cmpHeight);
    const sbDiff = new PNG({ width: sbW, height: cmpHeight });
    const sbDiffPx = pixelmatch(sbBefore.data, sbAfter.data, sbDiff.data, sbW, cmpHeight,
      { threshold: 0.1, includeAA: false, alpha: 0.3 });
    const sbPct = ((sbDiffPx / (sbW * cmpHeight)) * 100).toFixed(2);
    console.log(`  Sidebar (0-250px): ${sbDiffPx.toLocaleString()} diff pixels (${sbPct}%)`);

    // Map region (x:250+)
    const mapW = cmpWidth - 250;
    if (mapW > 0) {
      const mapBefore = extractRegion(before, 250, 0, mapW, cmpHeight);
      const mapAfter = extractRegion(after, 250, 0, mapW, cmpHeight);
      const mapDiff = new PNG({ width: mapW, height: cmpHeight });
      const mapDiffPx = pixelmatch(mapBefore.data, mapAfter.data, mapDiff.data, mapW, cmpHeight,
        { threshold: 0.1, includeAA: false, alpha: 0.3 });
      const mapPct = ((mapDiffPx / (mapW * cmpHeight)) * 100).toFixed(2);
      console.log(`  Map (250+): ${mapDiffPx.toLocaleString()} diff pixels (${mapPct}%)`);
    }
  }

  // For mobile: check if nav menu and map layout changed
  if (label.includes('mobile')) {
    console.log(`  Mobile layout comparison: ${before.width}x${before.height} → ${after.width}x${after.height}`);
  }

  if (before.height !== after.height) {
    const delta = after.height - before.height;
    console.log(`  Height change: ${delta > 0 ? '+' : ''}${delta}px (${delta > 0 ? 'taller' : 'shorter'})`);
  }

  return {
    label,
    totalPixels,
    diffPixels,
    diffPercent: parseFloat(diffPercent),
    cmpWidth,
    cmpHeight,
    heightDelta: after.height - before.height,
  };
}

// Run comparisons
const results = [];

results.push(compareImages(
  path.join(SCREENSHOTS_DIR, 'before', 'desktop-map-nav-overlap.png'),
  path.join(SCREENSHOTS_DIR, 'after', 'desktop-map-nav-fixed.png'),
  path.join(SCREENSHOTS_DIR, 'after', 'desktop-diff.png'),
  'desktop'
));

results.push(compareImages(
  path.join(SCREENSHOTS_DIR, 'before', 'mobile-map-nav-overlap.png'),
  path.join(SCREENSHOTS_DIR, 'after', 'mobile-map-nav-fixed.png'),
  path.join(SCREENSHOTS_DIR, 'after', 'mobile-diff.png'),
  'mobile'
));

// Summary
console.log('\n\n══════════ COMPARISON SUMMARY ══════════');
for (const r of results) {
  if (!r) continue;
  const tag = r.label.toUpperCase();
  const verdict = r.diffPercent < 5 ? 'MINIMAL change (expected stylistic diff)' 
                : r.diffPercent < 20 ? 'MODERATE change (layout fix likely)'
                : 'SIGNIFICANT change (major layout difference)';
  console.log(`\n[${tag}] ${r.cmpWidth}x${r.cmpHeight} | Diff: ${r.diffPercent}% | Height Δ: ${r.heightDelta > 0 ? '+' : ''}${r.heightDelta}px`);
  console.log(`  Verdict: ${verdict}`);
}
