# MSIX package (Microsoft Store)

`AppxManifest.xml` is the reference manifest for the Store package. The package itself is produced
with the MSIX Packaging Tool from the MSI, which regenerates its own manifest on every run, so compare
against this file (or replace the generated one with it) before submitting.

## Required compared to the tool's output

- **No `Microsoft.WindowsAppRuntime` dependency.** The app is published self-contained and doesn't use
  the Windows App SDK; the tool may add this dependency, which then fails to install where the runtime
  is missing.
- **`windows.appExecutionAlias` with `Alias="OpenNetMeter.exe"`.** "Start with Windows" registers a
  Task Scheduler task. Under MSIX it must launch the app through this alias
  (`%LOCALAPPDATA%\Microsoft\WindowsApps\OpenNetMeter.exe`): launching the exe by its path runs it
  without package identity (separate AppData, so a second database and settings file), and that path
  changes with every package version. The alias must match `ExecutionAlias` in
  `OpenNetMeter/Services/WindowsStartupRegistrationService.cs`.
- **Restricted capabilities** `runFullTrust` and `allowElevation` (ETW kernel capture needs admin).

Update `Version` in `<Identity>` for each submission.

## Testing locally without a certificate

With Developer Mode on, register the unpacked layout (the package contents plus this manifest):

```powershell
Get-AppxPackage Ashfaaq18.OpenNetMeter | Remove-AppxPackage
Add-AppxPackage -Register .\layout\AppxManifest.xml
```

Packaged data lives under
`%LOCALAPPDATA%\Packages\Ashfaaq18.OpenNetMeter_dvd0cr9yr1vx2\LocalCache\Local\OpenNetMeter`.
If `%LOCALAPPDATA%\OpenNetMeter` appears, something launched the app without package identity.

Never commit signing certificates (`.pfx`/`.p12`) or their passwords.
