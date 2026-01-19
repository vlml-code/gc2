using GameVote.Data;
using Microsoft.AspNetCore.SignalR;

namespace GameVote.Hubs;

public class RoomHub(RoomStore roomStore) : Hub
{
    public async Task JoinRoom(string code)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, code);

        if (roomStore.TryGetRoom(code, out var room))
        {
            await Clients.Caller.SendAsync("RoomUpdated", RoomDto.From(room));
        }
    }
}

public class RoomHubNotifier(IHubContext<RoomHub> hubContext)
{
    public Task NotifyRoomUpdated(string code, RoomState room)
    {
        return hubContext.Clients.Group(code).SendAsync("RoomUpdated", RoomDto.From(room));
    }
}
