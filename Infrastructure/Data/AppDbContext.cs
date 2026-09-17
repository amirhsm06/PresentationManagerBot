using Microsoft.EntityFrameworkCore;
using PresentationManagerBot.Domain.Entities;

namespace PresentationManagerBot.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<Presentation> Presentations =>
        Set<Presentation>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}