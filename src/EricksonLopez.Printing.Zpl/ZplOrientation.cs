// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.Zpl;

/// <summary>
/// Specifies text or element orientation in ZPL II format.
/// </summary>
public enum ZplOrientation
{
    /// <summary>
    /// Specifies normal orientation without rotation (0 degrees).
    /// </summary>
    Normal = 'N',

    /// <summary>
    /// Specifies orientation rotated 90 degrees clockwise.
    /// </summary>
    Rotated90 = 'R',

    /// <summary>
    /// Specifies inverted orientation rotated 180 degrees.
    /// </summary>
    Inverted180 = 'I',

    /// <summary>
    /// Specifies bottom-up orientation rotated 270 degrees clockwise.
    /// </summary>
    BottomUp270 = 'B'
}
