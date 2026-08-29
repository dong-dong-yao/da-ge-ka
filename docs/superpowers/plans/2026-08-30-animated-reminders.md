# Animated Reminders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Replace static meme reminders with edge character animation, add optional fixed-node break reminders, and animate the centered second gate.

**Architecture:** Pure scheduling and arbitration rules remain testable outside WinForms. MP4 files are converted once into embedded transparent PNG sequences; lightweight WinForms players render first-layer one-shot edge animations and the gate's restart loop without runtime media dependencies.

**Tech Stack:** C# / .NET 10 / WinForms / MSTest / FFmpeg frame extraction / Pillow chroma key

**Spec:** `docs/superpowers/specs/2026-08-30-animated-reminders-design.md`

## Global Constraints

- Target Windows 10/11 x64 only.
- Preserve exact reminder copy and fixed time-anchor semantics.
- First animation keeps its original approximately 7.1-second duration.
- Second animation loops end-to-start, never ping-pongs.
- Publish self-contained, single-file, and with trimming disabled.
- Never run unattended real shutdown or restart tests.

---

### Task 1: Break reminder schedule and settings contract

**Files:**
- Modify: `CheckInReminder/AppSettings.cs`
- Modify: `CheckInReminder/SettingsService.cs`
- Modify: `CheckInReminder/ScheduleCalculator.cs`
- Modify: `CheckInReminder.Tests/ScheduleCalculatorTests.cs`
- Create: `CheckInReminder.Tests/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `ScheduleCalculator.IsInBreakWindow(DateTime, TimeOnly, TimeOnly)` and `GetNextBreakDue(DateTime, TimeOnly, TimeOnly, int)`.
- Produces: four break properties on `AppSettings` with migration-safe defaults.

- [x] **Step 1: Write failing schedule and configuration tests**

```csharp
Assert.AreEqual(At(11, 0), ScheduleCalculator.GetNextBreakDue(At(10, 20), new(9, 0), new(18, 0), 60));
Assert.IsNull(ScheduleCalculator.GetNextBreakDue(At(17, 30), new(9, 0), new(18, 0), 60));
Assert.IsFalse(SettingsService.TryValidate(new AppSettings { BreakIntervalMinutes = 75 }, out _));
```

- [x] **Step 2: Run `dotnet test` and verify failures identify missing break APIs/properties.**
- [x] **Step 3: Add defaults, cloning, validation, and fixed-node calculation with an exclusive end boundary.**
- [x] **Step 4: Run all tests and verify green.**

### Task 2: Reminder priority state machine

**Files:**
- Create: `CheckInReminder/ReminderQueue.cs`
- Create: `CheckInReminder.Tests/ReminderQueueTests.cs`
- Modify: `CheckInReminder/ReminderScheduler.cs`

**Interfaces:**
- Produces: `ReminderQueue.Request(ReminderKind)`, `BeginNext()`, and `CompleteCurrent()`.
- Consumes: `ReminderKind.Break` emitted by the scheduler at fixed break nodes.

- [x] **Step 1: Write failing tests proving check-in requests outrank break requests and only one break request is retained.**
- [x] **Step 2: Run tests and verify the missing `ReminderQueue` failure.**
- [x] **Step 3: Implement the smallest deterministic queue and add break scheduling without moving fixed nodes.**
- [x] **Step 4: Run all tests and verify green.**

### Task 3: Convert and embed animation assets

**Files:**
- Create: `tools/convert-green-screen.ps1`
- Create: `CheckInReminder/Assets/Animations/Edge/*.png`
- Create: `CheckInReminder/Assets/Animations/Gate/*.png`
- Modify: `CheckInReminder/CheckInReminder.csproj`
- Create: `CheckInReminder/AnimationSequence.cs`
- Modify: `CheckInReminder/UiAssets.cs`
- Create: `CheckInReminder.Tests/AnimationSequenceTests.cs`

**Interfaces:**
- Produces: `AnimationSequence.LoadEmbedded(prefix, framesPerSecond, loop)` returning ordered frames and duration.

- [x] **Step 1: Write failing tests for numeric resource ordering, one-shot duration, and loop frame selection.**
- [x] **Step 2: Run tests and verify failure from the missing animation sequence.**
- [x] **Step 3: Extract both videos at 12 FPS, chroma-key green to alpha, trim transparent margins consistently, and embed numbered PNGs.**
- [x] **Step 4: Implement resource loading and elapsed-time frame selection; run all tests.**

### Task 4: First-layer animated edge reminder

**Files:**
- Create: `CheckInReminder/ScreenEdge.cs`
- Create: `CheckInReminder/AnimatedReminderForm.cs`
- Modify: `CheckInReminder/ReminderApplicationContext.cs`
- Remove: `CheckInReminder/ReminderBannerForm.cs`
- Remove: `CheckInReminder/Assets/ReminderBanner.jpg`
- Modify: `CheckInReminder.Tests/UiThemeTests.cs`

**Interfaces:**
- Consumes: the one-shot edge sequence and a `ReminderKind`.
- Produces: one completion callback carrying whether the check-in button was clicked.

- [x] **Step 1: Replace obsolete banner contract tests with failing geometry and copy-contract tests.**
- [x] **Step 2: Run tests and verify red.**
- [x] **Step 3: Implement randomized edge placement, rotated/mirrored frames, speech bubble, NoActivate behavior, and automatic close at sequence end.**
- [x] **Step 4: Connect the application context and queue; run all tests.**

### Task 5: Animated centered second gate

**Files:**
- Modify: `CheckInReminder/EveningConfirmForm.cs`
- Remove: `CheckInReminder/Assets/EveningConfirm.jpg`
- Modify: `CheckInReminder.Tests/UiThemeTests.cs`

**Interfaces:**
- Consumes: looping gate sequence.
- Preserves: callback meanings for “真的” and “假的”, and non-closable-until-choice behavior.

- [x] **Step 1: Add failing copy and loop-mode contract tests.**
- [x] **Step 2: Run tests and verify red.**
- [x] **Step 3: Replace the static image with a 12 FPS transparent animation player that restarts at frame zero.**
- [x] **Step 4: Run all tests and verify green.**

### Task 6: Settings UI, documentation, and release verification

**Files:**
- Modify: `CheckInReminder/SettingsForm.cs`
- Modify: `README.md`
- Modify: `VERIFICATION.md`
- Modify: `AGENTS.md`
- Create: `release/打个卡-animated/打个卡.exe`

**Interfaces:**
- Consumes: break settings and the existing save callback.
- Produces: a visible break toggle, 09:00–18:00 time controls, and 1/1.5/2/3-hour interval selector.

- [x] **Step 1: Add failing settings-default/theme contract tests.**
- [x] **Step 2: Run tests and verify red.**
- [x] **Step 3: Expand the settings layout without clipping and bind all four break fields.**
- [x] **Step 4: Update public behavior and manual Windows checks in documentation.**
- [x] **Step 5: Run Release tests and build; require 0 failures, 0 warnings, and 0 errors.**
- [x] **Step 6: Publish the required single-file executable and verify the output contains exactly the distributable EXE.**

