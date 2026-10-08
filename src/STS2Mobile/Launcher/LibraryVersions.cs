using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace STS2Mobile.Launcher;

public sealed record LibraryVersion(string Version, string ModuleId);

public sealed record GameReleaseVersion(string Version, string Commit);

public static class LibraryVersions
{
    public static LibraryVersion ReadLoaded(string name)
    {
        var assembly = AppDomain
            .CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == name);
        if (assembly == null)
            return null;
        var version =
            assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();
        return new LibraryVersion(version, assembly.ManifestModule.ModuleVersionId.ToString());
    }

    public static GameReleaseVersion ReadGameRelease(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (
                root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(version.GetString())
            )
                return null;
            string commit =
                root.TryGetProperty("commit", out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            return new GameReleaseVersion(version.GetString(), commit);
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
