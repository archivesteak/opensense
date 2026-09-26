using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OpenSense.App.Helpers;

public static class Pictures
{
    /// <summary>
    /// A picture from a file's bytes, decoded <paramref name="height"/> pixels high to save memory; null when Windows
    /// can't decode it. Call on the UI thread.
    /// </summary>
    public static async Task<BitmapImage?> FromBytesAsync(byte[] bytes, int height)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            var image = new BitmapImage { DecodePixelHeight = height };
            await image.SetSourceAsync(stream);
            return image;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The picture saved again as a JPEG the way Windows' encoder writes one: baseline, 8 bits, YCbCr sampled 4:2:0, turned
    /// the way its EXIF orientation says. Null when Windows can't decode it.
    /// </summary>
    public static async Task<byte[]?> ToBaselineJpegAsync(byte[] bytes)
    {
        try
        {
            using var input = new InMemoryRandomAccessStream();
            await input.WriteAsync(bytes.AsBuffer());
            input.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(input);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, new BitmapTransform(),
                ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);

            using var output = new InMemoryRandomAccessStream();
            var quality = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(0.95f, PropertyType.Single) };
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output, quality);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();

            var result = new byte[output.Size];
            output.Seek(0);
            await output.ReadAsync(result.AsBuffer(), (uint)result.Length, InputStreamOptions.None);
            return result;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return null;
        }
    }
}
