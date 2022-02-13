
# OpenNetMeter

A simple program to monitor your network/data usage. Made for the average windows user.

## Description

This program provides the following features,

- network speed
- current connection's session data usage 
- total data usage since the detection of a new connection.
- table of all the individual processes which consumed this data.
- A system tray icon to show the current session data usage and network speeds.
- A toolbar in the Taskbar to show the network speed (only available for windows 10). 

## Installation

1. Download and Install [.NET Desktop Runtime 5](https://dotnet.microsoft.com/en-us/download/dotnet/5.0) x64 bit version
2. Download and Install [Visual C++ redist](https://docs.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170) x64 bit version (need this since v0.5.0)
3. Download the latest release from this repository and simply extract it.
4. Run in Admin mode (this is necessary).
5. Optional : To add this as a startup program, go to the settings tab and tick the checkbox.

## Uninstallation

1. Untick the "Start program on windows startup" from the settings page (this is to remove the program from the startup records).
2. Untick the "Show Network speed from task bar" from the settings page  (this is to remove the dll from the registry).
3. Simply delete the folder.
    
## Usage/Examples

### Summary tab

![Summary - Copy](https://user-images.githubusercontent.com/27722888/151661081-e8bb7411-eba3-4078-9ac2-47075f45b880.png)

### Detailed tab

![Detailed - Copy](https://user-images.githubusercontent.com/27722888/151661086-44ead811-858e-4db6-af9d-dd1ad1dd5d4b.png)


### System tray

![TrayPopup_GIF1](https://user-images.githubusercontent.com/27722888/151661088-71349a72-f687-48be-ad33-805f7bf6771d.gif)

### TaskBar Speed

![DeskBand_GIF1](https://user-images.githubusercontent.com/27722888/153745070-669027d8-56eb-4982-b009-1be23e5b5d51.gif)


