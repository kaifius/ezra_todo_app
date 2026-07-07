namespace TaskManager.Data;

// A single todo item, owned by one user. Stored in the `tasks` table (configured
// in AppDbContext). Named TaskItem rather than Task to avoid colliding with
// System.Threading.Tasks.Task, which ImplicitUsings pulls into every file.
// Position gives the list a stable, user-controllable order so the frontend can
// implement drag-to-reorder by updating it.
public class TaskItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    // Sort key within a user's list. Lower comes first.
    public int Position { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // FK to the owning user. Every query is scoped by this so users only ever
    // see their own tasks.
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }
}
