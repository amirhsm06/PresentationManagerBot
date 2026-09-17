using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PresentationManagerBot.Bot;
using PresentationManagerBot.Infrastructure.Data;
using PresentationManagerBot.Infrastructure.Services;

var configuration = new Configuration();

var token = configuration.Token;

if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("❌ Bot token is missing.");
    return;
}

if (string.IsNullOrWhiteSpace(
        configuration.ConnectionString))
{
    Console.WriteLine(
        "❌ Database connection string is missing.");
    return;
}

var dbOptions =
    new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(
            configuration.ConnectionString)
        .Options;

using var db =
    new AppDbContext(dbOptions);

Console.WriteLine(
    "🗄️ Database: " +
    db.Database.GetDbConnection().Database);

Console.WriteLine(
    "🖥️ Server: " +
    db.Database.GetDbConnection().DataSource);

var topicService =
    new TopicService(db);

var presentationService =
    new PresentationService(db);

using var httpClient =
    new HttpClient();

var bot =
    new BaleBotClient(
        httpClient,
        token);

// ==========================================
// Temporary interaction state
// ==========================================

// Admin is expected to send topic list.
var waitingForTopics =
    new HashSet<(long ChatId, long UserId)>();

// User selected a topic and must enter a date.
var waitingForPresentationDate =
    new Dictionary<
        (long ChatId, long UserId),
        long>();

// Admin selected topic removal.
var waitingForTopicRemoval =
    new HashSet<(long ChatId, long UserId)>();

// Bot prompt messages that should be deleted
// when the user responds.
var promptMessages =
    new Dictionary<
        (long ChatId, long UserId),
        long>();

Console.WriteLine(
    "🤖 مدیر ارائه‌ها is running...");

Console.WriteLine(
    "Waiting for messages...\n");

long? offset = null;

while (true)
{
    try
    {
        var result =
            await bot.GetUpdatesAsync(offset);

        if (!result.TryGetProperty(
                "result",
                out var updates))
        {
            await Task.Delay(500);
            continue;
        }

        foreach (var update
                 in updates.EnumerateArray())
        {
            var updateId =
                update
                    .GetProperty("update_id")
                    .GetInt64();

            offset =
                updateId + 1;

            Console.WriteLine(
                "================================");

            Console.WriteLine(
                update.ToString());

            Console.WriteLine(
                "================================");

            // ==================================
            // CALLBACK QUERY
            // ==================================

            if (update.TryGetProperty(
                    "callback_query",
                    out var callbackQuery))
            {
                await HandleCallbackQuery(
                    callbackQuery);

                continue;
            }

            // ==================================
            // MESSAGE
            // ==================================

            if (!update.TryGetProperty(
                    "message",
                    out var message))
            {
                continue;
            }

            var chat =
                message.GetProperty("chat");

            var chatId =
                chat.GetProperty("id")
                    .GetInt64();

            var sender =
                message.GetProperty("from");

            var senderId =
                sender.GetProperty("id")
                    .GetInt64();

            var messageId =
                message.GetProperty("message_id")
                    .GetInt64();

            var text =
                message.TryGetProperty(
                    "text",
                    out var textElement)
                    ? textElement.GetString()
                    : null;

            var firstName =
                sender.TryGetProperty(
                    "first_name",
                    out var firstNameElement)
                    ? firstNameElement
                        .GetString() ?? ""
                    : "";

            var lastName =
                sender.TryGetProperty(
                    "last_name",
                    out var lastNameElement)
                    ? lastNameElement
                        .GetString() ?? ""
                    : "";

            Console.WriteLine(
                $"📩 Message: {text}");

            Console.WriteLine(
                $"💬 Chat ID: {chatId}");

            Console.WriteLine(
                $"👤 Sender ID: {senderId}");

            // ==================================
            // BOT ADDED TO GROUP
            // ==================================

            if (message.TryGetProperty(
                    "new_chat_members",
                    out var newMembers))
            {
                foreach (var member
                         in newMembers.EnumerateArray())
                {
                    if (member.TryGetProperty(
                            "is_bot",
                            out var isBot) &&
                        isBot.GetBoolean())
                    {
                        await SendMainMenuAsync(
                            chatId,
                            senderId);

                        break;
                    }
                }

                continue;
            }

            // ==================================
            // /start
            // ==================================

            if (text == "/start")
            {
                await TryDeleteAsync(
                    chatId,
                    messageId);

                await SendMainMenuAsync(
                    chatId,
                    senderId);

                continue;
            }

            // ==================================
            // /menu
            // ==================================

            if (text == "/menu")
            {
                await TryDeleteAsync(
                    chatId,
                    messageId);

                await SendMainMenuAsync(
                    chatId,
                    senderId);

                continue;
            }

            // ==================================
            // /cancel
            // ==================================

            if (text == "/cancel")
            {
                await TryDeleteAsync(
                    chatId,
                    messageId);

                var key =
                    (chatId, senderId);

                var cancelled =
                    waitingForTopics.Remove(key);

                cancelled |=
                    waitingForPresentationDate
                        .Remove(key);

                cancelled |=
                    waitingForTopicRemoval
                        .Remove(key);

                await DeletePromptAsync(key);

                if (cancelled)
                {
                    await bot.SendMessageAsync(
                        chatId,
                        "❌ عملیات لغو شد.");
                }

                await SendMainMenuAsync(
                    chatId,
                    senderId);

                continue;
            }

            // ==================================
            // /topics
            // ==================================

            if (text == "/topics")
            {
                await TryDeleteAsync(
                    chatId,
                    messageId);

                await ShowTopicsAsync(
                    chatId,
                    senderId);

                continue;
            }

            // ==================================
            // /presentations
            // ==================================

            if (text == "/presentations")
            {
                await TryDeleteAsync(
                    chatId,
                    messageId);

                await ShowPresentationsAsync(
                    chatId,
                    senderId);

                continue;
            }

            // ==================================
            // /addtopic
            // ==================================

            if (text == "/addtopic")
            {
                await TryDeleteAsync(
                    chatId,
                    messageId);

                await StartAddTopicsAsync(
                    chatId,
                    senderId);

                continue;
            }

            // ==================================
            // /removetopic
            // ==================================

            if (text == "/removetopic")
            {
                await TryDeleteAsync(
                    chatId,
                    messageId);

                await StartRemoveTopicAsync(
                    chatId,
                    senderId);

                continue;
            }

            // ==================================
            // WAITING FOR TOPIC INPUT
            // ==================================

            if (text != null &&
                waitingForTopics.Contains(
                    (chatId, senderId)))
            {
                await HandleTopicInputAsync(
                    chatId,
                    senderId,
                    messageId,
                    text);

                continue;
            }

            // ==================================
            // WAITING FOR DATE
            // ==================================

            if (text != null &&
                waitingForPresentationDate
                    .TryGetValue(
                        (chatId, senderId),
                        out var selectedTopicId))
            {
                await HandleDateInputAsync(
                    chatId,
                    senderId,
                    messageId,
                    selectedTopicId,
                    firstName,
                    lastName,
                    text);

                continue;
            }

            // ==================================
            // LEGACY NUMBER SELECTION
            //
            // Keeps old behavior working.
            // ==================================

            if (text != null &&
                int.TryParse(
                    NormalizeDigits(text.Trim()),
                    out var selectedNumber))
            {
                await TryDeleteAsync(
                    chatId,
                    messageId);

                var topics =
                    await topicService
                        .GetAvailableTopicsAsync(
                            chatId);

                if (selectedNumber < 1 ||
                    selectedNumber > topics.Count)
                {
                    await bot.SendMessageAsync(
                        chatId,
                        "❌ شماره موضوع نامعتبر است.\n\n" +
                        "برای مشاهده موضوعات از /topics استفاده کنید.");

                    continue;
                }

                var selectedTopic =
                    topics[selectedNumber - 1];

                waitingForPresentationDate[
                    (chatId, senderId)] =
                    selectedTopic.Id;

                await AskForDateAsync(
                    chatId,
                    senderId,
                    selectedTopic);

                continue;
            }
        }

        await Task.Delay(300);
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "================================");

        Console.WriteLine(
            "❌ ERROR");

        Console.WriteLine(ex);

        Console.WriteLine(
            "================================");

        await Task.Delay(3000);
    }
}

// =====================================================
// CALLBACK HANDLING
// =====================================================

async Task HandleCallbackQuery(
    JsonElement callbackQuery)
{
    var callbackId =
        callbackQuery
            .GetProperty("id")
            .GetString() ?? "";

    var sender =
        callbackQuery
            .GetProperty("from");

    var senderId =
        sender.GetProperty("id")
            .GetInt64();

    var data =
        callbackQuery.TryGetProperty(
            "data",
            out var dataElement)
            ? dataElement.GetString()
            : null;

    if (!callbackQuery.TryGetProperty(
            "message",
            out var callbackMessage))
    {
        return;
    }

    var chat =
        callbackMessage.GetProperty("chat");

    var chatId =
        chat.GetProperty("id")
            .GetInt64();

    var messageId =
        callbackMessage
            .GetProperty("message_id")
            .GetInt64();

    if (string.IsNullOrWhiteSpace(data))
    {
        return;
    }

    // ==========================================
    // MAIN MENU
    // ==========================================

    if (data == "menu_topics")
    {
        await bot.AnswerCallbackQueryAsync(callbackId);

        await ShowTopicsAsync(
            chatId,
            senderId,
            messageId);

        return;
    }

    if (data == "menu_register")
    {
        await bot.AnswerCallbackQueryAsync(callbackId);

        await StartRegistrationAsync(
            chatId,
            senderId,
            messageId);

        return;
    }

    if (data == "menu_presentations")
    {
        await bot.AnswerCallbackQueryAsync(callbackId);

        await ShowPresentationsAsync(
            chatId,
            senderId,
            messageId);

        return;
    }

    if (data == "menu_addtopic")
    {
        await bot.AnswerCallbackQueryAsync(callbackId);

        await StartAddTopicsAsync(
            chatId,
            senderId,
            messageId);

        return;
    }

    if (data == "menu_removetopic")
    {
        await bot.AnswerCallbackQueryAsync(callbackId);

        await StartRemoveTopicAsync(
            chatId,
            senderId,
            messageId);

        return;
    }

    if (data == "menu_home")
    {
        await bot.AnswerCallbackQueryAsync(callbackId);

        await SendMainMenuAsync(
            chatId,
            senderId,
            messageId);

        return;
    }

    // ==========================================
    // TOPIC SELECTION
    // ==========================================

    if (data.StartsWith("topic:"))
    {
        if (!long.TryParse(
                data["topic:".Length..],
                out var topicId))
        {
            return;
        }

        var topic =
            await topicService
                .GetAvailableTopicAsync(
                    chatId,
                    topicId);

        if (topic == null)
        {
            await bot.AnswerCallbackQueryAsync(
                callbackId,
                "این موضوع دیگر در دسترس نیست.");

            await ShowTopicsAsync(
                chatId,
                senderId,
                messageId);

            return;
        }

        waitingForPresentationDate[
            (chatId, senderId)] =
            topic.Id;

        await bot.AnswerCallbackQueryAsync(
            callbackId,
            "📌 موضوع انتخاب شد.");

        await AskForDateAsync(
            chatId,
            senderId,
            topic,
            messageId);

        return;
    }

    // ==========================================
    // TOPIC REMOVAL
    // ==========================================

    if (data.StartsWith("remove:"))
    {
        var isAdmin =
            await bot.IsAdminAsync(
                chatId,
                senderId);

        if (!isAdmin)
        {
            await bot.AnswerCallbackQueryAsync(
                callbackId,
                "فقط ادمین‌ها می‌توانند موضوع حذف کنند.");

            return;
        }

        if (!long.TryParse(
                data["remove:".Length..],
                out var topicId))
        {
            return;
        }

        var topic =
            await topicService
                .GetAvailableTopicAsync(
                    chatId,
                    topicId);

        if (topic == null)
        {
            await bot.AnswerCallbackQueryAsync(
                callbackId,
                "این موضوع دیگر در دسترس نیست.");

            await ShowRemoveTopicsAsync(
                chatId,
                senderId,
                messageId);

            return;
        }

        var removed =
            await topicService
                .RemoveTopicAsync(
                    chatId,
                    topicId);

        if (!removed)
        {
            await bot.AnswerCallbackQueryAsync(
                callbackId,
                "حذف موضوع انجام نشد.");

            await ShowRemoveTopicsAsync(
                chatId,
                senderId,
                messageId);

            return;
        }

        // Confirm the button click.
        await bot.AnswerCallbackQueryAsync(
            callbackId,
            "✅ موضوع حذف شد.");

        // Refresh the SAME message immediately.
        await ShowRemoveTopicsAsync(
            chatId,
            senderId,
            messageId);

        return;
    }
    // ==========================================
    // CANCEL BUTTON
    // ==========================================

    if (data == "cancel")
    {
        await bot.AnswerCallbackQueryAsync(
            callbackId,
            "❌ عملیات لغو شد.");

        var key =
            (chatId, senderId);

        waitingForTopics.Remove(key);

        waitingForPresentationDate
            .Remove(key);

        waitingForTopicRemoval
            .Remove(key);

        await DeletePromptAsync(key);

        await SendMainMenuAsync(
            chatId,
            senderId,
            messageId);

        return;
    }
}

// =====================================================
// MAIN MENU
// =====================================================

async Task SendMainMenuAsync(
    long chatId,
    long userId,
    long? existingMessageId = null)
{
    var isAdmin =
        await bot.IsAdminAsync(
            chatId,
            userId);

    var keyboard =
        new List<List<object>>
        {
            new()
            {
                Button(
                    "📚 موضوعات ارائه",
                    "menu_topics")
            },
            new()
            {
                Button(
                    "📝 ثبت ارائه",
                    "menu_register")
            },
            new()
            {
                Button(
                    "📋 برنامه ارائه‌ها",
                    "menu_presentations")
            }
        };

    if (isAdmin)
    {
        // Full-width Add Topic button
        keyboard.Add(
            new List<object>
            {
                Button(
                    "➕ افزودن موضوع",
                    "menu_addtopic")
            });

        // Full-width Remove Topic button
        keyboard.Add(
            new List<object>
            {
                Button(
                    "🗑 حذف موضوع",
                    "menu_removetopic")
            });
    }

    var markup =
        new
        {
            inline_keyboard = keyboard
        };

    const string text =
        "🤖 *مدیر ارائه‌ها*\n\n" +
        "سلام 👋\n" +
        "از گزینه‌های زیر استفاده کنید:";

    if (existingMessageId.HasValue)
    {
        await bot.EditMessageTextAsync(
            chatId,
            existingMessageId.Value,
            text,
            markup);

        return;
    }

    await bot.SendMessageAsync(
        chatId,
        text,
        markup);
}

// =====================================================
// SHOW TOPICS
// =====================================================

async Task ShowTopicsAsync(
    long chatId,
    long userId,
    long? messageId = null)
{
    var topics =
        await topicService
            .GetAvailableTopicsAsync(chatId);

    if (topics.Count == 0)
    {
        await EditOrSendAsync(
            chatId,
            messageId,
            "📚 *موضوعات ارائه*\n\n" +
            "در حال حاضر موضوعی موجود نیست.",
            HomeKeyboard());

        return;
    }

    var keyboard =
        new List<List<object>>();

    for (var i = 0;
         i < topics.Count;
         i++)
    {
        var topic =
            topics[i];

        var title =
            $"{ToPersianDigits((i + 1).ToString())}. " +
            Shorten(topic.Title, 45);

        keyboard.Add(
            new List<object>
            {
                Button(
                    title,
                    $"topic:{topic.Id}")
            });
    }

    keyboard.Add(
        new List<object>
        {
            Button(
                "🏠 بازگشت",
                "menu_home")
        });

    var markup =
        new
        {
            inline_keyboard =
                keyboard
        };

    await EditOrSendAsync(
        chatId,
        messageId,
        "📚 *موضوعات ارائه*\n\n" +
        "موضوع موردنظر خود را انتخاب کنید:",
        markup);
}

// =====================================================
// REGISTRATION
// =====================================================

async Task StartRegistrationAsync(
    long chatId,
    long userId,
    long messageId)
{
    await ShowTopicsAsync(
        chatId,
        userId,
        messageId);
}

async Task AskForDateAsync(
    long chatId,
    long userId,
    PresentationManagerBot.Domain.Entities.Topic topic,
    long? messageId = null)
{
    var key =
        (chatId, userId);

    var keyboard =
        new
        {
            inline_keyboard =
                new[]
                {
                    new object[]
                    {
                        Button(
                            "❌ لغو",
                            "cancel")
                    }
                }
        };

    var text =
        "📌 *موضوع انتخاب شد*\n\n" +
        $"{topic.Title}\n\n" +
        "📅 لطفاً تاریخ ارائه را وارد کنید.\n\n" +
        "مثال:\n" +
        "۱۴۰۵/۰۷/۱۳";

    if (messageId.HasValue)
    {
        await bot.EditMessageTextAsync(
            chatId,
            messageId.Value,
            text,
            keyboard);
    }
    else
    {
        var result =
            await bot.SendMessageAsync(
                chatId,
                text,
                keyboard);

        var newMessageId =
            BaleBotClient.GetMessageId(
                result);

        if (newMessageId.HasValue)
        {
            promptMessages[key] =
                newMessageId.Value;
        }
    }
}

// =====================================================
// DATE INPUT
// =====================================================

async Task HandleDateInputAsync(
    long chatId,
    long userId,
    long userMessageId,
    long selectedTopicId,
    string firstName,
    string lastName,
    string text)
{
    var key =
        (chatId, userId);

    if (!PersianDateService.TryParse(
            text,
            out var presentationDate))
    {
        await TryDeleteAsync(
            chatId,
            userMessageId);

        await bot.SendMessageAsync(
            chatId,
            "❌ تاریخ واردشده صحیح نیست.\n\n" +
            "لطفاً تاریخ شمسی را به شکل زیر وارد کنید:\n" +
            "۱۴۰۵/۰۷/۱۳");

        return;
    }

    if (presentationDate.Date <
        DateTime.Today)
    {
        await TryDeleteAsync(
            chatId,
            userMessageId);

        await bot.SendMessageAsync(
            chatId,
            "❌ تاریخ ارائه نمی‌تواند در گذشته باشد.\n\n" +
            "لطفاً تاریخ امروز یا آینده را وارد کنید.");

        return;
    }

    var topic =
        await topicService
            .GetAvailableTopicAsync(
                chatId,
                selectedTopicId);

    if (topic == null)
    {
        await TryDeleteAsync(
            chatId,
            userMessageId);

        waitingForPresentationDate
            .Remove(key);

        await DeletePromptAsync(key);

        await bot.SendMessageAsync(
            chatId,
            "❌ متأسفانه این موضوع دیگر در دسترس نیست.");

        return;
    }

    await presentationService.CreateAsync(
        chatId,
        topic,
        userId,
        firstName,
        lastName,
        presentationDate);

    waitingForPresentationDate
        .Remove(key);

    await TryDeleteAsync(
        chatId,
        userMessageId);

    await DeletePromptAsync(key);

    var fullName =
        $"{firstName} {lastName}".Trim();

    if (string.IsNullOrWhiteSpace(fullName))
    {
        fullName = "بدون نام";
    }

    var persianDate =
        PersianDateService.ToPersianDigits(
            PersianDateService.ToPersianDate(
                presentationDate));

    await bot.SendMessageAsync(
        chatId,
        "✅ *ارائه با موفقیت ثبت شد!*\n\n" +
        $"📌 موضوع: {topic.Title}\n" +
        $"👤 ارائه‌دهنده: {fullName}\n" +
        $"📅 تاریخ: {persianDate}");

    await SendMainMenuAsync(
        chatId,
        userId);
}

// =====================================================
// ADD TOPICS
// =====================================================

async Task StartAddTopicsAsync(
    long chatId,
    long userId,
    long? messageId = null)
{
    var isAdmin =
        await bot.IsAdminAsync(
            chatId,
            userId);

    if (!isAdmin)
    {
        await EditOrSendAsync(
            chatId,
            messageId,
            "❌ فقط ادمین‌های گروه می‌توانند موضوع اضافه کنند.",
            HomeKeyboard());

        return;
    }

    var key =
        (chatId, userId);

    waitingForTopics.Add(key);

    var keyboard =
        new
        {
            inline_keyboard =
                new[]
                {
                    new object[]
                    {
                        Button(
                            "❌ لغو",
                            "cancel")
                    }
                }
        };

    const string text =
        "➕ *افزودن موضوع*\n\n" +
        "موضوعات را ارسال کنید.\n" +
        "هر موضوع در یک خط.\n\n" +
        "شماره‌گذاری ابتدای موضوعات خودکار حذف می‌شود.\n\n" +
        "مثال:\n" +
        "1. موضوع اول\n" +
        "2. موضوع دوم";

    if (messageId.HasValue)
    {
        await bot.EditMessageTextAsync(
            chatId,
            messageId.Value,
            text,
            keyboard);
    }
    else
    {
        var result =
            await bot.SendMessageAsync(
                chatId,
                text,
                keyboard);

        var promptId =
            BaleBotClient.GetMessageId(
                result);

        if (promptId.HasValue)
        {
            promptMessages[key] =
                promptId.Value;
        }
    }
}

async Task HandleTopicInputAsync(
    long chatId,
    long userId,
    long messageId,
    string text)
{
    var key =
        (chatId, userId);

    waitingForTopics.Remove(key);

    await TryDeleteAsync(
        chatId,
        messageId);

    await DeletePromptAsync(key);

    var result =
        await topicService
            .AddTopicsAsync(
                chatId,
                text);

    var response =
        $"📚 *موضوعات ثبت شد*\n\n" +
        $"✅ {ToPersianDigits(result.Added.Count.ToString())} موضوع اضافه شد.";

    if (result.Duplicates.Count > 0)
    {
        response +=
            $"\n⚠️ {ToPersianDigits(result.Duplicates.Count.ToString())} موضوع تکراری نادیده گرفته شد.";
    }

    await bot.SendMessageAsync(
        chatId,
        response);

    await SendMainMenuAsync(
        chatId,
        userId);
}

// =====================================================
// REMOVE TOPICS
// =====================================================

async Task StartRemoveTopicAsync(
    long chatId,
    long userId,
    long? messageId = null)
{
    var isAdmin =
        await bot.IsAdminAsync(
            chatId,
            userId);

    if (!isAdmin)
    {
        await EditOrSendAsync(
            chatId,
            messageId,
            "❌ فقط ادمین‌های گروه می‌توانند موضوع حذف کنند.",
            HomeKeyboard());

        return;
    }

    await ShowRemoveTopicsAsync(
        chatId,
        userId,
        messageId);
}

async Task ShowRemoveTopicsAsync(
    long chatId,
    long userId,
    long? messageId = null)
{
    var topics =
        await topicService
            .GetAvailableTopicsAsync(
                chatId);

    if (topics.Count == 0)
    {
        await EditOrSendAsync(
            chatId,
            messageId,
            "🗑️ *حذف موضوع*\n\n" +
            "هیچ موضوع قابل حذفی وجود ندارد.",
            HomeKeyboard());

        return;
    }

    var keyboard =
        new List<List<object>>();

    for (var i = 0;
         i < topics.Count;
         i++)
    {
        var topic =
            topics[i];

        keyboard.Add(
            new List<object>
            {
                Button(
                    "🗑 " +
                    ToPersianDigits(
                        (i + 1).ToString()) +
                    ". " +
                    Shorten(
                        topic.Title,
                        42),
                    $"remove:{topic.Id}")
            });
    }

    keyboard.Add(
        new List<object>
        {
            Button(
                "🏠 بازگشت",
                "menu_home")
        });

    var markup =
        new
        {
            inline_keyboard =
                keyboard
        };

    await EditOrSendAsync(
        chatId,
        messageId,
        "🗑️ *حذف موضوع*\n\n" +
        "موضوعی را که می‌خواهید حذف کنید انتخاب کنید:",
        markup);
}

// =====================================================
// PRESENTATIONS
// =====================================================

async Task ShowPresentationsAsync(
    long chatId,
    long userId,
    long? messageId = null)
{
    var isAdmin =
        await bot.IsAdminAsync(
            chatId,
            userId);

    if (!isAdmin)
    {
        await EditOrSendAsync(
            chatId,
            messageId,
            "❌ فقط ادمین‌های گروه می‌توانند برنامه ارائه‌ها را مشاهده کنند.",
            HomeKeyboard());

        return;
    }

    var presentations =
        await presentationService
            .GetByGroupAsync(
                chatId);

    if (presentations.Count == 0)
    {
        await EditOrSendAsync(
            chatId,
            messageId,
            "📋 *برنامه ارائه‌ها*\n\n" +
            "هنوز هیچ ارائه‌ای ثبت نشده است.",
            HomeKeyboard());

        return;
    }

    var response =
        "📋 *برنامه ارائه‌ها*\n\n";

    for (var i = 0;
         i < presentations.Count;
         i++)
    {
        var presentation =
            presentations[i];

        var fullName =
            $"{presentation.FirstName} " +
            $"{presentation.LastName}"
                .Trim();

        if (string.IsNullOrWhiteSpace(
                fullName))
        {
            fullName = "بدون نام";
        }

        var date =
            PersianDateService
                .ToPersianDigits(
                    PersianDateService
                        .ToPersianDate(
                            presentation
                                .PresentationDate));

        response +=
            $"{ToPersianDigits((i + 1).ToString())}. " +
            $"{fullName}\n" +
            $"📌 {presentation.Topic.Title}\n" +
            $"📅 {date}\n\n";
    }

    await EditOrSendAsync(
        chatId,
        messageId,
        response,
        HomeKeyboard());
}

// =====================================================
// HELPERS
// =====================================================

async Task EditOrSendAsync(
    long chatId,
    long? messageId,
    string text,
    object markup)
{
    if (messageId.HasValue)
    {
        try
        {
            await bot.EditMessageTextAsync(
                chatId,
                messageId.Value,
                text,
                markup);

            return;
        }
        catch
        {
            // If editing fails, send a new message.
        }
    }

    await bot.SendMessageAsync(
        chatId,
        text,
        markup);
}

object HomeKeyboard()
{
    return new
    {
        inline_keyboard =
            new[]
            {
                new object[]
                {
                    Button(
                        "🏠 منوی اصلی",
                        "menu_home")
                }
            }
    };
}

object Button(
    string text,
    string callbackData)
{
    return new
    {
        text,
        callback_data = callbackData
    };
}

string Shorten(
    string text,
    int maxLength)
{
    if (text.Length <= maxLength)
    {
        return text;
    }

    return text[..(maxLength - 1)] + "…";
}

string NormalizeDigits(
    string text)
{
    return text
        .Replace('۰', '0')
        .Replace('۱', '1')
        .Replace('۲', '2')
        .Replace('۳', '3')
        .Replace('۴', '4')
        .Replace('۵', '5')
        .Replace('۶', '6')
        .Replace('۷', '7')
        .Replace('۸', '8')
        .Replace('۹', '9');
}

string ToPersianDigits(
    string text)
{
    return text
        .Replace('0', '۰')
        .Replace('1', '۱')
        .Replace('2', '۲')
        .Replace('3', '۳')
        .Replace('4', '۴')
        .Replace('5', '۵')
        .Replace('6', '۶')
        .Replace('7', '۷')
        .Replace('8', '۸')
        .Replace('9', '۹');
}

async Task TryDeleteAsync(
    long chatId,
    long messageId)
{
    await bot.DeleteMessageAsync(
        chatId,
        messageId);
}

async Task DeletePromptAsync(
    (long ChatId, long UserId) key)
{
    if (promptMessages.TryGetValue(
            key,
            out var promptId))
    {
        await TryDeleteAsync(
            key.ChatId,
            promptId);

        promptMessages.Remove(key);
    }
}

class Configuration
{
    public string Token { get; }

    public string ConnectionString { get; }

    public Configuration()
    {
        var path =
            Path.Combine(
                AppContext.BaseDirectory,
                "appsettings.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "appsettings.json was not found.",
                path);
        }

        var json =
            File.ReadAllText(path);

        using var document =
            JsonDocument.Parse(json);

        Token =
            document.RootElement
                .GetProperty("BaleBot")
                .GetProperty("Token")
                .GetString() ?? "";

        ConnectionString =
            document.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("DefaultConnection")
                .GetString() ?? "";
    }
}