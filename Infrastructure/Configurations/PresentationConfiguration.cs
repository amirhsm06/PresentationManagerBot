using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PresentationManagerBot.Domain.Entities;

namespace PresentationManagerBot.Infrastructure.Configurations;

public class PresentationConfiguration
    : IEntityTypeConfiguration<Presentation>
{
    public void Configure(
        EntityTypeBuilder<Presentation> builder)
    {
        builder.ToTable("Presentations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.PresentationDate)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.GroupId,
            x.UserId
        });

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}