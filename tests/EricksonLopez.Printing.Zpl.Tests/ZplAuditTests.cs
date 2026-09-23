// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;
using AwesomeAssertions;
using EricksonLopez.Printing.Zpl;
using Xunit;

namespace EricksonLopez.Printing.Zpl.Tests;

/// <summary>
/// Hardening, fuzzing, and compliance verification suite for the ZPL II label generator.
/// </summary>
public sealed class ZplAuditTests
{
    [Fact]
    public void FIND_PRINT_001_ZplBuilder_EscapesCaretAndTildeInFieldData()
    {
        var doc = new ZplBuilder()
            .Text(10, 20, "Price: $10 ^ Discount ~ 5%")
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        // Caret (^) and tilde (~) must be escaped with ^FH_ to prevent command injection
        zpl.Should().Contain("^FH_^FDPrice: $10 _5E Discount _7E 5%^FS");
    }

    [Theory]
    [InlineData("123^FDInjection^FS")]
    [InlineData("123~XZ")]
    public void FIND_PRINT_001_ZplBuilder_BarcodeCode128_RejectsIllegalControlCharacters(string maliciousData)
    {
        var act = () => new ZplBuilder().BarcodeCode128(10, 20, maliciousData);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*delimiters*")
            .WithParameterName("data");
    }

    [Theory]
    [InlineData("CODE^39")]
    [InlineData("CODE~39")]
    public void FIND_PRINT_001_ZplBuilder_BarcodeCode39_RejectsIllegalControlCharacters(string maliciousData)
    {
        var act = () => new ZplBuilder().BarcodeCode39(10, 20, maliciousData);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*delimiters*")
            .WithParameterName("data");
    }

    [Fact]
    public void FIND_PRINT_001_ZplBuilder_QrCode_EscapesCaretAndTilde()
    {
        var doc = new ZplBuilder()
            .QrCode(10, 20, "https://example.com/test?a=1^b=2~c=3")
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FH_^FDQA,https://example.com/test?a=1_5Eb=2_7Ec=3^FS");
    }

    [Fact]
    public void FIND_PRINT_001_ZplBuilder_Box_ThrowsOnNegativeDimensions()
    {
        var b = new ZplBuilder();
        var act1 = () => b.Box(0, 0, -1, 50, 2);
        var act2 = () => b.Box(0, 0, 50, -1, 2);
        var act3 = () => b.Box(0, 0, 50, 50, -1);

        act1.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("width");
        act2.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("height");
        act3.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("borderThickness");
    }

    [Fact]
    public void FIND_PRINT_009_ZplBuilder_Build_IsIdempotentAndDoesNotAccumulateXZ()
    {
        var builder = new ZplBuilder()
            .Text(10, 20, "Idempotency Test");

        var doc1 = builder.Build("Doc1");
        var doc2 = builder.Build("Doc2");

        var zpl1 = Encoding.UTF8.GetString(doc1.GetBytes());
        var zpl2 = Encoding.UTF8.GetString(doc2.GetBytes());

        zpl1.Should().Be(zpl2);
        // Ensure ^XZ appears exactly once at the end
        var firstXz = zpl1.IndexOf("^XZ", StringComparison.Ordinal);
        var lastXz = zpl1.LastIndexOf("^XZ", StringComparison.Ordinal);
        firstXz.Should().Be(lastXz);
    }

    [Fact]
    public void FIND_PRINT_022_IdempotencyKey_PreservedInBuilders()
    {
        var zplBuilder = new ZplBuilder();
        var zplDoc = zplBuilder.Text(10, 10, "Hello").Build("Label", "idem-zpl-002");
        zplDoc.IdempotencyKey.Should().Be("idem-zpl-002");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ZplBuilder_Text_And_Barcodes_ValidateHeightAndWidth(int invalidDim)
    {
        var b = new ZplBuilder();
        var actTextH = () => b.Text(0, 0, "Test", height: invalidDim);
        actTextH.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("height");

        var actTextW = () => b.Text(0, 0, "Test", width: invalidDim);
        actTextW.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("width");

        var actBc128 = () => b.BarcodeCode128(0, 0, "123", height: invalidDim);
        actBc128.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("height");

        var actBc39 = () => b.BarcodeCode39(0, 0, "123", height: invalidDim);
        actBc39.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("height");

        var actBcEan = () => b.BarcodeEan13(0, 0, "123456789012", height: invalidDim);
        actBcEan.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("height");
    }

    [Fact]
    public void PRNT_001_ZplBuilder_SafeMode_ThrowsOnRaw()
    {
        var builder = new ZplBuilder(safeMode: true);
        var act = () => builder.Raw("^JUS");
        act.Should().Throw<NotSupportedException>().WithMessage("*safe mode*");
    }

    [Fact]
    public void PRNT_003_ZplBuilder_Build_HandlesChunksCorrectly()
    {
        var builder = new ZplBuilder();
        builder.Text(10, 10, "First Line");
        builder.Text(10, 40, "Second Line");

        var doc = builder.Build();
        var zpl = Encoding.UTF8.GetString(doc.GetBytes());

        zpl.Should().StartWith("^XA");
        zpl.Should().Contain("^FO10,10");
        zpl.Should().Contain("^FO10,40");
        zpl.Should().EndWith($"^XZ{Environment.NewLine}");
    }

    [Fact]
    public void Concurrency_ZplBuilder_DeterministicSequentialOutput()
    {
        // 100 sequential runs must produce bit-for-bit identical byte outputs
        byte[]? baseline = null;
        for (var i = 0; i < 100; i++)
        {
            var doc = new ZplBuilder()
                .Text(10, 20, "Fixed Header")
                .Box(10, 50, 200, 100, 2)
                .BarcodeCode128(10, 160, "123456")
                .Build();

            var bytes = doc.GetBytes();
            if (baseline == null)
            {
                baseline = bytes;
            }
            else
            {
                bytes.Should().Equal(baseline);
            }
        }
    }

    [Fact]
    public void Fuzz_ZplBuilder_EscapesComplexStrings_WithoutCorruption()
    {
        var malicious = "Test ^XA ^XZ ~SD25 _5E ^FO0,0^FDInjected^FS";
        var doc = new ZplBuilder()
            .Text(10, 10, malicious)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FH_");
        // Verify no raw unescaped ^XA or ~SD25 in the field data
        zpl.Should().NotContain("^FDTest ^XA");
    }

    [Theory]
    [InlineData("123^456")]
    [InlineData("123~456")]
    public void Fuzz_ZplBuilder_Barcodes_RejectCommandDelimiters(string barcode)
    {
        var b = new ZplBuilder();
        Assert.Throws<ArgumentException>(() => b.BarcodeCode128(0, 0, barcode));
        Assert.Throws<ArgumentException>(() => b.BarcodeCode39(0, 0, barcode));
        Assert.Throws<ArgumentException>(() => b.BarcodeEan13(0, 0, barcode));
    }

    [Fact]
    public async Task ZplPrintDocument_WriteToAsync_And_IsEmpty_BehaveCorrectly()
    {
        var emptyDoc = new ZplBuilder().Build();
        var emptyDocExplicit = new ZplPrintDocument("", "EmptyDoc");
        emptyDocExplicit.IsEmpty.Should().BeTrue();

        // Use 5000 characters so sw.WriteAsync exceeds the 4096-char buffer and performs async stream write
        var doc = new ZplBuilder().Text(10, 10, new string('A', 5000)).Build("Doc1");
        doc.IsEmpty.Should().BeFalse();

        var actNullStream = async () => await doc.WriteToAsync(null!);
        await actNullStream.Should().ThrowAsync<ArgumentNullException>().WithParameterName("stream");

        var syncContext = new DisallowingSynchronizationContext();
        using var trackingStream = new TrackingStream();

        await Task.Run(async () =>
        {
            SynchronizationContext.SetSynchronizationContext(syncContext);
            try
            {
                await doc.WriteToAsync(trackingStream).ConfigureAwait(false);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        });

        syncContext.WasViolated.Should().BeFalse();
        trackingStream.WasFlushAsyncCalled.Should().BeTrue();
        trackingStream.IsDisposed.Should().BeFalse();
        trackingStream.CanWrite.Should().BeTrue();

        var streamedContent = Encoding.UTF8.GetString(trackingStream.ToArray());
        streamedContent.Should().Be(Encoding.UTF8.GetString(doc.GetBytes()));
    }

    [Fact]
    public async Task ZplPrintDocument_WriteToAsync_ShortDocument_ConfiguresAwaitFalseOnFlush()
    {
        var doc = new ZplBuilder().Text(10, 10, "Short ZPL").Build("DocShort");
        var syncContext = new DisallowingSynchronizationContext();
        using var trackingStream = new TrackingStream();

        await Task.Run(async () =>
        {
            SynchronizationContext.SetSynchronizationContext(syncContext);
            try
            {
                await doc.WriteToAsync(trackingStream).ConfigureAwait(false);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        });

        syncContext.WasViolated.Should().BeFalse();
        trackingStream.WasFlushAsyncCalled.Should().BeTrue();
    }

    [Fact]
    public void ZplBuilder_Text_TrailingNewline_Appended()
    {
        var doc = new ZplBuilder().Text(10, 20, "NewlineTest").Build();
        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain($"^FS{Environment.NewLine}");
    }

    [Fact]
    public void ZplBuilder_BarcodeCode39_And_BarcodeEan13_ShowText_Toggles()
    {
        var doc39True = new ZplBuilder().BarcodeCode39(10, 10, "CODE39", showText: true).Build();
        var doc39False = new ZplBuilder().BarcodeCode39(10, 10, "CODE39", showText: false).Build();

        Encoding.UTF8.GetString(doc39True.GetBytes()).Should().Contain(",Y,N");
        Encoding.UTF8.GetString(doc39False.GetBytes()).Should().Contain(",N,N");

        var docEanTrue = new ZplBuilder().BarcodeEan13(10, 10, "123456789012", showText: true).Build();
        var docEanFalse = new ZplBuilder().BarcodeEan13(10, 10, "123456789012", showText: false).Build();

        Encoding.UTF8.GetString(docEanTrue.GetBytes()).Should().Contain(",Y,N");
        Encoding.UTF8.GetString(docEanFalse.GetBytes()).Should().Contain(",N,N");
    }

    [Fact]
    public void ZplBuilder_QrCode_Escapes_Separate_Caret_Tilde_And_Underscore()
    {
        var docCaretOnly = new ZplBuilder().QrCode(10, 10, "only^caret").Build();
        var docTildeOnly = new ZplBuilder().QrCode(10, 10, "only~tilde").Build();
        var docWithUnderscoreAndCaret = new ZplBuilder().QrCode(10, 10, "under_and^caret").Build();

        var zplCaret = Encoding.UTF8.GetString(docCaretOnly.GetBytes());
        zplCaret.Should().Contain("^FH_^FDQA,only_5Ecaret^FS");

        var zplTilde = Encoding.UTF8.GetString(docTildeOnly.GetBytes());
        zplTilde.Should().Contain("^FH_^FDQA,only_7Etilde^FS");

        var zplUnder = Encoding.UTF8.GetString(docWithUnderscoreAndCaret.GetBytes());
        zplUnder.Should().Contain("^FH_^FDQA,under_5Fand_5Ecaret^FS");
    }

    [Fact]
    public void ZplBuilder_Text_Escapes_Separate_Caret_Tilde_And_Underscore()
    {
        var docCaretOnly = new ZplBuilder().Text(10, 10, "only^caret").Build();
        var docTildeOnly = new ZplBuilder().Text(10, 10, "only~tilde").Build();
        var docWithUnderscoreAndCaret = new ZplBuilder().Text(10, 10, "under_and^caret").Build();

        var zplCaret = Encoding.UTF8.GetString(docCaretOnly.GetBytes());
        zplCaret.Should().Contain("^FH_^FDonly_5Ecaret^FS");

        var zplTilde = Encoding.UTF8.GetString(docTildeOnly.GetBytes());
        zplTilde.Should().Contain("^FH_^FDonly_7Etilde^FS");

        var zplUnder = Encoding.UTF8.GetString(docWithUnderscoreAndCaret.GetBytes());
        zplUnder.Should().Contain("^FH_^FDunder_5Fand_5Ecaret^FS");
    }

    [Fact]
    public void ZplBuilder_Circle_ThrowsOnNegativeArguments()
    {
        var b = new ZplBuilder();
        var act1 = () => b.Circle(0, 0, -1, 2);
        var act2 = () => b.Circle(0, 0, 10, -1);

        act1.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("diameter");
        act2.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("borderThickness");
    }

    [Fact]
    public void ZplBuilder_Ellipse_ThrowsOnNegativeArguments()
    {
        var b = new ZplBuilder();
        var act1 = () => b.Ellipse(0, 0, -1, 10, 2);
        var act2 = () => b.Ellipse(0, 0, 10, -1, 2);
        var act3 = () => b.Ellipse(0, 0, 10, 10, -1);

        act1.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("width");
        act2.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("height");
        act3.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("borderThickness");
    }

    [Fact]
    public void ZplBuilder_DiagonalLine_ThrowsOnNegativeArguments_And_SupportsDirections()
    {
        var b = new ZplBuilder();
        var act1 = () => b.DiagonalLine(0, 0, -1, 10, 2);
        var act2 = () => b.DiagonalLine(0, 0, 10, -1, 2);
        var act3 = () => b.DiagonalLine(0, 0, 10, 10, -1);

        act1.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("width");
        act2.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("height");
        act3.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("borderThickness");

        var docRight = new ZplBuilder().DiagonalLine(10, 20, 100, 50, 2, rightLeaning: true).Build();
        var docLeft = new ZplBuilder().DiagonalLine(10, 20, 100, 50, 2, rightLeaning: false).Build();

        Encoding.UTF8.GetString(docRight.GetBytes()).Should().Contain(",B,R^FS");
        Encoding.UTF8.GetString(docLeft.GetBytes()).Should().Contain(",B,L^FS");
    }
}
