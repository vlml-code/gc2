namespace Gc2App.Services;

public class ParticipantSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public string? Name { get; set; }
}
