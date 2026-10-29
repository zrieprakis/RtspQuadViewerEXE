using System.Text.Json;
using System.Text.Json.Serialization;

namespace RtspQuadViewer;

/// <summary>Camera URIs persisted to %APPDATA%\RtspQuadViewer\config.json as { "cams": [4 strings] }.</summary>
sealed class AppConfig
{
    public const int Slots = 4;

    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RtspQuadViewer");
    static readonly string FilePath = Path.Combine(Dir, "config.json");
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [JsonPropertyName("cams")]
    public string[] Cams { get; set; } = new string[Slots];

    public static AppConfig Load()
    {
        var config = new AppConfig();
        try
        {
            var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath));
            if (loaded?.Cams != null)
                for (var i = 0; i < Slots && i < loaded.Cams.Length; i++)
                    config.Cams[i] = loaded.Cams[i]?.Trim() ?? "";
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Missing or unreadable config: start with empty slots.
        }
        for (var i = 0; i < Slots; i++) config.Cams[i] ??= "";
        return config;
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>Returns an error message, or null if the URI is empty or a usable rtsp(s) URI.</summary>
    public static string? Validate(string uri)
    {
        if (uri.Length == 0) return null;
        if (uri.Any(char.IsWhiteSpace)) return "must not contain spaces";
        // Prefix check only: camera URIs often contain characters System.Uri rejects but libvlc accepts.
        if (!uri.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) && !uri.StartsWith("rtsps://", StringComparison.OrdinalIgnoreCase))
            return "must start with rtsp:// or rtsps://";
        return null;
    }
}
