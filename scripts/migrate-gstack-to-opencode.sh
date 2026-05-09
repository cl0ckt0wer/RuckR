#!/usr/bin/env bash
set -euo pipefail

OLD_ROOT="$HOME/.claude/skills/gstack"
NEW_ROOT="$HOME/.config/opencode/skills/gstack"
NEW_PARENT="$HOME/.config/opencode/skills"
OLD_PARENT="$HOME/.claude/skills"
STAMP=$(date +%Y%m%d%H%M%S)

echo "Starting gstack migration to OpenCode paths..."
echo "  old: $OLD_ROOT"
echo "  new: $NEW_ROOT"

mkdir -p "$NEW_PARENT"
mkdir -p "$OLD_PARENT"

if [ -L "$NEW_ROOT" ]; then
    new_target=$(readlink "$NEW_ROOT" || true)
    case "$new_target" in
        "$OLD_ROOT"*|"$OLD_ROOT/."*)
            echo "OpenCode path points into old Claude path. Removing stale symlink first."
            rm "$NEW_ROOT"
            ;;
    esac
fi

if [ -L "$OLD_ROOT" ]; then
    target=$(readlink "$OLD_ROOT")
    echo "Old path is already a symlink -> $target"
    if [ "$target" = "$NEW_ROOT" ]; then
        echo "Migration already complete."
        exit 0
    fi
fi

if [ -d "$NEW_ROOT" ] && [ ! -L "$NEW_ROOT" ] && [ -d "$OLD_ROOT" ] && [ ! -L "$OLD_ROOT" ]; then
    backup="$OLD_ROOT.pre-migration-$STAMP"
    echo "Both old and new directories exist. Syncing old -> new, then backing up old to: $backup"
    rsync -a --delete "$OLD_ROOT/" "$NEW_ROOT/"
    mv "$OLD_ROOT" "$backup"
fi

if [ -d "$OLD_ROOT" ] && [ ! -L "$OLD_ROOT" ] && [ ! -e "$NEW_ROOT" ]; then
    echo "Moving existing gstack installation to OpenCode path..."
    mv "$OLD_ROOT" "$NEW_ROOT"
fi

if [ ! -d "$NEW_ROOT" ] && [ ! -L "$NEW_ROOT" ]; then
    echo "No gstack installation found at old path."
    echo "Run gstack setup first, then rerun this migration script."
    exit 1
fi

if [ -L "$OLD_ROOT" ]; then
    rm "$OLD_ROOT"
elif [ -d "$OLD_ROOT" ]; then
    backup="$OLD_ROOT.backup-$STAMP"
    mv "$OLD_ROOT" "$backup"
    echo "Backed up old Claude path to: $backup"
fi

ln -s "$NEW_ROOT" "$OLD_ROOT"

echo "Migration complete."
echo "  OpenCode canonical path: $NEW_ROOT"
echo "  Claude compatibility symlink: $OLD_ROOT -> $NEW_ROOT"
