namespace OpenNetMeter.ViewModels;

/// <summary>
/// One entry in the Settings network adapter list. An empty <paramref name="Id"/> means Automatic.
/// </summary>
public sealed record AdapterChoice(string Id, string Label);
