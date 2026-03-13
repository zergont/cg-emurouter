#!/usr/bin/env bash
# Управление службой cg-emurouter на Ubuntu 24
# Использование: sudo ./manage.sh install | update | uninstall

set -euo pipefail

REPO_URL="https://github.com/zergont/cg-emurouter.git"
INSTALL_DIR="/opt/cg-emurouter"
APP_DIR="$INSTALL_DIR/app"
SERVICE_NAME="cg-emurouter"
SERVICE_USER="cg-emulator"
UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; BOLD='\033[1m'; NC='\033[0m'
info()  { echo -e "${GREEN}[INFO]${NC}  $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error() { echo -e "${RED}[ERROR]${NC} $*" >&2; exit 1; }

require_root() {
    [[ $EUID -eq 0 ]] || error "Запустите от root: sudo ./manage.sh $1"
}

# ──────────────────────────────────────────────────────────────────────────────
cmd_install() {
    require_root install
    info "=== Установка $SERVICE_NAME ==="

    # 1. .NET 8 SDK
    if ! (command -v dotnet &>/dev/null && dotnet --version 2>/dev/null | grep -q "^8\."); then
        info "Установка .NET 8 SDK..."
        apt-get update -q
        apt-get install -y dotnet-sdk-8.0
    else
        info ".NET 8 SDK уже установлен ($(dotnet --version))."
    fi

    # 2. Системный пользователь
    if ! id "$SERVICE_USER" &>/dev/null; then
        info "Создание пользователя $SERVICE_USER..."
        useradd -r -s /usr/sbin/nologin "$SERVICE_USER"
    else
        info "Пользователь $SERVICE_USER уже существует."
    fi

    # 3. Репозиторий
    if [[ -d "$INSTALL_DIR/.git" ]]; then
        info "Репозиторий уже существует, выполняю git pull..."
        git -C "$INSTALL_DIR" pull
    else
        info "Клонирование репозитория в $INSTALL_DIR..."
        git clone "$REPO_URL" "$INSTALL_DIR"
    fi

    # 4. Публикация
    info "Сборка и публикация..."
    mkdir -p "$APP_DIR"
    dotnet publish "$INSTALL_DIR/src/CgEmulator" -c Release -o "$APP_DIR" --nologo -v quiet

    # 5. Конфигурация (не перезаписывать существующую)
    if [[ ! -f "$APP_DIR/emulator.yaml" ]]; then
        info "Создание emulator.yaml из примера..."
        cp "$INSTALL_DIR/emulator.example.yaml" "$APP_DIR/emulator.yaml"
        warn "Отредактируйте $APP_DIR/emulator.yaml (mqtt.host, web.bind_ip, web.port)"
    else
        info "Конфигурация уже существует, не перезаписываю."
    fi

    # 6. Права (только на опубликованное приложение)
    chown -R "$SERVICE_USER:$SERVICE_USER" "$APP_DIR"

    # 7. systemd-юнит
    info "Регистрация systemd-службы..."
    cat > "$UNIT_FILE" <<EOF
[Unit]
Description=CG Emurouter — эмулятор объектов MQTT
After=network.target

[Service]
Type=simple
User=$SERVICE_USER
WorkingDirectory=$APP_DIR
ExecStart=/usr/bin/dotnet $APP_DIR/CgEmulator.dll
Restart=on-failure
RestartSec=5
SyslogIdentifier=$SERVICE_NAME

[Install]
WantedBy=multi-user.target
EOF

    # 8. Включение и запуск
    systemctl daemon-reload
    systemctl enable "$SERVICE_NAME"
    systemctl start  "$SERVICE_NAME"
    systemctl status "$SERVICE_NAME" --no-pager -l

    local port
    port=$(grep -E '^\s+port:' "$APP_DIR/emulator.yaml" | head -1 | awk '{print $2}')
    local ip
    ip=$(hostname -I | awk '{print $1}')
    info "=== Установка завершена ==="
    info "Веб-интерфейс : http://${ip}:${port}/"
    info "Логи          : sudo journalctl -u $SERVICE_NAME -f"
}

# ──────────────────────────────────────────────────────────────────────────────
cmd_update() {
    require_root update
    info "=== Обновление $SERVICE_NAME ==="

    [[ -d "$INSTALL_DIR/.git" ]] \
        || error "Репозиторий не найден. Сначала выполните: sudo ./manage.sh install"

    info "Получение обновлений из Git..."
    git -C "$INSTALL_DIR" pull

    info "Пересборка и публикация..."
    dotnet publish "$INSTALL_DIR/src/CgEmulator" -c Release -o "$APP_DIR" --nologo -v quiet

    chown -R "$SERVICE_USER:$SERVICE_USER" "$APP_DIR"

    info "Перезапуск службы..."
    systemctl restart "$SERVICE_NAME"
    systemctl status  "$SERVICE_NAME" --no-pager -l

    info "=== Обновление завершено ==="
    warn "emulator.yaml не изменялся."
}

# ──────────────────────────────────────────────────────────────────────────────
cmd_uninstall() {
    require_root uninstall
    info "=== Удаление $SERVICE_NAME ==="

    if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
        info "Останавливаю службу..."
        systemctl stop "$SERVICE_NAME"
    fi

    if systemctl is-enabled --quiet "$SERVICE_NAME" 2>/dev/null; then
        systemctl disable "$SERVICE_NAME"
    fi

    if [[ -f "$UNIT_FILE" ]]; then
        rm -f "$UNIT_FILE"
        systemctl daemon-reload
    fi

    info "Удаление файлов $INSTALL_DIR..."
    rm -rf "$INSTALL_DIR"

    if id "$SERVICE_USER" &>/dev/null; then
        info "Удаление пользователя $SERVICE_USER..."
        userdel "$SERVICE_USER"
    fi

    info "=== Удаление завершено ==="
    warn "Для удаления .NET SDK:"
    warn "  sudo apt-get remove -y dotnet-sdk-8.0 && sudo apt-get autoremove -y"
}

# ──────────────────────────────────────────────────────────────────────────────
cmd_status() {
    echo ""
    echo -e "${BOLD}── Служба systemd ──────────────────────────────────────────────────${NC}"
    systemctl status "$SERVICE_NAME" --no-pager -l 2>/dev/null || true

    # Читаем порт из конфига
    local port="6666"
    if [[ -f "$APP_DIR/emulator.yaml" ]]; then
        port=$(grep -E '^\s+port:' "$APP_DIR/emulator.yaml" | head -1 | awk '{print $2}')
    fi
    local base_url="http://127.0.0.1:${port}"

    echo ""
    echo -e "${BOLD}── Симулятор (${base_url}) ──────────────────────────────────────────${NC}"

    local ver_json state_json replay_json
    if ! ver_json=$(curl -sf --max-time 3 "${base_url}/api/version" 2>/dev/null); then
        warn "Приложение не отвечает на ${base_url} — служба остановлена или порт закрыт."
        echo ""
        return
    fi

    state_json=$(curl -sf --max-time 3 "${base_url}/api/state")
    replay_json=$(curl -sf --max-time 3 "${base_url}/api/replay/status")

    VER_JSON="$ver_json" STATE_JSON="$state_json" REPLAY_JSON="$replay_json" \
    python3 - <<'PYEOF'
import json, os

STATE_NAMES = {0: 'простой', 1: 'запрос пуска', 2: 'пуск',
               3: 'прогрев', 4: 'работа', 5: 'останов', 6: 'авар.останов'}

ver    = json.loads(os.environ['VER_JSON'])
state  = json.loads(os.environ['STATE_JSON'])
replay = json.loads(os.environ['REPLAY_JSON'])

is_running = state.get('is_running', False)
period     = state.get('equipment_period_sec', '?')
objects    = state.get('objects', [])
run_str    = 'РАБОТАЕТ' if is_running else 'ОСТАНОВЛЕНО'

print(f"  Версия       : {ver.get('version', '?')}")
print(f"  Состояние    : {run_str}")
print(f"  Объектов     : {len(objects)}")
print(f"  Период PCC   : {period} с")

if objects:
    print()
    print(f"  {'Серийный №':<12}  {'Серв.':<6} {'Состояние 6109':<18} {'Нагрузка':>10} {'До смены':>9}")
    print(f"  {'-'*62}")
    for obj in objects:
        sn = obj.get('sn', '?')
        for e in obj.get('equipment', []):
            sid    = e.get('server_id', '?')
            s6109  = e.get('6109', '?')
            s34    = e.get('34', '?')
            ttrans = e.get('sec_to_transition', '?')
            sname  = STATE_NAMES.get(s6109, str(s6109))
            print(f"  {sn:<12}  #{sid:<5} {sname:<18} {str(s34)+' кВт':>10} {str(ttrans)+' с':>9}")
            sn = ''

print()
print(f"  ── Replay-буфер {'─'*46}")
mode    = 'Replaying' if replay.get('replaying') else 'Live'
buf     = replay.get('buffered', 0)
maxb    = replay.get('max', 0)
rate    = replay.get('rate', 0)
dropped = replay.get('droppedTotal', 0)
print(f"  Режим        : {mode}")
print(f"  Буфер        : {buf} / {maxb} сообщений")
print(f"  Скорость     : {rate} сообщ./с")
print(f"  Отброшено    : {dropped}")
PYEOF
    echo ""
}

# ──────────────────────────────────────────────────────────────────────────────
case "${1:-}" in
    install)   cmd_install   ;;
    update)    cmd_update    ;;
    uninstall) cmd_uninstall ;;
    status)    cmd_status    ;;
    *)
        echo "Использование: $0 {install|update|uninstall|status}"
        echo ""
        echo "  install   — установить .NET, собрать приложение, зарегистрировать systemd-службу  (sudo)"
        echo "  update    — обновить из Git, пересобрать, перезапустить службу                    (sudo)"
        echo "  uninstall — остановить и полностью удалить службу и файлы                         (sudo)"
        echo "  status    — показать состояние службы и текущие параметры симулятора"
        exit 1
        ;;
esac
