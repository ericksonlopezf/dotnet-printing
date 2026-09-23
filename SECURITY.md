<!-- Copyright © Erickson Lopez. MIT License. -->
# Security Policy — EricksonLopez.Printing

Security and deterministic reliability are fundamental priorities for industrial hardware, IoT edge gateways, and cloud-to-edge printing systems.

---

## Supported Versions

The `EricksonLopez.Printing` ecosystem follows the [.NET Support Lifecycle](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) across multi-targeted runtime frameworks.

| Version | Supported | Runtime Frameworks | Status |
|---|---|---|---|
| **1.0.x** | :white_check_mark: | .NET 10, .NET 9, .NET 8 | Active Support (Current Release) |
| **< 1.0** | :x: | N/A | Unsupported (Pre-release) |

---

## Reporting a Vulnerability

If you discover a security vulnerability or potential exploit in any `EricksonLopez.Printing` package:

1. **Do NOT open a public GitHub issue.**
2. Send a confidential report directly to the maintainer:
   - **Contact:** Erickson Lopez
   - **Email:** [ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com)
   - **Subject Line:** `[SECURITY DISCLOSURE] EricksonLopez.Printing - <Component / Description>`
3. Please include:
   - Affected package name(s) and version(s).
   - Target framework and operating system environment.
   - Proof of Concept (PoC) or step-by-step reproduction instructions.
   - Assessment of potential impact, exploitability, and attack vectors.

### Response Timelines
- **Initial Acknowledgment:** Within 48 hours of receipt.
- **Triage & Assessment:** Within 5 business days with patch timeline.
- **Coordinated Disclosure:** Security advisories and patched NuGet packages will be published simultaneously via GitHub Security Advisories and NuGet.org once resolved.

---

## Supply Chain Security

The build and packaging pipelines implement defense-in-depth supply chain integrity measures:

- **Strong Name Assembly Signing:** All released assemblies are cryptographically strong-named using `EricksonLopez.snk` to prevent binary tampering.
- **Central Package Management (CPM):** All NuGet dependency versions are centrally locked in `Directory.Packages.props`.
- **SourceLink Reproducibility:** Symbols (`.snupkg`) and SourceLink metadata are generated during packaging to allow verifiable source-code stepping.
- **Zero Vulnerability Suppression Policy:** NuGet vulnerability warnings (`NU1901`–`NU1904`) are actively monitored in CI pipelines.
- **Deterministic CI Packaging:** Releases are built in isolated GitHub Actions runners with strict quality gates prior to publishing.

---

## Known Security Boundaries

Understanding the operational boundaries of printer hardware protocols is critical for secure deployment:

| Layer | Protocol | Security Boundary & Recommendation |
|---|---|---|
| **Raw Socket Transport** | TCP Port 9100 | The raw socket protocol (JetDirect / AppSocket) transmits unencrypted, unauthenticated bytecode. **Never expose port 9100 to the public Internet.** Isolate physical printers within dedicated private VLANs with restrictive firewall policies. |
| **Serial COM Transport** | RS-232 / Virtual COM | Physical serial ports are subject to OS device file permissions. Ensure edge agent services run under service accounts with least-privilege device access. |
| **SignalR Cloud Bridge** | WebSockets / HTTPS | Cloud-to-edge dispatch via `PrinterHub` must require HTTPS/WSS and robust authentication (`[Authorize]`, JWT bearer tokens, or client certificates) to prevent unauthorized print job injection. |
| **Image Processing DoS** | Monochrome Imaging | All bitmap converters enforce `MaxDimension = 8192` and 64-bit checked buffer arithmetic to protect edge gateways from decompression bombs and memory exhaustion attacks. |
| **ZPL Command Injection** | ZPL II Generator | `ZplBuilder` validates control delimiters and applies automatic `^FH_` hexadecimal escaping (`^` as `_5E`, `~` as `_7E`) to prevent user input from breaking out of label fields. |
