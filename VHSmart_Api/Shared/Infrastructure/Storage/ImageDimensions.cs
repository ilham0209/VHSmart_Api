using System.Buffers.Binary;

namespace VHSmart_Api.Shared.Infrastructure.Storage;

// Pixel size of a decoded header, used by the D-22 "exactly 1200 x 1200" product-image rule.
public readonly record struct ImageSize(int Width, int Height);

// Server-side image size check (D-22, CodingRules 10): the browser cannot be trusted with the
// "exactly 1,200 x 1,200 px" guideline, so the server reads the size from the file itself.
// The BCL ships no image decoder (System.Drawing.Common is unsupported off Windows) and the
// allowed list of this system is JPEG and PNG only, so both headers are parsed directly -
// a few kilobytes instead of decoding the whole picture. Anything that is not one of those two
// formats answers null and the caller rejects it before anything is stored.
public static class ImageDimensions
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Reads the stream from its current position. Callers with a large file should hand it a
    // buffered stream: the whole content is copied once (uploads are capped at 10 MB by D-22).
    public static ImageSize? TryRead(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.TryGetBuffer(out var bytes) ? TryRead(bytes.AsSpan()) : null;
    }

    public static ImageSize? TryRead(ReadOnlySpan<byte> bytes)
    {
        if (IsPng(bytes))
            return ReadPng(bytes);

        if (IsJpeg(bytes))
            return ReadJpeg(bytes);

        return null;
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 && bytes[..8].SequenceEqual(PngSignature);

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;

    // PNG: 8-byte signature, the IHDR chunk header, then width and height as big-endian
    // uint32 at fixed offsets 16 and 20.
    private static ImageSize? ReadPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24)
            return null;

        // Width and height live in IHDR, the first chunk of every PNG.
        if (!bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
            return null;

        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
        return ToSize(width, height);
    }

    // JPEG: walk the segment chain (length-prefixed) until the Start-Of-Frame segment, whose
    // payload starts with the precision byte, then height and width as big-endian uint16.
    private static ImageSize? ReadJpeg(ReadOnlySpan<byte> bytes)
    {
        var index = 2; // skip the SOI marker checked by IsJpeg
        while (index < bytes.Length)
        {
            if (bytes[index] != 0xFF)
                return null; // lost sync: not a segment boundary

            var markerIndex = index + 1;
            while (markerIndex < bytes.Length && bytes[markerIndex] == 0xFF)
                markerIndex++; // fill bytes between 0xFF and the marker id

            if (markerIndex >= bytes.Length)
                return null;

            var marker = bytes[markerIndex];
            index = markerIndex + 1;

            // Standalone markers carry no payload.
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
                continue;

            // End of image / start of scan reached without a frame: not a picture we can read.
            if (marker is 0xD9 or 0xDA)
                return null;

            if (index + 2 > bytes.Length)
                return null;

            var length = (bytes[index] << 8) | bytes[index + 1];
            if (length < 2)
                return null;

            var segmentEnd = index + length;
            if (segmentEnd > bytes.Length)
                return null;

            if (IsStartOfFrame(marker))
            {
                // precision (1) + height (2) + width (2) after the 2-byte segment length
                if (index + 6 >= segmentEnd)
                    return null;

                var height = (bytes[index + 3] << 8) | bytes[index + 4];
                var width = (bytes[index + 5] << 8) | bytes[index + 6];
                return ToSize((uint)width, (uint)height);
            }

            index = segmentEnd;
        }

        return null;
    }

    // SOF0-SOF15 except DHT (C4), JPG (C8) and DAC (CC), which are not frame headers.
    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xCF && marker is not 0xC4 and not 0xC8 and not 0xCC;

    private static ImageSize? ToSize(uint width, uint height) =>
        width > 0 && height > 0 && width <= int.MaxValue && height <= int.MaxValue
            ? new ImageSize((int)width, (int)height)
            : null;
}
