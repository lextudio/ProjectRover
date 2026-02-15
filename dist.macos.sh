#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
cd "$script_dir"

echo "=== ProjectRover macOS Universal Binary Build ==="
echo

# Kill any lingering dotnet processes to release file locks
echo "[0/6] Cleaning up lingering processes..."
killall dotnet 2>/dev/null || true
sleep 1

# Clean previous builds
echo "[1/6] Cleaning previous build artifacts..."
rm -rf src/ProjectRover/bin/Release src/ProjectRover/obj ProjectRover.app
find . -name "packages.lock.json" -delete

# Build ARM64
echo "[2/6] Building ARM64 (Apple Silicon)..."
if ! dotnet restore ./src/ProjectRover/ProjectRover.csproj -r osx-arm64 --force-evaluate -q; then
    echo "❌ ARM64 restore failed"
    exit 1
fi

if ! dotnet publish ./src/ProjectRover/ProjectRover.csproj \
    -r osx-arm64 \
    -c Release \
    --no-restore \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    --self-contained; then
    echo "❌ ARM64 publish failed"
    exit 1
fi

if [ ! -f "src/ProjectRover/bin/Release/net10.0/osx-arm64/publish/ProjectRover" ]; then
    echo "❌ ARM64 binary not found"
    exit 1
fi
echo "   ✓ ARM64 binary created"

# Build x64
echo "[3/6] Building x64 (Intel)..."
find . -name "packages.lock.json" -delete

if ! dotnet restore ./src/ProjectRover/ProjectRover.csproj -r osx-x64 --force-evaluate -q; then
    echo "❌ x64 restore failed"
    exit 1
fi

if ! dotnet publish ./src/ProjectRover/ProjectRover.csproj \
    -r osx-x64 \
    -c Release \
    --no-restore \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    --self-contained; then
    echo "❌ x64 publish failed"
    exit 1
fi

if [ ! -f "src/ProjectRover/bin/Release/net10.0/osx-x64/publish/ProjectRover" ]; then
    echo "❌ x64 binary not found"
    exit 1
fi
echo "   ✓ x64 binary created"

echo "[4/6] Creating universal application bundle..."
if ! ./build/macos/build-application-bundle.sh osx-universal; then
    echo "❌ Failed to create universal bundle"
    exit 1
fi

echo "[5/6] Verifying universal binary..."
if file ProjectRover.app/Contents/MacOS/ProjectRover | grep -q "universal binary"; then
    echo "   ✓ Universal binary verified (both x86_64 and arm64)"
else
    echo "❌ Binary is not a universal binary"
    exit 1
fi

echo "[6/6] Cleaning up temporary artifacts..."
rm -rf src/ProjectRover/bin src/ProjectRover/obj
find . -name "packages.lock.json" -delete

echo
echo "✅ Universal macOS application bundle created successfully!"
echo
echo "📍 Location: $(pwd)/ProjectRover.app"
echo
echo "🚀 To run the app:"
echo "   open ProjectRover.app"
echo "   # or"
echo "   ./ProjectRover.app/Contents/MacOS/ProjectRover"
echo
echo "📦 To distribute, create a tarball:"
echo "   tar -czf ProjectRover-macos-universal.tar.gz ProjectRover.app"
