using System.Text.Json;

namespace PresentationManagerBot.Bot;

public class BaleBotClient
{
    private readonly HttpClient _httpClient;
    private readonly string _token;

    public BaleBotClient(HttpClient httpClient, string token)
    {
        _httpClient = httpClient;
        _token = token;
    }

    private string GetUrl(string method)
    {
        return $"https://tapi.bale.ai/bot{_token}/{method}";
    }

    public async Task<JsonElement> GetUpdatesAsync(long? offset = null)
    {
        var url = GetUrl("getUpdates");

        if (offset.HasValue)
        {
            url += $"?offset={offset.Value}";
        }

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    public async Task<JsonElement> SendMessageAsync(
        long chatId,
        string text,
        object? replyMarkup = null)
    {
        var data = new Dictionary<string, string>
        {
            ["chat_id"] = chatId.ToString(),
            ["text"] = text
        };

        if (replyMarkup != null)
        {
            data["reply_markup"] =
                JsonSerializer.Serialize(replyMarkup);
        }

        var content =
            new FormUrlEncodedContent(data);

        var response =
            await _httpClient.PostAsync(
                GetUrl("sendMessage"),
                content);

        response.EnsureSuccessStatusCode();

        var json =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    public async Task<JsonElement> EditMessageTextAsync(
        long chatId,
        long messageId,
        string text,
        object? replyMarkup = null)
    {
        var data = new Dictionary<string, string>
        {
            ["chat_id"] = chatId.ToString(),
            ["message_id"] = messageId.ToString(),
            ["text"] = text
        };

        if (replyMarkup != null)
        {
            data["reply_markup"] =
                JsonSerializer.Serialize(replyMarkup);
        }

        var content =
            new FormUrlEncodedContent(data);

        var response =
            await _httpClient.PostAsync(
                GetUrl("editMessageText"),
                content);

        response.EnsureSuccessStatusCode();

        var json =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    public async Task<JsonElement> AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null)
    {
        var data =
            new Dictionary<string, string>
            {
                ["callback_query_id"] =
                    callbackQueryId
            };

        if (!string.IsNullOrWhiteSpace(text))
        {
            data["text"] = text;
        }

        var content =
            new FormUrlEncodedContent(data);

        var response =
            await _httpClient.PostAsync(
                GetUrl("answerCallbackQuery"),
                content);

        response.EnsureSuccessStatusCode();

        var json =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    public async Task DeleteMessageAsync(
        long chatId,
        long messageId)
    {
        try
        {
            var content =
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["chat_id"] = chatId.ToString(),
                        ["message_id"] = messageId.ToString()
                    });

            var response =
                await _httpClient.PostAsync(
                    GetUrl("deleteMessage"),
                    content);

            // We intentionally don't throw here.
            // Failure to delete a temporary message
            // shouldn't crash the bot.
        }
        catch
        {
            // Ignore deletion failures.
        }
    }

    public async Task<JsonElement> GetChatMemberAsync(
        long chatId,
        long userId)
    {
        var content =
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["chat_id"] = chatId.ToString(),
                    ["user_id"] = userId.ToString()
                });

        var response =
            await _httpClient.PostAsync(
                GetUrl("getChatMember"),
                content);

        response.EnsureSuccessStatusCode();

        var json =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    public async Task<bool> IsAdminAsync(
        long chatId,
        long userId)
    {
        var result =
            await GetChatMemberAsync(
                chatId,
                userId);

        if (!result.TryGetProperty(
                "ok",
                out var ok) ||
            !ok.GetBoolean())
        {
            return false;
        }

        if (!result.TryGetProperty(
                "result",
                out var member))
        {
            return false;
        }

        var status =
            member.TryGetProperty(
                "status",
                out var statusElement)
                ? statusElement.GetString()
                : null;

        return status == "creator" ||
               status == "administrator";
    }

    public static long? GetMessageId(
        JsonElement result)
    {
        if (!result.TryGetProperty(
                "result",
                out var message))
        {
            return null;
        }

        if (!message.TryGetProperty(
                "message_id",
                out var messageId))
        {
            return null;
        }

        return messageId.GetInt64();
    }
}