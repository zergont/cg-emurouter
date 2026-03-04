using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CgEmulator.Config;

public static class ConfigLoader
{
    private const string DefaultConfigName = "emulator.yaml";

    public static EmulatorConfig Load(string[] args)
    {
        var requestedPath = GetConfigPath(args);
        var configPath = ResolveConfigPath(requestedPath);
        if (configPath is null)
        {
            if (!string.IsNullOrWhiteSpace(requestedPath))
            {
                throw new FileNotFoundException($"Config file was not found: '{requestedPath}'");
            }

            return new EmulatorConfig();
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var yaml = File.ReadAllText(configPath);
        return deserializer.Deserialize<EmulatorConfig>(yaml) ?? new EmulatorConfig();
    }

    private static string? ResolveConfigPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            if (Path.IsPathRooted(requestedPath))
            {
                var absoluteRequested = Path.GetFullPath(requestedPath);
                return File.Exists(absoluteRequested) ? absoluteRequested : null;
            }

            return FindByAscendingDirectories(requestedPath, Directory.GetCurrentDirectory())
                ?? FindByAscendingDirectories(requestedPath, AppContext.BaseDirectory);
        }

        return FindByAscendingDirectories(DefaultConfigName, Directory.GetCurrentDirectory())
            ?? FindByAscendingDirectories(DefaultConfigName, AppContext.BaseDirectory);
    }

    private static string? FindByAscendingDirectories(string fileName, string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? GetConfigPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--config", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
