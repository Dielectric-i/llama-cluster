#!/usr/bin/env python3
"""Minimal polling Telegram bot for slowrig via LiteLLM."""

from __future__ import annotations

import json
import logging
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from collections import defaultdict
from typing import Any


TELEGRAM_API = "https://api.telegram.org/bot{token}/{method}"
MAX_INPUT_CHARS = 6000
MAX_CONTEXT_MESSAGES = 8
MAX_TELEGRAM_CHARS = 3900


logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(message)s",
)
LOG = logging.getLogger("slowrig-telegram-bot")


class ConfigError(RuntimeError):
    pass


def env_required(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        raise ConfigError(f"{name} is required")
    return value


def parse_allowed_users(raw: str) -> set[int]:
    users: set[int] = set()
    for item in raw.split(","):
        item = item.strip()
        if not item:
            continue
        try:
            users.add(int(item))
        except ValueError as exc:
            raise ConfigError("TELEGRAM_ALLOWED_USER_IDS must contain numeric IDs") from exc
    if not users:
        raise ConfigError("TELEGRAM_ALLOWED_USER_IDS is required")
    return users


class SlowrigTelegramBot:
    def __init__(self) -> None:
        self.token = env_required("TELEGRAM_BOT_TOKEN")
        self.allowed_users = parse_allowed_users(env_required("TELEGRAM_ALLOWED_USER_IDS"))
        self.litellm_key = env_required("LITELLM_MASTER_KEY")
        self.litellm_base_url = os.environ.get("LITELLM_BASE_URL", "http://litellm:4000/v1").rstrip("/")
        self.default_model = os.environ.get("TELEGRAM_DEFAULT_MODEL", "slowrig/coder").strip() or "slowrig/coder"
        self.architect_model = os.environ.get("TELEGRAM_ARCHITECT_MODEL", "slowrig/architect").strip() or "slowrig/architect"
        self.user_models: dict[int, str] = defaultdict(lambda: self.default_model)
        self.context: dict[int, list[dict[str, str]]] = defaultdict(list)
        self.offset = 0

    def telegram(self, method: str, payload: dict[str, Any], timeout: int = 45) -> dict[str, Any]:
        url = TELEGRAM_API.format(token=self.token, method=method)
        data = json.dumps(payload).encode("utf-8")
        request = urllib.request.Request(
            url,
            data=data,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urllib.request.urlopen(request, timeout=timeout) as response:
            result = json.loads(response.read().decode("utf-8"))
        if not result.get("ok"):
            raise RuntimeError(f"Telegram API error in {method}")
        return result

    def litellm(self, path: str, payload: dict[str, Any] | None = None, timeout: int = 90) -> dict[str, Any]:
        data = None if payload is None else json.dumps(payload).encode("utf-8")
        request = urllib.request.Request(
            f"{self.litellm_base_url}{path}",
            data=data,
            headers={
                "Authorization": f"Bearer {self.litellm_key}",
                "Content-Type": "application/json",
            },
            method="GET" if payload is None else "POST",
        )
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))

    def send_message(self, chat_id: int, text: str) -> None:
        chunks = [text[i : i + MAX_TELEGRAM_CHARS] for i in range(0, len(text), MAX_TELEGRAM_CHARS)] or [""]
        for chunk in chunks:
            self.telegram(
                "sendMessage",
                {
                    "chat_id": chat_id,
                    "text": chunk,
                    "disable_web_page_preview": True,
                },
            )

    def deny(self, chat_id: int, user_id: int) -> None:
        LOG.warning("denied user_id=%s", user_id)
        self.send_message(chat_id, "Доступ не разрешён.")

    def help_text(self) -> str:
        return "\n".join(
            [
                "Доступные команды:",
                "/start — проверить доступ",
                "/help — показать команды",
                "/status — проверить bot и LiteLLM",
                "/model — показать текущую модель",
                "/coder — использовать slowrig/coder",
                "/architect — использовать slowrig/architect для следующего запроса",
                "/reset — сбросить короткий контекст",
            ]
        )

    def status_text(self) -> str:
        try:
            data = self.litellm("/models", timeout=12)
            models = [item.get("id") for item in data.get("data", []) if isinstance(item, dict)]
            available = ", ".join(model for model in models if model) or "models endpoint answered"
            return f"Bot OK\nLiteLLM OK\nModels: {available}"
        except Exception as exc:  # noqa: BLE001 - keep Telegram status robust
            LOG.warning("status check failed: %s", exc)
            return "Bot OK\nLiteLLM FAIL"

    def chat(self, user_id: int, text: str) -> str:
        if len(text) > MAX_INPUT_CHARS:
            return f"Сообщение слишком длинное. Лимит: {MAX_INPUT_CHARS} символов."

        model = self.user_models[user_id]
        messages = self.context[user_id][-MAX_CONTEXT_MESSAGES:] + [{"role": "user", "content": text}]
        payload = {
            "model": model,
            "messages": messages,
            "temperature": 0.2,
            "max_tokens": 768,
        }
        data = self.litellm("/chat/completions", payload=payload, timeout=180)
        answer = data["choices"][0]["message"]["content"].strip()
        self.context[user_id] = (messages + [{"role": "assistant", "content": answer}])[-MAX_CONTEXT_MESSAGES:]
        if model == self.architect_model:
            self.user_models[user_id] = self.default_model
        return answer or "Пустой ответ."

    def handle_text(self, chat_id: int, user_id: int, text: str) -> None:
        command = text.strip().split(maxsplit=1)[0].lower()
        LOG.info("message user_id=%s command=%s", user_id, command if command.startswith("/") else "text")

        if command == "/start":
            self.send_message(chat_id, "slowrig Telegram bot готов. " + self.help_text())
        elif command == "/help":
            self.send_message(chat_id, self.help_text())
        elif command == "/status":
            self.send_message(chat_id, self.status_text())
        elif command == "/model":
            self.send_message(chat_id, f"Текущая модель: {self.user_models[user_id]}")
        elif command == "/coder":
            self.user_models[user_id] = self.default_model
            self.send_message(chat_id, f"Модель: {self.default_model}")
        elif command == "/architect":
            self.user_models[user_id] = self.architect_model
            self.send_message(chat_id, f"Следующий запрос пойдёт в {self.architect_model}")
        elif command == "/reset":
            self.context[user_id] = []
            self.user_models[user_id] = self.default_model
            self.send_message(chat_id, "Контекст сброшен.")
        elif command.startswith("/"):
            self.send_message(chat_id, "Неизвестная команда. /help")
        else:
            self.send_message(chat_id, self.chat(user_id, text))

    def handle_update(self, update: dict[str, Any]) -> None:
        message = update.get("message")
        if not isinstance(message, dict):
            return
        chat = message.get("chat")
        sender = message.get("from")
        text = message.get("text")
        if not isinstance(chat, dict) or not isinstance(sender, dict) or not isinstance(text, str):
            return
        chat_id = int(chat["id"])
        user_id = int(sender["id"])
        if user_id not in self.allowed_users:
            self.deny(chat_id, user_id)
            return
        try:
            self.handle_text(chat_id, user_id, text)
        except Exception as exc:  # noqa: BLE001 - Telegram handler must keep polling
            LOG.exception("failed to handle update for user_id=%s: %s", user_id, exc)
            self.send_message(chat_id, "Ошибка обработки запроса. Подробности в логах bot.")

    def poll_once(self) -> None:
        payload = {"timeout": 30, "offset": self.offset, "allowed_updates": ["message"]}
        result = self.telegram("getUpdates", payload, timeout=45)
        for update in result.get("result", []):
            update_id = int(update.get("update_id", 0))
            self.offset = max(self.offset, update_id + 1)
            self.handle_update(update)

    def run(self) -> None:
        LOG.info("starting polling bot; allowed_users=%d default_model=%s", len(self.allowed_users), self.default_model)
        while True:
            try:
                self.poll_once()
            except urllib.error.HTTPError as exc:
                LOG.warning("HTTP error while polling: status=%s", exc.code)
                time.sleep(5)
            except urllib.error.URLError as exc:
                LOG.warning("network error while polling: %s", exc.reason)
                time.sleep(5)
            except Exception as exc:  # noqa: BLE001 - keep bot alive on transient errors
                LOG.exception("unexpected polling error: %s", exc)
                time.sleep(5)


def main() -> int:
    try:
        bot = SlowrigTelegramBot()
    except ConfigError as exc:
        LOG.error("configuration error: %s", exc)
        return 2
    bot.run()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
