using System.Globalization;

namespace PresentationManagerBot.Infrastructure.Services;

public static class PersianDateService
{
    private static readonly PersianCalendar Calendar = new();

    public static bool TryParse(
        string input,
        out DateTime gregorianDate)
    {
        gregorianDate = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized =
            NormalizeDigits(input.Trim());

        normalized =
            normalized.Replace(
                '-',
                '/');

        normalized =
            normalized.Replace(
                '.',
                '/');

        var parts =
            normalized.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(
                parts[0],
                out var year) ||
            !int.TryParse(
                parts[1],
                out var month) ||
            !int.TryParse(
                parts[2],
                out var day))
        {
            return false;
        }

        try
        {
            gregorianDate =
                Calendar.ToDateTime(
                    year,
                    month,
                    day,
                    0,
                    0,
                    0,
                    0);

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static string ToPersianDate(
        DateTime date)
    {
        var year =
            Calendar.GetYear(date);

        var month =
            Calendar.GetMonth(date);

        var day =
            Calendar.GetDayOfMonth(date);

        return
            $"{year:0000}/{month:00}/{day:00}";
    }

    public static string ToPersianDigits(
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

    private static string NormalizeDigits(
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
}