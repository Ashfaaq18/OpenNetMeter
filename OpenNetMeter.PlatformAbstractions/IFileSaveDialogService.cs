using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenNetMeter.PlatformAbstractions;

public sealed record FileTypeOption(string Name, string Extension);

public interface IFileSaveDialogService
{
    /// <summary>
    /// Shows a save dialog and returns the chosen local file path, or null if the user cancelled.
    /// </summary>
    Task<string?> PickSavePathAsync(string title, string suggestedFileName, IReadOnlyList<FileTypeOption> fileTypes);
}
