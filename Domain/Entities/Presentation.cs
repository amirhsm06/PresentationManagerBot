namespace PresentationManagerBot.Domain.Entities;

public class Presentation
{
    public long Id { get; set; }

    public long GroupId { get; set; }

    public long TopicId { get; set; }

    public long UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateTime PresentationDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public Topic Topic { get; set; } = null!;
}