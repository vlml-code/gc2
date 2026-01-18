namespace GameVote.Data;

public class GameListService
{
    private readonly string _filePath;

    public GameListService(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "Data", "games.txt");
    }

    public IReadOnlyList<string> GetGames()
    {
        if (!File.Exists(_filePath))
        {
            return new List<string>
            {
                "Rocket League",
                "Valorant",
                "Stardew Valley",
                "Minecraft"
            };
        }

        return File.ReadAllLines(_filePath)
            .Select(game => game.Trim())
            .Where(game => !string.IsNullOrWhiteSpace(game))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SaveGames(IReadOnlyList<string> games)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllLines(_filePath, games);
    }
}
