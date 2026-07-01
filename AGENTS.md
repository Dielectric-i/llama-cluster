# AGENTS.md — slowrig AI Cluster

## 1. Scope

This file applies to the entire repository.

`slowrig` is real operational infrastructure. Treat changes as changes to a live local AI cluster, not as disposable experiments.

`AGENTS.md` defines how Codex works in this repository. It should contain operating rules, safety rules, workflow rules, and documentation discipline. It must not duplicate the full content of `README.md`, `docs/passport.md`, `docs/runbook.md`, `docs/architecture.md`, `docs/gateway.md`, `docs/decisions.md`, `docs/changelog.md`, or `docs/roadmap.md`.

---

## 2. Required reading

Before non-trivial work, read:

```text
README.md
docs/codex-context.md
```

Use `README.md` as the authoritative documentation index.

Then read only the task-relevant documents:

* current factual state, hardware, GPU mapping, ports: `docs/passport.md`;
* operations, diagnostics, restart, rollback: `docs/runbook.md`;
* topology, routing, target design: `docs/architecture.md`, `docs/gateway.md`;
* trade-offs and history: `docs/decisions.md`, `docs/changelog.md`, `docs/roadmap.md`;
* subsystem work: the matching dedicated document under `docs/`, when it exists.

If a task introduces a new subsystem and no dedicated document exists yet, start with a design document under `docs/` and update the README documentation index.

---

## 3. User-facing language

Communicate with the user in Russian unless the user explicitly asks otherwise.

Use Russian labels in status reports, validation notes, rollback notes, questions, and next-step sections. Keep exact technical identifiers unchanged.

Examples of Russian labels:

```text
Этап:
Цель:
Изменённые файлы:
Проверки:
Откат:
Открытые вопросы:
Следующий этап:
```

Do not translate identifiers such as:

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

Repository files may use Russian, English, or mixed language when practical.

---

## 4. Work model

The human operator runs real server-side commands unless Codex has explicit approved access for the current action.

Approved SSH path, when access is explicitly granted:

```text
ssh discover@slowrig
```

Do not claim real server validation succeeded unless Codex actually ran the check or the user provided the output.

Default flow:

```text
edit locally -> review in git -> push when needed -> sync/run on slowrig when needed
```

Prefer Docker and Docker Compose over host OS changes. Use Docker without `sudo` only when the active SSH user already has Docker group access.

If validation requires the real server, GPU, Docker, network ports, UI, logs, or secrets and Codex does not have confirmed access, provide exact commands and expected results for the user to run.

---

## 5. Stage workflow

Work in small, reviewable stages.

For non-trivial work, start with:

```text
Этап:
Цель:
Ожидаемые файлы:
Что прочитать сначала:
Уровень риска:
План проверки:
Идея отката:
```

Then make the smallest safe change.

After a stage, report:

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

Stop at genuine stage boundaries and architectural forks. Do not silently continue into the next stage unless the user asks.

Ask the user only when a real decision or missing input is needed: architecture, security posture, new dependencies, risky operational state, conflicting documentation/config/output, real server validation, logs, secrets, or explicit user pause.

Do not ask unnecessary questions when the user already provided enough context for a safe reversible documentation-only change.

If the user gives a durable repository instruction, update `AGENTS.md` in the same stage when compatible with higher-priority rules and repository constraints. If it conflicts, explain the conflict instead of changing the rule.

---

## 6. Branch and git rules

Use a dedicated `codex/...` branch for non-trivial work.

The active Codex integration branch is:

```text
codex/main
```

`master` is legacy and must not be used for new work, stage integration, or routine pushes unless the user explicitly overrides this rule.

Do not work directly on `codex/main` for significant changes unless the user explicitly requests it. Tiny documentation-only corrections are acceptable.

Expected workflow:

```text
inspect state -> create/continue codex/... branch -> make focused change -> verify -> user review -> merge/fast-forward only after approval
```

Before changing files, inspect repository state when possible:

```bash
git status --short
```

After changing files, summarize:

```bash
git status --short
git diff --stat
```

Do not commit automatically unless the user explicitly asks for a commit. When a commit is appropriate, propose a clear commit message.

Stage files explicitly by purpose. Do not use:

```bash
git add .
```

Never run destructive git commands without explicit approval:

```text
git reset --hard
git clean -fd
git push --force
git rebase
git filter-branch
```

If unrelated changes already exist, do not overwrite them. Report them and ask how to proceed.

---

## 7. Change discipline

Prefer small, reversible patches.

Do not make broad unrelated edits, reformat whole files, rename files, move directories, or reorganize documentation unless that is the actual approved task.

For operational changes, prefer:

```text
one change -> one service -> one test -> logs -> rollback known
```

Avoid combining multiple operational parameter changes in one stage unless the user explicitly approves the combined migration.

---

## 8. Secrets and large files

Never print, copy, commit, or expose real secrets.

Do not ask the user to paste `.env`.

Sensitive and runtime data must not be committed:

```text
.env
secrets/
models/
cache/
data/
logs/
backups/
```

It is allowed to document variable names, but not real values.

If `.env` appears in `git status`, stop and tell the user to fix `.gitignore` before committing.

---

## 9. Dependency and design policy

Do not add new runtime dependencies without explicit approval.

This includes Docker services, databases, packages, model files, monitoring stacks, agent frameworks, MCP servers, and external APIs.

Before adding a dependency, explain:

```text
Why it is needed:
What problem it solves:
Operational cost:
Security impact:
Backup/restore impact:
Rollback:
Alternative with fewer dependencies:
```

Major new subsystems require a design document before implementation. Prefer the existing stack unless the new dependency clearly unlocks a needed capability.

---

## 10. Operational safety

Warn and ask before changes that may affect the stable baseline, including:

* restarting all services or running `docker compose down`;
* pulling new Docker images;
* changing GPU mapping, model files, context size, `parallel`, CPU offload, or Unified Memory;
* changing LiteLLM routing or Open WebUI auth/security;
* exposing ports outside LAN;
* installing system packages;
* changing NVIDIA, CUDA, Docker, firewall, reverse proxy, or VPN settings;
* deleting volumes, cache, model data, logs, backups, or Memory DB data.

Keep direct backend ports `8080` and `8081` available for diagnostics unless the user explicitly decides to lock them down.

Do not treat CPU offload, Unified Memory, increased context size, or increased parallelism as routine tuning. They require a separate test plan.

Avoid destructive operations without explicit approval and a rollback path.

---

## 11. Architecture and hardware guardrails

Ordinary clients should go through LiteLLM Gateway unless a documented exception exists:

```text
Client -> LiteLLM Gateway -> llama.cpp backend
```

Direct backend access is diagnostic:

```text
8080 -> llama-architect
8081 -> llama-coder
```

Use established gateway model names from project documentation. Do not invent public model names without a documented decision.

Do not change hardware assumptions, GPU mapping, model placement, context size, or parallelism based on guesswork.

Before hardware-sensitive changes, read `docs/passport.md`, `docs/architecture.md`, `docs/decisions.md`, and `docs/runbook.md`.

Preserve these assumptions unless the user explicitly approves a change:

* the fast worker model should stay on the fast GPU;
* the heavy model is not a low-latency backend;
* constrained PCIe links make aggressive multi-GPU experiments risky;
* VRAM headroom matters more than theoretical maximum model size;
* CPU offload is not a normal operating mode;
* unnecessary model reloads should be avoided.

---

## 12. Validation policy

For infrastructure changes, provide:

1. exact commands;
2. expected successful output;
3. how to collect logs if something fails;
4. rollback steps.

Primary checks:

```bash
/opt/llama-cluster/scripts/cluster-health-lite.sh
/opt/llama-cluster/scripts/cluster-status.sh
```

For Docker Compose changes:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
sudo docker compose ps
```

For Bash scripts:

```bash
bash -n scripts/<script-name>.sh
```

For Markdown-only changes:

```bash
git diff --stat
git diff -- AGENTS.md README.md docs/
```

For LiteLLM checks, load secrets without printing them:

```bash
cd /opt/llama-cluster
set -a
source .env
set +a
```

Then check:

```bash
curl -sS \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  http://127.0.0.1:4000/v1/models
```

Do not claim validation succeeded unless Codex actually ran the check or the user provided the output.

---

## 13. Documentation rules

This repository is documentation-first.

When behavior, architecture, services, ports, routing, security, memory, agents, Telegram integration, monitoring, backups, scripts, or operational workflows change, update the affected documentation in the same stage unless the user explicitly asks not to.

Use factual language and distinguish clearly between:

```text
implemented
planned
recommended
experimental
not implemented
tested by user
requires manual check
```

`README.md` is the documentation index. When creating a new documentation file under `docs/`, update the README table.

Do not duplicate large blocks across many docs. Link to the source of truth instead.

If documentation, configuration, and observed server output disagree, report the contradiction instead of silently choosing one:

```text
Несоответствие:
Документация говорит:
Конфигурация говорит:
Наблюдаемый вывод говорит:
Рекомендуемый источник истины:
Вопрос к пользователю:
```

When the user confirms the correct state, update affected documentation in the same stage.

---

## 14. Failure protocol

If a command fails, do not continue blindly.

On failure:

1. stop the current stage;
2. summarize what failed;
3. quote only relevant error lines;
4. explain the likely cause and uncertainty;
5. ask for missing logs only if needed;
6. propose the smallest safe diagnostic step;
7. provide rollback if the system may be partially changed.

Do not stack multiple speculative fixes in one step.

If the failure could affect running services, preserve the current stable baseline first.

---

## 15. Done definition

A change is done only when:

* files are updated;
* affected documentation matches actual behavior;
* risks are noted;
* validation commands and expected results are provided;
* rollback is documented when relevant;
* no secrets are exposed;
* git status implications are clear;
* the user has the information needed to test on the server.

For server-dependent work, do not claim the stage is fully verified until the user provides output or confirms manual testing.
