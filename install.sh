#!/bin/sh
# HeadlessCoder installer for Linux and macOS.
#
#   curl -fsSL https://raw.githubusercontent.com/SideswipeN7/HeadlessCoder/main/install.sh | sh
#
# Installs the `headlesscoder` binary and an `hc` shortcut into ~/.local/bin
# (override with HC_INSTALL_DIR). Pin a version with HC_VERSION=vX.Y.Z.
set -eu

REPO="SideswipeN7/HeadlessCoder"
BIN_NAME="headlesscoder"
ALIAS_NAME="hc"
INSTALL_DIR="${HC_INSTALL_DIR:-$HOME/.local/bin}"
VERSION="${HC_VERSION:-latest}"

info() { printf '\033[38;2;204;120;92m✳\033[0m %s\n' "$1"; }
err()  { printf '\033[38;2;198;69;69mError:\033[0m %s\n' "$1" >&2; exit 1; }

# --- fetch helpers (curl or wget) -------------------------------------------
have() { command -v "$1" >/dev/null 2>&1; }
fetch() { # url -> stdout
  if have curl; then curl -fsSL "$1"
  elif have wget; then wget -qO- "$1"
  else err "need curl or wget installed"; fi
}
download() { # url outfile
  if have curl; then curl -fSL "$1" -o "$2"
  elif have wget; then wget -q --show-progress -O "$2" "$1"
  else err "need curl or wget installed"; fi
}

# --- detect platform --------------------------------------------------------
os="$(uname -s)"
case "$os" in
  Linux)  OS="linux" ;;
  Darwin) OS="osx" ;;
  *) err "unsupported OS '$os' (this installer covers Linux and macOS; use install.ps1 on Windows)" ;;
esac

arch="$(uname -m)"
case "$arch" in
  x86_64|amd64)   ARCH="x64" ;;
  aarch64|arm64)  ARCH="arm64" ;;
  *) err "unsupported architecture '$arch'" ;;
esac

ASSET="${BIN_NAME}-${OS}-${ARCH}"

# --- resolve version --------------------------------------------------------
if [ "$VERSION" = "latest" ]; then
  info "Resolving latest release of $REPO ..."
  TAG="$(fetch "https://api.github.com/repos/${REPO}/releases/latest" \
    | grep '"tag_name"' | head -1 \
    | sed -E 's/.*"tag_name"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/')"
  [ -n "$TAG" ] || err "could not determine the latest release (set HC_VERSION=vX.Y.Z)"
else
  TAG="$VERSION"
fi

URL="https://github.com/${REPO}/releases/download/${TAG}/${ASSET}"

# --- install ----------------------------------------------------------------
mkdir -p "$INSTALL_DIR"
TARGET="$INSTALL_DIR/$BIN_NAME"

info "Downloading $ASSET ($TAG) ..."
download "$URL" "$TARGET" || err "download failed: $URL"
chmod +x "$TARGET"

# `hc` shortcut as a relative symlink alongside the binary.
ln -sf "$BIN_NAME" "$INSTALL_DIR/$ALIAS_NAME"

info "Installed $BIN_NAME -> $TARGET"
info "Shortcut  $ALIAS_NAME -> $BIN_NAME"

# --- PATH hint --------------------------------------------------------------
case ":${PATH}:" in
  *":${INSTALL_DIR}:"*) : ;;
  *)
    printf '\n'
    info "Add $INSTALL_DIR to your PATH, e.g.:"
    printf '    export PATH="%s:$PATH"\n' "$INSTALL_DIR"
    printf '  (add that line to ~/.bashrc, ~/.zshrc or ~/.profile)\n'
    ;;
esac

printf '\n'
info "Done. Run:  headlesscoder --no-sleep    (or just:  hc)"
