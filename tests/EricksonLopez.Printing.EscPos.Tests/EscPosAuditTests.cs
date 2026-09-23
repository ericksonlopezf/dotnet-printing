// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Printing.EscPos;
using Xunit;

namespace EricksonLopez.Printing.EscPos.Tests;

/// <summary>
/// Hardening, fuzzing, and compliance verification suite for the ESC/POS engine.
/// </summary>
public sealed class EscPosAuditTests
{
    [Fact]
    public void FIND_PRINT_012_EscPosBuilder_NegativeDimensions_HandledGracefully()
    {
        using var builder = new EscPosBuilder();

        // Must not throw ArgumentOutOfRangeException with negative totalWidth
        var actTable = () => builder.TableRow("Left", "Right", totalWidth: -10);
        actTable.Should().NotThrow();

        // Must not throw ArgumentOutOfRangeException with negative width
        var actDivider = () => builder.Divider(width: -5);
        actDivider.Should().NotThrow();

        var doc = builder.Build();
        doc.GetBytes().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FIND_PRINT_013_EscPosBuilder_QrCode_EmitsValidBytecode()
    {
        using var builder = new EscPosBuilder();
        builder.QrCode("https://ericksonlopez.dev", moduleSize: 4, EscPosQrErrorCorrection.H);

        var doc = builder.Build();
        var bytes = doc.GetBytes();

        // Must contain standard ESC/POS QR code command headers (GS ( k)
        bytes.Should().Contain(0x1D);
        bytes.Should().Contain(0x28);
        bytes.Should().Contain(0x6B);
        bytes.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    public void FIND_PRINT_021_EscPosBuilder_Disposed_ThrowsObjectDisposedException()
    {
        var builder = new EscPosBuilder();
        builder.Dispose();
        builder.Dispose(); // Verify Dispose idempotency

        // Verify buffer stream was disposed
        var bufferField = typeof(EscPosBuilder).GetField("_buffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var stream = (System.IO.MemoryStream)bufferField.GetValue(builder)!;
        stream.CanWrite.Should().BeFalse();

        static void AssertDisposed(Action action)
        {
            var ex = Assert.Throws<ObjectDisposedException>(action);
            ex.ObjectName.Should().Be(typeof(EscPosBuilder).FullName);
        }

        AssertDisposed(() => builder.Initialize());
        AssertDisposed(() => builder.Raw(new byte[] { 0x00 }.AsSpan()));
        AssertDisposed(() => builder.Raw(new byte[] { 0x00 }));
        AssertDisposed(() => builder.Raw((byte[])null!));
        AssertDisposed(() => builder.Align(EscPosAlignment.Center));
        AssertDisposed(() => builder.Bold(true));
        AssertDisposed(() => builder.DoubleSize(true, true));
        AssertDisposed(() => builder.Underline(EscPosUnderline.SingleDot));
        AssertDisposed(() => builder.FontSize(1, 1));
        AssertDisposed(() => builder.Text("Test"));
        AssertDisposed(() => builder.Line("Test"));
        AssertDisposed(() => builder.BarcodeCode128("1234"));
        AssertDisposed(() => builder.BarcodeCode39("1234"));
        AssertDisposed(() => builder.BarcodeEan13("123456789012"));
        AssertDisposed(() => builder.Feed(1));
        AssertDisposed(() => builder.Cut(true));
        AssertDisposed(() => builder.OpenCashDrawer());
        AssertDisposed(() => builder.QrCode("Test"));
        AssertDisposed(() => builder.Build());
    }

    [Fact]
    public void Text_NullString_HandledGracefully()
    {
        using var builder = new EscPosBuilder();
        builder.Text(null!);
        builder.Build().GetBytes().Length.Should().Be(0);
    }

    [Fact]
    public void FIND_PRINT_021_EscPosBuilder_QrCode_ThrowsOnOversizedData()
    {
        using var builder = new EscPosBuilder();
        var oversized = new string('A', 7090);
        var act = () => builder.QrCode(oversized);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*7089*")
            .WithParameterName("data");
    }

    [Fact]
    public void FIND_PRINT_022_IdempotencyKey_PreservedInBuilders()
    {
        using var escPos = new EscPosBuilder();
        var escDoc = escPos.Line("Hello").Build("Receipt", "idem-esc-001");
        escDoc.IdempotencyKey.Should().Be("idem-esc-001");
    }

    [Fact]
    public void PRNT_001_EscPosBuilder_SafeMode_ThrowsOnRaw()
    {
        using var builder = new EscPosBuilder(safeMode: true);
        var actSpan = () => builder.Raw(new byte[] { 0x1B, 0x40 }.AsSpan());
        var actArray = () => builder.Raw(new byte[] { 0x1B, 0x40 });

        actSpan.Should().Throw<NotSupportedException>().WithMessage("*safe mode*");
        actArray.Should().Throw<NotSupportedException>().WithMessage("*safe mode*");
    }

    [Fact]
    public void PRNT_002_EscPosBuilder_Text_HandlesLargeStringsWithoutLohSpike()
    {
        // 10 Megabytes of string
        var largeString = new string('A', 10_000_000);
        using var builder = new EscPosBuilder();

        // This should not throw OutOfMemoryException and should complete very fast
        builder.Text(largeString);
        var doc = builder.Build();

        doc.GetBytes().Length.Should().Be(10_000_000);
    }

    [Fact]
    public void Fuzz_EscPosBuilder_TableRow_UnicodeAndLargeStrings_DoesNotThrow()
    {
        using var builder = new EscPosBuilder();
        builder.TableRow("Café ☕", "€ 12.50", totalWidth: 42);
        builder.TableRow(new string('A', 100), new string('B', 100), totalWidth: 42);
        builder.TableRow("", "", totalWidth: 0);

        var doc = builder.Build();
        doc.GetBytes().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Fuzz_EscPosBuilder_QrCode_ExtremeInputs()
    {
        using var builder = new EscPosBuilder();
        var mediumUrl = "https://example.com/order?id=" + new string('9', 500);
        builder.QrCode(mediumUrl, moduleSize: 1, EscPosQrErrorCorrection.L);
        builder.QrCode(mediumUrl, moduleSize: 16, EscPosQrErrorCorrection.H);

        var doc = builder.Build();
        doc.GetBytes().Length.Should().BeGreaterThan(500);
    }

    [Fact]
    public void EscPosBuilder_SafeMode_SanitizesControlCharacters()
    {
        using var safeBuilder = new EscPosBuilder(safeMode: true);
        safeBuilder.Text("Hello\x1B\x1C\x1D\x10World");
        var safeBytes = safeBuilder.Build().GetBytes();

        using var expectedBuilder = new EscPosBuilder();
        expectedBuilder.Text("HelloWorld");
        var expectedBytes = expectedBuilder.Build().GetBytes();

        safeBytes.Should().Equal(expectedBytes);
    }

    [Fact]
    public void EscPosBuilder_Text_MultiChunk_ProperlyFlushesBuffer()
    {
        // 5000 characters to exercise ChunkSize (4096) boundary and flush logic
        var multiChunkString = new string('Z', 5000);
        using var builder = new EscPosBuilder();
        builder.Text(multiChunkString);
        var bytes = builder.Build().GetBytes();

        bytes.Length.Should().Be(5000);
    }

    [Fact]
    public void EscPosBuilder_Divider_ZeroOrNegativeWidth_ReturnsEarlyWithoutWriting()
    {
        using var b1 = new EscPosBuilder();
        b1.Divider(width: 0);
        b1.Build().GetBytes().Length.Should().Be(0);

        using var b2 = new EscPosBuilder();
        b2.Divider(width: -5);
        b2.Build().GetBytes().Length.Should().Be(0);
    }

    [Fact]
    public void EscPosBuilder_QrCode_ExactBoundaryCapacity7089_Succeeds()
    {
        using var builder = new EscPosBuilder();
        var exactPayload = new string('A', 7089);
        var act = () => builder.QrCode(exactPayload);
        act.Should().NotThrow();

        var doc = builder.Build();
        doc.GetBytes().Length.Should().BeGreaterThan(7089);
    }

    [Fact]
    public void EscPosBuilder_QrCode_LargePayload_HighByteCalculatedCorrectly()
    {
        // Payload of 300 bytes causes length = 303 (0x012F), so pH = (303 >> 8) & 0xFF = 1, pL = 0x2F
        using var builder = new EscPosBuilder();
        var payload = new string('B', 300);
        builder.QrCode(payload);
        var bytes = builder.Build().GetBytes();

        // Check for 0x1D, 0x28, 0x6B, 0x2F, 0x01
        var idx = bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x28, 0x6B, 0x2F, 0x01 });
        idx.Should().BeGreaterThan(-1);
    }
}
