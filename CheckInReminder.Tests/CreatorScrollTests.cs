using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass, DoNotParallelize]
public sealed class CreatorScrollTests
{
    [STATestMethod]
    public void ExpandingDirections_UpdatesScrollRangeWithoutResizingWindow()
    {
        using var form = new CharacterCreationWizard();
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
        form.Show();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(CharacterCreationWizard).GetMethod("ShowStep", flags)!.Invoke(form, [1]);
        Application.DoEvents();
        var host = (Panel)typeof(CharacterCreationWizard).GetField("host", flags)!.GetValue(form)!;
        var toggle = Descendants(form).OfType<CheckBox>().Single(c => c.Text.StartsWith("为某个方向"));
        var originalSize = form.Size;
        for (var repeat = 0; repeat < 3; repeat++)
        {
            toggle.Checked = true; Application.DoEvents();
            var page = host.Controls[0];
            Assert.IsGreaterThanOrEqualTo(page.Height, host.DisplayRectangle.Height, "Expanded content must immediately update the scroll extent.");
            host.AutoScrollPosition = new Point(0, page.Height); Application.DoEvents();
            Assert.IsLessThan(-100, host.AutoScrollPosition.Y, "Must scroll without resizing the form.");
            Assert.IsLessThanOrEqualTo(host.ClientSize.Height + 20, page.Bottom, "The bottom upload must be reachable.");
            toggle.Checked = false; Application.DoEvents();
            Assert.IsTrue(page.Bounds.IntersectsWith(host.ClientRectangle), "Collapsing must keep the page visible.");
        }
        Assert.AreEqual(originalSize, form.Size);
    }
    private static IEnumerable<Control> Descendants(Control control)
    { foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
}
