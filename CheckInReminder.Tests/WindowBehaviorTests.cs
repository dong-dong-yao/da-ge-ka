using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
[DoNotParallelize]
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
            Assert.IsTrue(WaitForImageChange(preview, preview.Image, 900), "测试前角色预览应处于播放状态。");

            InvokeInstanceMethod(form, "OnResizeBegin", EventArgs.Empty);
            var imageAtResizeStart = preview.Image;
            PumpEvents(260);
            Assert.AreSame(imageAtResizeStart, preview.Image, "拖动调整窗口尺寸时应暂停角色预览，避免与布局和绘制争抢 UI 线程。");

            InvokeInstanceMethod(form, "OnResizeEnd", EventArgs.Empty);
            Assert.IsTrue(WaitForImageChange(preview, imageAtResizeStart, 900), "结束调整窗口尺寸后应恢复角色预览。");
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
                // 滚动事件间隔必须稳定小于 120ms 空闲窗口，高负载下 60ms 步进会被拉长导致中途恢复
                PumpEvents(30);
                RaiseScroll(viewport, 140 + (step * 20));
            }
            Assert.AreSame(imageWhileScrolling, preview.Image, "滚动内容时应暂停角色预览，避免每 33ms 追加一次图片缩放重绘。");
            Assert.IsTrue(WaitForImageChange(preview, imageWhileScrolling, 1000), "停止滚动后应自动恢复角色预览。");
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
    public void SettingsWindow_PageHostCompositesChildWindows()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateSettingsForm();
            form.Show();
            Application.DoEvents();

            var pageHost = Descendants(form)
                .Single(control => control.GetType().Name == "CompositedPageHost");
            var createParams = (CreateParams)pageHost.GetType().GetProperty(
                "CreateParams",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pageHost)!;

            const int wsExComposited = 0x02000000;
            Assert.AreNotEqual(
                0,
                createParams.ExStyle & wsExComposited,
                "复杂页面区域应由 Windows 合成所有子窗口后一次呈现，避免缩放和换页暴露半绘制状态。");
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
    public void SettingsWindow_CharacterPreviewPlaceholderKeepsSixteenByNineRatio()
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

            var placeholder = Descendants(form)
                .Single(control => control.Visible && control.AccessibleName == "角色预览占位图");
            AssertAspectRatio(placeholder, 16d / 9d);

            form.Size = new Size(form.Width + 360, form.Height);
            Application.DoEvents();

            AssertAspectRatio(placeholder, 16d / 9d);
        });
    }

    [TestMethod]
    public void PetOverlay_StartsClickThroughAndTogglesDraggableMode()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateInternalForm("CheckInReminder.PetOverlayForm", 140);
            var clickThroughProperty = form.GetType().GetProperty("ClickThrough")!;

            Assert.IsTrue((bool)clickThroughProperty.GetValue(form)!, "宠物窗口默认应鼠标穿透。");

            InvokeInstanceMethod(form, "SetClickThrough", false);
            Assert.IsFalse((bool)clickThroughProperty.GetValue(form)!, "调整位置模式下应关闭穿透。");

            InvokeInstanceMethod(form, "SetClickThrough", true);
            Assert.IsTrue((bool)clickThroughProperty.GetValue(form)!, "拖完位置后应恢复穿透。");
        });
    }

    [TestMethod]
    public void GlobalInputService_DisposeUnhooksBothInstalledHooksExactlyOnce()
    {
        var serviceType = typeof(AppSettings).Assembly.GetType(
            "CheckInReminder.GlobalInputService",
            throwOnError: true)!;
        var installedHookTypes = new List<int>();
        var uninstalledHandles = new List<IntPtr>();
        Func<int, Delegate, IntPtr> installHook = (hookType, _) =>
        {
            installedHookTypes.Add(hookType);
            return hookType == 13 ? (IntPtr)101 : (IntPtr)202;
        };
        Func<IntPtr, bool> uninstallHook = handle =>
        {
            uninstalledHandles.Add(handle);
            return true;
        };
        using var service = (IDisposable)(Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [new DesktopInputState(), installHook, uninstallHook],
            culture: null) ?? throw new InvalidOperationException("无法创建 GlobalInputService。"));

        var installResult = serviceType.GetMethod("Install")!.Invoke(service, null)!;
        var success = (bool)installResult.GetType().GetProperty("Success")!.GetValue(installResult)!;
        var keyboardHandle = (IntPtr)serviceType.GetField(
            "keyboardHookHandle",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        var mouseHandle = (IntPtr)serviceType.GetField(
            "mouseHookHandle",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;

        Assert.IsTrue(success);
        CollectionAssert.AreEqual(new[] { 13, 14 }, installedHookTypes);
        Assert.AreEqual((IntPtr)101, keyboardHandle);
        Assert.AreEqual((IntPtr)202, mouseHandle);
        Assert.AreNotEqual(keyboardHandle, mouseHandle);

        service.Dispose();
        service.Dispose();

        CollectionAssert.AreEquivalent(
            new[] { (IntPtr)101, (IntPtr)202 },
            uninstalledHandles,
            "释放应恰好各卸载一次键盘与鼠标钩子。除此之外不应重复卸载。");
    }

    [TestMethod]
    public void GlobalInputService_SecondHookFailureRollsBackFirstHook()
    {
        var serviceType = typeof(AppSettings).Assembly.GetType(
            "CheckInReminder.GlobalInputService",
            throwOnError: true)!;
        var uninstalledHandles = new List<IntPtr>();
        Func<int, Delegate, IntPtr> installHook = (hookType, _) =>
        {
            if (hookType == 13)
            {
                return (IntPtr)303;
            }

            Marshal.SetLastPInvokeError(87);
            return IntPtr.Zero;
        };
        Func<IntPtr, bool> uninstallHook = handle =>
        {
            uninstalledHandles.Add(handle);
            return true;
        };
        using var service = (IDisposable)(Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [new DesktopInputState(), installHook, uninstallHook],
            culture: null) ?? throw new InvalidOperationException("无法创建 GlobalInputService。"));

        var installResult = serviceType.GetMethod("Install")!.Invoke(service, null)!;
        var success = (bool)installResult.GetType().GetProperty("Success")!.GetValue(installResult)!;
        var win32Error = (int)installResult.GetType().GetProperty("Win32Error")!.GetValue(installResult)!;
        var isInstalled = (bool)serviceType.GetProperty("IsInstalled")!.GetValue(service)!;

        Assert.IsFalse(success);
        Assert.AreEqual(87, win32Error);
        Assert.IsFalse(isInstalled);
        CollectionAssert.AreEqual(new[] { (IntPtr)303 }, uninstalledHandles);
    }

    [TestMethod]
    public void GlobalInputService_CallbackUpdatesStateBeforeSynchronousNotificationWithoutContext()
    {
        var serviceType = typeof(AppSettings).Assembly.GetType(
            "CheckInReminder.GlobalInputService",
            throwOnError: true)!;
        var state = new DesktopInputState();
        var callbacks = new Dictionary<int, Delegate>();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            Func<int, Delegate, IntPtr> installHook = (hookType, callback) =>
            {
                callbacks.Add(hookType, callback);
                return hookType == 13 ? (IntPtr)401 : (IntPtr)402;
            };
            using var service = (IDisposable)(Activator.CreateInstance(
                serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [state, installHook, new Func<IntPtr, bool>(_ => true)],
                culture: null) ?? throw new InvalidOperationException("无法创建 GlobalInputService。"));
            serviceType.GetMethod("Install")!.Invoke(service, null);
            var notificationCount = 0;
            var notifiedThread = -1;
            var snapshotWhenNotified = default(DesktopInputSnapshot);
            var handler = new EventHandler((_, _) =>
            {
                notificationCount++;
                notifiedThread = Environment.CurrentManagedThreadId;
                snapshotWhenNotified = state.ReadSnapshot();
            });
            serviceType.GetEvent("InputAvailable")!.AddEventHandler(service, handler);

            using var keyboardData = new NativeBuffer(sizeof(int));
            Marshal.WriteInt32(keyboardData.Pointer, 65);
            var callbackThread = Environment.CurrentManagedThreadId;
            callbacks[13].DynamicInvoke(0, (IntPtr)0x0100, keyboardData.Pointer);

            Assert.AreEqual(1, notificationCount);
            Assert.AreEqual(65, snapshotWhenNotified.ActiveVirtualKey, "通知前应已更新无文本键盘状态。");
            Assert.AreEqual(callbackThread, notifiedThread, "低级 Hook 回调应在安装线程同步通知轻量消费者。");
            Assert.AreEqual(Environment.CurrentManagedThreadId, notifiedThread);

            using var mouseData = new NativeBuffer(sizeof(int) * 2);
            Marshal.WriteInt32(mouseData.Pointer, 0, 120);
            Marshal.WriteInt32(mouseData.Pointer, sizeof(int), 240);
            callbacks[14].DynamicInvoke(0, (IntPtr)0x0201, mouseData.Pointer);

            var mouseSnapshot = state.ReadSnapshot();
            Assert.AreEqual(new Point(120, 240), mouseSnapshot.CursorScreen);
            Assert.IsTrue(mouseSnapshot.LeftButtonDown);
            Assert.AreEqual(2, notificationCount);
            Assert.AreEqual(callbackThread, notifiedThread);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
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

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DesktopPetLifecycle_ShowsBeforeInstallingAndUnsubscribesBeforeDisablingOrExiting(bool exit)
    {
        RunOnStaThread(() =>
        {
            var fixture = new PetInputFixture();
            using var context = fixture.CreateContext();
            DesktopPetController? controller = null;
            Form? form = null;
            fixture.OnInstall = () =>
            {
                controller = (DesktopPetController)GetField(context, "desktopPetController")!;
                form = (Form)GetField(context, "desktopPetForm")!;
                Assert.IsTrue(form.Visible, "安装 Hook 前必须已展示桌宠。");
                Assert.IsFalse(ControllerIsRendering(controller), "安装完成前不能开始输入循环。");
                Assert.IsNotNull(GetField(fixture.Service!, "InputAvailable"), "安装前必须订阅唤醒事件。");
            };
            fixture.OnUninstall = () =>
            {
                Assert.IsNull(GetField(fixture.Service!, "InputAvailable"), "卸载 Hook 前必须取消订阅。");
                Assert.IsFalse(form!.IsDisposed, "先停止输入监听，再释放窗体。");
                Assert.IsNotNull(GetField(form, "currentSource"), "卸载输入时当前帧应仍存活。");
            };
            try
            {
                InvokeInstanceMethod(context, "ApplyDesktopPet", true);
                Assert.IsNotNull(controller);
                Assert.IsTrue(ControllerIsRendering(controller));
                PumpEvents(700);
                Assert.IsFalse(ControllerIsRendering(controller));
                var initial = (Bitmap)GetField(form!, "currentSource")!;
                fixture.Key(0x41, true);
                Assert.IsTrue(ControllerIsRendering(controller), "真实 Hook 回调应唤醒控制器。");
                Assert.AreSame(initial, GetField(form!, "currentSource"), "Hook 回调不能同步 Render。");
                PumpEvents(100);
                Assert.AreNotSame(initial, GetField(form!, "currentSource"), "事件泵必须驱动新帧呈现。");

                InvokeInstanceMethod(context, exit ? "ExitApplication" : "ApplyDesktopPet", exit ? [] : [false]);
                CollectionAssert.AreEquivalent(new[] { (IntPtr)101, (IntPtr)202 }, fixture.Uninstalled);
                Assert.IsTrue(form!.IsDisposed);
                Assert.IsFalse(ControllerIsRendering(controller));
                Assert.IsNull(GetField(form, "currentSource"));
                Assert.IsNull(GetField(context, "desktopPetController"));
            }
            finally
            {
                InvokeInstanceMethod(context, "ExitApplication");
            }
        });
    }

    [TestMethod]
    public void DesktopPetLifecycle_HookFailureKeepsIdleAndReportsOnceWithoutBlocking()
    {
        RunOnStaThread(() =>
        {
            var fixture = new PetInputFixture { FailMouseHook = true };
            using var context = fixture.CreateContext();
            try
            {
                InvokeInstanceMethod(context, "ApplyDesktopPet", true);
                var form = (Form)GetField(context, "desktopPetForm")!;
                var controller = (DesktopPetController)GetField(context, "desktopPetController")!;
                var idle = (Bitmap)GetField(form, "currentSource")!;
                Assert.IsTrue(form.Visible);
                Assert.IsFalse(ControllerIsRendering(controller));
                Assert.IsNull(GetField(context, "globalInput"));
                Assert.IsNull(GetField(fixture.Service!, "InputAvailable"));
                var menu = (ContextMenuStrip)GetField(context, "trayMenu")!;
                var status = menu.Items.Cast<ToolStripItem>().Single(item => item.Text == "输入监听不可用（错误 87）");
                Assert.IsFalse(status.Enabled);
                Assert.IsTrue(status.Available);
                Assert.HasCount(1, fixture.Notifications);
                StringAssert.Contains(fixture.Notifications[0], "87");
                PumpEvents(150);
                Assert.AreSame(idle, GetField(form, "currentSource"));
                _ = idle.GetPixel(0, 0);

                InvokeInstanceMethod(context, "ApplyDesktopPet", false);
                InvokeInstanceMethod(context, "ApplyDesktopPet", true);
                Assert.HasCount(1, fixture.Notifications, "本次程序生命周期内只显示一次非阻塞通知。");
                InvokeInstanceMethod(context, "ApplyDesktopPet", false);
                fixture.FailMouseHook = false;
                InvokeInstanceMethod(context, "ApplyDesktopPet", true);
                Assert.IsFalse(status.Available, "重新启用并安装成功后隐藏过期失败状态。");
                Assert.IsNotNull(GetField(context, "globalInput"));
            }
            finally
            {
                InvokeInstanceMethod(context, "ExitApplication");
            }
        });
    }

    [TestMethod]
    public void PetOverlay_DisposalAndClearFrameReleaseBorrowedSourceReference()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateInternalForm("CheckInReminder.PetOverlayForm", 140);
            using var source = new Bitmap(30, 20);
            ((IPetFrameSink)form).SetFrame(source);
            InvokeInstanceMethod(form, "ClearFrame");
            Assert.IsNull(GetField(form, "currentSource"));
            ((IPetFrameSink)form).SetFrame(source);
            form.Dispose();
            Assert.IsNull(GetField(form, "currentSource"));
            _ = source.GetPixel(0, 0);
        });
    }

    [TestMethod]
    public void PetOverlay_RejectedFrameKeepsThePreviousLiveSourceForDpiRepaint()
    {
        RunOnStaThread(() =>
        {
            using var form = CreateInternalForm("CheckInReminder.PetOverlayForm", 140);
            using var previous = new Bitmap(30, 20);
            ((IPetFrameSink)form).SetFrame(previous);
            var invalid = new Bitmap(30, 20);
            invalid.Dispose();
            Assert.Throws<ArgumentException>(() => ((IPetFrameSink)form).SetFrame(invalid));
            Assert.AreSame(previous, GetField(form, "currentSource"),
                "失败的呈现不能把已释放位图留给 DPI 重绘。");
        });
    }

    private sealed class PetInputFixture
    {
        private readonly Dictionary<int, Delegate> callbacks = new();
        public object? Service { get; private set; }
        public bool FailMouseHook { get; set; }
        public Action? OnInstall { get; set; }
        public Action? OnUninstall { get; set; }
        public List<IntPtr> Uninstalled { get; } = [];
        public List<string> Notifications { get; } = [];

        public ApplicationContext CreateContext()
        {
            var serviceType = typeof(AppSettings).Assembly.GetType("CheckInReminder.GlobalInputService", true)!;
            var state = Expression.Parameter(typeof(DesktopInputState), "state");
            var factory = Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(DesktopInputState), serviceType),
                Expression.Convert(Expression.Call(Expression.Constant(this), nameof(CreateService), null, state), serviceType), state).Compile();
            var type = typeof(AppSettings).Assembly.GetType("CheckInReminder.ReminderApplicationContext", true)!;
            var settings = AppSettings.CreateDefault();
            settings.DesktopPetEnabled = false;
            var context = (ApplicationContext)Activator.CreateInstance(type,
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                [settings, factory, new Action<string>(Notifications.Add)], null)!;
            // Suppress reminder scheduling while pumping the desktop-pet lifecycle.
            type.GetField("morningCompleted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(context, true);
            type.GetField("eveningCompleted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(context, true);
            return context;
        }

        public object CreateService(DesktopInputState state)
        {
            var area = Screen.PrimaryScreen!.WorkingArea;
            state.UpdatePointer(area.Left + area.Width / 2, area.Top + area.Height / 2);
            var type = typeof(AppSettings).Assembly.GetType("CheckInReminder.GlobalInputService", true)!;
            Service = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                [state, new Func<int, Delegate, IntPtr>((hook, callback) =>
                {
                    OnInstall?.Invoke();
                    callbacks[hook] = callback;
                    if (hook == 14 && FailMouseHook)
                    {
                        Marshal.SetLastPInvokeError(87);
                        return IntPtr.Zero;
                    }
                    return hook == 13 ? (IntPtr)101 : (IntPtr)202;
                }), new Func<IntPtr, bool>(handle =>
                {
                    OnUninstall?.Invoke();
                    Uninstalled.Add(handle);
                    return true;
                })], null)!;
            return Service;
        }

        public void Key(int virtualKey, bool pressed)
        {
            using var data = new NativeBuffer(sizeof(int));
            Marshal.WriteInt32(data.Pointer, virtualKey);
            callbacks[13].DynamicInvoke(0, (IntPtr)(pressed ? 0x0100 : 0x0101), data.Pointer);
        }
    }

    private static object? GetField(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);

    private static bool ControllerIsRendering(DesktopPetController controller) =>
        (bool)typeof(DesktopPetController).GetProperty("IsRendering", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!;

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

    private static void AssertAspectRatio(Control control, double expected)
    {
        Assert.IsGreaterThan(0, control.Height);
        var actual = control.Width / (double)control.Height;
        Assert.AreEqual(
            expected,
            actual,
            0.03,
            $"角色预览占位图应保持固定比例；当前尺寸为 {control.Width}x{control.Height}。");
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

    private sealed class NativeBuffer : IDisposable
    {
        public NativeBuffer(int byteCount) => Pointer = Marshal.AllocHGlobal(byteCount);

        public IntPtr Pointer { get; }

        public void Dispose() => Marshal.FreeHGlobal(Pointer);
    }
}
