using Microsoft.EntityFrameworkCore;
using PresentationManagerBot.Domain.Entities;
using PresentationManagerBot.Infrastructure.Data;

namespace PresentationManagerBot.Infrastructure.Services;

public class PresentationService
{
    private readonly AppDbContext _db;

    public PresentationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Presentation> CreateAsync(
        long groupId,
        Topic topic,
        long userId,
        string firstName,
        string lastName,
        DateTime presentationDate)
    {
        var presentation = new Presentation
        {
            GroupId = groupId,
            TopicId = topic.Id,
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            PresentationDate = presentationDate,
            CreatedAt = DateTime.UtcNow
        };

        _db.Presentations.Add(presentation);

        topic.IsAvailable = false;

        await _db.SaveChangesAsync();

        return presentation;
    }

    public async Task<List<Presentation>> GetByGroupAsync(
        long groupId)
    {
        return await _db.Presentations
            .AsNoTracking()
            .Include(x => x.Topic)
            .Where(x => x.GroupId == groupId)
            .OrderBy(x => x.PresentationDate)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }
}