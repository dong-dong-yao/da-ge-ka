using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace CheckInReminder;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new HhMmTimeOnlyConverter() },
    };

    private readonly string configPath;

    public SettingsService(string? configPath = null)
    {
        this.configPath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CheckInReminder",
            "config.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(configPath))
        {
            var defaults = AppSettings.CreateDefault();
            try
            {
                Save(defaults);
            }
            catch
            {
                // Defaults remain usable even when the local profile is read-only.
            }

            return defaults;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(configPath), JsonOptions);
            return settings is not null && TryValidate(settings, out _)
                ? settings
                : AppSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return AppSettings.CreateDefault();
        }
        catch (IOException)
        {
            return AppSettings.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!TryValidate(settings, out var message))
        {
            throw new ArgumentException(message, nameof(settings));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static bool TryValidate(AppSettings settings, out string message)
    {
        if (settings.MorningStart >= settings.MorningEnd)
        {
            message = "上午开始时间必须早于结束时间。";
            return false;
        }

        if (settings.MorningIntervalMinutes is < 1 or > 1440)
        {
            message = "上午提醒间隔必须是 1 到 1440 分钟的整数。";
            return false;
        }

        if (settings.EveningIntervalMinutes is < 1 or > 1440)
        {
            message = "晚上提醒间隔必须是 1 到 1440 分钟的整数。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private sealed class HhMmTimeOnlyConverter : JsonConverter<TimeOnly>
    {
        public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (value is null ||
                !TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                throw new JsonException("时间必须使用 HH:mm 格式。");
            }

            return time;
        }

        public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("HH:mm", CultureInfo.InvariantCulture));
    }
}
