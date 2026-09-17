namespace PresentationManagerBot.Domain.Entities;

public class Topic
{
    public long Id { get; set; }

    public long GroupId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsAvailable { get; set; } = true;
}