using System.Reflection;

namespace CheckInReminder;

internal static class UiAssets
{
    public static Image Load(string fileName)
    {
        var resourceName = $"CheckInReminder.Assets.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"缺少嵌入图片资源：{resourceName}");
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }
}
