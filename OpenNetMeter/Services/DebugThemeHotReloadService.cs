using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace OpenNetMeter.Services;

public sealed class DebugThemeHotReloadService : IDisposable
{
    private readonly Window targetWindow;
    private readonly string themeFilePath;
    private readonly FileSystemWatcher watcher;
    private readonly DispatcherTimer reloadTimer;
    private bool disposed;

    public DebugThemeHotReloadService(Window targetWindow, string themeFilePath)
    {
        this.targetWindow = targetWindow;
        this.themeFilePath = themeFilePath;

        Console.WriteLine($"[ThemeHotReload] Watching '{themeFilePath}'");

        watcher = new FileSystemWatcher(Path.GetDirectoryName(themeFilePath)!, Path.GetFileName(themeFilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        reloadTimer.Tick += ReloadTimer_Tick;

        watcher.Changed += OnThemeFileChanged;
        watcher.Created += OnThemeFileChanged;
        watcher.Renamed += OnThemeFileChanged;
    }

    private void OnThemeFileChanged(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"[ThemeHotReload] Change detected: {e.ChangeType} {e.FullPath}");
        Dispatcher.UIThread.Post(() =>
        {
            reloadTimer.Stop();
            reloadTimer.Start();
        });
    }

    private void ReloadTimer_Tick(object? sender, EventArgs e)
    {
        reloadTimer.Stop();
        ReloadThemeDictionary();
    }

    private void ReloadThemeDictionary()
    {
        try
        {
            Console.WriteLine($"[ThemeHotReload] Reloading '{themeFilePath}'");

            var dictionary = LoadThemeDictionaryFromFile(themeFilePath);

            targetWindow.Resources.MergedDictionaries.Clear();
            targetWindow.Resources.MergedDictionaries.Add(dictionary);
            targetWindow.InvalidateVisual();

            Console.WriteLine("[ThemeHotReload] Reload succeeded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ThemeHotReload] Reload failed: {ex}");
        }
    }

    private static ResourceDictionary LoadThemeDictionaryFromFile(string path)
    {
        XNamespace xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("Themes.axaml has no root element.");

        if (root.Name.LocalName != "ResourceDictionary")
            throw new InvalidOperationException("Themes.axaml root element must be ResourceDictionary.");

        var dictionary = new ResourceDictionary();

        foreach (var element in root.Elements())
        {
            if (element.Name.LocalName == "ResourceDictionary.ThemeDictionaries")
            {
                LoadThemeDictionaries(dictionary, element, xNamespace);
                continue;
            }

            AddResource(dictionary, element, xNamespace);
        }

        return dictionary;
    }

    private static void LoadThemeDictionaries(ResourceDictionary target, XElement themeDictionariesElement, XNamespace xNamespace)
    {
        foreach (var themeDictionaryElement in themeDictionariesElement.Elements())
        {
            if (themeDictionaryElement.Name.LocalName != "ResourceDictionary")
                continue;

            string? themeKey = (string?)themeDictionaryElement.Attribute(xNamespace + "Key");
            if (string.IsNullOrWhiteSpace(themeKey))
                continue;

            var themeDictionary = new ResourceDictionary();
            foreach (var resourceElement in themeDictionaryElement.Elements())
                AddResource(themeDictionary, resourceElement, xNamespace);

            target.ThemeDictionaries[ParseThemeVariant(themeKey)] = themeDictionary;
        }
    }

    private static void AddResource(ResourceDictionary target, XElement element, XNamespace xNamespace)
    {
        string? key = (string?)element.Attribute(xNamespace + "Key");
        if (string.IsNullOrWhiteSpace(key))
            return;

        object value = element.Name.LocalName switch
        {
            "SolidColorBrush" => new SolidColorBrush(Color.Parse(GetRequiredAttribute(element, "Color"))),
            "BoxShadows" => BoxShadows.Parse(element.Value.Trim()),
            "Double" => double.Parse(element.Value.Trim(), CultureInfo.InvariantCulture),
            _ => throw new NotSupportedException($"Unsupported theme resource element '{element.Name}'.")
        };

        target[key] = value;
    }

    private static string GetRequiredAttribute(XElement element, string attributeName)
    {
        return (string?)element.Attribute(attributeName)
            ?? throw new InvalidOperationException($"Element '{element.Name}' is missing required attribute '{attributeName}'.");
    }

    private static ThemeVariant ParseThemeVariant(string themeKey)
    {
        return themeKey switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            "Default" => ThemeVariant.Default,
            _ => throw new NotSupportedException($"Unsupported theme variant key '{themeKey}'.")
        };
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        reloadTimer.Stop();
    }
}
