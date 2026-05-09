using CodexBar.Core;

namespace CodexBar.Runtime;

public static class CodexRuntimePathRefresher
{
    public static AppConfig RefreshCodexDesktopPath(
        AppConfig config,
        CodexDesktopLocator? desktopLocator = null)
    {
        desktopLocator ??= new CodexDesktopLocator();
        var detectedPath = desktopLocator.Locate(config.Settings.CodexDesktopPath);
        if (string.IsNullOrWhiteSpace(detectedPath) ||
            PathsEqual(config.Settings.CodexDesktopPath, detectedPath))
        {
            return config;
        }

        return config with
        {
            Settings = config.Settings with
            {
                CodexDesktopPath = detectedPath
            }
        };
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right);
        }

        return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return path.Trim();
        }
    }
}
