#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VERSION="${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n1)}"
ARCH="${ARCH:-amd64}"

case "$ARCH" in
    amd64) RID="linux-x64" ;;
    arm64) RID="linux-arm64" ;;
    *)
        echo "Unsupported ARCH '$ARCH' (use amd64 or arm64)." >&2
        exit 1
        ;;
esac

if [[ -z "$VERSION" ]]; then
    echo "Could not determine the package version." >&2
    exit 1
fi

if ! command -v dpkg-deb >/dev/null 2>&1; then
    echo "dpkg-deb is required. Install it or run this script on Debian/Ubuntu." >&2
    exit 1
fi

DOTNET="${DOTNET:-dotnet}"
PUBLISH="$ROOT/build/linux/$RID"
STAGE="$ROOT/build/deb/guvenon_${VERSION}_${ARCH}"
OUT="$ROOT/artifacts/linux"

rm -rf "$PUBLISH" "$STAGE"
mkdir -p "$OUT"

"$DOTNET" publish src/GuvenOn.Linux/GuvenOn.Linux.csproj \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$PUBLISH"

"$PUBLISH/guvenon-linuxd" version
"$PUBLISH/guvenon-linuxd" --print-ruleset > /dev/null

mkdir -p \
    "$STAGE/DEBIAN" \
    "$STAGE/usr/lib/guvenon" \
    "$STAGE/usr/bin" \
    "$STAGE/lib/systemd/system" \
    "$STAGE/usr/share/doc/guvenon"

cp -a "$PUBLISH/." "$STAGE/usr/lib/guvenon/"
chmod 0755 "$STAGE/usr/lib/guvenon/guvenon-linuxd"
ln -s /usr/lib/guvenon/guvenon-linuxd "$STAGE/usr/bin/guvenonctl"

install -m 0644 linux/guvenon.service "$STAGE/lib/systemd/system/guvenon.service"
install -m 0644 LICENSE "$STAGE/usr/share/doc/guvenon/copyright"
install -m 0644 README.md "$STAGE/usr/share/doc/guvenon/README.md"

sed -e "s/__VERSION__/$VERSION/g" -e "s/__ARCH__/$ARCH/g" \
    linux/deb/control > "$STAGE/DEBIAN/control"

install -m 0755 linux/deb/postinst "$STAGE/DEBIAN/postinst"
install -m 0755 linux/deb/prerm "$STAGE/DEBIAN/prerm"
install -m 0755 linux/deb/postrm "$STAGE/DEBIAN/postrm"

dpkg-deb --root-owner-group --build "$STAGE" "$OUT/guvenon_${VERSION}_${ARCH}.deb"

(
    cd "$OUT"
    sha256sum "guvenon_${VERSION}_${ARCH}.deb" | tee "guvenon_${VERSION}_${ARCH}.deb.sha256"
)

echo "Built $OUT/guvenon_${VERSION}_${ARCH}.deb"
