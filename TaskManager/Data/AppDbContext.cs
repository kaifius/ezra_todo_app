using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TaskManager.Data;

// IdentityUserContext wires up the Identity user tables (users, claims, logins,
// tokens) but omits the role tables — we don't use roles. App-specific entities
// (e.g. tasks) get added here as the app grows.
public class AppDbContext : IdentityUserContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TaskItem>(task =>
        {
            task.ToTable("tasks");
            task.Property(t => t.Title).IsRequired().HasMaxLength(500);
            // Delete a user's tasks with the user. Also indexes the FK, which is
            // the column every task query filters on.
            task.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
