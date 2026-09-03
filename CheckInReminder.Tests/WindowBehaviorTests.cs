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
                .FirstOrDefault(control => control.AutoScroll && control.Dock == DockStyle.Fill && control.Visible);
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

    [TestMethod]
    public void SettingsWindow_PreviewPausesDuringInteractiveResizeAndResumesAfterwards()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateSettingsForm();
            form.Show();
            Application.DoEvents();

            var preview = Descendants(form).OfType<PictureBox>().Single(control => control.Visible);
            Assert.IsTrue(WaitForImageChange(preview, preview.Image, 500), "测试前角色预览应处于播放状态。");

            InvokeInstanceMethod(form, "OnResizeBegin", EventArgs.Empty);
            var imageAtResizeStart = preview.Image;
            PumpEvents(260);
            Assert.AreSame(imageAtResizeStart, preview.Image, "拖动调整窗口尺寸时应暂停角色预览，避免与布局和绘制争抢 UI 线程。");

            InvokeInstanceMethod(form, "OnResizeEnd", EventArgs.Empty);
            Assert.IsTrue(WaitForImageChange(preview, imageAtResizeStart, 500), "结束调整窗口尺寸后应恢复角色预览。");
        });
    }

    [TestMethod]
    public void SettingsWindow_PreviewPausesDuringScrollAndResumesAfterIdle()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateSettingsForm();
            form.Show();
            Application.DoEvents();

            var preview = Descendants(form).OfType<PictureBox>().Single(control => control.Visible);
            var viewport = Descendants(form)
                .OfType<ScrollableControl>()
                .Single(control => control.AutoScroll && control.Dock == DockStyle.Fill && control.Visible);
            var scrollEvents = 0;
            viewport.Scroll += (_, _) => scrollEvents++;

            viewport.AutoScrollPosition = new Point(0, 120);
            RaiseScroll(viewport, 120);
            Application.DoEvents();
            Assert.IsGreaterThan(0, scrollEvents, "测试必须通过真实滚动事件触发交互状态。");

            var imageWhileScrolling = preview.Image;
            for (var step = 0; step < 4; step++)
            {
                PumpEvents(60);
                RaiseScroll(viewport, 140 + (step * 20));
            }
            Assert.AreSame(imageWhileScrolling, preview.Image, "滚动内容时应暂停角色预览，避免每 33ms 追加一次图片缩放重绘。");
            Assert.IsTrue(WaitForImageChange(preview, imageWhileScrolling, 600), "停止滚动后应自动恢复角色预览。");
        });
    }

    [TestMethod]
    public void SettingsWindow_ScrollViewportUsesDoubleBuffering()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateSettingsForm();
            form.Show();
            Application.DoEvents();

            var viewport = Descendants(form)
                .OfType<ScrollableControl>()
                .Single(control => control.AutoScroll && control.Dock == DockStyle.Fill && control.Visible);
            var doubleBuffered = (bool)(typeof(Control).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport) ?? false);

            Assert.IsTrue(doubleBuffered, "设置页滚动视口应合并绘制操作，避免滚动时逐层闪烁和撕裂。");
        });
    }

    [TestMethod]
    public void SettingsWindow_SideNavigationSwitchesBetweenPages()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateSettingsForm();
            form.Show();
            Application.DoEvents();

            var settingsPage = Descendants(form)
                .OfType<ScrollableControl>()
                .Single(control => control.AutoScroll && control.Dock == DockStyle.Fill
                    && control.GetType().Name == "BufferedScrollPanel");
            var charactersPage = Descendants(form)
                .Single(control => control.GetType().Name == "CharactersPage");
            var sideNav = Descendants(form)
                .Single(control => control.GetType().Name == "SideNavBar");

            Assert.IsTrue(settingsPage.Visible, "默认应显示设置页。");
            Assert.IsFalse(charactersPage.Visible, "默认角色页应隐藏。");

            InvokeInstanceMethod(sideNav, "SelectPage", "characters");
            Application.DoEvents();

            Assert.IsFalse(settingsPage.Visible, "切到角色页后设置页应隐藏。");
            Assert.IsTrue(charactersPage.Visible, "切到角色页后角色页应显示。");

            InvokeInstanceMethod(sideNav, "SelectPage", "settings");
            Application.DoEvents();

            Assert.IsTrue(settingsPage.Visible, "切回设置页后设置页应显示。");
            Assert.IsFalse(charactersPage.Visible, "切回设置页后角色页应隐藏。");
        });
    }

    [TestMethod]
    public void SettingsWindow_HiddenPagePreviewStaysPaused()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateSettingsForm();
            form.Show();
            Application.DoEvents();

            var sideNav = Descendants(form)
                .Single(control => control.GetType().Name == "SideNavBar");
            InvokeInstanceMethod(sideNav, "SelectPage", "characters");
            Application.DoEvents();

            var settingsPreview = Descendants(form)
                .OfType<PictureBox>()
                .Single(control => !control.Visible);
            var imageOnHiddenPage = settingsPreview.Image;
            PumpEvents(260);

            Assert.AreSame(
                imageOnHiddenPage,
                settingsPreview.Image,
                "设置页被切走隐藏后，其中的角色预览应暂停播放。");
        });
    }

    [TestMethod]
    public void CardHeader_RepeatedPaintingKeepsManagedAllocationBounded()
    {
        RunOnStaThread(() =>
        {
            var assembly = typeof(AppSettings).Assembly;
            var iconKindType = assembly.GetType("CheckInReminder.IconBadge+IconKind", throwOnError: true)!;
            var icon = Enum.Parse(iconKindType, "Power");
            using var header = CreateInternalControl(
                "CheckInReminder.CardHeader",
                icon,
                "开机自启动",
                "登录 Windows 后自动守候提醒");
            header.Width = 420;
            using var host = new Form();
            host.Controls.Add(header);
            host.Show();
            Application.DoEvents();
            using var target = new Bitmap(header.Width, header.Height);

            header.DrawToBitmap(target, header.ClientRectangle);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            const int paintCount = 50;
            const long maximumBytesPerPaint = 1024;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < paintCount; index++)
            {
                header.DrawToBitmap(target, header.ClientRectangle);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsLessThanOrEqualTo(
                paintCount * maximumBytesPerPaint,
                allocated,
                $"卡片标题重复绘制分配了 {allocated} 字节；绘制路径不应逐帧创建控件和位图。");
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

    private static Control CreateInternalControl(string typeName, params object[] arguments)
    {
        var type = typeof(AppSettings).Assembly.GetType(typeName, throwOnError: true)!;
        return (Control)(Activator.CreateInstance(
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

    private static bool WaitForImageChange(PictureBox pictureBox, Image? original, int timeoutMilliseconds)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            if (!ReferenceEquals(original, pictureBox.Image))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    private static void PumpEvents(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static void RaiseScroll(ScrollableControl control, int newValue) =>
        typeof(ScrollableControl).GetMethod(
            "OnScroll",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
                control,
                [new ScrollEventArgs(ScrollEventType.ThumbTrack, newValue, ScrollOrientation.VerticalScroll)]);

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
