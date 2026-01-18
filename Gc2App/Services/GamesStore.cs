using System.Text;

namespace Gc2App.Services;

public class GamesStore
{
    private static readonly string[] DefaultGames =
    [
        "Rocket League",
        "Mario Kart",
        "Overwatch",
        "Minecraft",
        "Stardew Valley",
        "Apex Legends"
    ];

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<string> _cache = [];

    public GamesStore(IWebHostEnvironment environment)
    {
        _filePath = Path.Combine(environment.ContentRootPath, "App_Data", "games.txt");
    }

    public async Task<IReadOnlyList<string>> GetGamesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache.Count == 0)
            {
                _cache = await LoadFromFileAsync();
            }

            return _cache.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveGamesAsync(IReadOnlyList<string> games)
    {
        var cleaned = games
            .Select(game => game.Trim())
            .Where(game => !string.IsNullOrWhiteSpace(game))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count == 0)
        {
            cleaned = DefaultGames.ToList();
        }

        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await File.WriteAllLinesAsync(_filePath, cleaned, Encoding.UTF8);
            _cache = cleaned;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<string>> LoadFromFileAsync()
    {
        if (!File.Exists(_filePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await File.WriteAllLinesAsync(_filePath, DefaultGames, Encoding.UTF8);
            return DefaultGames.ToList();
        }

        var lines = await File.ReadAllLinesAsync(_filePath, Encoding.UTF8);
        var games = lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return games.Count == 0 ? DefaultGames.ToList() : games;
    }
}
