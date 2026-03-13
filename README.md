# cg-emurouter

Эмулятор объектов и оборудования с веб-интерфейсом и публикацией телеметрии в MQTT.

Текущая версия: `0.0.2`

## Установка

Требования:
- `.NET SDK 8.0+`
- MQTT брокер (например, Mosquitto)

Шаги:
1. Клонировать репозиторий.
2. Перейти в папку проекта.
3. Восстановить зависимости:

```bash
dotnet restore
```

## Запуск

1. Скопировать `emulator.example.yaml` в `emulator.yaml` и отредактировать.
2. Запустить:

```bash
dotnet run --project src/CgEmulator -- --config emulator.yaml
```

Веб-интерфейс: `http://<host>:6699/`

## Быстрая проверка MQTT

```bash
mosquitto_sub -h 10.10.10.1 -t 'cg/v1/telemetry/SN/#' -v
```

Проверить наличие:
- GPS сообщений раз в 60 сек
- `PCC_3_3` пакетов раз в `equipment_period_sec`

## Установка на сервер из Git

1. Установить `.NET SDK 8.0+` на сервер.
2. Клонировать репозиторий:

```bash
git clone https://github.com/zergont/cg-emurouter.git
cd cg-emurouter
```

3. Подготовить конфиг:

```bash
cp emulator.example.yaml emulator.yaml
```

4. Отредактировать `emulator.yaml` (MQTT host/port, bind IP и т.д.).
5. Запустить:

```bash
dotnet run --project src/CgEmulator -- --config emulator.yaml
```

## Работа сервиса

- Веб-интерфейс: `http://<server-ip>:6699/`
- В интерфейсе:
  - создать объекты,
  - нажать `Start` для начала публикации,
  - нажать `Stop` для остановки и заморозки симуляции.

## Проверка работы

Проверка через MQTT:

```bash
mosquitto_sub -h <mqtt-host> -t 'cg/v1/telemetry/SN/#' -v
```

Ожидается:
- GPS сообщения раз в 60 сек,
- `PCC_3_3` сообщения раз в `equipment_period_sec`,
- формат `70` и `290` как `"[hi,lo]"`.

## Параметры replay

Секция `replay` управляет скоростью выгрузки накопленного MQTT-буфера после восстановления соединения:

```yaml
replay:
  rate_per_sec: 20
  buffer_max_size: 100000
```

- `rate_per_sec` — максимальное число сообщений в секунду при воспроизведении буфера.
  - `0` — без ограничения, старое поведение.
- `buffer_max_size` — максимальный размер буфера.
  - при переполнении дропаются самые старые сообщения.

В веб-интерфейсе отображаются:
- текущий размер буфера воспроизведения,
- режим `Live` / `Replaying`,
- настроенная скорость выгрузки,
- общее число отброшенных сообщений при переполнении буфера.

## Деинсталляция с сервера

1. Остановить процесс сервиса (`Ctrl+C`, если запущен в консоли).
2. Удалить директорию проекта:

```bash
cd ..
rm -rf cg-emurouter
```

3. (Опционально) удалить установленный `.NET SDK`, если он больше не нужен.

---

## Развёртывание на Ubuntu 24 как systemd-служба

Для Ubuntu 24 в репозитории есть скрипт `manage.sh`, который автоматизирует
установку, обновление и удаление.

### Подготовка (один раз)

```bash
git clone https://github.com/zergont/cg-emurouter.git
cd cg-emurouter
chmod +x manage.sh
```

### Установка

```bash
sudo ./manage.sh install
```

Скрипт выполнит:
- установку `.NET 8 SDK` из стандартных репозиториев Ubuntu 24;
- создание системного пользователя `cg-emulator`;
- сборку и публикацию приложения в `/opt/cg-emurouter/app/`;
- создание `emulator.yaml` из примера (только при первом запуске);
- регистрацию и запуск службы `cg-emurouter` через systemd.

После установки отредактируйте конфигурацию:

```bash
sudo nano /opt/cg-emurouter/app/emulator.yaml
sudo systemctl restart cg-emurouter
```

Обязательно проверить: `web.bind_ip`, `web.port`, `mqtt.host`, `mqtt.port`.

### Проверка работы

```bash
# Состояние службы и текущие параметры симулятора
./manage.sh status

# Логи в реальном времени
sudo journalctl -u cg-emurouter -f

# MQTT-поток
mosquitto_sub -h <mqtt-host> -t 'cg/v1/telemetry/SN/#' -v
```

### Обновление

```bash
sudo ./manage.sh update
```

Выполнит `git pull`, пересборку и перезапуск службы.
`emulator.yaml` при этом не перезаписывается.

### Удаление

```bash
sudo ./manage.sh uninstall
```

Останавливает и удаляет службу, файлы `/opt/cg-emurouter` и пользователя `cg-emulator`.
Для удаления `.NET SDK` следуйте подсказке в выводе скрипта.
