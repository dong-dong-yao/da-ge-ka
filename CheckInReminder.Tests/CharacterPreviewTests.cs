using System.Drawing;
using System.Windows.Forms;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CharacterPreviewTests
{
    [STATestMethod]
    [DataRow("white-bear")]
    [DataRow("yellow-hippo")]
    [DataRow("blue-hat-cat")]
    [DataRow("stick-dog")]
    [DataRow("scooter-dinosaur")]
    public void PausedCard_ShowsRecognizableCharacterInsteadOfEntranceFrame(string id)
    {
        using var card = new CharacterCardControl(AnimationCatalog.FindCharacter(id)!, false);
        card.SetPlaying(false);
        var box = Descendants(card).OfType<PictureBox>().Single();
        Assert.IsNotNull(box.Image);
        var image = (Bitmap)box.Image;
        var opaque = 0;
        var samples = 0;
        for (var y = 0; y < image.Height; y += 3)
        for (var x = 0; x < image.Width; x += 3)
        {
            samples++;
            if (image.GetPixel(x, y).A > 128) opaque++;
        }
        Assert.IsGreaterThan(samples * 0.18, (double)opaque, $"{id}: only {opaque * 100d / samples:F1}% visible pixels");
    }

    [STATestMethod]
    [DataRow(860, 600)]
    [DataRow(980, 700)]
    [DataRow(1280, 900)]
    public void Gallery_LayoutAndSelectionRemainUsable(int width, int height)
    {
        using var form = new SettingsForm(new AppSettings(), _ => null, () => { });
        form.ClientSize = new Size(width, height);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
        form.ShowInTaskbar = false;
        form.Show();
        Descendants(form).OfType<SideNavBar>().Single().SelectPage("characters");
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        var page = Descendants(form).OfType<CharactersPage>().Single();
        page.SetInteractionPaused(true);
        var cards = Descendants(page).OfType<CharacterCardControl>().ToArray();
        Assert.HasCount(5, cards);
        Assert.IsFalse(page.HorizontalScroll.Visible, "Gallery must not scroll horizontally");
        foreach (var card in cards)
        {
            var image = Descendants(card).OfType<PictureBox>().Single();
            Assert.IsGreaterThanOrEqualTo(130, image.Height, "Character stage is too short");
            Assert.IsGreaterThanOrEqualTo(140, image.Width, "Character stage is too narrow");
            var button = Descendants(card).OfType<Button>().Single();
            Assert.IsTrue(card.ClientRectangle.Contains(button.Bounds));
        }
        var output = Environment.GetEnvironmentVariable("CHARACTER_GALLERY_PROBE_DIR");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(output, $"gallery-{width}x{height}.png"));
        }
        if (width >= 980)
            Assert.IsFalse(page.VerticalScroll.Visible, $"All five characters should fit: page={page.ClientSize} content={cards[0].Parent!.Bounds} minimum={page.AutoScrollMinSize} last={cards[^1].Bounds}");
        string? confirmed = null;
        page.CharacterConfirmed += (_, id) => confirmed = id;
        var lastButton = Descendants(cards[^1]).OfType<Button>().Single();
        lastButton.PerformClick();
        Assert.AreEqual(cards[^1].Character.Id, confirmed);
        Assert.IsFalse(lastButton.Enabled);
        Assert.IsTrue(Descendants(cards[0]).OfType<Button>().Single().Enabled);
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
