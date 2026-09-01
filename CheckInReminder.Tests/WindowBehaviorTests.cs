using System.Reflection;
using System.Runtime.ExceptionServices;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class WindowBehaviorTests
{
    [TestMethod]
    public void ShutdownGuard_OrdinaryCloseRequestKeepsInfrastructureWindowAlive()
    {
        RunOnStaThread(() =>
        {
            Form? guard = null;
            try
            {
                guard = CreateInternalForm(
                    "CheckInReminder.ShutdownGuardForm",
                    new Func<bool>(() => false),
                    new Action(() => { }));
                guard.Show();
                Application.DoEvents();

                guard.Close();
                Application.DoEvents();

                Assert.IsFalse(guard.IsDisposed, "普通关闭请求不应销毁程序生命周期内的关机守护窗口。");

                InvokeInstanceMethod(guard, "CloseForExit");
                Assert.IsTrue(guard.IsDisposed, "程序明确退出时仍应释放关机守护窗口。");
            }
            finally
            {
                guard?.Dispose();
            }
        });
    }

    [TestMethod]
    public void ShutdownGuard_RegistrationUpdateAfterDisposalDoesNotThrow()
    {
        RunOnStaThread(() =>
        {
            var guard = CreateInternalForm(
                "CheckInReminder.ShutdownGuardForm",
                new Func<bool>(() => false),
                new Action(() => { }));
            guard.Dispose();

            InvokeInstanceMethod(guard, "UpdateRegistration", true);
        });
    }

    [TestMethod]
    public void SettingsWindow_CanResizeInBothDirectionsAndScrollAtMinimumHeight()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateInternalForm(
                "CheckInReminder.SettingsForm",
                AppSettings.CreateDefault(),
                new Func<AppSettings, string?>(_ => null),
                new Action(() => { }));

            form.Show();
            Application.DoEvents();

            Assert.AreEqual(FormBorderStyle.Sizable, form.FormBorderStyle);
            Assert.IsTrue(form.MaximizeBox);

            var originalClientSize = form.ClientSize;
            form.Size = new Size(form.Width + 120, form.Height + 100);
            Application.DoEvents();

            Assert.IsGreaterThan(originalClientSize.Width, form.ClientSize.Width);
            Assert.IsGreaterThan(originalClientSize.Height, form.ClientSize.Height);

            form.Size = form.MinimumSize;
            Application.DoEvents();

            var scrollViewport = Descendants(form)
                .OfType<ScrollableControl>()
                .FirstOrDefault(control => control.AutoScroll && control.Dock == DockStyle.Fill);
            Assert.IsNotNull(scrollViewport, "设置内容需要位于随窗口缩放的滚动视口中。");
            Assert.IsGreaterThan(
                scrollViewport.ClientRectangle.Height,
                scrollViewport.DisplayRectangle.Height,
                "窗口缩到最小高度后，纵向滚动区域应覆盖被遮挡的设置内容。");
        });
    }

    [TestMethod]
    public void SettingsWindow_ToggleSwitchesShareTheSameRightEdge()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateInternalForm(
                "CheckInReminder.SettingsForm",
                AppSettings.CreateDefault(),
                new Func<AppSettings, string?>(_ => null),
                new Action(() => { }));

            form.Show();
            Application.DoEvents();

            var breakReminderToggle = Descendants(form)
                .Single(control => control.AccessibleName == "启用久坐提醒");
            var autoStartToggle = Descendants(form)
                .Single(control => control.AccessibleName == "开机自启动");

            Assert.AreEqual(
                RightEdgeInForm(form, breakReminderToggle),
                RightEdgeInForm(form, autoStartToggle),
                "同一列中的开关应共享右侧对齐线，避免开机自启动按钮横向漂移。");
        });
    }

    [TestMethod]
    public void SettingsWindow_AutoStartHeaderHasTheSameVisibleHeightAsBreakReminderHeader()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateSettingsForm();
            form.Show();
            Application.DoEvents();

            var breakReminderToggle = FindByAccessibleName(form, "启用久坐提醒");
            var autoStartToggle = FindByAccessibleName(form, "开机自启动");
            var breakHeader = HeaderBeside(breakReminderToggle);
            var autoStartHeader = HeaderBeside(autoStartToggle);

            Assert.AreEqual(
                breakHeader.Height,
                autoStartHeader.Height,
                "开机自启动卡片必须为标题和副标题保留与久坐提醒相同的完整高度。");
        });
    }

    [TestMethod]
    public void SettingsWindow_AutoStartToggleMatchesBreakReminderHeaderAlignment()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateSettingsForm();
            form.Show();
            Application.DoEvents();

            var breakReminderToggle = FindByAccessibleName(form, "启用久坐提醒");
            var autoStartToggle = FindByAccessibleName(form, "开机自启动");

            Assert.AreEqual(
                ToggleCenterRelativeToHeader(breakReminderToggle),
                ToggleCenterRelativeToHeader(autoStartToggle),
                "开机自启动开关必须与久坐提醒开关采用相同的标题行垂直对齐方式。");
        });
    }

    private static Form CreateSettingsForm() => CreateInternalForm(
        "CheckInReminder.SettingsForm",
        AppSettings.CreateDefault(),
        new Func<AppSettings, string?>(_ => null),
        new Action(() => { }));

    private static Control FindByAccessibleName(Control parent, string accessibleName) =>
        Descendants(parent).Single(control => control.AccessibleName == accessibleName);

    private static Control HeaderBeside(Control toggle) =>
        toggle.Parent!.Controls.Cast<Control>().Single(control => control.GetType().Name == "CardHeader");

    private static int ToggleCenterRelativeToHeader(Control toggle)
    {
        var header = HeaderBeside(toggle);
        var toggleBounds = toggle.RectangleToScreen(toggle.ClientRectangle);
        var headerBounds = header.RectangleToScreen(header.ClientRectangle);
        return (toggleBounds.Top + (toggleBounds.Height / 2)) - headerBounds.Top;
    }

    private static Form CreateInternalForm(string typeName, params object[] arguments)
    {
        var type = typeof(AppSettings).Assembly.GetType(typeName, throwOnError: true)!;
        return (Form)(Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: arguments,
            culture: null) ?? throw new InvalidOperationException($"无法创建 {typeName}。"));
    }

    private static void InvokeInstanceMethod(object target, string methodName, params object[] arguments)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"找不到方法 {methodName}。");
        method.Invoke(target, arguments);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static int RightEdgeInForm(Form form, Control control)
    {
        var topLeft = form.PointToClient(control.PointToScreen(Point.Empty));
        return topLeft.X + control.Width;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception is TargetInvocationException { InnerException: not null }
                    ? exception.InnerException
                    : exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
