<!-- Copyright © Erickson Lopez. MIT License. -->
# Support Policy — EricksonLopez.Printing

Thank you for building with **`EricksonLopez.Printing`**. This document outlines the official support channels, response timelines, and resources available for developers and organizations deploying printing solutions in production.

---

## Support Channels

| Channel | Purpose | Expected Response Time |
|---|---|---|
| **[GitHub Issues](https://github.com/ericksonlopezf/dotnet-printing/issues)** | Bug reports, reproducible anomalies, and actionable feature proposals. | 2–3 business days |
| **[GitHub Discussions](https://github.com/ericksonlopezf/dotnet-printing/discussions)** | Architecture advice, general usage questions, hardware compatibility discussions, and cookbook recipes. | Community & maintainer driven |
| **[Security Disclosures](SECURITY.md)** | Confidential vulnerability reports submitted via email to maintainer. | Initial acknowledgment within 48 hours |
| **Maintainer Direct** | Enterprise licensing inquiries, strategic sponsorship, and architectural consulting. | Within 5 business days |

---

## Documentation & Self-Service Resources

Before opening an issue, please consult the comprehensive documentation suite in [`docs/`](docs/):

1. **[Quick Start Guide](docs/quick-start.md)** — First receipt and label in under 5 minutes.
2. **[Getting Started & Setup](docs/getting-started.md)** — Dependency injection, connection pooling, and container deployment.
3. **[Official Showcase Guide](docs/showcase-guide.md)** — Executable reference implementation with 11 progressive levels (`samples/EricksonLopez.Printing.Showcase`).
4. **[Troubleshooting & Diagnostics](docs/troubleshooting.md)** — Solutions for socket timeouts, paper-out errors, and serial port locks.
5. **[Frequently Asked Questions (FAQ)](docs/faq.md)** — Common questions regarding drivers, Linux containers, and mock testing.
6. **[Production Cookbook](docs/cookbook.md)** — 10 ready-to-run recipes for real-world scenarios.
7. **[API Reference](docs/api-reference.md)** — Detailed specification of all public classes, methods, and error codes.

---

## Reporting Bugs Effectively

To expedite resolution of hardware and driver issues, include:
- **Package name and version:** (e.g., `EricksonLopez.Printing.EscPos` v1.0.0).
- **Target framework:** (.NET 8.0, .NET 9.0, or .NET 10.0).
- **Operating system and deployment model:** (Windows, Linux, Alpine container, Native AOT).
- **Hardware model & transport:** (e.g., Epson TM-T20III via TCP port 9100, Zebra ZD421 via USB COM3).
- **Minimal reproducible sample:** A concise C# code snippet reproducing the behavior.

---

## Enterprise & Commercial Inquiries

For enterprise consulting, dedicated support service level agreements (SLAs), or customized industrial hardware integrations:
- **Maintainer:** Erickson Lopez
- **Email:** [ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com)
- **Website:** [https://ericksonlopez.dev](https://ericksonlopez.dev)
