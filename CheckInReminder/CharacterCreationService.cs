using System.Drawing.Imaging;
using System.Text.Json;

namespace CheckInReminder;

internal sealed class CharacterCreationRequest
{
    public string Name { get; set; } = "";
    public string VideoPath { get; set; } = "";
    public BackgroundRemoval Background { get; set; }
    public ScreenEdge SourceEdge { get; set; } = ScreenEdge.Right;
    public ScreenEdge[] AllowedEdges { get; set; } = [ScreenEdge.Right];
    public Dictionary<ScreenEdge, string> Directions { get; set; } = [];
    public Dictionary<string, string> PetImages { get; set; } = [];
    public BackgroundRemoval PetBackground { get; set; } = BackgroundRemoval.Preserve;
    public bool StrictPetPng { get; set; }
    public bool UseTemplateMouse { get; set; }
    public bool AllowMouseCalibration { get; set; }
    public CustomMouseRig? Mouse { get; set; }
    public RectangleF MousePlacement { get; set; } = new(0, 0, 1, 1);
}

internal sealed class PreparedCharacter : IDisposable
{
    private string? staging;
    private readonly CustomCharacterStore store;
    public ReminderCharacter Character { get; private set; }
    public PreparedCharacter(string staging, CustomCharacterStore store)
    { this.staging = staging; this.store = store; Character = CustomCharacterStore.LoadPackage(staging, staging: true); }
    public ReminderCharacter Commit()
    {
        if (staging is null) throw new InvalidOperationException("素材已经保存或取消。");
        var target = Path.Combine(store.Root, Character.Id);
        Directory.Move(staging, target);
        staging = null;
        return Character with { CustomPackagePath = target, SequenceName = Path.Combine(target, "notify") };
    }
    internal void ApplyMouseCalibration(IReadOnlyDictionary<string, MouseCalibrationEdit> edits)
    {
        if (staging is null) throw new InvalidOperationException("角色已保存。");
        var keys = new[] { "idle", "left", "center", "right" };
        if (edits.Count != 4 || keys.Any(k => !edits.ContainsKey(k))) throw new InvalidDataException("请确认四个姿势的鼠标效果。");
        var suffix = Guid.NewGuid().ToString("N");
        var pet = Path.Combine(staging, "pet"); var replacement = Path.Combine(staging, "pet-calibrating-" + suffix); var backup = Path.Combine(staging, "pet-backup-" + suffix);
        var manifestPath = Path.Combine(staging, "manifest.json"); var previous = File.ReadAllText(manifestPath);
        Directory.CreateDirectory(replacement);
        try
        {
            var manifest = JsonSerializer.Deserialize<CustomCharacterManifest>(previous)!;
            manifest.UsesTemplateMouse = false; manifest.Mouse = null; manifest.MouseCalibrations = [];
            foreach (var key in keys)
            {
                var original = Path.Combine(pet, "original-" + key + ".png");
                using var source = CharacterMediaProcessor.ReadBitmap(File.Exists(original) ? original : Path.Combine(pet, key + ".png"));
                using var layers = CalibratedMouseLayers.Create(source, edits[key].Mask, edits[key].Spec);
                layers.Save(replacement, key); edits[key].Mask.Save(Path.Combine(replacement, "mask-" + key + ".png"), ImageFormat.Png);
                manifest.MouseCalibrations[key] = edits[key].Spec;
            }
            Directory.Move(pet, backup); Directory.Move(replacement, pet);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, CustomCharacterStore.JsonOptions));
            Character = CustomCharacterStore.LoadPackage(staging, staging: true);
        }
        catch
        {
            if (Directory.Exists(backup)) { if (Directory.Exists(pet)) Directory.Delete(pet, true); Directory.Move(backup, pet); }
            File.WriteAllText(manifestPath, previous); throw;
        }
        finally { if (Directory.Exists(replacement)) Directory.Delete(replacement, true); }
        // The replacement has been validated and committed. Failure to remove a
        // possibly partially deleted backup must never roll back the live data.
        try { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    public void Dispose()
    {
        if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true);
        staging = null;
    }
}

internal sealed class CharacterCreationService(CustomCharacterStore store)
{
    public async Task<PreparedCharacter> PrepareAsync(CharacterCreationRequest request, IProgress<string>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 40 || request.Name.Any(char.IsControl))
            throw new InvalidDataException("请输入 1–40 字的角色名称。");
        if (request.AllowedEdges.Length == 0) throw new InvalidDataException("至少选择一个出现方向。");
        var hasPet = request.PetImages.Count > 0;
        if (hasPet && new[] { "idle", "left", "right" }.Any(k => !request.PetImages.ContainsKey(k)))
            throw new InvalidDataException(request.StrictPetPng
                ? "请上传抬手、按左、按中、按右四张完整图片，或清空图片后跳过桌面互动。"
                : "桌面互动需要待机、按左和按右三张图；也可以全部不传。");
        if (request.PetImages.ContainsKey("mouse") != (request.Mouse is not null)) throw new InvalidDataException("鼠标手臂素材需要完成连接位置设置。");
        request.Mouse?.Validate();
        if (hasPet && request.StrictPetPng && !request.PetImages.ContainsKey("center"))
            throw new InvalidDataException("请上传抬手、按左、按中、按右四张完整图片，或清空图片后跳过桌面互动。");
        Size? canvas = null;
        foreach (var (_, path) in request.PetImages.Where(x => x.Key != "mouse"))
        {
            if (request.StrictPetPng) CharacterMediaProcessor.ValidateTransparentPng(path);
            var size = await CharacterMediaProcessor.ImageSizeAsync(path, token);
            if (canvas.HasValue && canvas != size) throw new InvalidDataException("桌宠姿势图必须使用相同画布尺寸，请保持身体和键盘位置一致。");
            canvas = size;
        }
        Directory.CreateDirectory(store.Root);
        var id = "user-" + Guid.NewGuid().ToString("N");
        var staging = Path.Combine(store.Root, ".creating-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            progress?.Report("正在处理提醒动画……");
            var animation = await CharacterMediaProcessor.VideoAsync(request.VideoPath, Path.Combine(staging, "notify"), request.Background, token);
            File.Copy(Path.Combine(staging, "notify", $"frame_{animation.FrameCount / 2:0000}.png"), Path.Combine(staging, "preview.png"));
            var manifest = new CustomCharacterManifest
            {
                Id = id, Name = request.Name.Trim(), DurationSeconds = animation.DurationSeconds, FrameCount = animation.FrameCount,
                SourceEdge = request.SourceEdge, AllowedEdges = request.AllowedEdges.Distinct().ToArray(), HasPet = hasPet, Mouse = request.Mouse,
                UsesTemplateMouse = hasPet && request.UseTemplateMouse
            };
            foreach (var (edge, path) in request.Directions)
            {
                progress?.Report("正在处理独立方向动画……");
                manifest.Directions[edge] = await CharacterMediaProcessor.VideoAsync(path, Path.Combine(staging, "direction-" + edge), request.Background, token);
            }
            if (hasPet)
            {
                progress?.Report("正在整理桌面互动素材……");
                var pet = Path.Combine(staging, "pet"); Directory.CreateDirectory(pet);
                foreach (var (key, path) in request.PetImages)
                {
                    if (!new[] { "idle", "left", "right", "center", "mouse" }.Contains(key)) throw new InvalidDataException("未知素材类型。");
                    using var image = await CharacterMediaProcessor.ImageAsync(path, key == "mouse" ? BackgroundRemoval.Preserve : request.PetBackground, token);
                    if (key == "mouse")
                    {
                        using var placed = new Bitmap(600, 448);
                        using (var g = Graphics.FromImage(placed))
                        {
                            var r = request.MousePlacement;
                            g.DrawImage(image, new RectangleF(r.X * 600, r.Y * 448, r.Width * 600, r.Height * 448));
                        }
                        placed.Save(Path.Combine(pet, key + ".png"), ImageFormat.Png);
                    }
                    else image.Save(Path.Combine(pet, key + ".png"), ImageFormat.Png);
                }
                if (manifest.UsesTemplateMouse)
                {
                    try { using var verified = DesktopPetSpriteSet.LoadCustom(pet); }
                    catch (InvalidDataException) when (request.AllowMouseCalibration) { manifest.UsesTemplateMouse = false; }
                }
            }
            token.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(staging, "manifest.json"), JsonSerializer.Serialize(manifest, CustomCharacterStore.JsonOptions));
            return new PreparedCharacter(staging, store);
        }
        catch { if (Directory.Exists(staging)) Directory.Delete(staging, true); throw; }
    }
}
