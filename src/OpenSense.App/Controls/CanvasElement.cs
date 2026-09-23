using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.UI;

namespace OpenSense.App.Controls;

/// <summary>
/// Base for controls drawn with Win2D. Creates the CanvasControl while loaded and releases it on
/// unload (Win2D's documented requirement to avoid leaking device resources).
/// </summary>
public abstract partial class CanvasElement : UserControl
{
    private readonly Grid _root = new();
    private CanvasControl? _canvas;

    protected CanvasElement()
    {
        Content = _root;
        // Graphs, fans and the keyboard are pictures: right-to-left languages mustn't mirror them (or the text drawn on them).
        FlowDirection = FlowDirection.LeftToRight;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => Invalidate();
    }

    /// <summary>Put XAML overlays (text etc.) here; the canvas sits underneath.</summary>
    protected Grid Root => _root;

    protected CanvasControl? Canvas => _canvas;

    public void Invalidate() => _canvas?.Invalidate();

    protected abstract void OnDraw(CanvasDrawingSession session, Size size);

    /// <summary>Hook pointer input here; called each time a canvas is created.</summary>
    protected virtual void AttachInput(CanvasControl canvas)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_canvas is not null)
            return;
        _canvas = new CanvasControl { ClearColor = Colors.Transparent };
        _canvas.Draw += (s, args) => OnDraw(args.DrawingSession, s.Size);
        AttachInput(_canvas);
        _root.Children.Insert(0, _canvas);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_canvas is null)
            return;
        _root.Children.Remove(_canvas);
        _canvas.RemoveFromVisualTree();
        _canvas = null;
    }

    // Theme-aware colours matching WinUI's text and stroke brushes.
    protected bool IsLight => ActualTheme == ElementTheme.Light;

    protected Color TextPrimary => IsLight ? Color.FromArgb(0xE4, 0, 0, 0) : Colors.White;

    protected Color TextSecondary => IsLight ? Color.FromArgb(0x9E, 0, 0, 0) : Color.FromArgb(0xC5, 0xFF, 0xFF, 0xFF);

    protected Color TextTertiary => IsLight ? Color.FromArgb(0x72, 0, 0, 0) : Color.FromArgb(0x87, 0xFF, 0xFF, 0xFF);

    protected Color Track => IsLight ? Color.FromArgb(0x18, 0, 0, 0) : Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);

    protected Color GridLine => IsLight ? Color.FromArgb(0x14, 0, 0, 0) : Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF);

    protected Color Accent => IsLight ? Color.FromArgb(0xFF, 0xE4, 0x47, 0x3C) : Color.FromArgb(0xFF, 0xFF, 0x5A, 0x4E);

    protected Color Surface => IsLight ? Color.FromArgb(0xFF, 0xFB, 0xFB, 0xFB) : Color.FromArgb(0xFF, 0x2B, 0x2B, 0x2B);
}
