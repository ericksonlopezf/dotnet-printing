// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Printing.Zpl.Tests;

internal sealed class DisallowingSynchronizationContext : SynchronizationContext
{
    public bool WasViolated { get; private set; }

    public override void Post(SendOrPostCallback d, object? state)
    {
        WasViolated = true;
        d(state);
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        WasViolated = true;
        d(state);
    }
}

internal sealed class TrackingStream : MemoryStream
{
    public bool WasFlushAsyncCalled { get; private set; }
    public bool IsDisposed { get; private set; }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        WasFlushAsyncCalled = true;
        await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        await base.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        await base.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
        }
        base.Dispose(disposing);
    }
}
