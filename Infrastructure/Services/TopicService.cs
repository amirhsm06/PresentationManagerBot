using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PresentationManagerBot.Domain.Entities;
using PresentationManagerBot.Infrastructure.Data;

namespace PresentationManagerBot.Infrastructure.Services;

public class TopicService
{
    private readonly AppDbContext _db;

    public TopicService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(List<string> Added, List<string> Duplicates)>
        AddTopicsAsync(
            long groupId,
            string text)
    {
        var topics = text
            .Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries)
            .Select(RemoveLeadingNumber)
            .Select(x => x.Trim())
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .ToList();

        var existingTopics =
            await _db.Topics
                .Where(x =>
                    x.GroupId == groupId &&
                    x.IsAvailable)
                .Select(x => x.Title)
                .ToListAsync();

        var existingNormalized =
            existingTopics
                .Select(Normalize)
                .ToHashSet();

        var batchNormalized =
            new HashSet<string>();

        var added =
            new List<string>();

        var duplicates =
            new List<string>();

        foreach (var title in topics)
        {
            var normalized =
                Normalize(title);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!batchNormalized.Add(normalized) ||
                existingNormalized.Contains(normalized))
            {
                duplicates.Add(title);
                continue;
            }

            var topic = new Topic
            {
                GroupId = groupId,
                Title = title,
                CreatedAt = DateTime.UtcNow,
                IsAvailable = true
            };

            _db.Topics.Add(topic);

            added.Add(title);

            existingNormalized.Add(normalized);
        }

        await _db.SaveChangesAsync();

        return (added, duplicates);
    }

    public async Task<List<Topic>>
        GetAvailableTopicsAsync(
            long groupId)
    {
        return await _db.Topics
            .AsNoTracking()
            .Where(x =>
                x.GroupId == groupId &&
                x.IsAvailable)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Topic?>
        GetAvailableTopicAsync(
            long groupId,
            long topicId)
    {
        return await _db.Topics
            .FirstOrDefaultAsync(x =>
                x.Id == topicId &&
                x.GroupId == groupId &&
                x.IsAvailable);
    }

    public async Task<bool>
        RemoveTopicAsync(
            long groupId,
            long topicId)
    {
        var topic =
            await _db.Topics
                .FirstOrDefaultAsync(x =>
                    x.Id == topicId &&
                    x.GroupId == groupId &&
                    x.IsAvailable);

        if (topic == null)
        {
            return false;
        }

        _db.Topics.Remove(topic);

        await _db.SaveChangesAsync();

        return true;
    }

    private static string RemoveLeadingNumber(
        string text)
    {
        /*
         * Removes:
         *
         * 1. Topic
         * 2) Topic
         * 3- Topic
         * 4: Topic
         * ۵. Topic
         * ۶) Topic
         *
         * But leaves topics without numbering untouched.
         */

        return Regex.Replace(
            text,
            @"^\s*[\d۰-۹]+\s*[\.\)\-:]\s*",
            "");
    }

    private static string Normalize(
        string text)
    {
        return text
            .Trim()
            .ToLowerInvariant();
    }
}