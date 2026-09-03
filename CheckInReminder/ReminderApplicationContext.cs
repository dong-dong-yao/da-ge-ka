using Microsoft.Win32;

namespace CheckInReminder;

internal sealed class ReminderApplicationContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip trayMenu;
    private readonly Icon applicationIcon;
    private readonly SettingsService settingsService = new();
    private readonly AutoStartService autoStartService = new();
    private readonly ReminderScheduler scheduler;
    private readonly ShutdownGuardForm shutdownGuard;
    private readonly ReminderQueue reminderQueue = new();
    private AppSettings settings;
    private SettingsForm? settingsForm;
    private AnimatedReminderSession? reminder;
    private EveningConfirmForm? confirm;
    private PetOverlayForm? desktopPetForm;
    private AnimationSequence? desktopPetSequence;
    private ToolStripMenuItem? desktopPetItem;
    private ToolStripMenuItem? adjustPetPositionItem;
    private bool morningCompleted;
    private bool eveningCompleted;
    private bool isExiting;
    private bool cleanupComplete;
    private bool schedulerStarted;

    public ReminderApplicationContext()
    {
        settings = settingsService.Load();
        autoStartService.Apply(settings.AutoStart, out _);

        applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ??
            (Icon)SystemIcons.Application.Clone();

        var settingsItem = new ToolStripMenuItem("设置")
        {
            Padding = new Padding(16, 7, 24, 7),
        };
        settingsItem.Click += (_, _) => OpenSettings();
        desktopPetItem = new ToolStripMenuItem("桌面宠物")
        {
            Padding = new Padding(16, 7, 24, 7),
            CheckOnClick = true,
            Checked = settings.DesktopPetEnabled,
        };
        desktopPetItem.CheckedChanged += (_, _) => ToggleDesktopPetFromTray();
        adjustPetPositionItem = new ToolStripMenuItem("调整宠物位置")
        {
            Padding = new Padding(16, 7, 24, 7),
            CheckOnClick = true,
            Enabled = settings.DesktopPetEnabled,
        };
        adjustPetPositionItem.CheckedChanged += (_, _) =>
            desktopPetForm?.SetClickThrough(!adjustPetPositionItem.Checked);
        var exitItem = new ToolStripMenuItem("退出程序")
        {
            Padding = new Padding(16, 7, 24, 7),
        };
        exitItem.Click += (_, _) => ExitApplication();

        trayMenu = new ContextMenuStrip
        {
            BackColor = UiTheme.WarmBackgroundColor,
            ForeColor = UiTheme.TextColor,
            Font = new Font((SystemFonts.MenuFont ?? Control.DefaultFont).FontFamily, 11, FontStyle.Bold),
            Renderer = new BrandMenuRenderer(),
            ShowImageMargin = false,
            Padding = new Padding(3),
        };
        trayMenu.Items.Add(settingsItem);
        trayMenu.Items.Add(desktopPetItem);
        trayMenu.Items.Add(adjustPetPositionItem);
        trayMenu.Items.Add(exitItem);

        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = trayMenu,
            Icon = applicationIcon,
            Text = UiTheme.ProductName,
            Visible = true,
        };
        notifyIcon.DoubleClick += (_, _) => OpenSettings();

        shutdownGuard = new ShutdownGuardForm(ShouldBlockEveningShutdown, HandleShutdownVeto);
        shutdownGuard.Show();
        UpdateShutdownBlockRegistration(DateTime.Now);

        scheduler = new ReminderScheduler(
            settings,
            () => morningCompleted,
            () => eveningCompleted,
            RequestReminder,
            UpdateShutdownBlockRegistration);

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        Application.Idle += StartSchedulerOnIdle;

        if (settings.DesktopPetEnabled)
        {
            ApplyDesktopPet(true);
        }
    }

    private void StartSchedulerOnIdle(object? sender, EventArgs eventArgs)
    {
        if (schedulerStarted || isExiting)
        {
            return;
        }

        schedulerStarted = true;
        Application.Idle -= StartSchedulerOnIdle;
        scheduler.Start(DateTime.Now);
    }

    private void OpenSettings()
    {
        if (settingsForm is { IsDisposed: false })
        {
            settingsForm.Show();
            settingsForm.Activate();
            return;
        }

        settingsForm = new SettingsForm(settings.Clone(), SaveSettings, () => RequestReminder(ReminderKind.Test));
        settingsForm.FormClosed += (_, _) => settingsForm = null;
        settingsForm.Show();
        settingsForm.Activate();
    }

    private string? SaveSettings(AppSettings candidate)
    {
        var previousCharacterId = settings.CharacterId;
        try
        {
            settingsService.Save(candidate);
            settings = candidate.Clone();
            if (!settings.BreakReminderEnabled)
            {
                reminderQueue.CancelPendingBreak();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return $"配置保存失败：{exception.Message}";
        }

        ApplyDesktopPet(settings.DesktopPetEnabled);
        if (desktopPetForm is not null &&
            !string.Equals(previousCharacterId, settings.CharacterId, StringComparison.Ordinal))
        {
            LoadDesktopPetCharacter();
        }

        var autoStartApplied = autoStartService.Apply(settings.AutoStart, out var autoStartError);
        scheduler.ApplySettings(settings, DateTime.Now);
        UpdateShutdownBlockRegistration(DateTime.Now);
        return autoStartApplied ? null : autoStartError;
    }

    private bool updatingPetMenuItem;

    private void ToggleDesktopPetFromTray()
    {
        if (updatingPetMenuItem || desktopPetItem is null)
        {
            return;
        }

        var candidate = settings.Clone();
        candidate.DesktopPetEnabled = desktopPetItem.Checked;
        var saveError = SaveSettings(candidate);
        if (saveError is not null)
        {
            updatingPetMenuItem = true;
            desktopPetItem.Checked = settings.DesktopPetEnabled;
            updatingPetMenuItem = false;
        }
    }

    private void ApplyDesktopPet(bool enabled)
    {
        if (desktopPetItem is not null && desktopPetItem.Checked != enabled)
        {
            updatingPetMenuItem = true;
            desktopPetItem.Checked = enabled;
            updatingPetMenuItem = false;
        }
        if (adjustPetPositionItem is not null)
        {
            if (!enabled)
            {
                adjustPetPositionItem.Checked = false;
            }

            adjustPetPositionItem.Enabled = enabled;
        }

        if (enabled && desktopPetForm is null)
        {
            LoadDesktopPetCharacter();
        }
        else if (!enabled && desktopPetForm is not null)
        {
            desktopPetForm.Close();
            desktopPetForm.Dispose();
            desktopPetForm = null;
            desktopPetSequence?.Dispose();
            desktopPetSequence = null;
        }
    }

    private void LoadDesktopPetCharacter()
    {
        var character = AnimationCatalog.FindCharacter(settings.CharacterId)
            ?? AnimationCatalog.Characters[0];
        desktopPetSequence?.Dispose();
        // 骨架阶段：用提醒动画第 0 帧做静帧占位；敲击动画在钩子接入后播放
        desktopPetSequence = AnimationSequence.Load(character.SequenceName, character.Duration, character.Loop);
        if (desktopPetForm is null)
        {
            desktopPetForm = new PetOverlayForm();
            desktopPetForm.SetFrame(desktopPetSequence.Frames[0]);
            desktopPetForm.Show();
            desktopPetForm.SetClickThrough(adjustPetPositionItem?.Checked != true);
        }
        else
        {
            desktopPetForm.SetFrame(desktopPetSequence.Frames[0]);
        }
    }

    private void RequestReminder(ReminderKind kind)
    {
        if (isExiting ||
            (kind == ReminderKind.Morning && morningCompleted) ||
            (kind == ReminderKind.Evening && eveningCompleted))
        {
            return;
        }

        var action = reminderQueue.Request(kind);
        if (action == ReminderQueueAction.ReplaceCurrent)
        {
            reminder?.CloseWithoutResult();
            reminder = null;
        }

        if (action is ReminderQueueAction.Show or ReminderQueueAction.ReplaceCurrent)
        {
            ShowReminder(kind);
        }
    }

    private void HandleBannerResult(ReminderKind kind, bool clicked)
    {
        reminder = null;
        if (isExiting)
        {
            return;
        }

        if (!clicked || kind == ReminderKind.Break)
        {
            CompleteReminderFlow();
            return;
        }

        if (kind == ReminderKind.Morning)
        {
            morningCompleted = true;
            scheduler.CompletionChanged(DateTime.Now);
            CompleteReminderFlow();
            return;
        }

        confirm = new EveningConfirmForm(confirmed => HandleConfirmResult(kind, confirmed));
        confirm.Show();
    }

    private void HandleConfirmResult(ReminderKind kind, bool confirmed)
    {
        confirm = null;
        if (isExiting)
        {
            return;
        }

        if (kind != ReminderKind.Test && confirmed)
        {
            eveningCompleted = true;
            scheduler.CompletionChanged(DateTime.Now);
            UpdateShutdownBlockRegistration(DateTime.Now);
        }

        CompleteReminderFlow();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs eventArgs)
    {
        if (eventArgs.Mode == PowerModes.Resume)
        {
            MarshalResumeOrUnlock();
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs eventArgs)
    {
        if (eventArgs.Reason == SessionSwitchReason.SessionUnlock)
        {
            MarshalResumeOrUnlock();
        }
    }

    private void MarshalResumeOrUnlock()
    {
        if (isExiting || shutdownGuard.IsDisposed)
        {
            return;
        }

        try
        {
            shutdownGuard.BeginInvoke(new Action(() => scheduler.HandleResumeOrUnlock(DateTime.Now)));
        }
        catch (InvalidOperationException)
        {
            // The application is already tearing down its UI thread.
        }
    }

    private bool ShouldBlockEveningShutdown() =>
        !isExiting &&
        !eveningCompleted &&
        ScheduleCalculator.IsInEveningWindow(DateTime.Now, settings.EveningStart);

    private void UpdateShutdownBlockRegistration(DateTime now)
    {
        if (isExiting || shutdownGuard.IsDisposed)
        {
            return;
        }

        var shouldRegister = !isExiting &&
            !eveningCompleted &&
            ScheduleCalculator.IsInEveningWindow(now, settings.EveningStart);
        shutdownGuard.UpdateRegistration(shouldRegister);
    }

    private void HandleShutdownVeto()
    {
        if (isExiting)
        {
            return;
        }

        if (confirm is not null)
        {
            confirm.BringForward();
            return;
        }

        RequestReminder(ReminderKind.Evening);
    }

    private void ExitApplication()
    {
        if (isExiting)
        {
            return;
        }

        isExiting = true;
        notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        if (!cleanupComplete)
        {
            cleanupComplete = true;
            isExiting = true;
            Application.Idle -= StartSchedulerOnIdle;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            scheduler.Dispose();
            reminderQueue.Clear();
            reminder?.CloseWithoutResult();
            reminder = null;
            confirm?.CloseForExit();
            confirm = null;
            settingsForm?.Close();
            settingsForm = null;
            desktopPetForm?.Close();
            desktopPetForm?.Dispose();
            desktopPetForm = null;
            desktopPetSequence?.Dispose();
            desktopPetSequence = null;
            shutdownGuard.CloseForExit();
            shutdownGuard.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            trayMenu.Dispose();
            applicationIcon.Dispose();
        }

        base.ExitThreadCore();
    }

    private void ShowReminder(ReminderKind kind)
    {
        reminder = new AnimatedReminderSession(
            kind,
            settings.CharacterId,
            clicked => HandleBannerResult(kind, clicked));
        reminder.Show();
    }

    private void CompleteReminderFlow()
    {
        var next = reminderQueue.CompleteCurrent();
        if (!isExiting && next is { } nextKind)
        {
            ShowReminder(nextKind);
        }
    }
}
