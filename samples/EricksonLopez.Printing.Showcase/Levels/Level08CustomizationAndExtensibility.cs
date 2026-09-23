// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Printing;
using EricksonLopez.Result;

namespace EricksonLopez.Printing.Showcase.Levels;

/// <summary>
/// Demonstrates extensibility through custom IPrinterClient decorators (auditing/archiving) and custom IPrintDocument implementations.
/// </summary>
public static class Level08CustomizationAndExtensibility
{
    /// <summary>
    /// Executes the customization and extensibility showcase demonstration asynchronously.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  LEVEL 08: CUSTOMIZATION & EXTENSIBILITY — DECORATORS & CUSTOM CONTRACTS");
        Console.WriteLine("================================================================================\n");

        // 1. Implementing an Auditing / Electronic Journal Decorator on IPrinterClient
        Console.WriteLine("[1] Creating and registering an AuditingJournalPrinterClient decorator:");

        var baseClient = new TestRecordingPrinterClient();
        var auditDecorator = new AuditingJournalPrinterClient(baseClient);

        // Decorator pipeline: AuditingJournal -> WithRetry -> BaseClient
        var pipelineClient = auditDecorator.WithRetry(opt =>
        {
            opt.MaxRetries = 2;
            opt.InitialDelay = TimeSpan.FromMilliseconds(20);
        });

        var sampleDoc = new RawPrintDocument(
            bytes: Encoding.UTF8.GetBytes("PRINT TRANSACTION SAMPLE 123"),
            documentName: "FiscalJournalEntry",
            idempotencyKey: "TX-9981",
            isIdempotent: true);

        var result = await pipelineClient.PrintAsync(sampleDoc);

        Console.WriteLine($"    Print Result: IsSuccess={result.IsSuccess}, Value={result.Value}");
        Console.WriteLine($"    Total documents audited in electronic journal: {auditDecorator.AuditedJobsCount}");
        Console.WriteLine($"    Audit Journal summary: '{auditDecorator.LastAuditRecord}'\n");

        // 2. Creating a Custom IPrintDocument Implementation
        Console.WriteLine("[2] Implementing a custom DynamicTemplatePrintDocument:");
        var templateDoc = new DynamicTemplatePrintDocument(
            template: "^XA^FO50,50^A0N,30,30^FDUser: {USER}^FS^FO50,100^A0N,25,25^FDRole: {ROLE}^FS^XZ",
            userName: "Alice Admin",
            roleName: "Store Manager",
            documentName: "DynamicBadge");

        using var memorySink = new MemoryStream();
        await templateDoc.WriteToAsync(memorySink);
        var renderedOutput = Encoding.UTF8.GetString(memorySink.ToArray());

        Console.WriteLine($"    Document Name: {templateDoc.DocumentName}");
        Console.WriteLine($"    Idempotent: {templateDoc.IsIdempotent}, IsEmpty: {templateDoc.IsEmpty}");
        Console.WriteLine($"    Rendered dynamic content:");
        Console.WriteLine($"    {renderedOutput.Trim()}");

        Console.WriteLine("\n✔ Level 08 completed successfully.\n");
    }

    /// <summary>
    /// Custom production decorator that archives every print job to an electronic journal for tax audit compliance.
    /// </summary>
    public sealed class AuditingJournalPrinterClient : IPrinterClient
    {
        private readonly IPrinterClient _inner;
        public int AuditedJobsCount { get; private set; }
        public string? LastAuditRecord { get; private set; }

        public AuditingJournalPrinterClient(IPrinterClient inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            var timestamp = DateTimeOffset.UtcNow;
            var result = await _inner.PrintAsync(document, cancellationToken);

            AuditedJobsCount++;
            LastAuditRecord = $"[{timestamp:O}] Doc='{document.DocumentName}' (IdemKey='{document.IdempotencyKey}') Bytes={document.GetBytes().Length} Success={result.IsSuccess}";
            Console.WriteLine($"    [AuditJournal] Recorded: {LastAuditRecord}");

            return result;
        }
    }

    /// <summary>
    /// Custom IPrintDocument dynamically replacing parameters directly on stream without storing large rendered copies.
    /// </summary>
    public sealed class DynamicTemplatePrintDocument : IPrintDocument
    {
        private readonly string _template;
        private readonly string _userName;
        private readonly string _roleName;

        public DynamicTemplatePrintDocument(string template, string userName, string roleName, string documentName)
        {
            _template = template ?? throw new ArgumentNullException(nameof(template));
            _userName = userName;
            _roleName = roleName;
            DocumentName = documentName;
        }

        public string DocumentName { get; }
        public bool IsEmpty => string.IsNullOrEmpty(_template);
        public bool IsIdempotent => true;
        public string? IdempotencyKey => $"DYN-{_userName.GetHashCode()}-{DocumentName}";

        public byte[] GetBytes()
        {
            var rendered = _template.Replace("{USER}", _userName).Replace("{ROLE}", _roleName);
            return Encoding.UTF8.GetBytes(rendered);
        }

        public async ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var bytes = GetBytes();
            await stream.WriteAsync(bytes, cancellationToken);
        }
    }

    private sealed class TestRecordingPrinterClient : IPrinterClient
    {
        public Task<Result<bool>> PrintAsync(IPrintDocument document, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<bool>.Success(true));
        }
    }
}
