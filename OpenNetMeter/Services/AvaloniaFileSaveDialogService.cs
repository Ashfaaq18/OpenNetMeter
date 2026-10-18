using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Services;

public sealed class AvaloniaFileSaveDialogService : IFileSaveDialogService
{
    public async Task<string?> PickSavePathAsync(string title, string suggestedFileName, IReadOnlyList<FileTypeOption> fileTypes)
    {
        if (GetMainWindow() is not { } window)
            return null;

        var result = await window.StorageProvider.SaveFilePickerWithResultAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = fileTypes.Count > 0 ? fileTypes[0].Extension : null,
            ShowOverwritePrompt = true,
            FileTypeChoices = fileTypes
                .Select(t => new FilePickerFileType(t.Name) { Patterns = [$"*.{t.Extension}"] })
                .ToList()
        });

        string? path = result.File?.TryGetLocalPath();
        if (path == null)
            return null;

        var selectedType = fileTypes.FirstOrDefault(t => t.Name == result.SelectedFileType?.Name);
        return ResolvePath(path, selectedType, fileTypes);
    }

    /// <summary>
    /// Keeps an extension the user typed if it is one of <paramref name="fileTypes"/>;
    /// otherwise appends the extension of the file type chosen in the dialog.
    /// </summary>
    internal static string ResolvePath(string path, FileTypeOption? selectedType, IReadOnlyList<FileTypeOption> fileTypes)
    {
        string extension = Path.GetExtension(path).TrimStart('.');
        if (fileTypes.Any(t => string.Equals(t.Extension, extension, StringComparison.OrdinalIgnoreCase)))
            return path;

        var type = selectedType ?? (fileTypes.Count > 0 ? fileTypes[0] : null);
        return type == null ? path : $"{path}.{type.Extension}";
    }

    private static Window? GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
