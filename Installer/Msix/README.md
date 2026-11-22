# MSIX package (Microsoft Store)

## What this is

The Store package is built from the release MSI: `scripts/build-msix-from-msi.ps1` unpacks the MSI
(without installing it), lays the app files out at the package root, adds the logos in `Assets/` and
`AppxManifest.template.xml` (with the version taken from the MSI), and packs the result with `makeappx`.
The package is unsigned on purpose: the Microsoft Store signs it on submission.

## Build

```powershell
.\scripts\build-avalonia-msi.ps1 -Runtime win-x64 -Configuration Release
.\scripts\build-msix-from-msi.ps1                 # or: -MsiPath <path to the msi>
```

Output: `Installer\bin\msix\OpenNetMeter-<version>.0-x64.msix` (plus the `layout` folder it was packed
from and a `work` folder with the extraction log). Requires the Windows SDK (`makeappx.exe` and
`makepri.exe`). The script checks the rendered manifest before packing: the version, that every
`Executable` exists, no package dependencies and no `VFS\` paths.

The logos in `Assets/` are committed. `generate-assets.ps1` regenerates them from `Resources/AppIcon.ico`
and only needs to be run when the app icon changes.

## What the template guarantees

- **No `Microsoft.WindowsAppRuntime` dependency.** The app is published self-contained and doesn't use
  the Windows App SDK; the MSIX Packaging Tool may add this dependency, which then fails to install where
  the runtime is missing.
- **`windows.appExecutionAlias` with `Alias="OpenNetMeter.exe"`.** "Start with Windows" registers a
  Task Scheduler task. Under MSIX it must launch the app through this alias
  (`%LOCALAPPDATA%\Microsoft\WindowsApps\OpenNetMeter.exe`): launching the exe by its path runs it
  without package identity (separate AppData, so a second database and settings file), and that path
  changes with every package version. The alias must match `ExecutionAlias` in
  `OpenNetMeter/Services/WindowsStartupRegistrationService.cs`.
- **Restricted capabilities** `runFullTrust` and `allowElevation` (ETW kernel capture needs admin).
- **Files at the package root** (`OpenNetMeter.exe`, not under `VFS\ProgramFilesX64`). The package is
  not built by an installer, so there is no virtual file system layer to map.
- **No desktop shortcut.** The Start menu entry comes from the package; the MSI creates its own shortcuts.

## App behaviour when packaged

- The GitHub update check is hidden in Settings (`AppPackage.IsPackaged`): updates come from the Store.
- Where the data lives depends on whether `%LOCALAPPDATA%\OpenNetMeter` already exists when the packaged
  app first runs (Windows only redirects folders the package creates itself under AppData):
  - **It exists** (an MSI install is or was on the PC): the packaged app uses that folder directly, so it
    shares history and settings with the MSI install. Uninstalling the MSI deletes this folder (its
    `CleanupAppData` action), which also removes the Store app's history. Seen with 0.16.1.0 on Windows 11.
  - **It doesn't exist**: the folder is created inside the package, at
    `%LOCALAPPDATA%\Packages\Ashfaaq18.OpenNetMeter_dvd0cr9yr1vx2\LocalCache\Local\OpenNetMeter`, and it is
    removed when the package is uninstalled.
- MSIX can't run anything on uninstall, so a "Start with Windows" task stays behind when the package is
  removed. It points at the removed alias and fails silently at logon.

## Versions

The package version is the MSI's `ProductVersion` plus `.0` (for example `0.16.1` becomes `0.16.1.0`);
the Store reserves the fourth part. Every package published to the Store needs a higher version than the
last published one. The version itself is set in `Directory.Build.props`.

## Partner Center

Product identity (must match the template's `<Identity>`; see Partner Center → Product identity):
Name `Ashfaaq18.OpenNetMeter`, Publisher `CN=472488E8-5ADB-4CA0-B9F6-30BB0DED678A`, PublisherDisplayName
`Ashfaaq18`, package family name `Ashfaaq18.OpenNetMeter_dvd0cr9yr1vx2`.

Store ID `9NT6DDJJ8D4W`; listing: https://apps.microsoft.com/detail/9NT6DDJJ8D4W

Justifications for the restricted capabilities, ready to paste:

- **allowElevation**: "OpenNetMeter shows how much data each app uses. It reads this from the Windows
  kernel network trace (ETW), and Windows only lets an administrator start a kernel trace session, so
  the app runs elevated. It does not change system settings."
- **runFullTrust**: "OpenNetMeter is a Win32 desktop app (.NET) packaged as MSIX."

## Testing locally

Without a certificate, with Developer Mode on, register the unpacked layout:

```powershell
# Close OpenNetMeter first: the packaged and installed copies share the capture session
# and the single-instance lock.
.\scripts\build-msix-from-msi.ps1 -Register
```

The `layout` folder must stay in place while the package is registered. Don't toggle "Start with
Windows" while testing: the task name is shared with the MSI install. To unregister:

```powershell
Get-AppxPackage Ashfaaq18.OpenNetMeter | Remove-AppxPackage
```

On a PC with an MSI install, the packaged app reads and writes the real `%LOCALAPPDATA%\OpenNetMeter`
data (see above): back up its files before testing and restore them afterwards.

Never commit signing certificates (`.pfx`/`.p12`) or their passwords.
