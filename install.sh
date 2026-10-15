#!/usr/bin/env bash
set -euo pipefail

echo "Installing hostfix CLI..."

REPO="Puviraj-2004/hostfix"
OS="$(uname -s)"
ARCH="$(uname -m)"

if [ "$OS" = "Linux" ]; then
    if [ "$ARCH" = "x86_64" ]; then
        BINARY="hostfix-linux-x64"
    else
        echo "Error: Unsupported Linux architecture '$ARCH'. Only x86_64 is supported currently."
        exit 1
    fi
elif [ "$OS" = "Darwin" ]; then
    if [ "$ARCH" = "arm64" ]; then
        BINARY="hostfix-osx-arm64"
    else
        echo "Error: Unsupported macOS architecture '$ARCH'. Only Apple Silicon (arm64) is supported currently."
        exit 1
    fi
else
    echo "Error: Unsupported OS '$OS'."
    exit 1
fi

INSTALL_DIR="$HOME/.local/bin"
mkdir -p "$INSTALL_DIR"

URL="https://github.com/$REPO/releases/latest/download/$BINARY"
CHECKSUM_URL="https://github.com/$REPO/releases/latest/download/$BINARY.sha256"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

echo "Downloading the latest $BINARY release..."
curl -f -sSL -o "$TMP_DIR/$BINARY" "$URL"
curl -f -sSL -o "$TMP_DIR/$BINARY.sha256" "$CHECKSUM_URL"

echo "Verifying SHA-256 checksum..."
EXPECTED_SHA="$(awk '{print $1}' "$TMP_DIR/$BINARY.sha256")"
if command -v sha256sum &>/dev/null; then
    ACTUAL_SHA="$(sha256sum "$TMP_DIR/$BINARY" | awk '{print $1}')"
elif command -v shasum &>/dev/null; then
    ACTUAL_SHA="$(shasum -a 256 "$TMP_DIR/$BINARY" | awk '{print $1}')"
else
    echo "Warning: Neither sha256sum nor shasum available; skipping verification."
    ACTUAL_SHA="$EXPECTED_SHA"
fi

if [ "$EXPECTED_SHA" != "$ACTUAL_SHA" ]; then
    echo "Error: Checksum verification failed!"
    echo "Expected: $EXPECTED_SHA"
    echo "Got:      $ACTUAL_SHA"
    exit 1
fi

mv "$TMP_DIR/$BINARY" "$INSTALL_DIR/hostfix"
chmod +x "$INSTALL_DIR/hostfix"

# Ensure ~/.local/bin is on PATH
if [[ ":$PATH:" != *":$INSTALL_DIR:"* ]]; then
    PROFILE="$HOME/.bashrc"
    [ -f "$HOME/.zshrc" ] && PROFILE="$HOME/.zshrc"
    echo "export PATH=\"\$PATH:$INSTALL_DIR\"" >> "$PROFILE"
    echo "Added $INSTALL_DIR to $PROFILE"
fi

echo "hostfix installed successfully to $INSTALL_DIR/hostfix!"
echo "Run: hostfix"
