<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0007: Typed ZPL Font Overload with `ZplFont` Enum

> **Navigation:** [⬅️ ADR-0006 (ZplBuilder Resource Management)](adr-0006-zpl-builder-resource-management.md) | [ADR Index](README.md) | [Next: ADR-0008 (DI Lifecycles & Serial Concurrency) ➡️](adr-0008-di-client-lifecycles-and-serial-concurrency.md)

---

## Status
**Accepted** (2026-09-03)

## Context
In the Zebra Programming Language (ZPL II) specification, the font selection command `^Afo,h,w` expects a font identifier corresponding to individual characters recognized by Zebra firmware (e.g., `'0'` for the default scalable font, or `'A'` through `'H'` for bitmap fonts).

In version v1.0.0, the `Text` method accepted `char font = '0'`. While flexible, an arbitrary `char` lacks compile-time safety, allowing callers to pass invalid characters (e.g., `'\n'`, `'?'`, `'$'`) that generate malformed ZPL commands silently ignored by hardware.

## Decision
1. Introduce the strongly typed enum `ZplFont`:
   ```csharp
   public enum ZplFont
   {
       Font0 = '0',
       FontA = 'A',
       FontB = 'B',
       FontC = 'C',
       FontD = 'D',
       FontE = 'E',
       FontF = 'F',
       FontG = 'G',
       FontH = 'H'
   }
   ```
2. Provide a typed overload in `ZplBuilder.Text(...)`:
   ```csharp
   public ZplBuilder Text(
       int x,
       int y,
       string text,
       ZplFont font,
       int height = 30,
       int width = 30,
       ZplOrientation orientation = ZplOrientation.Normal)
       => Text(x, y, text, height, width, (char)font, orientation);
   ```
3. Retain the underlying `char font` overload to accommodate custom fonts stored on printer flash storage (`E:FONT.FNT`) and preserve full backward compatibility.

## Related Decisions
- **Complements:** [ADR-0006](adr-0006-zpl-builder-resource-management.md) (ZPL API ergonomics).
- **Motivates Rejections:** [ADR-0015](adr-0015-rejection-of-built-in-zpl-template-engine.md) (Rejection of template engines in favor of typed fluent builders and `Raw` escape hatch).

## Consequences
- Enhanced Developer Experience (DX) with IDE autocompletion for standard Zebra fonts.
- Prevents typographical errors at compile time.
- Zero breaking changes for existing code.

---

> **Navigation:** [⬅️ ADR-0006 (ZplBuilder Resource Management)](adr-0006-zpl-builder-resource-management.md) | [ADR Index](README.md) | [Next: ADR-0008 (DI Lifecycles & Serial Concurrency) ➡️](adr-0008-di-client-lifecycles-and-serial-concurrency.md)
