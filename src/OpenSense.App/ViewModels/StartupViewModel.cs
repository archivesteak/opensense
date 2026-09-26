using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Hardware.Boot;

namespace OpenSense.App.ViewModels;

/// <summary>What the laptop shows at power-on: the firmware's boot animation and sound, and a custom boot logo.</summary>
public sealed partial class StartupViewModel(DeviceSession session, NotificationService notifications) : ObservableObject
{
    private bool _loading;
    private int _logoRequest;
    private PixelSize? _screen;

    [ObservableProperty]
    public partial bool Available { get; set; }

    [ObservableProperty]
    public partial bool BootAnimationAvailable { get; set; }

    [ObservableProperty]
    public partial bool BootAnimation { get; set; }

    [ObservableProperty]
    public partial bool BootLogoAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AcerLogo))]
    public partial bool CustomLogo { get; set; }

    /// <summary>Acer's own logo shows.</summary>
    public bool AcerLogo => !CustomLogo;

    /// <summary>The custom logo's thumbnail.</summary>
    [ObservableProperty]
    public partial BitmapImage? Logo { get; set; }

    [ObservableProperty]
    public partial string BootLogoDescription { get; set; } = "";

    /// <summary>A picture is being written or removed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Idle))]
    public partial bool Busy { get; set; }

    public bool Idle => !Busy;

    /// <summary>Asks for a picture file; its path, or null when cancelled.</summary>
    public Func<Task<string?>>? PickPicture { get; set; }

    /// <summary>Shows the picture before it becomes the boot logo; the flag says the boot animation will be turned on.</summary>
    public Func<byte[], bool, Task<bool>>? ConfirmLogo { get; set; }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        _loading = true;
        var caps = session.Capabilities;
        BootAnimationAvailable = caps.BootAnimation;
        BootAnimation = session.Firmware?.BootAnimation ?? false;
        BootLogoAvailable = caps.CustomBootLogo;
        Available = BootAnimationAvailable || BootLogoAvailable;
        _loading = false;
        if (BootLogoAvailable)
            _ = LoadLogoAsync();
    }

    partial void OnBootAnimationChanged(bool value)
    {
        if (!_loading)
            _ = SetBootAnimationAsync(value);
    }

    private async Task SetBootAnimationAsync(bool value)
    {
        if (await session.SetBootAnimationAsync(value))
            return;
        _loading = true;
        BootAnimation = !value;
        _loading = false;
        Warn(Strings.Get("Notice_BootAnimationRejected"));
    }

    [RelayCommand]
    private async Task ChooseLogoAsync()
    {
        if (PickPicture is not { } pick || await pick() is not { } path)
            return;
        await LoadLogoAsync(); // the screen the picture must fit, as it is now
        if (_screen is null)
        {
            Warn(Message(BootLogoResult.ScreenUnknown, null));
            return;
        }
        byte[] bytes;
        try
        {
            // Acer's size limit, checked before reading the whole file.
            if (new FileInfo(path).Length > BootLogoRules.MaxBytes)
            {
                Warn(Message(BootLogoResult.FileTooLarge, null));
                return;
            }
            bytes = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warn(Strings.Format("BootLogo_ReadFailed", ex.Message));
            return;
        }

        var problem = BootLogoRules.Check(bytes, _screen, out var image);
        // The firmware decodes only baseline JPEGs: a progressive one (or one sampled oddly) is saved again as one.
        if (problem == BootLogoProblem.NotBaseline && await Pictures.ToBaselineJpegAsync(bytes) is { } baseline)
        {
            bytes = baseline;
            problem = BootLogoRules.Check(bytes, _screen, out image);
        }
        if (problem != BootLogoProblem.None)
        {
            Warn(Message(problem switch
            {
                BootLogoProblem.FileTooLarge => BootLogoResult.FileTooLarge,
                BootLogoProblem.TooManyPixels => BootLogoResult.TooManyPixels,
                BootLogoProblem.ColorDepth => BootLogoResult.ColorDepth,
                BootLogoProblem.NotBaseline => BootLogoResult.NotBaseline,
                _ => BootLogoResult.NotGifOrJpeg,
            }, image));
            return;
        }

        // The firmware shows the logo as part of its boot animation.
        var turnOnAnimation = BootAnimationAvailable && !BootAnimation;
        if (ConfirmLogo is { } confirm && !await confirm(bytes, turnOnAnimation))
            return;

        Busy = true;
        try
        {
            var result = await session.SetBootLogoAsync(bytes);
            if (result != BootLogoResult.Done)
            {
                Warn(Message(result, image));
                return;
            }
            if (turnOnAnimation)
                BootAnimation = true;
            await LoadLogoAsync();
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreLogoAsync()
    {
        Busy = true;
        try
        {
            if (!await session.RestoreBootLogoAsync())
                Warn(Strings.Get("BootLogo_RestoreFailed"));
            await LoadLogoAsync();
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task LoadLogoAsync()
    {
        var request = ++_logoRequest;
        var state = await session.GetBootLogoAsync();
        // A picture Windows can't decode still counts as custom, without a thumbnail.
        var logo = state.Image is { } bytes ? await Pictures.FromBytesAsync(bytes, 96) : null;
        if (request != _logoRequest)
            return;
        _screen = state.Screen;
        CustomLogo = state.Custom;
        Logo = logo;
        var description = state.Screen is { } screen && BootLogoRules.MaxSize(screen) is var max
            ? Strings.Format("BootLogo_Description", max.Width, max.Height)
            : Strings.Get("BootLogo_DescriptionAnySize");
        // Firmware with the setup's own switch for the picture deletes it when the setup's defaults are loaded.
        BootLogoDescription = session.Capabilities.CustomBootLogoSwitch
            ? description + Environment.NewLine + Strings.Get("BootLogo_BiosDefaults")
            : description;
    }

    private string Message(BootLogoResult result, BootLogoImage? image) => result switch
    {
        BootLogoResult.NotGifOrJpeg => Strings.Get("BootLogo_NotGifOrJpeg"),
        BootLogoResult.FileTooLarge => Strings.Get("BootLogo_FileTooLarge"),
        BootLogoResult.TooManyPixels when image is not null && _screen is { } screen && BootLogoRules.MaxSize(screen) is var max =>
            Strings.Format("BootLogo_TooManyPixels", image.Width, image.Height, max.Width, max.Height),
        BootLogoResult.ColorDepth => Strings.Get("BootLogo_ColorDepth"),
        BootLogoResult.NotBaseline => Strings.Get("BootLogo_NotBaseline"),
        BootLogoResult.ScreenUnknown or BootLogoResult.TooManyPixels => Strings.Get("BootLogo_ScreenUnknown"),
        BootLogoResult.NoSpace => Strings.Get("BootLogo_NoSpace"),
        _ => Strings.Get("BootLogo_WriteFailed"),
    };

    private void Warn(string message) => notifications.Show(Title, message, InfoBarSeverity.Warning);

    private static string Title => Strings.Get("Notice_Startup_Title");
}
