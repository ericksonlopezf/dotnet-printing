// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.Zpl.Imaging;

/// <summary>
/// Specifies the dithering algorithm used to convert color or grayscale pixels into a 1-bit monochrome label bitmap.
/// </summary>
public enum ZplDitherAlgorithm
{
    /// <summary>
    /// Specifies direct luminance threshold comparison, suitable for solid black and white logos, line art, and text.
    /// </summary>
    Threshold = 0,

    /// <summary>
    /// Specifies Floyd-Steinberg error diffusion dithering, suitable for continuous-tone photos and shaded illustrations.
    /// </summary>
    FloydSteinberg = 1
}
