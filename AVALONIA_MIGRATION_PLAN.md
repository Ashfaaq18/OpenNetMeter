# Incremental, Review-Friendly Avalonia Migration Plan

## Summary
Migrate from WPF to Avalonia using very small, end-to-end phases that are easy to review.
Each phase must keep both WPF and the in-progress Avalonia path buildable.

## Review Rules
1. Target 3-6 files changed per phase.
2. One concern per phase.
3. Every phase ends buildable/testable.
4. Keep WPF green while adding Avalonia.
5. Provide short diff summary + verification notes after each phase.

## Long-term Goal
Run the Avalonia app on Windows, Linux, and macOS.

## Public Interfaces Introduced/Planned
- `INetworkCaptureService`
- `IStartupRegistrationService`
- `IProcessIconService`
- `IUiDispatcher`
- `UiVisibility`

## Phase 1 (Completed)
- Add `OpenNetMeter.PlatformAbstractions` project.
- Add interface/type shells.
- Add project to solution.
- No behavior changes.

## Phase 2 (Completed)
- Add `OpenNetMeter.Core` project.
- Move only `ConfirmationDialogVM` first.
- Replace WPF `Visibility` in moved code with `UiVisibility`.
- Add temporary WPF mapping adapter.

## Phase 3 (Completed)
- Introduce dispatcher abstraction usage (`IUiDispatcher`) in one focused flow.
- Keep behavior identical.

## Phase 4 (Completed)
- Extract startup registration logic to `IStartupRegistrationService` Windows implementation.

## Phase 5 (Completed)
- Wrap current Windows network capture behind `INetworkCaptureService`.

## Phase 6 (Completed)
- Wrap process icon loading behind `IProcessIconService`.

## Phase 7 (Completed)
- Add `OpenNetMeter.Avalonia` app skeleton (launches, minimal shell only).

## Phase 8 (Completed)
- Port Avalonia main shell/tab navigation bound to shared VM.

## Phase 9 (Completed)
- Port Avalonia Settings screen.

## Phase 10 (Completed)
- Port Avalonia History screen.

## Phase 11 (Completed)
- Port Avalonia Summary screen + graph rendering.

## Phase 11.1 (Completed)
- Add Linux (`linux-x64`) Avalonia RC publish support for WSL validation.

## Phase 12 (Completed)
- Stabilization + Windows Avalonia release candidate (WPF still buildable).

## Phase 13 (Completed)
- Port the Avalonia desktop shell to near-WPF parity for the mini widget and surrounding window behavior.
- Completed in small sub-slices, but now tracked as one completed phase for handoff simplicity.
- Includes:
  - Avalonia mini widget window
  - live Summary data wiring
  - drag/pin/context menu behavior
  - widget visibility and appearance sync with settings
  - widget and main-window geometry persistence + out-of-bounds recovery
  - Windows tray icon/menu
  - hide-to-tray behavior
  - Windows widget z-order maintenance above the taskbar
  - visual polish closer to the WPF widget

## Cross-Platform Follow-up
After core tabs are stable on Windows Avalonia:
1. Implement Linux backend(s) for capture/startup/tray-related features.
2. Implement macOS backend(s) for capture/startup/tray-related features.
3. Enable platform capability flags and converge to feature parity.

## Verification Gate Per Phase
1. `dotnet build OpenNetMeter.sln --configuration Debug`
2. `dotnet test OpenNetMeter.Tests/OpenNetMeter.Tests.csproj --configuration Debug`
3. Short manual smoke checklist for only the changed surface.

## New Chat Handoff (Context Reset Safe)
Use this checklist when starting a fresh chat:
1. Share this file first: `AVALONIA_MIGRATION_PLAN.md`
2. Share repo status:
   - `git status --short`
   - `git log -5 --oneline`
3. State your intent in one line:
   - example: `Continue from Phase 12; next focus is visual parity / Linux testing / packaging`

### Current Snapshot
- Completed phases: `1` through `12`, plus sub-phase `11.1`.
- Completed Phase `13` for Avalonia desktop shell / mini widget parity.
- Avalonia app exists and runs with Summary/History/Settings screens.
- Avalonia shell now includes:
  - in-window About modal
  - Windows mini widget
  - settings-driven widget visibility/appearance
  - window geometry persistence
  - tray icon/menu
  - hide-to-tray
  - Windows widget z-order maintenance
- Avalonia parity work already landed for:
  - settings persistence through `settings.json`
  - settings-driven speed format / network target wiring
  - summary `Usage From` card logic
  - process icons in Summary and History
  - centralized Avalonia window colors/resources
- Windows-only callsites are isolated/annotated for Windows-specific services.
- Avalonia app-side logging/event viewer hooks were added for exception and catch-path diagnostics.
- Linux publish path exists via `scripts/publish-avalonia-rc.ps1` (`linux-x64` supported).
- WPF app remains buildable in parallel.
- Current Linux/macOS status:
  - Avalonia app can build/run
  - mini widget/network capture/process icon/tray behavior are still Windows-first
  - non-Windows uses placeholders where platform backends are not implemented yet

### Known Local Friction
- Occasional `CS2012` / `BG1002` / copy-lock errors can appear when multiple `dotnet` operations overlap.
- If hit, run commands sequentially and clean build artifacts for the affected project.

### Verification Commands
1. `dotnet build OpenNetMeter.sln --configuration Debug`
2. `dotnet test OpenNetMeter.Tests/OpenNetMeter.Tests.csproj --configuration Debug`
3. Avalonia run:
   - `dotnet run --project .\OpenNetMeter.Avalonia\OpenNetMeter.Avalonia.csproj`
4. Linux RC publish:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\publish-avalonia-rc.ps1 -Runtime linux-x64 -Configuration Debug`

### Copy/Paste Starter for New Chat
`Use AVALONIA_MIGRATION_PLAN.md as source of truth. Assume phases 1-13 + 11.1 are complete. First read git status and latest commits, then continue with small, reviewable slices only.`

## Recent Commits
- `6312c6f` (2026-03-25): `feat(avalonia): polish mini widget visuals to match WPF`
- `5c70dea` (2026-03-25): `feat(avalonia): keep mini widget above taskbar on windows`
- `c295eb7` (2026-03-25): `feat(avalonia): hide main window to tray and exit only from tray`
- `bb9f197` (2026-03-25): `feat(avalonia): add system tray icon and menu actions`
- `c99a10d` (2026-03-25): `feat(avalonia): persist main window geometry and recover off-screen windows`
