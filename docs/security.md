# slowrig AI Cluster — Security Design v0.1

Дата: 2026-06-23
Статус: Stage 7 design; hardening не внедрён

## 1. Назначение

Этот документ фиксирует первый security design для `slowrig AI Cluster`.

Цель:

```text
сохранить LAN/VPN-first posture и определить порядок будущего hardening без случайного открытия сервисов наружу
```

---

## 2. Текущая позиция

Текущий baseline рассчитан на домашнюю LAN.

Не открывать публично без отдельного security stage:

```text
3000  # Open WebUI
4000  # LiteLLM Gateway
8080  # llama-architect direct backend
8081  # llama-coder direct backend
4010  # memory-embed loopback only, не LAN/public
```

`telegram-bot` не открывает inbound port и работает через outbound polling.

---

## 3. Direct backend ports

Direct backend ports `8080` и `8081` пока остаются доступны в LAN для diagnostics и rollback.

Закрывать или ограничивать их можно только отдельным security hardening stage, потому что это меняет operational workflow и rollback path.

Варианты будущего решения:

* оставить LAN-only как сейчас;
* ограничить firewall-ом по host/IP;
* привязать к localhost;
* оставить доступ только через Docker network;
* закрыть после появления стабильного gateway/security operational baseline.

---

## 4. Secrets

Secrets должны жить вне git:

* `.env`;
* offline password manager / encrypted storage;
* future dedicated secrets mechanism, если понадобится.

Запрещено:

* коммитить `.env`;
* печатать реальные tokens/API keys в logs или changelog;
* вставлять реальные secrets в docs;
* индексировать secrets в Memory/RAG.

---

## 5. Будущий порядок hardening

Рекомендуемый порядок:

1. Зафиксировать текущий LAN baseline.
2. Описать desired external access model: VPN first или reverse proxy.
3. Включить auth для Open WebUI перед любым внешним доступом.
4. Ограничить LiteLLM Gateway access policy.
5. Решить судьбу direct backend ports.
6. Добавить backup/restore перед рискованными hardening changes.
7. Проверить rollback.

Не менять firewall/reverse proxy/VPN без отдельного approval.
