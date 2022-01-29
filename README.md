
# OpenNetMeter

A simple program to monitor your network/data usage. Made for the average windows user.

## Description

This program provides the following features,

- network speed
- current connection's session data usage 
- total data usage since the detection of a new connection.
- table of all the individual processes which consumed this data.
- A system tray icon to show the current session data usage and network speeds.

## Installation

1. Download and Install [.NET Desktop Runtime 5](https://dotnet.microsoft.com/en-us/download/dotnet/5.0)
2. Download the latest release from this repository and simply extract it.
3. Run in Admin mode (this is necessary).
4. Optional : To add this as a startup program <a href="#startup">click here</a>
    
## Usage/Examples

### Summary tab

![2](https://user-images.githubusercontent.com/27722888/149655800-9f7b1b1f-ea90-4696-b927-072a8d54f671.png)

### Detailed tab
![3](https://user-images.githubusercontent.com/27722888/149655347-da59c1f1-7eb9-4629-8315-e0969fd20340.png)

### System tray

![TrayPopup_GIF1](https://user-images.githubusercontent.com/27722888/150639166-e65e4a04-56f9-4468-8918-dda03ac8b0a0.gif)

<div id="startup"></div>

## Add as startup

1. type "Task Scheduler" (without quotes) in the Start Search box, and press [Enter].
2. Click create task.

<img src="https://user-images.githubusercontent.com/27722888/149656312-7803b97a-884f-4e1e-bd9c-b75cbfab38db.png" width="500" height="300"/>

3. Set the task Name and description as required. Tick the Highest privilege checkbox, Tick Run only when user is logged on, and set it to windows 10.

<img src="https://user-images.githubusercontent.com/27722888/149656420-3f3e0808-b813-446f-9b04-ef353b0635d3.png" width="500" height="300"/>

4. select the Trigger tab and then click the New button. In the new Trigger dialog you can set it to start at log on or when the pc starts up.

<img src="https://user-images.githubusercontent.com/27722888/149656678-d9cd516e-65e2-49d3-8805-e82f3c60bb11.png" width="500" height="300"/>

5. Next, select the Action tab and then click the New button. And finally, browse to the programs directory and set its .exe as the startup program.

<img src="https://user-images.githubusercontent.com/27722888/149656782-1de149b1-d8dc-42b9-b765-bba07186032b.png" width="300" height="300"/>

6. Click Ok to finalize the setup. Restart your system and check if its working. When your PC starts up, the program might take a few minutes to appear.
