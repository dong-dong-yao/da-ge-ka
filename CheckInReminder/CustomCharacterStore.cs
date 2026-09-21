using System.Text.Json;
using System.Text.RegularExpressions;

namespace CheckInReminder;

public sealed class CustomCharacterStore
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CheckInReminder", "characters");
    public string Root { get; }
    public List<string> Errors { get; } = [];
    public CustomCharacterStore(string? root = null) => Root = Path.GetFullPath(root ?? DefaultRoot);

    public IReadOnlyList<ReminderCharacter> Load()
    {
        Errors.Clear();
        var result = new List<ReminderCharacter>();
        if (!Directory.Exists(Root)) return result;
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(Root, "user-*"))
            {
                try { result.Add(LoadPackage(folder)); }
                catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or System.Runtime.InteropServices.ExternalException or OutOfMemoryException)
                { Errors.Add($"{Path.GetFileName(folder)}：{e.Message}"); }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Errors.Add(e.Message); }
        return result;
    }

    internal static ReminderCharacter LoadPackage(string folder, bool staging = false)
    {
        RejectLink(folder);
        var file = Path.Combine(folder, "manifest.json");
        RejectLink(file);
        if (new FileInfo(file).Length > 65536) throw new InvalidDataException("角色说明文件过大。");
        var m = JsonSerializer.Deserialize<CustomCharacterManifest>(File.ReadAllText(file)) ?? throw new InvalidDataException("角色说明为空。");
        ValidateId(m.Id);
        if (!staging && Path.GetFileName(folder) != m.Id) throw new InvalidDataException("角色 ID 与目录不一致。");
        if (m.Version != 1 || string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 40 || m.Name.Any(char.IsControl)
            || !Enum.IsDefined(m.SourceEdge) || m.AllowedEdges is not { Length: > 0 and <= 4 }
            || m.AllowedEdges.Any(e => !Enum.IsDefined(e)) || m.Directions is null || m.Directions.Count > 4)
            throw new InvalidDataException("角色名称、版本或出现方向无效。");
        ValidateSequence(Path.Combine(folder, "notify"), m.FrameCount, m.DurationSeconds);
        var preview = Path.Combine(folder, "preview.png");
        if (File.Exists(preview)) ValidateImage(preview, 520);
        foreach (var (edge, info) in m.Directions)
        {
            if (!Enum.IsDefined(edge) || info is null) throw new InvalidDataException("独立方向无效。");
            ValidateSequence(Path.Combine(folder, "direction-" + edge), info.FrameCount, info.DurationSeconds);
        }
        if (m.HasPet)
        {
            var pet = Path.Combine(folder, "pet");
            RejectLink(pet);
            Size? size = null;
            foreach (var name in new[] { "idle", "left", "right", "center", "mouse" })
            {
                var path = Path.Combine(pet, name + ".png");
                if (!File.Exists(path) && (name == "center" || name == "mouse" && m.Mouse is null)) continue;
                var next = ValidateImage(path, 600);
                if (size.HasValue && next != size) throw new InvalidDataException("桌宠图片必须使用相同画布。");
                size = next;
            }
            m.Mouse?.Validate();
        }
        else if (m.Mouse is not null) throw new InvalidDataException("鼠标动作需要桌宠底图。");
        if (m.UsesTemplateMouse && (!m.HasPet || m.Mouse is not null)) throw new InvalidDataException("桌宠动作配置冲突。");
        if (m.UsesTemplateMouse) { using var verified = DesktopPetSpriteSet.LoadCustom(Path.Combine(folder, "pet")); }
        if (m.MouseCalibrations is not null)
        {
            var keys = new[] { "idle", "left", "center", "right" };
            if (!m.HasPet || m.UsesTemplateMouse || m.Mouse is not null || m.MouseCalibrations.Count != 4 || keys.Any(k => !m.MouseCalibrations.ContainsKey(k)))
                throw new InvalidDataException("鼠标校准姿势不完整或配置冲突。");
            foreach (var key in keys)
            {
                var spec = m.MouseCalibrations[key] ?? throw new InvalidDataException("鼠标校准信息为空。"); spec.Validate();
                foreach (var stem in new[] { key, "original-" + key, "arm-" + key, "mask-" + key })
                    if (ValidateImage(Path.Combine(folder, "pet", stem + ".png"), 600) != new Size(600, 448)) throw new InvalidDataException("鼠标校准图层画布不一致。");
            }
        }
        return new ReminderCharacter(m.Id, m.Name.Trim(), Path.Combine(folder, "notify"), TimeSpan.FromSeconds(m.DurationSeconds), false)
        {
            SourceEdge = m.SourceEdge, AllowedEdges = Array.AsReadOnly(m.AllowedEdges), HasPetAssets = m.HasPet,
            CustomPackagePath = folder, CustomManifest = m
        };
    }

    private static void ValidateSequence(string folder, int count, double seconds)
    {
        if (count is < 1 or > 180 || !double.IsFinite(seconds) || seconds is < 3 or > 15)
            throw new InvalidDataException("动画应为 3–15 秒，最多 180 帧。");
        RejectLink(folder);
        Size? size = null;
        for (var i = 0; i < count; i++)
        {
            var next = ValidateImage(Path.Combine(folder, $"frame_{i:0000}.png"), 520);
            if (size.HasValue && next != size) throw new InvalidDataException("动画画布不一致。");
            size = next;
        }
        if (Directory.EnumerateFiles(folder, "*.png").Count() != count) throw new InvalidDataException("动画帧数不一致。");
    }

    internal static Size ValidateImage(string path, int maxSize)
    {
        RejectLink(path);
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("图片文件过大。");
        using var image = Image.FromFile(path);
        if (image.Width < 1 || image.Height < 1 || image.Width > maxSize || image.Height > maxSize)
            throw new InvalidDataException("图片尺寸超出范围。");
        return image.Size;
    }

    internal static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("角色包不能包含目录链接。");
    }
    internal static void ValidateId(string id)
    {
        if (id is null || !Regex.IsMatch(id, "^user-[a-z0-9-]{1,64}$")) throw new InvalidDataException("无效的自定义角色 ID。");
    }
    public void Delete(string id)
    {
        ValidateId(id);
        var path = Path.GetFullPath(Path.Combine(Root, id));
        if (!path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("角色目录越界。");
        if (!Directory.Exists(path)) return;
        RejectLink(path);
        foreach (var item in Directory.EnumerateFileSystemEntries(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })) RejectLink(item);
        Directory.Delete(path, true);
    }
}
