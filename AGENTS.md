# AGENTS.md — slowrig AI Cluster

## 1. Область действия

Этот файл действует для всего репозитория.

`slowrig` — реальная локальная инфраструктура. Любые изменения нужно рассматривать как изменения live AI-кластера, а не как одноразовый эксперимент.

`AGENTS.md` задаёт правила работы Codex: workflow, безопасность, git-дисциплину, валидацию и документационную дисциплину. Он не должен дублировать подробное содержимое `README.md`, `docs/passport.md`, `docs/runbook.md`, `docs/architecture.md`, `docs/gateway.md`, `docs/decisions.md`, `docs/changelog.md` или `docs/roadmap.md`.

---

## 2. Что читать перед работой

Перед нетривиальной задачей сначала читать:

```text
README.md
docs/codex-context.md
```

`README.md` — главный индекс документации.

Затем читать только документы, относящиеся к задаче:

* факты стенда, hardware, GPU mapping, ports: `docs/passport.md`;
* эксплуатация, диагностика, restart, rollback: `docs/runbook.md`;
* topology, routing, target design: `docs/architecture.md`, `docs/gateway.md`;
* trade-offs и история: `docs/decisions.md`, `docs/changelog.md`, `docs/roadmap.md`;
* subsystem work: соответствующий dedicated document под `docs/`, если он существует.

Если задача вводит новый subsystem и dedicated document ещё нет, сначала подготовить design document под `docs/` и добавить ссылку в `README.md`.

---

## 3. Язык общения

С пользователем общаться на русском языке, если пользователь явно не попросил иначе.

Русские labels использовать в статусах, validation notes, rollback notes, вопросах и next steps:

```text
Этап:
Цель:
Изменённые файлы:
Проверки:
Откат:
Открытые вопросы:
Следующий этап:
```

Технические идентификаторы не переводить:

```text
slowrig/coder
slowrig/architect
llama-coder
llama-architect
litellm
open-webui
LITELLM_MASTER_KEY
LITELLM_SALT_KEY
docker-compose.yaml
```

Документация должна быть на русском языке. Технические термины на английском допустимы, если они являются identifiers, protocol names, product names, command names или устойчивыми engineering terms.

---

## 4. Рабочая модель

Оператор-человек выполняет реальные server-side команды, если Codex не получил явный доступ для текущего действия.

Разрешённый SSH path, если доступ явно выдан:

```text
ssh discover@slowrig
```

Не утверждать, что проверка на сервере прошла, если Codex реально её не запускал и пользователь не предоставил вывод.

Обычный поток работы:

```text
edit locally -> review in git -> push when needed -> sync/run on slowrig when needed
```

Предпочитать Docker и Docker Compose изменениям host OS. Docker без `sudo` использовать только если активный SSH user уже имеет Docker group access.

Если проверка требует реального сервера, GPU, Docker, network ports, UI, logs или secrets, а у Codex нет подтверждённого доступа, дать пользователю точные команды и ожидаемый результат.

---

## 5. Stage workflow

Работать маленькими reviewable stages.

Для нетривиальной работы начинать с:

```text
Этап:
Цель:
Ожидаемые файлы:
Что прочитать сначала:
Уровень риска:
План проверки:
Идея отката:
```

Затем делать минимальное безопасное изменение.

После stage отчитаться:

```text
Этап:
Что изменилось:
Изменённые файлы:
Проверки:
Команды для пользователя:
Ожидаемый результат:
Откат:
Открытые вопросы / развилки:
Рекомендуемый следующий этап:
```

Останавливаться на настоящих stage boundaries и архитектурных развилках. Не продолжать молча в следующий stage, если пользователь этого не просил.

Спрашивать пользователя только когда нужно реальное решение или отсутствующий input: архитектура, security posture, новые dependencies, рискованное operational state, конфликт docs/config/output, real server validation, logs, secrets или явная пауза.

Не задавать лишних вопросов, если контекста достаточно для безопасной обратимой documentation-only правки.

Если пользователь даёт устойчивое правило работы с репозиторием, обновить `AGENTS.md` в том же stage, если это не конфликтует с более приоритетными правилами.

---

## 6. Branch и git

Для нетривиальной работы использовать отдельную ветку `codex/...`.

Активная интеграционная ветка Codex:

```text
codex/main
```

`master` — legacy. Не использовать его для новой работы, stage integration или routine pushes без явного override от пользователя.

Не работать напрямую на `codex/main` для значимых изменений, если пользователь явно не попросил. Мелкие documentation-only исправления допустимы.

Ожидаемый workflow:

```text
inspect state -> create/continue codex/... branch -> make focused change -> verify -> user review -> merge/fast-forward only after approval
```

Перед изменениями по возможности проверить:

```bash
git status --short
```

После изменений показать:

```bash
git status --short
git diff --stat
```

Не коммитить автоматически без явной просьбы пользователя. Если commit уместен, предложить понятное commit message.

Stage files явно по назначению. Не использовать:

```bash
git add .
```

Никогда не запускать destructive git commands без явного approval:

```text
git reset --hard
git clean -fd
git push --force
git rebase
git filter-branch
```

Если уже есть unrelated changes, не перетирать их. Сообщить о них и работать вокруг них.

---

## 7. Change discipline

Предпочитать маленькие обратимые patches.

Не делать широкие unrelated edits, массовое форматирование, переименование файлов, перенос directories или реорганизацию документации, если это не является согласованной задачей.

Для operational changes придерживаться:

```text
one change -> one service -> one test -> logs -> rollback known
```

Не объединять несколько operational parameter changes в один stage без явного approval.

---

## 8. Secrets и большие файлы

Никогда не печатать, копировать, коммитить или раскрывать реальные secrets.

Не просить пользователя вставлять `.env`.

Нельзя коммитить sensitive/runtime data:

```text
.env
secrets/
models/
cache/
data/
logs/
backups/
```

Можно документировать имена переменных, но не реальные значения.

Если `.env` появился в `git status`, остановиться и сказать пользователю исправить `.gitignore` до commit.

---

## 9. Dependencies и design policy

Не добавлять новые runtime dependencies без явного approval.

Это включает Docker services, databases, packages, model files, monitoring stacks, agent frameworks, MCP servers и external APIs.

Перед добавлением dependency объяснить:

```text
Зачем нужно:
Какую проблему решает:
Operational cost:
Security impact:
Backup/restore impact:
Откат:
Альтернатива с меньшим числом dependencies:
```

Крупные новые subsystem-ы требуют design document перед implementation. Предпочитать существующий stack, если новая dependency явно не открывает нужную возможность.

---

## 10. Operational safety

Предупреждать и спрашивать перед изменениями, которые могут затронуть stable baseline:

* restart всех services или `docker compose down`;
* pull новых Docker images;
* изменение GPU mapping, model files, context size, `parallel`, CPU offload или Unified Memory;
* изменение LiteLLM routing или Open WebUI auth/security;
* публикация ports за пределы LAN;
* установка system packages;
* изменение NVIDIA, CUDA, Docker, firewall, reverse proxy или VPN settings;
* удаление volumes, cache, model data, logs, backups или Memory DB data.

Прямые backend-порты `8080` и `8081` должны оставаться доступными для диагностики, пока пользователь явно не решил закрыть их.

CPU offload, Unified Memory, увеличение context size и увеличение parallelism не являются routine tuning. Для них нужен отдельный test plan.

Не выполнять destructive operations без явного approval и rollback path.

---

## 11. Architecture и hardware guardrails

Обычные клиенты должны идти через LiteLLM Gateway, если нет documented exception:

```text
Client -> LiteLLM Gateway -> llama.cpp backend
```

Direct backend access — диагностический:

```text
8080 -> llama-architect
8081 -> llama-coder
```

Использовать established gateway model names из документации. Не придумывать public model names без documented decision.

Не менять hardware assumptions, GPU mapping, model placement, context size или parallelism на догадках.

Перед hardware-sensitive changes читать `docs/passport.md`, `docs/architecture.md`, `docs/decisions.md` и `docs/runbook.md`.

Сохранять assumptions, если пользователь явно не одобрил изменение:

* fast worker model должен оставаться на fast GPU;
* heavy model не является low-latency backend;
* constrained PCIe links делают агрессивные multi-GPU experiments рискованными;
* VRAM headroom важнее теоретического максимального размера model;
* CPU offload не является нормальным operating mode;
* unnecessary model reloads нужно избегать.

---

## 12. Политика проверки

Для infrastructure changes давать:

1. точные команды;
2. ожидаемый успешный вывод;
3. как собрать logs при ошибке;
4. rollback steps.

Основные checks:

```bash
/opt/llama-cluster/scripts/cluster-health-lite.sh
/opt/llama-cluster/scripts/cluster-status.sh
```

Для Docker Compose changes:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
sudo docker compose ps
```

Для Bash scripts:

```bash
bash -n scripts/<script-name>.sh
```

Для Markdown-only changes:

```bash
git diff --stat
git diff -- AGENTS.md README.md docs/
```

Для LiteLLM checks загружать secrets без печати:

```bash
cd /opt/llama-cluster
set -a
source .env
set +a
```

Затем:

```bash
curl -sS \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  http://127.0.0.1:4000/v1/models
```

Не утверждать, что validation прошла, если Codex реально не запускал check и пользователь не предоставил вывод.

---

## 13. Documentation rules

Репозиторий documentation-first.

Когда меняется behavior, architecture, services, ports, routing, security, memory, agents, Telegram integration, monitoring, backups, scripts или operational workflows, обновить affected docs в том же stage, если пользователь явно не попросил иначе.

Писать фактически и явно различать:

```text
внедрено
запланировано
рекомендовано
экспериментально
не внедрено
проверено пользователем
требует ручной проверки
```

`README.md` — главный documentation index. При создании нового файла под `docs/` обновить README table.

Не дублировать большие блоки в нескольких документах. Ссылаться на source of truth.

Если docs, config и observed server output расходятся, не выбирать молча:

```text
Несоответствие:
Документация говорит:
Конфигурация говорит:
Наблюдаемый вывод говорит:
Рекомендуемый источник истины:
Вопрос к пользователю:
```

После подтверждения правильного состояния обновить affected docs в том же stage.

---

## 14. Failure protocol

Если команда завершилась ошибкой, не продолжать вслепую.

При failure:

1. остановить текущий stage;
2. кратко описать, что failed;
3. процитировать только relevant error lines;
4. объяснить likely cause и uncertainty;
5. запросить missing logs только если нужно;
6. предложить минимальный safe diagnostic step;
7. дать rollback, если system мог быть частично изменён.

Не накладывать несколько speculative fixes в один step.

Если failure может повлиять на running services, сначала сохранить stable baseline.

---

## 15. Done definition

Change считается done, когда:

* files updated;
* affected docs соответствуют actual behavior;
* risks noted;
* validation commands и expected results provided;
* rollback documented when relevant;
* secrets не раскрыты;
* git status implications понятны;
* пользователь имеет всё нужное для server test.

Для server-dependent work не считать stage полностью verified, пока пользователь не предоставил output или не подтвердил manual testing.
