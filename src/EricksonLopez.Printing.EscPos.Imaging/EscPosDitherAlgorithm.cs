// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos.Imaging;

/// <summary>
/// Specifies the dithering algorithm used to convert color or grayscale pixels into a 1-bit monochrome printer bitmap.
/// </summary>
public enum EscPosDitherAlgorithm
{
    /// <summary>
    /// Applies direct luminance threshold comparison, suitable for line art, solid logos, and text.
    /// </summary>
    Threshold = 0,

    /// <summary>
    /// Applies Floyd-Steinberg error diffusion dithering, suitable for photographs and shaded continuous-tone images.
    /// </summary>
    FloydSteinberg = 1
}
