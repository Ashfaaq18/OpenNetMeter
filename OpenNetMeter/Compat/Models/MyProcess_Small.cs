namespace OpenNetMeter.Models;

public class MyProcess_Small
{
    public string? Name { get; set; }
    public long CurrentDataRecv { get; set; }
    public long CurrentDataSend { get; set; }

    public MyProcess_Small(string nameP, long currentDataRecvP, long currentDataSendP)
    {
        Name = nameP;
        CurrentDataRecv = currentDataRecvP;
        CurrentDataSend = currentDataSendP;
    }
}
