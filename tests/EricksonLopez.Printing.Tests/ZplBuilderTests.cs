// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;
using AwesomeAssertions;
using EricksonLopez.Printing.Zpl;
using Xunit;

namespace EricksonLopez.Printing.Tests;

public sealed class ZplBuilderTests
{
    [Fact]
    public void Build_ZplLabel_ContainsValidZplTags()
    {
        var doc = new ZplBuilder()
            .Box(50, 50, 400, 300, 3)
            .Text(70, 70, "PRODUCT: WIDGET-A")
            .BarcodeCode128(70, 130, "WID-123456", 60)
            .QrCode(70, 220, "https://ericksonlopez.dev")
            .Build("PalletLabel");

        doc.Should().NotBeNull();
        doc.DocumentName.Should().Be("PalletLabel");

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());

        zpl.Should().StartWith("^XA");
        zpl.Should().Contain("^FO50,50^GB400,300,3^FS");
        zpl.Should().Contain("^FO70,70^A0N,30,30^FDPRODUCT: WIDGET-A^FS");
        zpl.Should().Contain("^FO70,130^BCN,60,Y,N,N^FDWID-123456^FS");
        zpl.Should().Contain("^FO70,220^BQN,2,4^FDQA,https://ericksonlopez.dev^FS");
        zpl.TrimEnd().Should().EndWith("^XZ");
    }

    [Fact]
    public void Text_WithRotatedOrientation_EmitsCorrectOrientationCode()
    {
        var doc = new ZplBuilder()
            .Text(10, 20, "Rotated90", orientation: ZplOrientation.Rotated90)
            .Text(10, 40, "Inverted180", orientation: ZplOrientation.Inverted180)
            .Text(10, 60, "BottomUp270", orientation: ZplOrientation.BottomUp270)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^A0R,30,30^FDRotated90^FS");
        zpl.Should().Contain("^A0I,30,30^FDInverted180^FS");
        zpl.Should().Contain("^A0B,30,30^FDBottomUp270^FS");
    }

    [Fact]
    public void BarcodeCode128_NoTextAndCustomHeight_EmitsCorrectZpl()
    {
        var doc = new ZplBuilder()
            .BarcodeCode128(100, 150, "BAR123", height: 120, showText: false, orientation: ZplOrientation.Rotated90)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO100,150^BCR,120,N,N,N^FDBAR123^FS");
    }

    [Fact]
    public void QrCode_CustomMagnificationAndOrientation_EmitsCorrectZpl()
    {
        var doc = new ZplBuilder()
            .QrCode(200, 300, "QR-DATA", magnification: 8, orientation: ZplOrientation.Inverted180)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO200,300^BQI,2,8^FDQA,QR-DATA^FS");
    }

    [Fact]
    public void Box_CustomThickness_EmitsCorrectZpl()
    {
        var doc = new ZplBuilder()
            .Box(0, 0, 800, 600, borderThickness: 5)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO0,0^GB800,600,5^FS");
    }

    [Fact]
    public void Raw_AppendsRawZplDirectly()
    {
        var doc = new ZplBuilder()
            .Raw("^FX Custom comment ^FS")
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FX Custom comment ^FS");
    }

    [Fact]
    public void Text_WithZplFontEnum_EmitsCorrectZpl()
    {
        var doc = new ZplBuilder()
            .Text(10, 20, "FontA-Text", ZplFont.FontA, 40, 40, ZplOrientation.Normal)
            .Text(10, 70, "FontD-Text", ZplFont.FontD, 30, 30, ZplOrientation.Rotated90)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO10,20^AAN,40,40^FDFontA-Text^FS");
        zpl.Should().Contain("^FO10,70^ADR,30,30^FDFontD-Text^FS");
    }

    [Fact]
    public void BarcodeCode39_EmitsCorrectZpl()
    {
        var doc = new ZplBuilder()
            .BarcodeCode39(50, 100, "CODE39-TEST", height: 80, showText: true, orientation: ZplOrientation.Normal)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO50,100^B3N,N,80,Y,N^FDCODE39-TEST^FS");
    }

    [Fact]
    public void BarcodeEan13_EmitsCorrectZpl()
    {
        var doc = new ZplBuilder()
            .BarcodeEan13(50, 200, "4006381333931", height: 75, showText: false, orientation: ZplOrientation.Rotated90)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO50,200^BER,75,N,N^FD4006381333931^FS");
    }

    [Fact]
    public void Shapes_Circle_Ellipse_DiagonalLine_EmitCorrectZpl()
    {
        var doc = new ZplBuilder()
            .Circle(100, 100, diameter: 50, borderThickness: 3)
            .Ellipse(200, 100, width: 80, height: 40, borderThickness: 2)
            .DiagonalLine(300, 100, width: 60, height: 60, borderThickness: 4, rightLeaning: true)
            .DiagonalLine(400, 100, width: 60, height: 60, borderThickness: 4, rightLeaning: false)
            .Build();

        var zpl = Encoding.UTF8.GetString(doc.GetBytes());
        zpl.Should().Contain("^FO100,100^GC50,3,B^FS");
        zpl.Should().Contain("^FO200,100^GE80,40,2,B^FS");
        zpl.Should().Contain("^FO300,100^GD60,60,4,B,R^FS");
        zpl.Should().Contain("^FO400,100^GD60,60,4,B,L^FS");
    }

    [Fact]
    public void NullInputs_ThrowArgumentNullException()
    {
        var builder = new ZplBuilder();

        var actText = () => builder.Text(0, 0, null!);
        var actBarcode = () => builder.BarcodeCode128(0, 0, null!);
        var actCode39 = () => builder.BarcodeCode39(0, 0, null!);
        var actEan13 = () => builder.BarcodeEan13(0, 0, null!);
        var actQr = () => builder.QrCode(0, 0, null!);
        var actRaw = () => builder.Raw(null!);

        actText.Should().Throw<ArgumentNullException>();
        actBarcode.Should().Throw<ArgumentNullException>();
        actCode39.Should().Throw<ArgumentNullException>();
        actEan13.Should().Throw<ArgumentNullException>();
        actQr.Should().Throw<ArgumentNullException>();
        actRaw.Should().Throw<ArgumentNullException>();
    }
}
