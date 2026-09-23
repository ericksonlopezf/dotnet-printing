<!-- Copyright © Erickson Lopez. MIT License. -->
# Contributing to EricksonLopez.Printing

Thank you for your interest in contributing to **`EricksonLopez.Printing`**!

This project provides high-performance, Native AOT compatible printing libraries for modern .NET (ESC/POS, ZPL II, TCP/Serial transports, and SignalR cloud-to-edge dispatch). We welcome issues, bug reports, documentation enhancements, and pull requests.

---

## Code of Conduct

All contributors and maintainers are expected to adhere to our [Code of Conduct](CODE_OF_CONDUCT.md). Please treat all participants with respect and professionalism.

---

## Architectural Invariants

Before proposing or implementing changes, review the following non-negotiable architectural invariants:

1. **Native AOT First:** All core and satellite packages must compile with `<IsAotCompatible>true</IsAotCompatible>` and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`. Zero runtime reflection in hot paths, zero unpreserved dynamic code generation.
2. **Zero Native Dependencies in Core:** Core packages (`EricksonLopez.Printing`, `EscPos`, `Zpl`) must remain free from unmanaged C/C++ libraries, graphics runtimes, or OS print spoolers.
3. **Result Pattern over Exceptions:** Hardware I/O failures, timeouts, and network disconnections must return `Result<bool>` with canonical error types (`ErrorType.Unavailable`), never throw unhandled runtime exceptions.
4. **One Type Per File:** Each C# top-level class, struct, record, interface, enum, or delegate must reside in its own dedicated file matching the type name.
5. **XML Documentation (`CS1591`):** All publicly exposed APIs must include comprehensive XML documentation comments (`<summary>`, `<param>`, `<returns>`). Suppressing `CS1591` via `#pragma` or `NoWarn` is strictly prohibited.
6. **Zero Obsolete APIs:** Zero `[Obsolete]` attributes or deprecated API calls are permitted in the codebase.
7. **English-First:** All code symbols, namespaces, comments, XML doc, commit messages, and documentation must use consistent technical English.
8. **Physical Idempotency:** The Golden Rule of Physical Idempotency dictates that retries must never re-send non-idempotent documents over broken sockets if partial byte transmission occurred (`RetryOnTransmissionError = false`).

For detailed architectural rationale, consult our [Architecture Decision Records (ADRs)](docs/adr/README.md).

---

## Development Workflow

### 1. Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (builds and multi-targets .NET 8.0, 9.0, and 10.0)
- Git
- Visual Studio 2026, JetBrains Rider, or Visual Studio Code with C# Dev Kit

### 2. Fork and Clone
```bash
git clone https://github.com/ericksonlopezf/dotnet-printing.git
cd dotnet-printing
```

### 3. Restore and Build
```bash
dotnet restore EricksonLopez.Printing.slnx
dotnet build EricksonLopez.Printing.slnx --configuration Release
```
Ensure build output reports **0 Warnings and 0 Errors**.

### 4. Execute Test Suites
```bash
dotnet test EricksonLopez.Printing.slnx --configuration Release
```

### 5. Run the Official Showcase
```bash
dotnet run --project samples/EricksonLopez.Printing.Showcase --framework net10.0 -- all
```

### 6. Verify Code Formatting & Compliance
```bash
dotnet format EricksonLopez.Printing.slnx --verify-no-changes
```
To run the automated repository compliance script (PowerShell):
```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/verify-compliance.ps1
```

---

## Submitting Pull Requests

1. **Create a Feature Branch:**
   ```bash
   git checkout -b feat/your-feature-name
   ```
2. **Commit Conventions:** Follow [Conventional Commits](https://www.conventionalcommits.org/):
   - `feat(escpos): add Code39 barcode support to EscPosBuilder`
   - `fix(transport): correct network stream timeout handling in TcpPrinterClient`
   - `docs(cookbook): add multi-item kitchen receipt recipe`
3. **Automated Testing:** Every bug fix or new feature must include comprehensive unit tests verifying both success paths and error states.
4. **Open a Pull Request:** Submit your PR against the `main` branch using the provided [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md).

---

## Support & Contact

- **Support Policy & Channels:** Consult [SUPPORT.md](SUPPORT.md)
- **Security Inquiries:** Consult [SECURITY.md](SECURITY.md)
- **Maintainer:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))
