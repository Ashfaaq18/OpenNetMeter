using System.Collections.Generic;
using System.Threading.Tasks;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Services;

public sealed class PlaceholderFileSaveDialogService : IFileSaveDialogService
{
    public Task<string?> PickSavePathAsync(string title, string suggestedFileName, IReadOnlyList<FileTypeOption> fileTypes) =>
        Task.FromResult<string?>(null);
}
