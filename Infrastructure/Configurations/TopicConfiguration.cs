using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PresentationManagerBot.Domain.Entities;

namespace PresentationManagerBot.Infrastructure.Configurations;

public class TopicConfiguration
    : IEntityTypeConfiguration<Topic>
{
    public void Configure(
        EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("Topics");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.IsAvailable)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.GroupId,
            x.Title
        });
    }
}