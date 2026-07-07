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

    private record TaskDto(int Id, string Title, bool IsCompleted, int Position, DateTimeOffset CreatedAt);
}
