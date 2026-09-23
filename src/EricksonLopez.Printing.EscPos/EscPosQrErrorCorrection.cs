// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Printing.EscPos;

/// <summary>Specifies the error correction level for ESC/POS two-dimensional QR codes.</summary>
public enum EscPosQrErrorCorrection : byte
{
    /// <summary>Recovers approximately 7% of damaged or unreadable data.</summary>
    L = 48,

    /// <summary>Recovers approximately 15% of damaged or unreadable data.</summary>
    M = 49,

    /// <summary>Recovers approximately 25% of damaged or unreadable data.</summary>
    Q = 50,

    /// <summary>Recovers approximately 30% of damaged or unreadable data.</summary>
    H = 51
}
