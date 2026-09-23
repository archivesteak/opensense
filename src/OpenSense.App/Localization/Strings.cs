using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace OpenSense.App.Localization;

/// <summary>
/// Text from Strings\&lt;language&gt;\Resources.resw in the app's language (<see cref="AppLanguage"/>). XAML reaches the same
/// resources through x:Uid; a property string such as "Nav_Dashboard.Content" reads here as "Nav_Dashboard/Content".
/// </summary>
public static class Strings
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key) => Loader.GetString(key);

    /// <summary>A string with {0}-style placeholders, filled in with the user's number and date formats.</summary>
    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
