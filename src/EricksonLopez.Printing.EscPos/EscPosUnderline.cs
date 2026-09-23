// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos;

/// <summary>Specifies the underline mode for ESC/POS text output.</summary>
public enum EscPosUnderline : byte
{
    /// <summary>Disables text underlining.</summary>
    None = 0,

    /// <summary>Enables single-dot thickness text underlining.</summary>
    SingleDot = 1,

    /// <summary>Enables double-dot thickness text underlining.</summary>
    DoubleDot = 2
}
