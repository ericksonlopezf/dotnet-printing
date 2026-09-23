// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Printing.SignalR;

/// <summary>
/// Represents a print job message transmitted over SignalR containing the document payload and target printer details.
/// </summary>
/// <param name="JobId">The unique identifier of the print job</param>
/// <param name="PrinterName">The target physical printer name or workstation identifier</param>
/// <param name="DocumentName">The name of the document being printed</param>
/// <param name="PayloadBase64">The raw printer commands encoded in Base64</param>
/// <param name="CreatedAt">The timestamp when the job was dispatched</param>
public sealed record PrintJobMessage(
    string JobId,
    string PrinterName,
    string DocumentName,
    string PayloadBase64,
    DateTimeOffset CreatedAt);
