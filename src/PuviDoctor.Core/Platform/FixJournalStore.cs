using System.Text.Json;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Core.Platform;

public static class FixJournalStore
{
    private static string GetSessionsDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dir = Path.Combine(userProfile, ".hostfix", "sessions");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static async Task SaveSessionAsync(FixSessionJournal journal, CancellationToken cancellationToken = default)
    {
        var dir = GetSessionsDirectory();
        var filePath = Path.Combine(dir, $"{journal.SessionId}.json");
        var json = JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public static async Task<FixSessionJournal?> GetLatestSessionAsync(CancellationToken cancellationToken = default)
    {
        var dir = GetSessionsDirectory();
        var files = Directory.GetFiles(dir, "*.json");
        if (files.Length == 0)
        {
            return null;
        }

        var latestFile = files.OrderByDescending(f => f).First();
        var json = await File.ReadAllTextAsync(latestFile, cancellationToken);
        return JsonSerializer.Deserialize<FixSessionJournal>(json);
    }
}