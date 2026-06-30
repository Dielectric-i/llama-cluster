# AGENTS.md — slowrig AI Cluster

## 1. Scope

This file applies to the entire repository.

This repository describes and operates the `slowrig` local AI cluster. Treat it as real operational infrastructure, not as a throwaway experiment.

`AGENTS.md` defines how Codex must work in this repository.

It should contain operating rules, safety rules, workflow rules, and documentation discipline. It should not duplicate the full content of `README.md`, `docs/passport.md`, `docs/runbook.md`, `docs/architecture.md`, `docs/gateway.md`, `docs/decisions.md`, or `docs/changelog.md`.

---

## 2. Required reading order

Before doing non-trivial work, read the repository entry points first:

```text
README.md
docs/codex-context.md
```

Use `README.md` as the authoritative documentation index.

Then read task-relevant documents based on the requested change.

Examples:

| Task type | Read additionally |
| --- | --- |
| hardware, GPU mapping, ports, current factual state | `docs/passport.md` |
| operations, diagnostics, rollback, commands | `docs/runbook.md` |
| service topology, routing, target design | `docs/architecture.md` |
| LiteLLM Gateway, model routing, Open WebUI routing | `docs/gateway.md` |
| architectural reasons and trade-offs | `docs/decisions.md` |
| recent factual changes and tested results | `docs/changelog.md` |
| completed stage context | relevant `docs/stage*-summary.md` |
| memory/RAG work | `docs/memory.md` when it exists |
| Telegram work | `docs/telegram.md` when it exists |
| agent framework work | `docs/agent-framework.md` when it exists |
| monitoring/security/backups | matching dedicated docs when they exist |

If the required document does not exist and the task introduces a new subsystem, start with a design document under `docs/`.

Do not maintain a long duplicate documentation index in `AGENTS.md`. Keep the authoritative documentation index in `README.md`.

---

## 3. Communication language

Codex must communicate with the user in Russian.

All user-facing status labels, stage reports, summaries, questions, validation notes, rollback notes, and next-step sections must use Russian labels. Do not use English report labels such as `Files changed`, `Required checks`, `Open questions / forks`, or `Recommended next stage` in messages to the user. Keep exact technical identifiers, command names, branch names, file names, environment variables, and product names unchanged.

Use Russian labels such as:

```text
Этап:
Цель:
Проверенные файлы:
Найденные проблемы:
Ожидаемые изменения:
Уровень риска:
Предлагаемые изменения:
План проверки:
Откат:
Открытые вопросы:
Следующий этап:
```

Repository files may use Russian, English, or mixed language depending on what is practical.

Keep exact technical identifiers unchanged.

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

Even if internal instructions or project files are written in English, all user-facing communication must be in Russian unless the user explicitly asks otherwise.

---

## 4. Operator model

The human operator runs the real server-side commands.

By default, assume the user will:

* upload or sync files to the server;
* run Docker Compose commands;
* restart services;
* inspect logs;
* run diagnostics;
* check browser UI;
* check network/API behavior;
* approve architectural forks;
* decide when a stage is complete.

Codex should:

* prepare patches;
* explain changes;
* update documentation;
* provide commands for validation;
* describe expected results;
* provide rollback steps;
* stop at stage boundaries.

Do not assume Codex can validate real server state unless the user provides command output or explicitly confirms manual checks.

When the user explicitly grants SSH/server access, Codex may run real server-side commands through the approved access path, currently:

```text
ssh discover@slowrig
```

Even with server access, Codex must:

* keep commands scoped and documented;
* prefer Docker/Docker Compose over installing applications into the host OS;
* use Docker without `sudo` when the SSH user already has Docker group access;
* avoid exposing secrets in logs or chat;
* treat sudo passwords as secrets: do not store them in files, commits, commands shown to the user, or documentation;
* avoid destructive operations without an explicit approval and rollback path;
* document what was run and what result was observed.

Operational work must be done in the local repository first, then sent to the remote server through git when needed.

Default flow:

```text
edit locally -> review in git -> push to the remote repository when needed -> sync the local clone from the remote if required
```

Direct edits on the remote repository are allowed only when there is a clear reason, for example:

* a very small change that should be checked in place;
* a change that would otherwise create unnecessary git noise;
* a temporary operational fix where remote-side editing is the safest path;
* another practical reason that makes direct remote editing preferable to a local round-trip.

Remote server:

```text
slowrig
```

SSH access:

```text
ssh discover@slowrig
```

Code is executed on the remote server. The local repository is used for preparing, reviewing, and packaging changes before they are sent to the server.

When testing requires the real server, GPU, Docker, network ports, or UI and Codex does not have confirmed access for that action, provide exact commands and ask the user to run them.

---

## 5. Stage-based workflow

Work in explicit stages.

A stage is a small, reviewable unit of progress with a clear goal, limited scope, validation plan, and rollback path.

For any non-trivial task, Codex should first state:

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

After every stage, stop and report:

```text
Этап:
Что изменилось:
Новая возможность:
Изменённые файлы:
Проверки:
Команды для пользователя:
Ожидаемый результат:
Откат:
Открытые вопросы / развилки:
Рекомендуемый следующий этап:
```

Do not silently continue into the next stage unless the user explicitly asks.

If there is an architectural fork, stop and discuss it before implementation.

Do not stop after every small message or routine progress update. Once the user has approved moving forward, continue within the current stage until the stage is genuinely handled, unless a real stop condition appears.

At every architectural fork, explain the existing options in detail before asking the user to choose. For each option, describe how it affects the final result and how it affects the implementation or operational process.

Stop and ask the user only when one of these is true:

* an architectural decision or trade-off requires user approval;
* a problem, failure, conflict, or documentation/config mismatch requires user input;
* real server validation, UI verification, logs, secrets, or operational output are needed from the operator;
* a change would affect security posture, runtime dependencies, model/GPU settings, ports, volumes, or other risky operational state;
* the user explicitly asks Codex to pause, stop, or wait.

---

## 6. Branch workflow

For non-trivial stages, work in a dedicated branch.

Branch names should use the prefix:

```text
codex/
```

Examples:

```text
codex/stage-4-memory-design
codex/telegram-design
codex/gateway-policy
codex/runbook-cleanup
codex/agents-design
```

Expected workflow:

1. inspect current state;
2. create or switch to a `codex/...` branch;
3. make the smallest coherent change;
4. update affected documentation;
5. provide validation commands;
6. wait for user review and real checks when needed;
7. merge or fast-forward into the Codex integration branch only after full verification and user approval.

Do not merge automatically unless the user explicitly asks.

In this repository the active Codex integration branch is:

```text
codex/main
```

The legacy branch `master` must not be used for new work, stage integration, or routine pushes unless the user explicitly overrides this rule.

Do not work directly on `codex/main` for significant changes unless the user explicitly requests it or the change is a tiny documentation-only correction.

For normal stages:

1. create or continue a dedicated stage branch such as `codex/stage-8`;
2. implement and verify the stage there;
3. push the stage result into `codex/main`;
4. start the next stage from updated `codex/main` in a new `codex/...` branch.

When moving from one stage to the next:

1. finish and review the current stage branch;
2. merge or fast-forward the completed `codex/...` branch into `codex/main` after user approval;
3. push `codex/main`;
4. create the next `codex/...` branch from updated `codex/main`;
4. keep only the new stage changes in the new branch.

If next-stage work was started before the previous branch was merged, temporarily stash or otherwise preserve those uncommitted changes, merge the completed branch first, create the new stage branch from updated main, and only then restore the next-stage changes.

If the user gives a durable instruction about how Codex should behave in this repository, update `AGENTS.md` in the same stage so the rule is preserved for future work. If the instruction conflicts with higher-priority instructions, existing safety rules, or repository constraints, stop and explain the conflict instead of silently changing the rule.

---

## 7. Git repository rules

This is a git repository.

Before changing files, inspect the current repository state when possible:

```bash
git status --short
```

If a new stage branch is needed:

```bash
git checkout -b codex/<stage-or-task-name>
```

After changing files, summarize:

```bash
git status --short
git diff --stat
```

Do not commit automatically unless the user explicitly asks for a commit.

When a commit is appropriate, propose a clear commit message.

Do not use:

```bash
git add .
```

Instead, stage files explicitly by purpose, for example:

```bash
git add README.md docs/runbook.md docs/changelog.md
```

Never run destructive git commands unless explicitly requested:

```text
git reset --hard
git clean -fd
git push --force
git rebase
git filter-branch
```

Never remove history, secrets protections, documentation, or baseline files without explicit approval.

If unrelated changes already exist in the working tree, do not overwrite them. Report them to the user and ask how to proceed.

---

## 8. Change discipline

Prefer small, reviewable patches.

Do not make broad unrelated edits.

Do not reformat whole files unless formatting is the actual task.

Do not rename files, move directories, or reorganize documentation without explicit approval.

Do not change multiple operational parameters at once unless the user explicitly approves a combined migration.

For operational changes, prefer this pattern:

```text
one change -> one service -> one test -> logs -> rollback known
```

Avoid this pattern:

```text
many config changes -> multiple restarts -> unclear failure source
```

---

## 9. Secrets and large files

Never print, copy, commit, or expose real secrets.

Sensitive files:

```text
.env
secrets/
```

Large runtime/model/cache data must not be committed:

```text
models/
cache/
data/
logs/
```

Allowed to document variable names:

```text
LITELLM_MASTER_KEY
LITELLM_SALT_KEY
```

Not allowed to document real values.

Do not ask the user to paste `.env`.

If `.env` appears in `git status`, stop and tell the user to fix `.gitignore` before committing.

---

## 10. Dependency policy

Do not add new runtime dependencies without explicit approval.

This includes:

* new Docker services;
* new databases;
* new Python packages;
* new Node packages;
* new system packages installed through `apt`;
* new model files;
* new monitoring stacks;
* new agent frameworks;
* new MCP servers;
* new external APIs.

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

Prefer the existing stack unless the new dependency clearly unlocks a needed capability.

Major new subsystems require a design document before implementation.

Examples:

| Subsystem | Design doc |
| --- | --- |
| Memory / RAG | `docs/memory.md` |
| Telegram bot | `docs/telegram.md` |
| Agent framework | `docs/agents.md` |
| Monitoring | `docs/monitoring.md` |
| Security hardening | `docs/security.md` |
| Backups | `docs/backups.md` |

---

## 11. Operational safety

Do not run or propose risky operational changes without warning.

Ask the user before:

* restarting all services;
* running `docker compose down`;
* pulling new Docker images;
* changing GPU mapping;
* changing model files;
* changing context size;
* changing `parallel`;
* changing LiteLLM routing;
* changing Open WebUI auth/security;
* exposing any port outside LAN;
* installing new system packages;
* changing NVIDIA/CUDA/Docker driver stack;
* deleting volumes or cache;
* changing firewall/reverse proxy/VPN settings.

Prefer minimal, reversible changes.

Keep direct backend ports `8080` and `8081` available for diagnostics unless the user explicitly decides to lock them down.

Do not treat CPU offload, Unified Memory, increased context size, or increased parallelism as normal tuning changes. They require a separate test plan.

---

## 12. Architecture guardrails

All ordinary clients should go through LiteLLM Gateway unless a documented exception exists.

Normal client path:

```text
Client -> LiteLLM Gateway -> llama.cpp backend
```

Current ordinary client path:

```text
Open WebUI -> LiteLLM -> llama-coder / llama-architect
```

Future clients should also use LiteLLM unless there is a documented reason not to:

```text
Telegram bot -> LiteLLM
IDE assistant -> LiteLLM
Agent framework -> LiteLLM
Memory/RAG service -> LiteLLM
Custom scripts -> LiteLLM
```

Direct backend access is diagnostic:

```text
8080 -> llama-architect
8081 -> llama-coder
```

Do not make direct backend access the normal client path unless implementing rollback or diagnostics.

Do not invent new public model names without a documented decision. Use the established gateway model names from the project documentation.

---

## 13. Hardware constraints

The cluster is intentionally budget and constrained.

Do not change hardware assumptions, GPU mapping, model placement, context size, or parallelism based only on guesswork.

Before hardware-sensitive changes, read:

```text
docs/passport.md
docs/architecture.md
docs/decisions.md
docs/runbook.md
```

Important constraints to preserve:

* the fast worker model should stay on the fast GPU unless the user explicitly decides otherwise;
* the heavy model is not a low-latency backend;
* constrained PCIe links make aggressive multi-GPU experiments risky;
* VRAM headroom matters more than theoretical maximum model size;
* CPU offload is not a normal operating mode;
* unnecessary model reloads should be avoided.

---

## 14. Testing policy

Codex may suggest tests, but the user runs tests that require the real server.

For infrastructure changes, provide:

1. exact commands;
2. expected successful output;
3. how to collect logs if something fails;
4. rollback steps.

Primary health command:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

Important manual checks may include:

```bash
sudo docker ps
sudo docker logs --tail=160 litellm
sudo docker logs --tail=160 open-webui
sudo docker logs --tail=160 llama-coder
sudo docker logs --tail=160 llama-architect
curl http://127.0.0.1:8080/v1/models
curl http://127.0.0.1:8081/v1/models
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

## 15. Validation checklist by file type

When changing Docker Compose files, suggest:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
sudo docker compose ps
```

When changing LiteLLM config, suggest:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
sudo docker compose up -d litellm
sudo docker logs --tail=160 litellm
```

When changing Open WebUI routing, suggest:

```bash
cd /opt/llama-cluster
sudo docker compose up -d open-webui
sudo docker logs --tail=160 open-webui
```

When changing Bash scripts, suggest:

```bash
bash -n scripts/<script-name>.sh
```

If the script affects the real cluster, ask the user to run it manually and provide output.

When changing Markdown docs, suggest:

```bash
git diff --stat
git diff -- README.md docs/
```

When changing anything operational, suggest:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

---

## 16. Documentation rules

This repository is documentation-first.

Whenever behavior, architecture, services, ports, routing, security, or operational procedure changes, update the relevant docs.

Do not rewrite docs wholesale unless requested. Prefer precise edits that preserve existing style.

Use factual language.

Clearly distinguish:

```text
implemented
planned
recommended
experimental
not implemented
tested by user
requires manual check
```

Do not claim something is tested unless the user provided test output or explicitly confirmed it.

When creating a new documentation file under `docs/`, also update the documentation index in `README.md`.

The README documentation table is the authoritative index of project documents.

`AGENTS.md` should explain how Codex must use documentation, but it should not duplicate the full documentation index.

---

## 17. Documentation as source of truth

Documentation is a first-class part of this repository.

Before changing architecture, services, configs, scripts, ports, routing, security, memory, agents, Telegram integration, monitoring, or operational workflows, read the relevant docs first.

When a change affects behavior, Codex must update all affected documentation in the same stage unless the user explicitly asks not to.

Examples:

* if Docker Compose changes, update runbook, architecture, changelog, and any relevant service docs;
* if ports change, update README, passport, runbook, architecture, and changelog;
* if routing changes, update gateway docs, architecture, decisions, runbook, and changelog;
* if a new subsystem appears, create a dedicated document under `docs/`;
* if an architectural decision is made, add or update an ADR in `docs/decisions.md`;
* if an operational command changes, update `docs/runbook.md`;
* if a baseline is completed, create or update the matching `docs/stage*-summary.md`.

Codex must not treat documentation as optional cleanup.

Documentation updates are part of the definition of done.

If Codex cannot determine which docs are affected, it must stop and ask the user before continuing.

Do not duplicate large blocks blindly across many docs.

Prefer:

* README for quick orientation;
* passport for factual current state;
* runbook for operations and diagnostics;
* architecture for system structure;
* gateway docs for LiteLLM-specific routing;
* decisions for why choices were made;
* changelog for dated factual changes;
* dedicated docs for subsystem details;
* stage summaries for completed milestones;
* codex-context for supplemental context only.

---

## 18. Drift and contradiction handling

If documentation, configuration, and observed server output disagree, Codex must not silently choose one.

Report the contradiction using this format:

```text
Mismatch found:
Documentation says:
Config says:
Observed output says:
Recommended source of truth:
Question for user:
```

Examples:

* docs say Open WebUI is direct, but compose routes it through LiteLLM;
* README says port 4000, but compose exposes another port;
* docs say a stage is complete, but runbook has no checks;
* changelog says a test passed, but no user confirmation exists.

When the user confirms the correct state, update the affected documentation in the same stage.

---

## 19. Failure protocol

If a command fails, Codex must not continue blindly.

On failure, Codex should:

1. stop the current stage;
2. summarize what failed;
3. quote only the relevant error lines;
4. explain the likely cause;
5. ask the user for missing logs if needed;
6. propose the smallest safe diagnostic step;
7. provide rollback if the system may be partially changed.

Do not stack multiple speculative fixes in one step.

Do not hide uncertainty.

If the failure could affect running services, preserve the current stable baseline first.

---

## 20. Coding style

Prefer simple, inspectable infrastructure.

Use:

* plain Bash where enough;
* Docker Compose over complex orchestration;
* Markdown documentation;
* explicit config files;
* conservative defaults;
* reversible changes.

Avoid:

* hidden magic;
* unnecessary frameworks;
* premature automation;
* unexplained scripts;
* destructive operations;
* storing state only in chat.

---

## 21. Future work awareness

The desired long-term direction is described in:

```text
docs/codex-context.md
docs/architecture.md
docs/decisions.md
```

Major future areas:

```text
Stage 4 — Memory / RAG
Stage 5 — Telegram bot
Stage 6 — Agent framework / autonomous workflows
Stage 7 — Monitoring, security, backups
```

Do not start installing new databases, bots, frameworks, or monitoring stacks until the design document for that stage exists and the user approves it.

A design stage should answer:

```text
Цель:
Не-цели:
Текущий baseline:
Предлагаемая архитектура:
Затронутые файлы/сервисы:
Влияние на безопасность:
Влияние на persistence/backup:
Ручные проверки:
Откат:
Открытые вопросы:
```

---

## 22. Clarifying questions

Ask the user when:

* the expected behavior is unclear;
* a decision affects architecture;
* a change may break the current stable baseline;
* a test requires the real server;
* multiple reasonable implementation paths exist;
* security posture changes;
* the task requires choosing a new dependency.

When asking, provide a short recommended default and the trade-off.

Do not ask unnecessary questions when the user already provided enough context to make a safe, reversible documentation-only change.

---

## 23. Done definition

A change is done only when:

* files are updated;
* affected documentation is updated;
* docs match actual behavior;
* risks are noted;
* user-facing checks are provided;
* rollback is documented when relevant;
* no secrets are exposed;
* `git status` implications are clear;
* the user has the information needed to test on the server.

If testing cannot be performed by Codex, say so explicitly and ask the user to run the provided commands.

For server-dependent work, do not claim the stage is fully verified until the user provides output or confirms manual testing.
