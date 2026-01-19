using System.Collections.Concurrent;

namespace GameVote.Data;

public class RoomStore
{
    private readonly ConcurrentDictionary<string, RoomState> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();
    private const string RandomChoice = "Random";

    public RoomState CreateRoom()
    {
        var code = GenerateCode();
        var room = new RoomState(code);
        _rooms[code] = room;
        return room;
    }

    public bool TryGetRoom(string code, out RoomState room)
    {
        return _rooms.TryGetValue(code, out room!);
    }

    public Participant AddParticipant(RoomState room, string name)
    {
        lock (room.SyncRoot)
        {
            var participant = new Participant
            {
                Name = name
            };

            room.Participants[participant.Id] = participant;
            return participant;
        }
    }

    public bool AddVote(RoomState room, string participantId, string? choice)
    {
        lock (room.SyncRoot)
        {
            if (!room.Participants.TryGetValue(participantId, out var participant))
            {
                return false;
            }

            if (participant.HasVoted)
            {
                return false;
            }

            var finalChoice = string.IsNullOrWhiteSpace(choice) ? RandomChoice : choice.Trim();
            room.Votes[participantId] = finalChoice;
            participant.HasVoted = true;
            return true;
        }
    }

    public string? FinalizeVote(RoomState room, IReadOnlyList<string> games)
    {
        lock (room.SyncRoot)
        {
            if (room.Result is not null)
            {
                return room.Result;
            }

            if (room.Participants.Count == 0 || room.Participants.Values.Any(participant => !participant.HasVoted))
            {
                return null;
            }

            var voteGroups = room.Votes
                .GroupBy(vote => vote.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => new { Choice = group.Key, Count = group.Count() })
                .ToList();

            if (voteGroups.Count == 0)
            {
                return null;
            }

            var maxVotes = voteGroups.Max(group => group.Count);
            var topChoices = voteGroups
                .Where(group => group.Count == maxVotes)
                .Select(group => group.Choice)
                .ToList();

            var selectedChoice = topChoices[_random.Next(topChoices.Count)];
            if (string.Equals(selectedChoice, RandomChoice, StringComparison.OrdinalIgnoreCase))
            {
                if (games.Count > 0)
                {
                    selectedChoice = games[_random.Next(games.Count)];
                }
            }

            room.Result = selectedChoice;
            return room.Result;
        }
    }

    private string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        while (true)
        {
            var code = new string(Enumerable.Range(0, 6).Select(_ => chars[_random.Next(chars.Length)]).ToArray());
            if (!_rooms.ContainsKey(code))
            {
                return code;
            }
        }
    }
}
