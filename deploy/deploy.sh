#!/usr/bin/env bash
# Ships a WebGL build to the studio server and switches the served version.
#
#   deploy/deploy.sh <X.Y.Z.B>        e.g. deploy/deploy.sh 0.1.0.9
#
# Needs: a local build in Builds/<version>/WebGL (GGS Core BuildTool), rsync, and the SSH alias
# "ggs-vps" in ~/.ssh/config. On the server: Docker with the compose plugin, the host nginx site
# (deploy/nginx/host-site.conf) and the certificates, installed once by hand.
#
# Layout on the server, under $REMOTE_DIR:
#   docker-compose.yml, nginx/site.conf      copied from deploy/ at every run
#   releases/<version>/                      one folder per shipped build, kept for rollback
#   current -> releases/<version>            the version the container serves
set -euo pipefail

VERSION="${1:-}"
VPS="${RESUME_VPS:-ggs-vps}"
REMOTE_DIR="${RESUME_REMOTE_DIR:-/home/goras/resume}"
HERE="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$HERE/../Builds/$VERSION/WebGL"

if [[ -z "$VERSION" ]]; then
    echo "usage: deploy/deploy.sh <X.Y.Z.B>   (available: $(ls "$HERE/../Builds" 2>/dev/null | tr '\n' ' '))" >&2
    exit 1
fi
if [[ ! -f "$BUILD_DIR/index.html" ]]; then
    echo "no build at $BUILD_DIR" >&2
    exit 1
fi

echo "==> ResuME $VERSION -> $VPS:$REMOTE_DIR"

# 1. Compose file and container nginx config, then the build itself.
ssh "$VPS" "mkdir -p '$REMOTE_DIR/nginx' '$REMOTE_DIR/releases/$VERSION'"
rsync -az "$HERE/docker-compose.yml" "$VPS:$REMOTE_DIR/docker-compose.yml"
rsync -az "$HERE/nginx/site.conf" "$VPS:$REMOTE_DIR/nginx/site.conf"
# The host nginx site is only copied next to the rest: installing it needs sudo and is done by hand (README).
rsync -az "$HERE/nginx/host-site.conf" "$VPS:$REMOTE_DIR/nginx/host-site.conf"
rsync -az --delete "$BUILD_DIR/" "$VPS:$REMOTE_DIR/releases/$VERSION/"

# 2. Switch the served version and (re)start the container. The bind mount follows the symlink
#    target at container start, so the container is recreated to pick up the new version.
#    Guard: "current" must be a symlink; a real folder means Docker created it before the first
#    link (root-owned, the link would nest inside it), so stop rather than serve the wrong thing.
ssh "$VPS" "cd '$REMOTE_DIR' && if [ -e current ] && [ ! -L current ]; then echo 'current is a folder, not a symlink: remove it first' >&2; exit 1; fi"
ssh "$VPS" "cd '$REMOTE_DIR' && ln -sfn 'releases/$VERSION' current && docker compose up -d --force-recreate resume-web"

# 3. Check from the server itself (the host nginx and TLS are checked by hand after the first install).
ssh "$VPS" "sleep 2 && curl -sS -o /dev/null -w 'container answers: %{http_code}\n' http://127.0.0.1:8090/index.html"

echo "==> done. Previous versions stay in $REMOTE_DIR/releases for rollback:"
echo "    ssh $VPS \"cd $REMOTE_DIR && ln -sfn releases/<old> current && docker compose up -d --force-recreate resume-web\""
