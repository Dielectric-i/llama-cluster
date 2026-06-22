using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var bot = SlowrigTelegramBot.FromEnvironment();
await bot.RunAsync();

sealed class SlowrigTelegramBot
{
    private const int MaxInputChars = 6000;
    private const int MaxContextMessages = 8;
    private const int MaxTelegramChars = 3900;
    private const int TelegramPollTimeoutSeconds = 0;
    private const int TelegramPollHttpTimeoutSeconds = 8;
    private const int TelegramCallTimeoutSeconds = 25;
    private const int TelegramSendMaxAttempts = 3;
    private static readonly TimeSpan TelegramRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TelegramEmptyPollDelay = TimeSpan.FromMilliseconds(1200);

    private readonly string _token;
    private readonly HashSet<long> _allowedUsers;
    private readonly string _liteLlmKey;
    private readonly string _liteLlmBaseUrl;
    private readonly string _defaultModel;
    private readonly string _architectModel;
    private readonly string _telegramApiBaseUrl;
    private readonly HttpClient _telegramHttp;
    private readonly HttpClient _liteLlmHttp = new();
    private readonly ConcurrentDictionary<long, string> _userModels = new();
    private readonly ConcurrentDictionary<long, List<ChatMessage>> _contexts = new();
    private long _offset;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private SlowrigTelegramBot(
        string token,
        HashSet<long> allowedUsers,
        string liteLlmKey,
        string liteLlmBaseUrl,
        string defaultModel,
        string architectModel,
        string telegramApiBaseUrl,
        string? telegramProxyUrl)
    {
        _token = token;
        _allowedUsers = allowedUsers;
        _liteLlmKey = liteLlmKey;
        _liteLlmBaseUrl = liteLlmBaseUrl.TrimEnd('/');
        _defaultModel = defaultModel;
        _architectModel = architectModel;
        _telegramApiBaseUrl = NormalizeTelegramApiBaseUrl(telegramApiBaseUrl);
        _telegramHttp = CreateTelegramHttpClient(telegramProxyUrl);
        _liteLlmHttp.Timeout = TimeSpan.FromSeconds(190);
        Log($"telegram transport timeouts; poll_timeout_s={TelegramPollTimeoutSeconds}; poll_http_timeout_s={TelegramPollHttpTimeoutSeconds}; empty_poll_delay_ms={TelegramEmptyPollDelay.TotalMilliseconds}; call_timeout_s={TelegramCallTimeoutSeconds}; send_attempts={TelegramSendMaxAttempts}");
    }

    public static SlowrigTelegramBot FromEnvironment()
    {
        var token = Required("TELEGRAM_BOT_TOKEN");
        var allowedUsers = ParseAllowedUsers(Required("TELEGRAM_ALLOWED_USER_IDS"));
        var liteLlmKey = Required("LITELLM_MASTER_KEY");
        var liteLlmBaseUrl = Environment.GetEnvironmentVariable("LITELLM_BASE_URL");
        if (string.IsNullOrWhiteSpace(liteLlmBaseUrl))
        {
            liteLlmBaseUrl = "http://litellm:4000/v1";
        }

        var defaultModel = Environment.GetEnvironmentVariable("TELEGRAM_DEFAULT_MODEL");
        if (string.IsNullOrWhiteSpace(defaultModel))
        {
            defaultModel = "slowrig/coder";
        }

        var architectModel = Environment.GetEnvironmentVariable("TELEGRAM_ARCHITECT_MODEL");
        if (string.IsNullOrWhiteSpace(architectModel))
        {
            architectModel = "slowrig/architect";
        }

        var telegramApiBaseUrl = Environment.GetEnvironmentVariable("TELEGRAM_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(telegramApiBaseUrl))
        {
            telegramApiBaseUrl = "https://api.telegram.org";
        }

        var telegramProxyUrl = Environment.GetEnvironmentVariable("TELEGRAM_PROXY_URL");

        return new SlowrigTelegramBot(token, allowedUsers, liteLlmKey, liteLlmBaseUrl, defaultModel, architectModel, telegramApiBaseUrl, telegramProxyUrl);
    }

    public async Task RunAsync()
    {
        Log($"starting polling bot; allowed_users={_allowedUsers.Count}; default_model={_defaultModel}");

        while (true)
        {
            try
            {
                await PollOnceAsync();
            }
            catch (HttpRequestException ex)
            {
                Log($"network/http error while polling: {ex.Message}", error: true);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (TaskCanceledException ex)
            {
                Log($"timeout while polling: {ex.Message}", error: true);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Log($"unexpected polling error: {ex}", error: true);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    private async Task PollOnceAsync()
    {
        var offsetBefore = _offset;
        var payload = new
        {
            timeout = TelegramPollTimeoutSeconds,
            offset = offsetBefore,
            allowed_updates = new[] { "message" }
        };

        var response = await TelegramAsync<TelegramUpdatesResponse>(
            "getUpdates",
            payload,
            TimeSpan.FromSeconds(TelegramPollHttpTimeoutSeconds),
            maxAttempts: 1,
            logSuccess: false);

        var updates = response.Result ?? [];
        if (updates.Count > 0)
        {
            Log($"poll received updates={updates.Count}; offset_before={offsetBefore}; first_update_id={updates.First().UpdateId}; last_update_id={updates.Last().UpdateId}");
        }

        foreach (var update in updates)
        {
            await HandleUpdateAsync(update);
            _offset = Math.Max(_offset, update.UpdateId + 1);
            Log($"update checkpoint update_id={update.UpdateId}; next_offset={_offset}");
        }

        if (updates.Count == 0)
        {
            await Task.Delay(TelegramEmptyPollDelay);
        }
    }

    private async Task HandleUpdateAsync(TelegramUpdate update)
    {
        var message = update.Message;
        if (message?.Chat is null || message.From is null || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var chatId = message.Chat.Id;
        var userId = message.From.Id;

        if (!_allowedUsers.Contains(userId))
        {
            Log($"denied update_id={update.UpdateId}; user_id={userId}; chat_id={chatId}", error: true);
            await TrySendMessageAsync(chatId, "Доступ не разрешён.");
            return;
        }

        try
        {
            await HandleTextAsync(chatId, userId, message.Text);
        }
        catch (Exception ex)
        {
            Log($"failed to handle update update_id={update.UpdateId}; user_id={userId}; chat_id={chatId}; error={BriefException(ex)}", error: true);
            await TrySendMessageAsync(chatId, "Ошибка обработки запроса. Подробности в логах bot.");
        }
    }

    private async Task HandleTextAsync(long chatId, long userId, string text)
    {
        var trimmed = text.Trim();
        var command = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "";
        var kind = command.StartsWith('/') ? command : "text";
        Log($"message received user_id={userId}; chat_id={chatId}; kind={kind}; chars={trimmed.Length}; model={GetUserModel(userId)}");

        switch (command)
        {
            case "/start":
                await SendMessageAsync(chatId, "slowrig Telegram bot готов.\n" + HelpText());
                break;
            case "/help":
                await SendMessageAsync(chatId, HelpText());
                break;
            case "/status":
                await SendMessageAsync(chatId, await StatusTextAsync());
                break;
            case "/model":
                await SendMessageAsync(chatId, $"Текущая модель: {GetUserModel(userId)}");
                break;
            case "/coder":
                _userModels[userId] = _defaultModel;
                await SendMessageAsync(chatId, $"Модель: {_defaultModel}");
                break;
            case "/architect":
                _userModels[userId] = _architectModel;
                await SendMessageAsync(chatId, $"Следующий запрос пойдёт в {_architectModel}");
                break;
            case "/reset":
                _contexts[userId] = [];
                _userModels[userId] = _defaultModel;
                await SendMessageAsync(chatId, "Контекст сброшен.");
                break;
            default:
                if (command.StartsWith('/'))
                {
                    await SendMessageAsync(chatId, "Неизвестная команда. /help");
                }
                else
                {
                    await SendMessageAsync(chatId, await ChatAsync(userId, trimmed));
                }
                break;
        }
    }

    private async Task<string> ChatAsync(long userId, string text)
    {
        if (text.Length > MaxInputChars)
        {
            return $"Сообщение слишком длинное. Лимит: {MaxInputChars} символов.";
        }

        var model = GetUserModel(userId);
        var context = _contexts.GetOrAdd(userId, _ => []);
        var messages = context.TakeLast(MaxContextMessages).Append(new ChatMessage("user", text)).ToList();
        var payload = new ChatCompletionRequest(model, messages, 0.2, 768);
        var stopwatch = Stopwatch.StartNew();
        Log($"llm request user_id={userId}; model={model}; input_chars={text.Length}; context_messages={messages.Count}");
        var response = await LiteLlmAsync<ChatCompletionResponse>("/chat/completions", payload);
        var answer = response.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = "Пустой ответ.";
        }

        _contexts[userId] = messages.Append(new ChatMessage("assistant", answer)).TakeLast(MaxContextMessages).ToList();

        if (model == _architectModel)
        {
            _userModels[userId] = _defaultModel;
        }

        Log($"llm response user_id={userId}; model={model}; elapsed_ms={stopwatch.ElapsedMilliseconds}; answer_chars={answer.Length}");
        return answer;
    }

    private async Task<string> StatusTextAsync()
    {
        try
        {
            var response = await LiteLlmAsync<ModelsResponse>("/models", payload: null);
            var models = response.Data?.Select(item => item.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray() ?? [];
            var modelText = models.Length == 0 ? "models endpoint answered" : string.Join(", ", models);
            return $"Bot OK\nLiteLLM OK\nModels: {modelText}";
        }
        catch (Exception ex)
        {
            Log($"status check failed: {ex.Message}", error: true);
            return "Bot OK\nLiteLLM FAIL";
        }
    }

    private async Task SendMessageAsync(long chatId, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            text = " ";
        }

        var totalChunks = (text.Length + MaxTelegramChars - 1) / MaxTelegramChars;
        for (var i = 0; i < text.Length; i += MaxTelegramChars)
        {
            var chunk = text.Substring(i, Math.Min(MaxTelegramChars, text.Length - i));
            var chunkIndex = (i / MaxTelegramChars) + 1;
            Log($"telegram send chunk chat_id={chatId}; chunk={chunkIndex}/{totalChunks}; chars={chunk.Length}");
            await TelegramAsync<TelegramOkResponse>("sendMessage", new
            {
                chat_id = chatId,
                text = chunk,
                disable_web_page_preview = true
            }, TimeSpan.FromSeconds(TelegramCallTimeoutSeconds), TelegramSendMaxAttempts, logSuccess: true);
        }
    }

    private async Task TrySendMessageAsync(long chatId, string text)
    {
        try
        {
            await SendMessageAsync(chatId, text);
        }
        catch (Exception ex)
        {
            Log($"failed to send fallback message chat_id={chatId}; error={BriefException(ex)}", error: true);
        }
    }

    private async Task<T> TelegramAsync<T>(string method, object payload, TimeSpan timeout, int maxAttempts, bool logSuccess)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                var url = $"{_telegramApiBaseUrl}/bot{_token}/{method}";
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent(payload)
                };

                using var response = await _telegramHttp.SendAsync(request, cts.Token);
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransientStatusCode(response.StatusCode) && attempt < maxAttempts)
                    {
                        Log($"telegram_api retry method={method}; attempt={attempt}/{maxAttempts}; status={(int)response.StatusCode}; elapsed_ms={stopwatch.ElapsedMilliseconds}", error: true);
                        await Task.Delay(TelegramRetryDelay);
                        continue;
                    }

                    throw new HttpRequestException($"Telegram API {method} failed: status={(int)response.StatusCode}");
                }

                var result = JsonSerializer.Deserialize<TelegramOkResponse>(body, JsonOptions);
                if (result?.Ok != true)
                {
                    throw new InvalidOperationException($"Telegram API {method} returned ok=false");
                }

                if (logSuccess)
                {
                    Log($"telegram_api method={method}; attempt={attempt}/{maxAttempts}; elapsed_ms={stopwatch.ElapsedMilliseconds}; status={(int)response.StatusCode}; ok=true");
                }

                return JsonSerializer.Deserialize<T>(body, JsonOptions)
                    ?? throw new InvalidOperationException($"Telegram API {method} returned invalid JSON");
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < maxAttempts)
            {
                lastError = ex;
                Log($"telegram_api retry method={method}; attempt={attempt}/{maxAttempts}; timeout_s={(int)timeout.TotalSeconds}; elapsed_ms={stopwatch.ElapsedMilliseconds}; error={BriefException(ex)}", error: true);
                await Task.Delay(TelegramRetryDelay);
            }
            catch (Exception ex)
            {
                Log($"telegram_api failed method={method}; attempt={attempt}/{maxAttempts}; timeout_s={(int)timeout.TotalSeconds}; elapsed_ms={stopwatch.ElapsedMilliseconds}; error={BriefException(ex)}", error: true);
                throw;
            }
        }

        throw lastError ?? new InvalidOperationException($"Telegram API {method} failed without an exception");
    }

    private async Task<T> LiteLlmAsync<T>(string path, object? payload)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(payload is null ? HttpMethod.Get : HttpMethod.Post, $"{_liteLlmBaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _liteLlmKey);
            if (payload is not null)
            {
                request.Content = JsonContent(payload);
            }

            using var response = await _liteLlmHttp.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"LiteLLM {path} failed: status={(int)response.StatusCode}");
            }

            Log($"litellm_api path={path}; elapsed_ms={stopwatch.ElapsedMilliseconds}; status={(int)response.StatusCode}; ok=true");
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new InvalidOperationException($"LiteLLM {path} returned invalid JSON");
        }
        catch (Exception ex)
        {
            Log($"litellm_api failed path={path}; elapsed_ms={stopwatch.ElapsedMilliseconds}; error={BriefException(ex)}", error: true);
            throw;
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var status = (int)statusCode;
        return status is 408 or 429 || status >= 500;
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is HttpRequestException or TaskCanceledException or TimeoutException or IOException;
    }

    private static string BriefException(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null && parts.Count < 3; current = current.InnerException)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join(" <- ", parts);
    }

    private string GetUserModel(long userId) => _userModels.GetOrAdd(userId, _defaultModel);

    private static StringContent JsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required");
        }

        return value.Trim();
    }

    private static string NormalizeTelegramApiBaseUrl(string telegramApiBaseUrl)
    {
        if (!Uri.TryCreate(telegramApiBaseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("TELEGRAM_API_BASE_URL must be an HTTP(S) URL like https://api.telegram.org or https://example.workers.dev.");
        }

        var normalized = uri.ToString().TrimEnd('/');
        if (!string.Equals(normalized, "https://api.telegram.org", StringComparison.OrdinalIgnoreCase))
        {
            Log("Telegram API base URL override is configured");
        }

        return normalized;
    }

    private static HttpClient CreateTelegramHttpClient(string? telegramProxyUrl)
    {
        if (string.IsNullOrWhiteSpace(telegramProxyUrl))
        {
            return new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        }

        if (!Uri.TryCreate(telegramProxyUrl.Trim(), UriKind.Absolute, out var proxyUri) ||
            (proxyUri.Scheme != Uri.UriSchemeHttp && proxyUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("TELEGRAM_PROXY_URL must be an HTTP(S) proxy URL like http://host:port. tg:// MTProto proxy links are not supported by Bot API HTTP polling.");
        }

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxyUri),
            UseProxy = true
        };

        Log("Telegram API proxy is configured");
        return new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    private static HashSet<long> ParseAllowedUsers(string raw)
    {
        var result = new HashSet<long>();
        foreach (var item in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(item, out var userId))
            {
                throw new InvalidOperationException("TELEGRAM_ALLOWED_USER_IDS must contain numeric IDs");
            }

            result.Add(userId);
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("TELEGRAM_ALLOWED_USER_IDS is required");
        }

        return result;
    }

    private static string HelpText()
    {
        return string.Join('\n',
        [
            "Доступные команды:",
            "/start — проверить доступ",
            "/help — показать команды",
            "/status — проверить bot и LiteLLM",
            "/model — показать текущую модель",
            "/coder — использовать slowrig/coder",
            "/architect — использовать slowrig/architect для следующего запроса",
            "/reset — сбросить короткий контекст"
        ]);
    }

    private static void Log(string message, bool error = false)
    {
        var line = $"{DateTimeOffset.UtcNow:O} {(error ? "ERROR" : "INFO")} {message}";
        if (error)
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.WriteLine(line);
        }
    }
}

sealed record TelegramOkResponse(bool Ok);
sealed record TelegramUpdatesResponse(bool Ok, List<TelegramUpdate>? Result);
sealed record TelegramUpdate([property: JsonPropertyName("update_id")] long UpdateId, TelegramMessage? Message);
sealed record TelegramMessage(TelegramChat Chat, TelegramUser From, string? Text);
sealed record TelegramChat(long Id);
sealed record TelegramUser(long Id);

sealed record ChatCompletionRequest(
    string Model,
    List<ChatMessage> Messages,
    double Temperature,
    [property: JsonPropertyName("max_tokens")] int MaxTokens);

sealed record ChatMessage(string Role, string Content);
sealed record ChatCompletionResponse(List<ChatChoice>? Choices);
sealed record ChatChoice(ChatMessage? Message);
sealed record ModelsResponse(List<ModelInfo>? Data);
sealed record ModelInfo(string? Id);
