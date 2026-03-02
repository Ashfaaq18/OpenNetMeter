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
- Avalonia app exists and runs with Summary/History/Settings screens.
- Linux publish path exists via `scripts/publish-avalonia-rc.ps1` (`linux-x64` supported).
- WPF app remains buildable in parallel.

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
`Use AVALONIA_MIGRATION_PLAN.md as source of truth. Assume phases 1-12 + 11.1 are complete. First read git status and latest commits, then continue with small, reviewable slices only.`

## Recent Commits
- `46b6fc3` (2026-03-02): connect the sample realtime points to livecharts
- `1717fa7` (2026-03-02): making the ui look more like the wpf version.
