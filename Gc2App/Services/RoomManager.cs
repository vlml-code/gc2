namespace Gc2App.Services;

public class RoomManager
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string>? RoomUpdated;

    public string CreateRoom(IReadOnlyList<string> games)
    {
        var cleanedGames = games
            .Select(game => game.Trim())
            .Where(game => !string.IsNullOrWhiteSpace(game))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanedGames.Count == 0)
        {
            cleanedGames.Add("Mystery Game");
        }

        lock (_lock)
        {
            var code = GenerateCode();
            var room = new Room(code, cleanedGames);
            _rooms[code] = room;
            Notify(code);
            return code;
        }
    }

    public RoomSnapshot? GetRoom(string code)
    {
        lock (_lock)
        {
            return _rooms.TryGetValue(code, out var room) ? room.ToSnapshot() : null;
        }
    }

    public void JoinRoom(string code, Guid participantId, string displayName)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room))
            {
                return;
            }

            if (room.Participants.TryGetValue(participantId, out var participant))
            {
                participant.Name = displayName;
            }
            else
            {
                room.Participants[participantId] = new Participant(participantId, displayName);
            }

            Notify(code);
        }
    }

    public void CastVote(string code, Guid participantId, string option)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room))
            {
                return;
            }

            if (!room.IsValidOption(option))
            {
                return;
            }

            if (!room.Participants.TryGetValue(participantId, out var participant))
            {
                return;
            }

            if (participant.HasVoted)
            {
                return;
            }

            participant.Vote = option;
            participant.HasVoted = true;
            Notify(code);
        }
    }

    public string? StartRoom(string code)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(code, out var room))
            {
                return null;
            }

            if (room.Started)
            {
                return room.SelectedGame;
            }

            if (!room.AllVoted)
            {
                return null;
            }

            var voteGroups = room.Participants.Values
                .Where(participant => participant.HasVoted && !string.IsNullOrWhiteSpace(participant.Vote))
                .GroupBy(participant => participant.Vote!)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            if (voteGroups.Count == 0)
            {
                return null;
            }

            var maxVotes = voteGroups.Values.Max();
            var topOptions = voteGroups
                .Where(kvp => kvp.Value == maxVotes)
                .Select(kvp => kvp.Key)
                .ToList();

            var selectedOption = topOptions[Random.Shared.Next(topOptions.Count)];
            var selectedGame = string.Equals(selectedOption, Room.RandomVote, StringComparison.OrdinalIgnoreCase)
                ? room.Games[Random.Shared.Next(room.Games.Count)]
                : selectedOption;

            room.Started = true;
            room.SelectedGame = selectedGame;
            Notify(code);

            return selectedGame;
        }
    }

    private void Notify(string code)
    {
        RoomUpdated?.Invoke(code);
    }

    private string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;

        do
        {
            var buffer = new char[6];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = chars[Random.Shared.Next(chars.Length)];
            }

            code = new string(buffer);
        } while (_rooms.ContainsKey(code));

        return code;
    }

    private sealed class Room
    {
        public const string RandomVote = "Random";

        public Room(string code, List<string> games)
        {
            Code = code;
            Games = games;
        }

        public string Code { get; }
        public List<string> Games { get; }
        public Dictionary<Guid, Participant> Participants { get; } = new();
        public bool Started { get; set; }
        public string? SelectedGame { get; set; }

        public bool AllVoted => Participants.Count > 0 && Participants.Values.All(participant => participant.HasVoted);

        public bool IsValidOption(string option)
        {
            return option.Equals(RandomVote, StringComparison.OrdinalIgnoreCase)
                || Games.Any(game => game.Equals(option, StringComparison.OrdinalIgnoreCase));
        }

        public RoomSnapshot ToSnapshot()
        {
            var participants = Participants.Values
                .OrderBy(participant => participant.Name, StringComparer.OrdinalIgnoreCase)
                .Select(participant => new ParticipantSnapshot(
                    participant.Id,
                    participant.Name,
                    participant.HasVoted,
                    participant.Vote,
                    participant.HasVoted ? "Already Voted" : "Waiting"))
                .ToList();

            var voteOptions = Games.Concat([RandomVote]).ToList();

            return new RoomSnapshot(
                Code,
                Games.ToList(),
                voteOptions,
                participants,
                Started,
                SelectedGame,
                AllVoted);
        }
    }

    private sealed class Participant
    {
        public Participant(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public Guid Id { get; }
        public string Name { get; set; }
        public bool HasVoted { get; set; }
        public string? Vote { get; set; }
    }
}

public record RoomSnapshot(
    string Code,
    IReadOnlyList<string> Games,
    IReadOnlyList<string> VoteOptions,
    IReadOnlyList<ParticipantSnapshot> Participants,
    bool Started,
    string? SelectedGame,
    bool AllVoted);

public record ParticipantSnapshot(
    Guid Id,
    string Name,
    bool HasVoted,
    string? Vote,
    string Status);
