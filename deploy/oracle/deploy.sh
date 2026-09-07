#!/usr/bin/env bash
set -euo pipefail

# Execute com sudo, a partir de um clone atualizado deste repositório.
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
APP_ROOT="/opt/casamento"
RELEASES_DIR="$APP_ROOT/releases"
RELEASE_DIR="$RELEASES_DIR/$(date -u +%Y%m%d%H%M%S)"

if [[ "${EUID}" -ne 0 ]]; then
    echo "Execute: sudo ./deploy/oracle/deploy.sh" >&2
    exit 1
fi

install -d -o casamento -g casamento -m 0755 "$RELEASES_DIR"
dotnet publish "$PROJECT_DIR/CasamentoTatianaDiogo.csproj" --configuration Release --output "$RELEASE_DIR"
chown -R casamento:casamento "$RELEASE_DIR"

# A versão atual só é trocada depois de uma publicação concluída com sucesso.
ln -sTfn "$RELEASE_DIR" "$APP_ROOT/current"
systemctl restart casamento
sleep 2
systemctl --no-pager --full status casamento
curl --fail --silent --show-error http://127.0.0.1:5000/health > /dev/null
echo "Publicação concluída: $RELEASE_DIR"
