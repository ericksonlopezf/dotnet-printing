<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0015: Rejection of Embedded ZPL Template Engine

> **Navigation:** [⬅️ ADR-0014 (Rejection of Samba / SMB)](adr-0014-rejection-of-samba-and-network-file-shares.md) | [ADR Index](README.md) | [Next: ADR-0016 (Rejection of Windows GDI) ➡️](adr-0016-rejection-of-windows-gdi-system-drawing.md)

---

## Status
**Rejected** (2026-09-03)

## Context
When designing logistics and parcel labels, companies often work with predefined label layouts where variable fields (customer name, tracking number, address) are replaced dynamically using placeholders (`{{tracking_number}}`, `{{customer_name}}`).

We evaluated creating a custom template engine inside `EricksonLopez.Printing.Zpl` with custom token parsing, loops, and conditional statements.

## Decision
Building an embedded template engine within the library is **formally rejected**.

## Rationale
1. **Single Responsibility Principle (SRP):** The purpose of `EricksonLopez.Printing.Zpl` is to emit syntactically valid, high-performance ZPL II command byte sequences. A template engine requires expression parsers, grammar lexers, and variable interpolation logic, creating massive feature creep.
2. **Superior Existing .NET Ecosystem:** The .NET ecosystem already offers mature, battle-tested, high-performance templating engines (such as `Fluid`, `Scriban`, `Handlebars.Net`, or C# `FormattableString`).
3. **First-Class Escape Hatch Provided:** With `ZplBuilder.Raw(string rawZpl)` and `RawPrintDocument`, developers can render their template using any template engine of their choice and stream the result directly to physical printers.

## Related Decisions
- **Complements:** [ADR-0007](adr-0007-strongly-typed-zpl-fonts.md) (Typed fluent API handles efficient generation, while `Raw()` and `RawPrintDocument` delegate templating to dedicated application-layer libraries).

## Recommended Alternative
```csharp
// Render template with preferred template engine
string zplPayload = TemplateEngine.Render(myZplTemplate, model);

// Dispatch to printer via RawPrintDocument
var document = new RawPrintDocument(Encoding.UTF8.GetBytes(zplPayload), "ShippingLabel");
Result<bool> result = await printerClient.PrintAsync(document);
```

---

> **Navigation:** [⬅️ ADR-0014 (Rejection of Samba / SMB)](adr-0014-rejection-of-samba-and-network-file-shares.md) | [ADR Index](README.md) | [Next: ADR-0016 (Rejection of Windows GDI) ➡️](adr-0016-rejection-of-windows-gdi-system-drawing.md)
