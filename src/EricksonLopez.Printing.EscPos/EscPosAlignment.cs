// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos;

/// <summary>Specifies horizontal text and graphics alignment for ESC/POS commands.</summary>
public enum EscPosAlignment : byte
{
    /// <summary>Aligns content to the left margin.</summary>
    Left = 0,

    /// <summary>Centers content between margins.</summary>
    Center = 1,

    /// <summary>Aligns content to the right margin.</summary>
    Right = 2
}
