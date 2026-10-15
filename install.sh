#!/usr/bin/env bash
set -e

echo "Installing hostfix CLI..."

REPO="Puviraj-2004/hostfix"
OS="$(uname -s)"
ARCH="$(uname -m)"

if [ "$OS" = "Linux" ]; then
    BINARY="hostfix-linux-x64"
elif [ "$OS" = "Darwin" ]; then
    BINARY="hostfix-osx-arm64"
else
    echo "Unsupported OS: $OS"
    exit 1
fi

INSTALL_DIR="$HOME/.local/bin"
mkdir -p "$INSTALL_DIR"

URL="https://github.com/$REPO/releases/latest/download/$BINARY"
echo "Downloading $BINARY from GitHub Releases..."
curl -sSL -o "$INSTALL_DIR/hostfix" "$URL"
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
