# Desktop Settings Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a polished, functional desktop settings window with a scalable animated character selector.

**Architecture:** Keep persisted behavior in `AppSettings`/`SettingsService`, introduce a small immutable character catalog, and compose the WinForms UI from focused custom controls. The preview reuses the embedded Edge PNG sequence and the existing animation timeline instead of adding decorative assets.

**Tech Stack:** C# 14, .NET 10, WinForms, MSTest, embedded PNG resources

**Spec:** `docs/superpowers/specs/2026-08-30-desktop-settings-redesign.md`

## Global Constraints

- Windows 10/11 x64 only.
- Keep all existing reminder copy and scheduling semantics unchanged.
- Old configuration files must load with `white-bear` selected.
- Publish self-contained and single-file with trimming disabled.
- Do not run unattended shutdown or restart tests.

---

### Task 1: Persisted Character Catalog

**Files:**
- Create: `CheckInReminder/ReminderCharacter.cs`
- Modify: `CheckInReminder/AnimationCatalog.cs`
- Modify: `CheckInReminder/AppSettings.cs`
- Modify: `CheckInReminder/SettingsService.cs`
- Test: `CheckInReminder.Tests/SettingsServiceTests.cs`
- Test: `CheckInReminder.Tests/UiThemeTests.cs`

**Interfaces:**
- Produces: `ReminderCharacter(string Id, string DisplayName, string SequenceName, TimeSpan Duration)`
- Produces: `AnimationCatalog.Characters`, `AnimationCatalog.DefaultCharacterId`, `AnimationCatalog.FindCharacter(string)`
- Produces: `AppSettings.CharacterId`

- [ ] **Step 1: Write failing tests** proving defaults and old JSON select `white-bear`, clone preserves the selection, known IDs validate, and unknown IDs fail validation.
- [ ] **Step 2: Run** `dotnet test .\CheckInReminder.slnx -c Release` and verify the tests fail because the character API is absent.
- [ ] **Step 3: Implement the minimal catalog, setting, clone and validation behavior.**
- [ ] **Step 4: Run the test suite and verify it passes.**

### Task 2: Reusable Warm Desktop Controls

**Files:**
- Create: `CheckInReminder/RoundedPanel.cs`
- Create: `CheckInReminder/BrandButton.cs`
- Create: `CheckInReminder/ToggleSwitch.cs`
- Modify: `CheckInReminder/UiTheme.cs`
- Test: `CheckInReminder.Tests/UiThemeTests.cs`

**Interfaces:**
- Produces: rounded card painting, pill button hover/press behavior, and an accessible boolean switch.
- Produces: warm palette values used by all settings controls.

- [ ] **Step 1: Write failing theme behavior tests** for readable contrast across primary, secondary and muted surfaces.
- [ ] **Step 2: Run the focused theme tests and verify the new contract fails.**
- [ ] **Step 3: Add the warm palette and focused custom controls without changing reminder behavior.**
- [ ] **Step 4: Run all tests and verify they pass.**

### Task 3: Animated Character Selector

**Files:**
- Create: `CheckInReminder/CharacterSelectorControl.cs`
- Modify: `CheckInReminder/AnimationSequence.cs`

**Interfaces:**
- Consumes: `AnimationCatalog.Characters` and `AnimationSequence.Load(...)`.
- Produces: `SelectedCharacterId`, `ConfirmedCharacterId`, and `SelectionConfirmed`.

- [ ] **Step 1: Implement resource ownership so the preview can load and dispose the selected character sequence safely.**
- [ ] **Step 2: Build the preview card with animation, previous/next buttons, name, position label, and confirmation feedback.**
- [ ] **Step 3: Disable browsing automatically when the catalog contains one item, while retaining the controls for future characters.**
- [ ] **Step 4: Build and run tests to catch disposal or API regressions.**

### Task 4: Desktop Settings Composition and Verification

**Files:**
- Replace: `CheckInReminder/SettingsForm.cs`
- Modify: `README.md`
- Modify: `VERIFICATION.md`

**Interfaces:**
- Consumes: existing save and test-reminder callbacks.
- Produces: an 860×700 two-column settings window whose save candidate includes `CharacterId`.

- [ ] **Step 1: Compose the header, setting cards, character column and bottom actions using only functional controls.**
- [ ] **Step 2: Add opening fade/slide, button feedback, break-setting enable animation, and character confirmation feedback.**
- [ ] **Step 3: Run** `dotnet test .\CheckInReminder.slnx -c Release` and `dotnet build .\CheckInReminder.slnx -c Release`.
- [ ] **Step 4: Publish with** `dotnet publish .\CheckInReminder\CheckInReminder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false`.
- [ ] **Step 5: Inspect the settings window on Windows and record remaining manual checks in `VERIFICATION.md`.**
