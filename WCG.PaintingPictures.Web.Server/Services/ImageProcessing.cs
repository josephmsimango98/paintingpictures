using ImageMagick;
using ImageMagick.Formats;

namespace WCG.PaintingPictures.Web.Server.Services;

public static class ImageProcessing
{
    public sealed record ExtractedImageInfo(
        int Width,
        int Height,
        bool IsHeic,
        double? Latitude,
        double? Longitude,
        DateTimeOffset? TakenAt,
        string? CameraMake,
        string? CameraModel);

    public static ExtractedImageInfo Inspect(byte[] sourceBytes, string? fileName, string? contentType)
    {
        var isHeic = IsHeicFile(fileName, contentType, sourceBytes);

        using var images = new MagickImageCollection();
        try
        {
            var readSettings = CreateReadSettings(fileName, contentType, isHeic);
            if (readSettings is null)
                images.Read(sourceBytes);
            else
                images.Read(sourceBytes, readSettings);
        }
        catch (MagickException) when (isHeic)
        {
            // Some phones write HEIF brands; retry with HEIF format hint.
            images.Clear();
            images.Read(sourceBytes, new MagickReadSettings { Format = MagickFormat.Heif });
        }
        if (images.Count == 0)
            throw new InvalidOperationException("Image contained no frames.");

        using var frame = images[0].Clone();
        // Apply EXIF orientation so stored width/height match how the photo is shown.
        frame.AutoOrient();

        var (width, height) = ReadDimensions(frame);
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Could not read image dimensions from file metadata.");

        var (lat, lng) = ReadGps(frame);
        var takenAt = ReadTakenAt(frame);
        var make = ReadExifString(frame, ExifTag.Make);
        var model = ReadExifString(frame, ExifTag.Model);

        return new ExtractedImageInfo(
            width,
            height,
            isHeic || IsHeicFormat(frame.Format),
            lat,
            lng,
            takenAt,
            make,
            model);
    }

    public static byte[] ConvertToJpeg(byte[] sourceBytes)
    {
        try
        {
            using var images = new MagickImageCollection();
            var isHeic = IsHeicFile(null, null, sourceBytes);
            var readSettings = CreateReadSettings(null, null, isHeic);
            if (readSettings is null)
                images.Read(sourceBytes);
            else
                images.Read(sourceBytes, readSettings);
            if (images.Count == 0)
                throw new InvalidOperationException("Image contained no frames.");

            using var frame = images[0].Clone();
            frame.AutoOrient();
            if (frame.ColorSpace != ColorSpace.sRGB)
                frame.ColorSpace = ColorSpace.sRGB;

            frame.Format = MagickFormat.Jpeg;
            frame.Quality = 90;
            var jpeg = frame.ToByteArray(MagickFormat.Jpeg);
            if (!IsJpeg(jpeg))
                throw new InvalidOperationException("Conversion did not produce a valid JPEG.");
            return jpeg;
        }
        catch (MagickException ex)
        {
            throw new InvalidOperationException(
                "Could not convert image for browser display.",
                ex);
        }
    }

    private static MagickReadSettings? CreateReadSettings(string? fileName, string? contentType, bool isHeic)
    {
        if (!isHeic && !IsHeicFile(fileName, contentType, []))
            return null;

        return new MagickReadSettings
        {
            Format = MagickFormat.Heic
        };
    }

    public static bool IsHeicFile(string? fileName, string? contentType, byte[] bytes)
    {
        var extension = Path.GetExtension(fileName ?? "");
        if (extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(contentType)
            && (contentType.Equals("image/heic", StringComparison.OrdinalIgnoreCase)
                || contentType.Equals("image/heif", StringComparison.OrdinalIgnoreCase)
                || contentType.Equals("image/heic-sequence", StringComparison.OrdinalIgnoreCase)
                || contentType.Equals("image/heif-sequence", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (bytes.Length >= 12)
        {
            var box = System.Text.Encoding.ASCII.GetString(bytes, 4, 4);
            if (!box.Equals("ftyp", StringComparison.OrdinalIgnoreCase))
                return false;

            var brand = System.Text.Encoding.ASCII.GetString(bytes, 8, 4);
            if (IsHeifBrand(brand))
                return true;

            for (var offset = 16; offset + 4 <= bytes.Length && offset < 64; offset += 4)
            {
                var compatible = System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
                if (IsHeifBrand(compatible))
                    return true;
            }
        }

        return false;
    }

    private static bool IsHeifBrand(string brand) =>
        brand.Equals("heic", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("heix", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("hevc", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("hevx", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("heim", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("heis", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("hevm", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("hevs", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("mif1", StringComparison.OrdinalIgnoreCase)
        || brand.Equals("msf1", StringComparison.OrdinalIgnoreCase);

    private static bool IsHeicFormat(MagickFormat format)
    {
        var name = format.ToString();
        return name.Contains("Heic", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Heif", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    /// <summary>
    /// Prefer decoded pixel size (after AutoOrient). Fall back to EXIF PixelX/YDimension.
    /// </summary>
    private static (int Width, int Height) ReadDimensions(IMagickImage image)
    {
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        if (width > 0 && height > 0)
            return (width, height);

        var exif = image.GetExifProfile();
        if (exif is null)
            return (0, 0);

        var exifW = ReadExifNumber(exif, ExifTag.PixelXDimension)
            ?? ReadExifNumber(exif, ExifTag.ImageWidth);
        var exifH = ReadExifNumber(exif, ExifTag.PixelYDimension)
            ?? ReadExifNumber(exif, ExifTag.ImageLength);

        if (exifW is null || exifH is null || exifW == 0 || exifH == 0)
            return (0, 0);

        // If AutoOrient was not applied to pixels but orientation says rotate 90/270, swap.
        var orientation = exif.GetValue(ExifTag.Orientation)?.Value ?? (ushort)1;
        if (orientation is 5 or 6 or 7 or 8)
            return (exifH.Value, exifW.Value);

        return (exifW.Value, exifH.Value);
    }

    private static int? ReadExifNumber(IExifProfile exif, ExifTag<Number> tag)
    {
        var value = exif.GetValue(tag)?.Value;
        if (value is null)
            return null;

        var number = (int)(uint)value;
        return number > 0 ? number : null;
    }

    private static (double? Latitude, double? Longitude) ReadGps(IMagickImage image)
    {
        var exif = image.GetExifProfile();
        if (exif is null)
            return (null, null);

        var lat = ReadGpsCoordinate(exif, ExifTag.GPSLatitude, ExifTag.GPSLatitudeRef, isLatitude: true);
        var lng = ReadGpsCoordinate(exif, ExifTag.GPSLongitude, ExifTag.GPSLongitudeRef, isLatitude: false);
        return (lat, lng);
    }

    private static double? ReadGpsCoordinate(
        IExifProfile exif,
        ExifTag<Rational[]> valueTag,
        ExifTag<string> refTag,
        bool isLatitude)
    {
        var values = exif.GetValue(valueTag)?.Value;
        var reference = exif.GetValue(refTag)?.Value;
        if (values is null || values.Length < 3)
            return null;

        var degrees = values[0].ToDouble();
        var minutes = values[1].ToDouble();
        var seconds = values[2].ToDouble();
        var decimalDegrees = degrees + (minutes / 60d) + (seconds / 3600d);

        if (string.Equals(reference, isLatitude ? "S" : "W", StringComparison.OrdinalIgnoreCase))
            decimalDegrees *= -1;

        if (double.IsNaN(decimalDegrees) || double.IsInfinity(decimalDegrees))
            return null;

        return Math.Round(decimalDegrees, 6);
    }

    private static DateTimeOffset? ReadTakenAt(IMagickImage image)
    {
        var exif = image.GetExifProfile();
        var raw = exif?.GetValue(ExifTag.DateTimeOriginal)?.Value
            ?? exif?.GetValue(ExifTag.DateTime)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // EXIF typically: "2024:07:19 14:30:00"
        if (DateTime.TryParseExact(
                raw.Trim(),
                "yyyy:MM:dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal,
                out var local))
            return new DateTimeOffset(local);

        if (DateTimeOffset.TryParse(raw, out var parsed))
            return parsed;

        return null;
    }

    private static string? ReadExifString(IMagickImage image, ExifTag<string> tag)
    {
        var value = image.GetExifProfile()?.GetValue(tag)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
