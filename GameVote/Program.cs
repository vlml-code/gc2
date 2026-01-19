using GameVote.Data;
using GameVote.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<GameListService>();
builder.Services.AddSingleton<RoomStore>();
builder.Services.AddSingleton<RoomHubNotifier>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<RoomHub>("/roomHub");

app.MapGet("/api/games", (GameListService gameListService) =>
{
    return Results.Ok(gameListService.GetGames());
});

app.MapPost("/api/games", async (HttpContext context, GameListService gameListService) =>
{
    var games = await context.Request.ReadFromJsonAsync<List<string>>() ?? new List<string>();
    var sanitized = games
        .Select(game => game.Trim())
        .Where(game => !string.IsNullOrWhiteSpace(game))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    gameListService.SaveGames(sanitized);
    return Results.Ok(sanitized);
});

app.MapPost("/api/rooms", (RoomStore roomStore) =>
{
    var room = roomStore.CreateRoom();
    return Results.Ok(new { room.Code });
});

app.MapGet("/api/rooms/{code}", (string code, RoomStore roomStore) =>
{
    if (!roomStore.TryGetRoom(code, out var room))
    {
        return Results.NotFound();
    }

    return Results.Ok(RoomDto.From(room));
});

app.MapPost("/api/rooms/{code}/join", async (
    string code,
    HttpContext context,
    RoomStore roomStore,
    RoomHubNotifier notifier) =>
{
    var request = await context.Request.ReadFromJsonAsync<JoinRequest>();
    if (request is null || string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest();
    }

    if (!roomStore.TryGetRoom(code, out var room))
    {
        return Results.NotFound();
    }

    var participant = roomStore.AddParticipant(room, request.Name.Trim());
    await notifier.NotifyRoomUpdated(code, room);
    return Results.Ok(new { participant.Id, participant.Name });
});

app.MapPost("/api/rooms/{code}/vote", async (
    string code,
    HttpContext context,
    RoomStore roomStore,
    RoomHubNotifier notifier) =>
{
    var request = await context.Request.ReadFromJsonAsync<VoteRequest>();
    if (request is null || string.IsNullOrWhiteSpace(request.ParticipantId))
    {
        return Results.BadRequest();
    }

    if (!roomStore.TryGetRoom(code, out var room))
    {
        return Results.NotFound();
    }

    var success = roomStore.AddVote(room, request.ParticipantId, request.Choice);
    if (!success)
    {
        return Results.BadRequest();
    }

    await notifier.NotifyRoomUpdated(code, room);
    return Results.Ok(RoomDto.From(room));
});

app.MapPost("/api/rooms/{code}/start", async (
    string code,
    RoomStore roomStore,
    GameListService gameListService,
    RoomHubNotifier notifier) =>
{
    if (!roomStore.TryGetRoom(code, out var room))
    {
        return Results.NotFound();
    }

    var result = roomStore.FinalizeVote(room, gameListService.GetGames());
    if (result is null)
    {
        return Results.BadRequest();
    }

    await notifier.NotifyRoomUpdated(code, room);
    return Results.Ok(new { Result = result });
});

app.Run();

record JoinRequest(string Name);
record VoteRequest(string ParticipantId, string? Choice);
