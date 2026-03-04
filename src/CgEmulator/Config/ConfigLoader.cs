using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CgEmulator.Config;

public static class ConfigLoader
{
    public static EmulatorConfig Load(string[] args)
    {
        var configPath = GetConfigPath(args) ?? "emulator.yaml";
        if (!File.Exists(configPath))
        {
            return new EmulatorConfig();
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var yaml = File.ReadAllText(configPath);
        return deserializer.Deserialize<EmulatorConfig>(yaml) ?? new EmulatorConfig();
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
