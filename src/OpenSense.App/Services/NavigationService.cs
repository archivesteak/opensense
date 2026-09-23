namespace OpenSense.App.Services;

/// <summary>Lets pages ask the main window to switch pages.</summary>
public sealed class NavigationService
{
    public event Action<string>? Requested;

    public void Navigate(string page) => Requested?.Invoke(page);
}
