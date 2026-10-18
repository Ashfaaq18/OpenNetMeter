namespace OpenNetMeter.PlatformAbstractions;

public interface IFileRevealService
{
    /// <summary>
    /// Opens the system file manager at the file's folder, selecting the file where the platform supports it.
    /// </summary>
    void Reveal(string filePath);
}
