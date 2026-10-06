using System.Buffers.Binary;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Storage;

// Minimal but structurally correct JPEG / PNG headers for the D-22 "exactly 1200 x 1200" tests.
// The pictures carry no real image data - ImageDimensions reads the header only, which is
// exactly what the server does before it stores an upload.
internal static class TestImages
{
    public static byte[] Png(int width, int height)
    {
        // Signature (8) + IHDR chunk header (8) + width (4) + height (4) + the rest of IHDR.
        var bytes = new byte[33];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), 13); // IHDR data length
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), (uint)height);
        bytes[24] = 8;  // bit depth
        bytes[25] = 6;  // colour type: truecolour with alpha
        return bytes;
    }

    public static byte[] Jpeg(int width, int height)
    {
        // SOI + SOF0 (baseline) + EOI. SOF0 carries precision, height and width.
        var bytes = new byte[32];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xC0;
        bytes[4] = 0x00; // segment length high byte
        bytes[5] = 0x11; // 17 bytes: precision + size + 3 components
        bytes[6] = 8;    // precision
        bytes[7] = (byte)(height >> 8);
        bytes[8] = (byte)height;
        bytes[9] = (byte)(width >> 8);
        bytes[10] = (byte)width;
        bytes[11] = 3; // component count
        bytes[30] = 0xFF;
        bytes[31] = 0xD9;
        return bytes;
    }
}
