# BongoCat-Style White Bear Rig Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace whole-image desktop-pet feedback with BongoCat-style keyboard-hand motion, mouse-hand tracking, and mouse-button presses driven by real global input.

**Architecture:** A Win32 global input service writes a fixed-size input state without logging. A pure motion model maps transient virtual-key and pointer state to smoothed rig poses. A renderer deterministically extracts layers from the user-supplied white-bear JPG, removes the original arm pixels from the base, and composites transformed mouse and keyboard arms into the existing layered WinForms overlay.

**Tech Stack:** C# 14, .NET 10, WinForms, Win32 `WH_KEYBOARD_LL` / `WH_MOUSE_LL`, System.Drawing, MSTest.

**Spec:** `docs/superpowers/specs/2026-09-05-bongocat-style-white-bear-rig-design.md`

## Global Constraints

- Target Windows 10/11 x64 only.
- Use `./.dotnet/dotnet.exe`; the machine-wide `dotnet` has no suitable SDK.
- Publish `win-x64`, self-contained, single-file, with trimming disabled.
- Use only `CheckInReminder/Assets/DesktopPet/white-bear-typing.jpg` for the white-bear visual; do not use generated images or BongoCat model assets.
- Key identity may exist transiently in memory only for local motion mapping; never log, persist, upload, or expose it as text.
- Preserve transparent, topmost, no-activate, click-through overlay behavior.
- Do not run unattended real shutdown or restart tests.
- Preserve unrelated working-tree files and changes.

## File Map

- Create `CheckInReminder/DesktopInputState.cs`: allocation-free mutable input state plus immutable snapshots.
- Replace `CheckInReminder/KeyboardHookService.cs` with `CheckInReminder/GlobalInputService.cs`: keyboard and mouse Win32 hooks.
- Create `CheckInReminder/KeyboardTargetMapper.cs`: virtual-key to keyboard-perspective target mapping.
- Create `CheckInReminder/DesktopPetMotionModel.cs`: BongoCat-style damping and pose state.
- Replace `CheckInReminder/DesktopPetArtwork.cs` with `CheckInReminder/WhiteBearRigRenderer.cs`: source extraction, hole reconstruction, and layer compositing.
- Modify `CheckInReminder/DesktopPetController.cs`: poll snapshots, step motion, render rig, and stop at rest.
- Modify `CheckInReminder/ReminderApplicationContext.cs`: own the new input service and report install failure.
- Modify `CheckInReminder/PetOverlayForm.cs`: keep current presentation contract; no rendering policy moves into the form.
- Modify `CheckInReminder.Tests/DesktopPetControllerTests.cs`: motion and lifecycle integration tests.
- Create `CheckInReminder.Tests/DesktopInputStateTests.cs`: input-state and multi-key ordering tests.
- Create `CheckInReminder.Tests/KeyboardTargetMapperTests.cs`: representative keyboard geometry tests.
- Create `CheckInReminder.Tests/DesktopPetMotionModelTests.cs`: damping, bounds, and return-to-rest tests.
- Create `CheckInReminder.Tests/WhiteBearRigRendererTests.cs`: source integrity and independent hand movement tests.
- Create `THIRD_PARTY_NOTICES.md`: BongoCat MIT attribution for the translated event flow and damping formula.
- Modify `README.md`: input privacy, controls, and manual verification instructions.

---

### Task 1: Fixed-Size Input State and Active-Key Ordering

**Files:**
- Create: `CheckInReminder/DesktopInputState.cs`
- Create: `CheckInReminder.Tests/DesktopInputStateTests.cs`

**Interfaces:**
- Produces: `DesktopInputState.UpdateKey(int virtualKey, bool pressed)`
- Produces: `DesktopInputState.UpdatePointer(int x, int y)`
- Produces: `DesktopInputState.UpdateMouseButton(DesktopMouseButton button, bool pressed)`
- Produces: `DesktopInputSnapshot DesktopInputState.ReadSnapshot()`
- Produces: `readonly record struct DesktopInputSnapshot(Point CursorScreen, bool LeftButtonDown, bool RightButtonDown, int ActiveVirtualKey, long Version)`

- [ ] **Step 1: Write the failing state tests**

```csharp
[TestMethod]
public void ActiveKey_FallsBackToPreviouslyHeldKeyWhenLatestIsReleased()
{
    var state = new DesktopInputState();
    state.UpdateKey(0x41, true); // A
    state.UpdateKey(0x44, true); // D
    Assert.AreEqual(0x44, state.ReadSnapshot().ActiveVirtualKey);

    state.UpdateKey(0x44, false);
    Assert.AreEqual(0x41, state.ReadSnapshot().ActiveVirtualKey);
    state.UpdateKey(0x41, false);
    Assert.AreEqual(0, state.ReadSnapshot().ActiveVirtualKey);
}

[TestMethod]
public void Snapshot_CoalescesPointerAndTracksButtonsWithoutText()
{
    var state = new DesktopInputState();
    state.UpdatePointer(100, 200);
    state.UpdatePointer(320, 480);
    state.UpdateMouseButton(DesktopMouseButton.Left, true);

    var snapshot = state.ReadSnapshot();
    Assert.AreEqual(new Point(320, 480), snapshot.CursorScreen);
    Assert.IsTrue(snapshot.LeftButtonDown);
    Assert.IsFalse(snapshot.RightButtonDown);
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run: `./.dotnet/dotnet.exe test ./CheckInReminder.Tests/CheckInReminder.Tests.csproj -c Release --filter DesktopInputStateTests`

Expected: compile failure because `DesktopInputState`, `DesktopInputSnapshot`, and `DesktopMouseButton` do not exist.

- [ ] **Step 3: Implement the fixed-size state**

Use two fixed arrays of length 256: one `int[]` for pressed flags and one `long[]` for press order. `UpdateKey` ignores virtual keys outside `1..255`, uses `Volatile.Write`, and stores an incremented sequence number only on a transition from up to down. `ReadSnapshot` scans the 256 entries and returns the pressed key with the largest order. Pointer coordinates, mouse-button bits, and version use `Interlocked`/`Volatile`; no key name or character conversion is added.

- [ ] **Step 4: Run the state tests and verify GREEN**

Run the Step 2 command.

Expected: all `DesktopInputStateTests` pass.

- [ ] **Step 5: Commit only this task**

```powershell
git add -- CheckInReminder/DesktopInputState.cs CheckInReminder.Tests/DesktopInputStateTests.cs
git commit -m "feat: track transient desktop input state"
```

### Task 2: Keyboard Geometry and BongoCat-Style Motion Model

**Files:**
- Create: `CheckInReminder/KeyboardTargetMapper.cs`
- Create: `CheckInReminder/DesktopPetMotionModel.cs`
- Create: `CheckInReminder.Tests/KeyboardTargetMapperTests.cs`
- Create: `CheckInReminder.Tests/DesktopPetMotionModelTests.cs`

**Interfaces:**
- Consumes: `DesktopInputSnapshot`
- Produces: `bool KeyboardTargetMapper.TryMap(int virtualKey, out PointF normalizedTarget)`
- Produces: `readonly record struct DesktopPetRigPose(PointF MouseOffset, float MouseRotationDegrees, float MousePress, PointF KeyboardTarget, float KeyboardPress, bool IsAtRest)`
- Produces: `DesktopPetRigPose DesktopPetMotionModel.Step(DesktopInputSnapshot input, Rectangle workingArea, TimeSpan elapsed)`

- [ ] **Step 1: Write failing keyboard geometry tests**

```csharp
[DataTestMethod]
[DataRow(0x51, 0.10f, 0.28f)] // Q: upper-left letter area
[DataRow(0x47, 0.50f, 0.55f)] // G: center
[DataRow(0x4D, 0.78f, 0.80f)] // M: lower-right letter area
[DataRow(0x20, 0.50f, 0.92f)] // Space: bottom-center
public void RepresentativeKeys_MapInsideExpectedKeyboardRegion(
    int virtualKey, float expectedX, float expectedY)
{
    Assert.IsTrue(KeyboardTargetMapper.TryMap(virtualKey, out var target));
    Assert.AreEqual(expectedX, target.X, 0.12f);
    Assert.AreEqual(expectedY, target.Y, 0.12f);
    Assert.IsTrue(target.X is >= 0 and <= 1);
    Assert.IsTrue(target.Y is >= 0 and <= 1);
}
```

- [ ] **Step 2: Write failing motion tests**

```csharp
[TestMethod]
public void MouseMotion_ApproachesTargetMonotonicallyAndStaysBounded()
{
    var model = new DesktopPetMotionModel();
    var input = new DesktopInputSnapshot(new Point(1920, 1080), false, false, 0, 1);
    var area = new Rectangle(0, 0, 1920, 1080);

    var first = model.Step(input, area, TimeSpan.FromMilliseconds(16.667));
    var second = model.Step(input, area, TimeSpan.FromMilliseconds(16.667));

    Assert.IsGreaterThan(first.MouseOffset.X, second.MouseOffset.X);
    Assert.IsLessThanOrEqualTo(1f, second.MouseOffset.X);
    Assert.IsLessThanOrEqualTo(1f, second.MouseOffset.Y);
}

[TestMethod]
public void KeyboardPress_MapsActiveKeyAndReturnsToRestAfterRelease()
{
    var model = new DesktopPetMotionModel();
    var area = new Rectangle(0, 0, 1920, 1080);
    var pressed = model.Step(
        new DesktopInputSnapshot(Point.Empty, false, false, 0x47, 1),
        area,
        TimeSpan.FromMilliseconds(16.667));

    Assert.IsGreaterThan(0f, pressed.KeyboardPress);
    Assert.IsFalse(pressed.IsAtRest);

    DesktopPetRigPose released = default;
    for (var frame = 0; frame < 120; frame++)
    {
        released = model.Step(
            new DesktopInputSnapshot(Point.Empty, false, false, 0, 2),
            area,
            TimeSpan.FromMilliseconds(16.667));
    }

    Assert.IsTrue(released.IsAtRest);
}
```

- [ ] **Step 3: Run the mapper and motion tests and verify RED**

Run: `./.dotnet/dotnet.exe test ./CheckInReminder.Tests/CheckInReminder.Tests.csproj -c Release --filter "KeyboardTargetMapperTests|DesktopPetMotionModelTests"`

Expected: compile failure because the mapper, model, and pose do not exist.

- [ ] **Step 4: Implement virtual-key rows and perspective-neutral targets**

Implement literal row definitions for digits, `QWERTYUIOP`, `ASDFGHJKL`, `ZXCVBNM`, Space, Enter, Backspace, Tab, Shift, Control, Alt, Escape, and F1-F12. Each row returns normalized logical keyboard coordinates. Unknown keys return `false`; the motion model retains the last valid target only while that key remains active.

- [ ] **Step 5: Implement BongoCat-equivalent damping**

Use the reference formula:

```csharp
var frameUnits = elapsed.TotalMilliseconds / (1000d / 60d);
var alpha = 1d - Math.Pow(0.75d, frameUnits);
current += (target - current) * alpha;
```

Normalize cursor coordinates against the supplied working area into `[-1, 1]`, clamp both axes, map left/right mouse buttons to press/tilt targets, and set `IsAtRest` only when mouse and keyboard values are within `0.002f` of their targets and no button/key is down.

- [ ] **Step 6: Run the mapper and motion tests and verify GREEN**

Run the Step 3 command.

Expected: all mapper and motion tests pass.

- [ ] **Step 7: Commit only this task**

```powershell
git add -- CheckInReminder/KeyboardTargetMapper.cs CheckInReminder/DesktopPetMotionModel.cs CheckInReminder.Tests/KeyboardTargetMapperTests.cs CheckInReminder.Tests/DesktopPetMotionModelTests.cs
git commit -m "feat: map desktop input to white bear rig poses"
```

### Task 3: Dual Win32 Global Input Hooks

**Files:**
- Create: `CheckInReminder/GlobalInputService.cs`
- Delete: `CheckInReminder/KeyboardHookService.cs`
- Modify: `CheckInReminder.Tests/WindowBehaviorTests.cs`

**Interfaces:**
- Consumes: `DesktopInputState`
- Produces: `readonly record struct GlobalInputInstallResult(bool Success, int Win32Error)`
- Produces: `GlobalInputInstallResult GlobalInputService.Install()`
- Produces: `bool GlobalInputService.IsInstalled`
- Produces: `event EventHandler GlobalInputService.InputAvailable`
- Produces: `void GlobalInputService.Dispose()`

- [ ] **Step 1: Add a hook lifecycle contract test**

Use reflection, as existing window behavior tests do, to verify `GlobalInputService` owns two distinct hook handles after a test-only injected installer succeeds, and that `Dispose` invokes both unhook delegates exactly once. The injected constructor is production-useful dependency injection:

```csharp
internal GlobalInputService(
    DesktopInputState state,
    Func<int, Delegate, IntPtr> installHook,
    Func<IntPtr, bool> uninstallHook)
```

The public/internal normal constructor supplies the real Win32 functions.

- [ ] **Step 2: Run the lifecycle test and verify RED**

Run: `./.dotnet/dotnet.exe test ./CheckInReminder.Tests/CheckInReminder.Tests.csproj -c Release --filter GlobalInputService`

Expected: compile or reflection failure because `GlobalInputService` does not exist.

- [ ] **Step 3: Implement both low-level hooks**

Handle keyboard messages `WM_KEYDOWN`, `WM_SYSKEYDOWN`, `WM_KEYUP`, and `WM_SYSKEYUP` by reading only `KBDLLHOOKSTRUCT.vkCode`. Handle mouse messages `WM_MOUSEMOVE`, `WM_LBUTTONDOWN/UP`, and `WM_RBUTTONDOWN/UP` by reading `MSLLHOOKSTRUCT.pt`. Each callback updates `DesktopInputState`, raises `InputAvailable` with `EventArgs.Empty` on the installing UI thread, and immediately calls `CallNextHookEx`; subscribers may only wake a timer and must not render inside the callback. Install both hooks with `GetModuleHandle(null)` and thread id `0`; if either install fails, capture `Marshal.GetLastWin32Error()`, unhook the other handle, and return failure.

- [ ] **Step 4: Run the lifecycle test and verify GREEN**

Run the Step 2 command.

Expected: lifecycle test passes without installing a real system hook.

- [ ] **Step 5: Commit only this task**

```powershell
git add -- CheckInReminder/GlobalInputService.cs CheckInReminder/KeyboardHookService.cs CheckInReminder.Tests/WindowBehaviorTests.cs
git commit -m "feat: capture transient keyboard and mouse input"
```

### Task 4: Deterministic White-Bear Layer Extraction and Rendering

**Files:**
- Create: `CheckInReminder/WhiteBearRigRenderer.cs`
- Delete: `CheckInReminder/DesktopPetArtwork.cs`
- Create: `CheckInReminder.Tests/WhiteBearRigRendererTests.cs`

**Interfaces:**
- Consumes: embedded `Assets/DesktopPet/white-bear-typing.jpg`
- Consumes: `DesktopPetRigPose`
- Produces: `WhiteBearRigRenderer.Load()`
- Produces: `Size WhiteBearRigRenderer.FrameSize`
- Produces: `Bitmap WhiteBearRigRenderer.Render(DesktopPetRigPose pose)`; caller owns returned bitmap
- Produces: `void WhiteBearRigRenderer.Dispose()`

- [ ] **Step 1: Write failing source-integrity and independent-motion tests**

```csharp
[TestMethod]
public void Renderer_PreservesSourceAspectAndTransparentExterior()
{
    using var renderer = WhiteBearRigRenderer.Load();
    using var frame = renderer.Render(DesktopPetRigPose.Rest);
    Assert.AreEqual(2400d / 1792d, (double)frame.Width / frame.Height, 0.01);
    Assert.AreEqual(0, frame.GetPixel(0, 0).A);
}

[TestMethod]
public void MouseAndKeyboardPosesMoveIndependentPixelRegions()
{
    using var renderer = WhiteBearRigRenderer.Load();
    using var idle = renderer.Render(DesktopPetRigPose.Rest);
    using var mouse = renderer.Render(DesktopPetRigPose.Rest with {
        MouseOffset = new PointF(1, -1), MousePress = 1
    });
    using var keyboard = renderer.Render(DesktopPetRigPose.Rest with {
        KeyboardTarget = new PointF(0.8f, 0.7f), KeyboardPress = 1
    });

    var mouseBounds = DifferenceBounds(idle, mouse);
    var keyboardBounds = DifferenceBounds(idle, keyboard);
    Assert.IsLessThan(mouseBounds.Right, keyboardBounds.Left);
    Assert.IsGreaterThan(20, mouseBounds.Width);
    Assert.IsGreaterThan(20, keyboardBounds.Width);
}
```

Add a SHA-256 assertion that the embedded JPG bytes equal `A20BB054864E32BD60F356F1931171D97B3CC79C6CCF9DE25C3B8A2E6C429782`.

- [ ] **Step 2: Run renderer tests and verify RED**

Run: `./.dotnet/dotnet.exe test ./CheckInReminder.Tests/CheckInReminder.Tests.csproj -c Release --filter WhiteBearRigRendererTests`

Expected: compile failure because `WhiteBearRigRenderer` and `DesktopPetRigPose.Rest` do not exist.

- [ ] **Step 3: Build 600×448 source and connected-background alpha**

Reuse the proven premultiplied-alpha connected-border removal from `DesktopPetArtwork`, preserving the exact source hash. Add `DesktopPetRigPose.Rest` with zero mouse offset/rotation/press, keyboard target at `(0.64f, 0.34f)`, zero keyboard press, and `IsAtRest = true`.

- [ ] **Step 4: Extract and reconstruct the mouse arm**

Use a normalized `GraphicsPath` mask around source-space points `(0.17,0.39)`, `(0.34,0.49)`, `(0.31,0.62)`, `(0.08,0.61)`, `(0.08,0.48)`. Extract those pixels into `mouseArm`. On `baseLayer`, clear the mask, reconstruct transparent space above the desk boundary, fill the desk region with sampled gray from `(0.18,0.75)`, and redraw the two existing black desk/mouse-pad boundary segments using colors sampled from the original. Render the arm around pivot `(0.31,0.48)` with offsets limited to ±12 px X, ±8 px Y and rotation limited to ±2.5 degrees; button press adds 5 px Y.

- [ ] **Step 5: Extract and reconstruct the keyboard arm**

Use a normalized `GraphicsPath` mask around `(0.62,0.34)`, `(0.77,0.34)`, `(0.79,0.69)`, `(0.64,0.66)`. Extract those pixels into `keyboardArm`; fill the removed inner-body region white while preserving the outer body outline outside the mask. Convert normalized logical key targets into the source keyboard quadrilateral with corners `(0.39,0.55)`, `(0.83,0.60)`, `(0.80,0.92)`, `(0.35,0.77)` using bilinear interpolation. Limit final arm translation to ±48 px X and 0..52 px Y at 600×448; press adds 6 px Y. Rotate around shoulder pivot `(0.66,0.35)` toward the target with a ±8 degree clamp.

- [ ] **Step 6: Composite in stable z-order**

For each frame draw `baseLayer`, then `mouseArm`, then `keyboardArm`. Use `CompositingMode.SourceOver`, high-quality bicubic interpolation, and `PixelOffsetMode.HighQuality`. Do not draw key names, new facial details, or generated textures.

- [ ] **Step 7: Run renderer tests and verify GREEN**

Run the Step 2 command.

Expected: renderer tests pass; source hash remains exact and difference regions are independent.

- [ ] **Step 8: Commit only this task**

```powershell
git add -- CheckInReminder/WhiteBearRigRenderer.cs CheckInReminder/DesktopPetArtwork.cs CheckInReminder.Tests/WhiteBearRigRendererTests.cs
git commit -m "feat: rig the provided white bear artwork"
```

### Task 5: Controller and Application Lifecycle Integration

**Files:**
- Modify: `CheckInReminder/DesktopPetController.cs`
- Modify: `CheckInReminder/ReminderApplicationContext.cs`
- Modify: `CheckInReminder/PetOverlayForm.cs`
- Modify: `CheckInReminder.Tests/DesktopPetControllerTests.cs`
- Modify: `CheckInReminder.Tests/WindowBehaviorTests.cs`

**Interfaces:**
- Consumes: `DesktopInputState`, `DesktopPetMotionModel`, `WhiteBearRigRenderer`, `GlobalInputService`
- Changes: `DesktopPetController(IPetFrameSink sink, DesktopInputState inputState)`
- Produces: `void DesktopPetController.Start()`, `void DesktopPetController.NotifyInputAvailable()`, and existing `Dispose()`

- [ ] **Step 1: Replace the old alternating-frame test with a failing real-motion test**

Create a real `DesktopInputState`, pass it to the controller, set pointer position and a key, pump WinForms events for 100 ms, and assert the fake sink receives a frame whose mouse and keyboard difference regions both changed. Release the inputs, pump for up to 1 second, and assert the last frame matches the idle frame by sampled pixel difference under `0.5`.

- [ ] **Step 2: Add a failing idle-stop test**

Expose `internal bool IsRendering` as a behavior-observable controller property. After inputs settle, assert `IsRendering` becomes false; after updating input state and calling `NotifyInputAvailable()`, assert it becomes true again. This catches both a permanently running 60 Hz timer and a stopped timer that cannot wake.

- [ ] **Step 3: Run controller tests and verify RED**

Run: `./.dotnet/dotnet.exe test ./CheckInReminder.Tests/CheckInReminder.Tests.csproj -c Release --filter DesktopPetControllerTests`

Expected: compile failure because the constructor and `Start` contract changed.

- [ ] **Step 4: Integrate the 16 ms polling/render loop**

Remove placeholder flip/offset generation and `OnKeyTapped`. The controller reads snapshots, steps the motion model with measured elapsed time, renders only when the snapshot version changes or the pose is not at rest, disposes the previously rendered owned bitmap after the sink has presented it, and stops the timer at rest. `NotifyInputAvailable()` restarts the timer without rendering, so the hook callback remains fast. Keep dedicated animation-sequence behavior only for future non-white-bear characters; white bear always uses the rig renderer.

- [ ] **Step 5: Integrate application ownership and failure status**

In `ReminderApplicationContext.ApplyDesktopPet(true)`, create one `DesktopInputState`, pass it to controller and `GlobalInputService`, subscribe `GlobalInputService.InputAvailable` to `DesktopPetController.NotifyInputAvailable`, show the overlay, install hooks, then call `controller.Start()`. On install failure, dispose the service, keep the idle frame, set a disabled tray item text to `输入监听不可用（错误 {code}）`, and show one non-blocking tray balloon. On disable/exit, unsubscribe and dispose service before controller and form. Remove `keyboardHook.KeyTapped` wiring.

- [ ] **Step 6: Run controller and lifecycle tests and verify GREEN**

Run: `./.dotnet/dotnet.exe test ./CheckInReminder.Tests/CheckInReminder.Tests.csproj -c Release --filter "DesktopPetControllerTests|WindowBehaviorTests"`

Expected: all selected tests pass.

- [ ] **Step 7: Commit only this task**

```powershell
git add -- CheckInReminder/DesktopPetController.cs CheckInReminder/ReminderApplicationContext.cs CheckInReminder/PetOverlayForm.cs CheckInReminder.Tests/DesktopPetControllerTests.cs CheckInReminder.Tests/WindowBehaviorTests.cs
git commit -m "feat: drive desktop pet from live input"
```

### Task 6: Attribution, Privacy Documentation, Full Verification, and Preview Publish

**Files:**
- Create: `THIRD_PARTY_NOTICES.md`
- Modify: `README.md`
- Output: `release/bongocat-rig/打个卡.exe`

**Interfaces:**
- Documents the behavior delivered by Tasks 1-5.

- [ ] **Step 1: Add BongoCat MIT attribution**

Add a section naming `ayangweb/BongoCat`, copyright `2025 ayangweb`, repository URL, and the complete MIT license text. State that the input-event flow and damping formula were translated; no BongoCat model or image assets are included.

- [ ] **Step 2: Update README behavior and privacy wording**

Replace “只计数、从不读取键值” with the exact behavior: the app transiently reads the virtual-key identity in memory solely to place the keyboard hand, never converts it into typed text, and never logs, saves, uploads, or transmits it. Document mouse tracking, clicks, keyboard-hand motion, click-through, the save requirement, and the Windows manual test checklist.

- [ ] **Step 3: Run the complete Release test suite**

Run: `./.dotnet/dotnet.exe test ./CheckInReminder.slnx -c Release`

Expected: zero failed tests.

- [ ] **Step 4: Build Release**

Run: `./.dotnet/dotnet.exe build ./CheckInReminder.slnx -c Release --no-restore`

Expected: exit code 0 with no compiler errors.

- [ ] **Step 5: Publish the required single file**

Run:

```powershell
./.dotnet/dotnet.exe publish ./CheckInReminder/CheckInReminder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o ./release/bongocat-rig
```

Expected: `release/bongocat-rig/打个卡.exe` is the only required runtime file.

- [ ] **Step 6: Verify artifact and preserve manual-test boundary**

Record file size and SHA-256. Do not claim global hooks, real pointer tracking, transparency, or security-software compatibility are manually verified unless the new executable is actually run after exiting the old single-instance process. Give the user exact steps: exit old tray process, launch new EXE, enable and save the pet, test pointer movement/clicks and typing in Notepad, then test click-through.

- [ ] **Step 7: Commit docs only**

```powershell
git add -- README.md THIRD_PARTY_NOTICES.md
git commit -m "docs: document interactive desktop pet privacy"
```
