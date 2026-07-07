using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Tests;

// Exercises the task CRUD we wrote: auth gating, per-user isolation, the
// create/read/update/delete lifecycle, and the validation-error body shape.
public class TaskEndpointsTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public TaskEndpointsTests(TestAppFactory factory) => _factory = factory;

    // Registers + logs in a fresh user, returning a client whose cookie container
    // now holds that user's session.
    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = _factory.CreateClient();
        var creds = new
        {
            email = $"tasks-{Guid.NewGuid():N}@example.com",
            password = "P@ssw0rd!",
        };
        await client.PostAsJsonAsync("/account/register", creds);
        await client.PostAsJsonAsync("/account/login", creds);
        return client;
    }

    [Fact]
    public async Task List_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ThenList_ReturnsTheTask()
    {
        var client = await SignedInClientAsync();

        var created = await client.PostAsJsonAsync("/api/tasks", new { title = "Buy milk" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<TaskDto>();
        Assert.Equal("Buy milk", body!.Title);
        Assert.False(body.IsCompleted);

        var list = await client.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.Single(list!);
        Assert.Equal(body.Id, list![0].Id);
    }

    [Fact]
    public async Task Create_WithBlankTitle_Returns400WithErrorsBody()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks", new { title = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Http.HttpValidationProblemDetails>();
        Assert.Contains("Title", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Update_ChangesTitleAndCompletion()
    {
        var client = await SignedInClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/tasks", new { title = "Draft" }))
            .Content.ReadFromJsonAsync<TaskDto>();

        var updated = await client.PutAsJsonAsync($"/api/tasks/{created!.Id}",
            new { title = "Final", isCompleted = true });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var body = await updated.Content.ReadFromJsonAsync<TaskDto>();
        Assert.Equal("Final", body!.Title);
        Assert.True(body.IsCompleted);
    }

    [Fact]
    public async Task Update_TitleOnly_LeavesCompletionUnchanged()
    {
        var client = await SignedInClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/tasks", new { title = "Task" }))
            .Content.ReadFromJsonAsync<TaskDto>();
        await client.PutAsJsonAsync($"/api/tasks/{created!.Id}", new { title = "Task", isCompleted = true });

        // Rename without sending isCompleted — it should stay true.
        var renamed = await (await client.PutAsJsonAsync($"/api/tasks/{created.Id}", new { title = "Renamed" }))
            .Content.ReadFromJsonAsync<TaskDto>();

        Assert.Equal("Renamed", renamed!.Title);
        Assert.True(renamed.IsCompleted);
    }

    [Fact]
    public async Task Delete_RemovesTheTask()
    {
        var client = await SignedInClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/tasks", new { title = "Temp" }))
            .Content.ReadFromJsonAsync<TaskDto>();

        var deleted = await client.DeleteAsync($"/api/tasks/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var list = await client.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.Empty(list!);
    }

    // The core isolation guarantee: one user can never see or touch another's tasks.
    [Fact]
    public async Task Tasks_AreScopedToTheOwningUser()
    {
        var alice = await SignedInClientAsync();
        var bob = await SignedInClientAsync();

        var aliceTask = await (await alice.PostAsJsonAsync("/api/tasks", new { title = "Alice's task" }))
            .Content.ReadFromJsonAsync<TaskDto>();

        // Bob's list is empty and Bob can't fetch, edit, or delete Alice's task.
        Assert.Empty((await bob.GetFromJsonAsync<List<TaskDto>>("/api/tasks"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/tasks/{aliceTask!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await bob.PutAsJsonAsync($"/api/tasks/{aliceTask.Id}", new { title = "hijacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/tasks/{aliceTask.Id}")).StatusCode);
    }

    [Fact]
    public async Task Reorder_ChangesListOrder()
    {
        var client = await SignedInClientAsync();
        var a = await CreateTask(client, "A");
        var b = await CreateTask(client, "B");
        var c = await CreateTask(client, "C");

        // Tasks list in creation order to start.
        Assert.Equal(new[] { a.Id, b.Id, c.Id }, await OrderedIds(client));

        var response = await client.PutAsJsonAsync("/api/tasks/order",
            new { ids = new[] { c.Id, a.Id, b.Id } });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(new[] { c.Id, a.Id, b.Id }, await OrderedIds(client));
    }

    // A submitted list that isn't exactly the user's current tasks (here, one is
    // missing) is rejected and the existing order is left untouched.
    [Fact]
    public async Task Reorder_WithStaleIdSet_Returns400AndLeavesOrder()
    {
        var client = await SignedInClientAsync();
        var a = await CreateTask(client, "A");
        var b = await CreateTask(client, "B");

        var response = await client.PutAsJsonAsync("/api/tasks/order", new { ids = new[] { b.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(new[] { a.Id, b.Id }, await OrderedIds(client));
    }

    // Reorder is scoped like everything else: a list containing someone else's task
    // id is rejected rather than reordering (or exposing) another user's tasks.
    [Fact]
    public async Task Reorder_CannotIncludeAnotherUsersTask()
    {
        var alice = await SignedInClientAsync();
        var bob = await SignedInClientAsync();
        var aliceTask = await CreateTask(alice, "Alice's task");
        var bobTask = await CreateTask(bob, "Bob's task");

        var response = await bob.PutAsJsonAsync("/api/tasks/order",
            new { ids = new[] { bobTask.Id, aliceTask.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<TaskDto> CreateTask(HttpClient client, string title) =>
        (await (await client.PostAsJsonAsync("/api/tasks", new { title }))
            .Content.ReadFromJsonAsync<TaskDto>())!;

    private static async Task<int[]> OrderedIds(HttpClient client) =>
        (await client.GetFromJsonAsync<List<TaskDto>>("/api/tasks"))!.Select(t => t.Id).ToArray();

    private record TaskDto(int Id, string Title, bool IsCompleted, int Position, DateTimeOffset CreatedAt);
}
