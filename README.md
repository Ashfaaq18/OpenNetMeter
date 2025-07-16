
# OpenNetMeter

A simple program to monitor your network/data usage. Made for the average windows user.

## Description

This program provides the following features,

- network speed.
- current connection's session data usage.
- Data usage for today.
- retrieve data usage up to the past 60 days and anywhere in between in detailed format.
- A mini widget to show the network speed (can be placed over the taskbar). 

## Installation

1. Download and Install [.NET 6.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime) desktop apps version. (for versions 0.9.0 and below, download [.NET 5.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/5.0/runtime))
2. Download the latest release from this repository and simply extract it.
3. Run in Admin mode (this is necessary).
4. Optional : To add this as a startup program, go to the settings tab and tick the checkbox.

## Uninstallation

1. Untick the "Start program on windows startup" from the settings page (this is to remove the program from the startup records).
2. for versions 0.8.1 to 0.5.1, Untick the "Show Network speed from task bar" from the settings page  (this is to remove the dll from the registry).
3. Simply delete the folder.
    
## Usage/Examples

### Summary tab

![image](https://user-images.githubusercontent.com/27722888/177024162-66ada1ab-05a8-4cea-9903-68eb0abad834.png)

### Detailed tab

![image](https://user-images.githubusercontent.com/27722888/178145774-dc5bebc0-e4fc-49e3-8c85-d27ea6ee5a40.png)

### History tab

*can retrieve data recorded by this application up to the past 60 days

![image](https://user-images.githubusercontent.com/27722888/177024251-003625cf-412e-49a8-aff5-e556ea15e80d.png)

### Darkmode

![image](https://user-images.githubusercontent.com/27722888/177024169-137f804d-a3f6-4cb3-8c9a-6e068357fe2c.png)

### Network speed mini widget

![MiniWidget_demo1](https://user-images.githubusercontent.com/27722888/168587020-10bb15cc-7176-4d46-a4e9-8fdaf380bbe5.gif)

### System tray & TaskBarSpeed (Discontinued since v0.9.0)

These are replaced by the mini widget

![TrayPopup_GIF1](https://user-images.githubusercontent.com/27722888/151661088-71349a72-f687-48be-ad33-805f7bf6771d.gif)

![DeskBand_GIF1](https://user-images.githubusercontent.com/27722888/153745070-669027d8-56eb-4982-b009-1be23e5b5d51.gif)
