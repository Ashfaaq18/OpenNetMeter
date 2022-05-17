
# OpenNetMeter

A simple program to monitor your network/data usage. Made for the average windows user.

## Description

This program provides the following features,

- network speed
- current connection's session data usage 
- total data usage since the detection of a new connection.
- table of all the individual processes which consumed this data.
- A system tray icon to show the current session data usage and network speeds.
- A toolbar in the Taskbar to show the network speed. 

## Installation

1. Download and Install [.NET Desktop Runtime 5](https://dotnet.microsoft.com/en-us/download/dotnet/5.0) x64 bit version
2. Download the latest release from this repository and simply extract it.
3. Run in Admin mode (this is necessary).
4. Optional : To add this as a startup program, go to the settings tab and tick the checkbox.

## Uninstallation

1. Untick the "Start program on windows startup" from the settings page (this is to remove the program from the startup records).
2. Untick the "Show Network speed from task bar" from the settings page  (this is to remove the dll from the registry).
3. Simply delete the folder.
    
## Usage/Examples

### Summary tab

![4](https://user-images.githubusercontent.com/27722888/164838606-8b4144aa-1f51-4d5e-891d-a7a98e6e4fed.png)

### Detailed tab

![5](https://user-images.githubusercontent.com/27722888/164840174-b917c3ed-7cc9-4c2c-aaab-3fe5d7064265.png)

### Darkmode

![image](https://user-images.githubusercontent.com/27722888/164841661-a104f40c-442c-4c87-9408-da1793d25b77.png)

### Network speed mini widget

![MiniWidget_demo1](https://user-images.githubusercontent.com/27722888/168587020-10bb15cc-7176-4d46-a4e9-8fdaf380bbe5.gif)

### System tray & TaskBarSpeed (Discontinued since v0.9.0)

These are replaced by the mini widget

![TrayPopup_GIF1](https://user-images.githubusercontent.com/27722888/151661088-71349a72-f687-48be-ad33-805f7bf6771d.gif)

![DeskBand_GIF1](https://user-images.githubusercontent.com/27722888/153745070-669027d8-56eb-4982-b009-1be23e5b5d51.gif)
