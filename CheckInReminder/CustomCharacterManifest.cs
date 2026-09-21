namespace CheckInReminder;

public sealed class CustomCharacterManifest
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double DurationSeconds { get; set; }
    public int FrameCount { get; set; }
    public ScreenEdge SourceEdge { get; set; } = ScreenEdge.Right;
    public ScreenEdge[] AllowedEdges { get; set; } = [ScreenEdge.Right];
    public Dictionary<ScreenEdge, CustomAnimationInfo> Directions { get; set; } = [];
    public bool HasPet { get; set; }
    public bool UsesTemplateMouse { get; set; }
    public CustomMouseRig? Mouse { get; set; }
    public Dictionary<string, MouseCalibration>? MouseCalibrations { get; set; }
}

public sealed class CustomAnimationInfo
{
    public double DurationSeconds { get; set; }
    public int FrameCount { get; set; }
}

public sealed class CustomMouseRig
{
    public float ShoulderX { get; set; } = .5f;
    public float ShoulderY { get; set; } = .4f;
    public float MouseX { get; set; } = .3f;
    public float MouseY { get; set; } = .7f;
    public void Validate()
    {
        if (new[] { ShoulderX, ShoulderY, MouseX, MouseY }.Any(v => !float.IsFinite(v) || v < 0 || v > 1)
            || MathF.Abs(ShoulderX - MouseX) + MathF.Abs(ShoulderY - MouseY) < .08f)
            throw new InvalidDataException("请在图上分别标记手臂连接处和鼠标中心，两点不能重合。");
    }
}
