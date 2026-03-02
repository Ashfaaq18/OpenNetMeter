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

## Phase 11
- Port Avalonia Summary screen + graph rendering.

## Phase 12
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

## Notes for New Chat Continuation
If context resets, start from:
- This file
- Current completed phase number
- `git status` + latest commit
Then continue with the next uncompleted phase using the same small-slice rules.
