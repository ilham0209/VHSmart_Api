using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Storage;

// Server-side pixel size reading (D-22): the upload rejects anything that is not a JPEG or PNG
// and anything that is not exactly 1200 x 1200, so both parsers must be right on the header
// and must answer null (not throw) for everything else.
public class ImageDimensionsTests
{
    [Fact]
    public void TryRead_PngHeader_ReturnsTheSize()
    {
        var size = ImageDimensions.TryRead(TestImages.Png(1200, 1200));

        Assert.NotNull(size);
        Assert.Equal(1200, size.Value.Width);
        Assert.Equal(1200, size.Value.Height);
    }

    [Fact]
    public void TryRead_JpegHeader_ReturnsTheSize()
    {
        var size = ImageDimensions.TryRead(TestImages.Jpeg(640, 480));

        Assert.NotNull(size);
        Assert.Equal(640, size.Value.Width);
        Assert.Equal(480, size.Value.Height);
    }

    [Fact]
    public void TryRead_StreamOverTheSameBytes_ReturnsTheSize()
    {
        using var stream = new MemoryStream(TestImages.Png(1200, 800));

        var size = ImageDimensions.TryRead(stream);

        Assert.NotNull(size);
        Assert.Equal(1200, size.Value.Width);
        Assert.Equal(800, size.Value.Height);
    }

    [Fact]
    public void TryRead_PngWithALaterSegmentBeforeIhdr_IsRejected()
    {
        // A PNG always starts with IHDR: a file that does not is not one we can read.
        var bytes = TestImages.Png(1200, 1200);
        bytes[12] = (byte)'j';
        bytes[13] = (byte)'U';
        bytes[14] = (byte)'N';
        bytes[15] = (byte)'K';

        Assert.Null(ImageDimensions.TryRead(bytes));
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })] // GIF
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })]              // PDF
    [InlineData(new byte[] { })]
    public void TryRead_UnknownOrEmptyBytes_ReturnsNull(byte[] bytes) =>
        Assert.Null(ImageDimensions.TryRead(bytes));

    [Fact]
    public void TryRead_TruncatedHeaders_ReturnNull()
    {
        var png = TestImages.Png(1200, 1200);
        Assert.Null(ImageDimensions.TryRead(png.AsSpan(0, 20)));

        var jpeg = TestImages.Jpeg(1200, 1200);
        Assert.Null(ImageDimensions.TryRead(jpeg.AsSpan(0, 4)));
    }

    [Fact]
    public void TryRead_JpegWithoutAFrameSegment_ReturnsNull()
    {
        // SOI then end of image: never reaches a Start-Of-Frame segment.
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };

        Assert.Null(ImageDimensions.TryRead(bytes));
    }

    [Fact]
    public void TryRead_JpegWithASegmentBeforeTheFrame_SkipsIt()
    {
        // A real picture carries EXIF / quantisation segments before the frame; the size must
        // still be found.
        var frame = TestImages.Jpeg(1200, 1200);
        var bytes = new byte[frame.Length + 8];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF; // APP0 marker
        bytes[3] = 0xE0;
        bytes[4] = 0x00; // length 6 -> 4 payload bytes
        bytes[5] = 0x06;
        "JFIF"u8.CopyTo(bytes.AsSpan(6, 4));
        frame.AsSpan(2).CopyTo(bytes.AsSpan(10)); // the rest, starting at the SOF marker

        var size = ImageDimensions.TryRead(bytes);

        Assert.NotNull(size);
        Assert.Equal(1200, size.Value.Width);
        Assert.Equal(1200, size.Value.Height);
    }
}
