namespace GameVote.Data;

public class RoomState
{
    public string Code { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public Dictionary<string, Participant> Participants { get; } = new();
    public Dictionary<string, string> Votes { get; } = new();
    public string? Result { get; set; }
    public object SyncRoot { get; } = new();

    public RoomState(string code)
    {
        Code = code;
    }
}

public class Participant
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public bool HasVoted { get; set; }
}

public record RoomDto(
    string Code,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyDictionary<string, int> VoteCounts,
    bool AllVoted,
    string? Result)
{
    public static RoomDto From(RoomState room)
    {
        var participants = room.Participants.Values
            .OrderBy(participant => participant.Name, StringComparer.OrdinalIgnoreCase)
            .Select(participant => new ParticipantDto(participant.Id, participant.Name, participant.HasVoted))
            .ToList();

        var voteCounts = room.Votes
            .GroupBy(vote => vote.Value)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var allVoted = participants.Count > 0 && participants.All(participant => participant.HasVoted);

        return new RoomDto(room.Code, participants, voteCounts, allVoted, room.Result);
    }
}

public record ParticipantDto(string Id, string Name, bool HasVoted);
