using System;

namespace OpenNetMeter.Models;

internal class ApplicationDB : IDisposable
{
    public const int DataStoragePeriodInDays = 60;

    public ApplicationDB(string dBFileName, string[]? extraParams = null)
    {
    }

    public int CreateTable() => 0;
    public int InsertUniqueRow_AdapterTable(string adapter) => 0;
    public void UpdateDatesInDB() { }
    public void PushToDB(string processName, long totalDataRecv, long totalDataSend) { }

    public void Dispose()
    {
    }
}
