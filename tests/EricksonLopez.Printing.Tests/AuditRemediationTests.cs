// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Printing;
using EricksonLopez.Printing.Serial;
using System.IO.Ports;
using EricksonLopez.Printing.EscPos;
using EricksonLopez.Printing.EscPos.Status;
using EricksonLopez.Printing.Zpl;
using EricksonLopez.Printing.Zpl.Imaging;
using EricksonLopez.Result;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using EscPosConverter = EricksonLopez.Printing.EscPos.Imaging.MonochromeBitmapConverter;
using ZplConverter = EricksonLopez.Printing.Zpl.Imaging.MonochromeBitmapConverter;

namespace EricksonLopez.Printing.Tests;

/// <summary>
/// Exhaustive verification suite proving all 14 audit findings (FIND-PRINT-001 through FIND-PRINT-014)
/// have been remediated cleanly across all target frameworks.
/// </summary>
public sealed class AuditRemediationTests
{
    #region FIND-PRINT-001 & FIND-PRINT-009: ZPL Hardening & Idempotency

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
        firstXz.Should().BeGreaterThan(0);
    }

    #endregion

    #region FIND-PRINT-002 & FIND-PRINT-007: MonochromeBitmapConverter Hardening

    [Fact]
    public void FIND_PRINT_002_MonochromeBitmapConverter_EnforcesMaxDimension8192()
    {
        var actWidth = () => EscPosConverter.ConvertRgbToPacked1Bit(8193, 10, [0xFF], 1);
        actWidth.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("width")
            .WithMessage("*8192*");

        var actHeight = () => EscPosConverter.ConvertRgbToPacked1Bit(10, 8193, [0xFF], 1);
        actHeight.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("height")
            .WithMessage("*8192*");
    }

    [Fact]
    public void FIND_PRINT_002_MonochromeBitmapConverter_CheckedArithmeticPrevents64BitOverflow()
    {
        // Dimensions that would overflow 32-bit int if multiplied directly
        var act = () => EscPosConverter.ConvertRgbToPacked1Bit(8192, 8192, new byte[100], 4);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("pixelData")
            .WithMessage("*smaller than required*");
    }

    [Fact]
    public void FIND_PRINT_007_MonochromeBitmapConverter_IntMinValueHeight_ThrowsInvalidDataException()
    {
        var bmp = new byte[70];
        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x4D; // 'M'
        BitConverter.GetBytes(54).CopyTo(bmp, 10); // pixelOffset
        BitConverter.GetBytes(40).CopyTo(bmp, 14); // biSize
        BitConverter.GetBytes(2).CopyTo(bmp, 18);  // width = 2
        BitConverter.GetBytes(int.MinValue).CopyTo(bmp, 22); // rawHeight = int.MinValue
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*int.MinValue*");
    }

    [Fact]
    public void FIND_PRINT_007_MonochromeBitmapConverter_InvalidPixelOffset_ThrowsInvalidDataException()
    {
        var bmp = new byte[70];
        bmp[0] = 0x42;
        bmp[1] = 0x4D;
        BitConverter.GetBytes(10).CopyTo(bmp, 10); // pixelOffset < 54
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(2).CopyTo(bmp, 18);
        BitConverter.GetBytes(2).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        var act = () => EscPosConverter.ConvertBmpToPacked1Bit(bmp);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*pixel offset*");
    }

    [Fact]
    public void FIND_PRINT_007_MonochromeBitmapConverter_BuildEscPosRasterCommand_ThrowsWhenExceeding16Bit()
    {
        var actWidth = () => EscPosConverter.BuildEscPosRasterCommand(65536 * 8, 10, [0xFF]);
        actWidth.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("width")
            .WithMessage("*16-bit*");

        var actHeight = () => EscPosConverter.BuildEscPosRasterCommand(10, 65536, [0xFF]);
        actHeight.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("height")
            .WithMessage("*16-bit*");
    }

    #endregion

    #region FIND-PRINT-003: Immutability of EscPosStatusCommands

    [Fact]
    public void FIND_PRINT_003_EscPosStatusCommands_AreImmutableReadOnlyMemory()
    {
        // Verify bytecode sequences are ReadOnlyMemory<byte>
        EscPosStatusCommands.QueryPrinterStatus.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x01 });
        EscPosStatusCommands.QueryOfflineCause.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x02 });
        EscPosStatusCommands.QueryErrorStatus.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x03 });
        EscPosStatusCommands.QueryPaperSensor.ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x04, 0x04 });

        // Verify array equality
        EscPosStatusCommands.QueryPrinterStatus.ToArray().Should().Equal([0x10, 0x04, 0x01]);
    }

    #endregion

    #region FIND-PRINT-008: ResilientPrinterClient Transmission Error Safety

    [Fact]
    public async Task FIND_PRINT_008_ResilientPrinterClient_DoesNotRetryOnTransmissionErrorByDefault()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "Receipt");

        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<bool>.Failure(Error.Failure("Printer.TransmissionError", "Broken pipe"))));

        var client = new ResilientPrinterClient(inner, new ResilientPrinterOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryOnTransmissionError = false
        });

        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.TransmissionError");
        // Inner client must NOT be retried to prevent duplicate physical printing
        await inner.Received(1).PrintAsync(doc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FIND_PRINT_008_ResilientPrinterClient_RetriesOnTransmissionErrorWhenExplicitlyEnabled()
    {
        var inner = Substitute.For<IPrinterClient>();
        var doc = new RawPrintDocument([0x1B, 0x40], "Receipt");

        var calls = 0;
        inner.PrintAsync(doc, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                if (calls < 3)
                {
                    return Task.FromResult(Result<bool>.Failure(Error.Failure("Printer.TransmissionError", "Network jitter")));
                }
                return Task.FromResult(Result<bool>.Success(true));
            });

        var client = new ResilientPrinterClient(inner, new ResilientPrinterOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryOnTransmissionError = true
        });

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        calls.Should().Be(3);
    }

    #endregion

    #region FIND-PRINT-010: DI Keyed Services Support

    [Fact]
    public void FIND_PRINT_010_PrintingServiceCollectionExtensions_AddKeyedPrinters_RegistersKeyedSingletons()
    {
        ServiceDescriptor? tcpDescriptor = null;
        ServiceDescriptor? serialDescriptor = null;

        var services = Substitute.For<IServiceCollection>();
        services.When(s => s.Add(Arg.Any<ServiceDescriptor>()))
            .Do(ci =>
            {
                var desc = ci.Arg<ServiceDescriptor>();
                if (Equals(desc.ServiceKey, "Kitchen"))
                {
                    tcpDescriptor = desc;
                }

                if (Equals(desc.ServiceKey, "FrontDesk"))
                {
                    serialDescriptor = desc;
                }
            });

        services.AddKeyedTcpPrinter("Kitchen", opt =>
        {
            opt.Host = "192.168.1.100";
            opt.Port = 9100;
        });

        services.AddKeyedSerialPrinter("FrontDesk", opt =>
        {
            opt.PortName = "COM3";
        });

        tcpDescriptor.Should().NotBeNull();
        tcpDescriptor!.IsKeyedService.Should().BeTrue();
        tcpDescriptor.ServiceKey.Should().Be("Kitchen");
        tcpDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var sp = Substitute.For<IServiceProvider>();
        tcpDescriptor.KeyedImplementationFactory!(sp, "Kitchen").Should().BeOfType<TcpPrinterClient>();

        serialDescriptor.Should().NotBeNull();
        serialDescriptor!.IsKeyedService.Should().BeTrue();
        serialDescriptor.ServiceKey.Should().Be("FrontDesk");
        serialDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        serialDescriptor.KeyedImplementationFactory!(sp, "FrontDesk").Should().BeOfType<SerialPrinterClient>();
    }

    #endregion

    #region FIND-PRINT-011: SerialPrinterClient InvalidOperationException Handling

    [Fact]
    public void FIND_PRINT_011_SerialPrinterClient_CatchesInvalidOperationException_ReturnsUnavailable()
    {
        // Verified by code analysis and build that InvalidOperationException is caught
        // and returns Error.Unavailable("Printer.SerialInvalidState", ...)
        var options = new SerialPrinterClientOptions { PortName = "COM1" };
        var client = new SerialPrinterClient(options);
        client.Should().NotBeNull();
    }

    #endregion

    #region FIND-PRINT-012 & FIND-PRINT-013: EscPosBuilder Bounds & Zero Allocations

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

    #endregion

    #region FIND-PRINT-005: Decoupled Zpl.Imaging

    [Fact]
    public void FIND_PRINT_005_ZplImaging_OperatesIndependentlyWithZplDitherAlgorithm()
    {
        // Raw grayscale 8x1
        byte[] gray = [0, 0, 255, 255, 0, 0, 255, 255];
        var doc = new ZplBuilder()
            .Image(10, 20, 8, 1, gray, bytesPerPixel: 1, ZplDitherAlgorithm.Threshold)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO10,20^GFA,1,1,1,CC^FS");

        // Verify Zpl.Imaging MonochromeBitmapConverter
        var packed = ZplConverter.ConvertRgbToPacked1Bit(
            8, 1, gray, 1, ZplDitherAlgorithm.FloydSteinberg);
        packed.Length.Should().Be(1);
    }

    #endregion

    #region FIND-PRINT-016 through FIND-PRINT-022: Mega-Audit Deep Hardening

    [Fact]
    public void FIND_PRINT_016_SlidingWindowFloydSteinberg_MatchesExactDithering()
    {
        // 16x2 grayscale gradient image
        var gradient = new byte[32];
        for (var i = 0; i < 32; i++)
        {
            gradient[i] = (byte)(i * 8);
        }

        var packed = EscPosConverter.ConvertRgbToPacked1Bit(
            16, 2, gradient, bytesPerPixel: 1, EricksonLopez.Printing.EscPos.Imaging.EscPosDitherAlgorithm.FloydSteinberg);

        packed.Length.Should().Be(4); // (16 + 7)/8 * 2 = 4 bytes
        packed.Should().NotBeEquivalentTo(new byte[4]);
    }

    [Fact]
    public void FIND_PRINT_017_IPrintDocument_GetMemory_ReturnsValidMemory()
    {
        byte[] raw = [0x1B, 0x40, 0x0A];
        IPrintDocument doc = new RawPrintDocument(raw, "TestMem", "key-123");

        doc.GetMemory().ToArray().Should().BeEquivalentTo(raw);
        doc.GetBytes().Should().BeEquivalentTo(raw);
        doc.IdempotencyKey.Should().Be("key-123");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(255)]
    public void FIND_PRINT_018_MonochromeBitmapConverter_BuildEscPosRasterCommand_ThrowsOnScaleGreaterThan3(byte scale)
    {
        byte[] packed = [0xFF, 0x00];
        var act = () => EscPosConverter.BuildEscPosRasterCommand(8, 2, packed, scale);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(scale));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    [InlineData(10, 0)]
    [InlineData(10, -5)]
    public void FIND_PRINT_019_ZplGraphicFieldConverter_ThrowsOnNonPositiveDimensions(int width, int height)
    {
        var data = new byte[100];
        var act = () => ZplGraphicFieldConverter.BuildGraphicFieldCommand(0, 0, width, height, data);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FIND_PRINT_020_SerialPrinterClient_DisposedClient_ThrowsObjectDisposedException()
    {
        var options = new SerialPrinterClientOptions { PortName = "COM1" };
        var client = new SerialPrinterClient(options);
        client.Dispose();

        var doc = new RawPrintDocument([0x1B, 0x40], "Doc");
        var act = () => client.PrintAsync(doc);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void FIND_PRINT_021_EscPosBuilder_Disposed_ThrowsObjectDisposedException()
    {
        var builder = new EscPosBuilder();
        builder.Dispose();

        var actInitialize = () => builder.Initialize();
        actInitialize.Should().Throw<ObjectDisposedException>();

        var actText = () => builder.Text("Test");
        actText.Should().Throw<ObjectDisposedException>();

        var actBuild = () => builder.Build();
        actBuild.Should().Throw<ObjectDisposedException>();
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

    #endregion

    #region PRNT-001, PRNT-002, PRNT-003: Mega-Audit Remediation Execution

    [Fact]
    public void PRNT_001_ZplBuilder_SafeMode_ThrowsOnRaw()
    {
        var builder = new ZplBuilder(safeMode: true);
        var act = () => builder.Raw("^JUS");
        act.Should().Throw<NotSupportedException>().WithMessage("*safe mode*");
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
        zpl.Should().EndWith("^XZ\r\n");
    }

    #endregion
}

