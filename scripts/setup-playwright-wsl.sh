#!/usr/bin/env bash
# =============================================================================
# setup-playwright-wsl.sh
# Install Playwright + Chromium for gstack browse daemon in WSL2 with WSLg.
#
# Key findings from research:
#   - WSLg provides built-in Wayland/X11 — no X server install needed
#   - DISPLAY=:0 already set by WSLg — headed mode works out of the box
#   - Linux-native node (nvm) and bun must be first in PATH, NOT Windows interop
#   - Chromium in WSL may need --no-sandbox (handled by gstack daemon)
#   - fonts-liberation needed for proper text rendering
#   - ~290MB download for Chromium browser binary
#
# Usage: chmod +x scripts/setup-playwright-wsl.sh && ./scripts/setup-playwright-wsl.sh
# =============================================================================
set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
log()  { echo -e "${GREEN}[OK]${NC} $*"; }
warn() { echo -e "${YELLOW}[--]${NC} $*"; }
err()  { echo -e "${RED}[!!]${NC} $*"; }

echo ""
echo "============================================"
echo " RuckR — Playwright + gstack WSL Setup"
echo "============================================"
echo ""

# ── 1. Ensure ~/.bun/bin is on PATH ──────────────────────
if ! echo "$PATH" | grep -q "$HOME/.bun/bin"; then
    echo "Adding ~/.bun/bin to PATH in ~/.bashrc..."
    echo 'export PATH="$HOME/.bun/bin:$PATH"' >> "$HOME/.bashrc"
    export PATH="$HOME/.bun/bin:$PATH"
    log "~/.bun/bin added to PATH"
else
    log "~/.bun/bin already on PATH"
fi

# ── 2. Verify native Linux tools (not Windows interop) ───
check_native() {
    local tool="$1"
    local path
    path=$(command -v "$tool" 2>/dev/null || true)
    if [ -z "$path" ]; then
        err "$tool not found on PATH"
        exit 1
    fi
    # Windows interop binaries live under /mnt/c/
    if echo "$path" | grep -q "^/mnt/"; then
        err "$tool resolves to Windows interop wrapper: $path"
        err "This causes UNC path errors. Linux-native $tool must come first in PATH."
        exit 1
    fi
    echo "$path"
}

BUN_PATH=$(check_native bun)
NODE_PATH=$(check_native node)
log "bun:  $BUN_PATH  ($(bun --version 2>/dev/null))"
log "node: $NODE_PATH  ($(node --version 2>/dev/null))"

# ── 3. Check WSLg display server ─────────────────────────
echo ""
if [ -d /mnt/wslg ] && [ -n "${DISPLAY:-}" ]; then
    log "WSLg detected — headed Chromium will work (DISPLAY=$DISPLAY)"
else
    warn "WSLg not detected — only headless mode will work"
    warn "If you need headed mode (visible browser), install WSLg or an X server"
fi

# ── 4. Install fonts-liberation ──────────────────────────
echo ""
echo "Checking fonts-liberation..."
if ! dpkg -l fonts-liberation 2>/dev/null | grep -q "^ii"; then
    warn "fonts-liberation not installed. Installing..."
    sudo apt-get update -qq && sudo apt-get install -y -qq fonts-liberation
    log "fonts-liberation installed"
else
    log "fonts-liberation already installed"
fi

# ── 5. Check Chromium system dependencies ────────────────
echo ""
echo "Checking Chromium system dependencies..."
MISSING_DEPS=""
for dep in \
    libnss3 libnspr4 libatk1.0-0t64 libatk-bridge2.0-0t64 \
    libcups2t64 libdrm2 libdbus-1-3 libgbm1 \
    libxkbcommon0 libx11-xcb1 libxcomposite1 libxdamage1 \
    libxext6 libxfixes3 libxrandr2 libxrender1 libxtst6 \
    libgtk-3-0t64 libpango-1.0-0 libcairo2 libasound2t64; do
    if ! dpkg -l "$dep" 2>/dev/null | grep -q "^ii"; then
        MISSING_DEPS="$MISSING_DEPS $dep"
    fi
done

if [ -n "$MISSING_DEPS" ]; then
    warn "Missing system packages:$MISSING_DEPS"
    sudo apt-get install -y -qq $MISSING_DEPS
    log "System dependencies installed"
else
    log "All Chromium system dependencies present"
fi

# ── 6. Resolve gstack path (OpenCode-first, Claude fallback) ──
echo ""
GSTACK_DIR=""
if [ -d "$HOME/.config/opencode/skills/gstack" ]; then
    GSTACK_DIR="$HOME/.config/opencode/skills/gstack"
elif [ -d "$HOME/.claude/skills/gstack" ]; then
    GSTACK_DIR="$HOME/.claude/skills/gstack"
fi

BUN_EXE="$HOME/.bun/bin/bun"

if [ -z "$GSTACK_DIR" ]; then
    err "gstack not found at either:"
    err "  $HOME/.config/opencode/skills/gstack"
    err "  $HOME/.claude/skills/gstack"
    exit 1
fi

log "Using gstack at: $GSTACK_DIR"

# ── 7. Install Playwright Chromium browser ────────────────
echo ""

echo "Ensuring Playwright Chromium is installed (~290 MB first download)..."
cd "$GSTACK_DIR"

# Prefer bunx; fall back to npx
if INSTALLED_OUTPUT=$("$BUN_EXE" x playwright install --list 2>&1); then
    if printf '%s\n' "$INSTALLED_OUTPUT" | grep -Eq '^[[:space:]]*chromium([[:space:]]|@|$)'; then
        log "Playwright Chromium already installed (bunx check)"
    else
        echo "Installing Playwright Chromium..."
        "$BUN_EXE" x playwright install chromium 2>&1
        log "Playwright Chromium installed via bunx"
    fi
else
    warn "bunx check failed, trying npx (native Linux node)..."
    NPX_PATH="$HOME/.bun/bin:$HOME/.nvm/versions/node/v24.15.0/bin:$PATH"
    INSTALLED_OUTPUT=$(PATH="$NPX_PATH" npx playwright install --list 2>&1 || true)

    if printf '%s\n' "$INSTALLED_OUTPUT" | grep -Eq '^[[:space:]]*chromium([[:space:]]|@|$)'; then
        log "Playwright Chromium already installed (npx check)"
    else
        echo "Installing Playwright Chromium..."
        PATH="$NPX_PATH" npx playwright install chromium 2>&1
        log "Playwright Chromium installed via npx"
    fi
fi

# ── 8. Verify Playwright works (headless, then headed) ────
echo ""
echo "Verifying Playwright headless launch..."
HEADLESS_OUTPUT=$(node -e '
const { chromium } = require("playwright");
(async () => {
    try {
        const browser = await chromium.launch({ headless: true });
        const page = await browser.newPage();
        await page.goto("about:blank", { waitUntil: "domcontentloaded" });
        const title = await page.title();
        await browser.close();
        console.log("Headless: OK — title: " + JSON.stringify(title));
    } catch (e) {
        console.log("Headless: FAILED — " + e.message);
        process.exit(1);
    }
})();
' 2>&1) || true
echo "  $HEADLESS_OUTPUT"

# Only test headed if WSLg is available
if [ -d /mnt/wslg ] && [ -n "${DISPLAY:-}" ]; then
    echo ""
    echo "Verifying Playwright headed launch (browser window should flash)..."
    HEADED_OUTPUT=$(timeout 10 node -e '
const { chromium } = require("playwright");
(async () => {
    try {
        const browser = await chromium.launch({ headless: false });
        const page = await browser.newPage();
        await page.goto("about:blank", { waitUntil: "domcontentloaded" });
        await browser.close();
        console.log("Headed: OK");
    } catch (e) {
        console.log("Headed: FAILED — " + e.message);
        process.exit(1);
    }
})();
' 2>&1) || echo "  Headed: SKIPPED (timed out or WSLg not responding)"
    echo "  $HEADED_OUTPUT"
fi

echo ""
log "Playwright verification complete"

# ── 9. Verify gstack browse binary ────────────────────────
echo ""
BROWSE_BIN="$GSTACK_DIR/browse/dist/browse"
if [ -x "$BROWSE_BIN" ]; then
    log "gstack browse binary ready: $BROWSE_BIN"
else
    warn "gstack browse binary not found"
    warn "Run: cd $GSTACK_DIR && PATH=\"$HOME/.bun/bin:\$PATH\" ./setup --host opencode"
fi

# ── 10. Summary ───────────────────────────────────────────
echo ""
echo "============================================"
echo " Setup complete."
echo ""
echo " WSL environment:"
echo "   WSLg:        $( [ -d /mnt/wslg ] && echo 'yes (headed mode works)' || echo 'no (headless only)' )"
echo "   bun:         $BUN_PATH"
echo "   node:        $NODE_PATH"
echo "   display:     ${DISPLAY:-none}"
echo ""
echo " To use gstack browser skills:"
echo "   1. Restart shell: exec \$SHELL"
echo "   2. Test browse:   ~/.config/opencode/skills/gstack/browse/dist/browse status"
echo "      (fallback):    ~/.claude/skills/gstack/browse/dist/browse status"
echo "   3. First QA test: invoke /qa https://localhost:7161 in OpenCode"
echo "============================================"
