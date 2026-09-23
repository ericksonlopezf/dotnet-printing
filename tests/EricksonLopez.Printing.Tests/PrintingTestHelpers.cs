// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Printing.Tests;

internal sealed class FlushTrackingStream : Stream
{
    private readonly Stream _inner;
    private readonly Action? _onFlush;
    private readonly SynchronizationContext? _syncContextToSet;
    private readonly Action? _onDispose;

    public bool IsDisposed { get; private set; }

    public FlushTrackingStream(
        Stream inner,
        Action? onFlush = null,
        SynchronizationContext? syncContextToSet = null,
        Action? onDispose = null)
    {
        _inner = inner;
        _onFlush = onFlush;
        _syncContextToSet = syncContextToSet;
        _onDispose = onDispose;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        _onFlush?.Invoke();
        if (_syncContextToSet is not null)
        {
            SynchronizationContext.SetSynchronizationContext(_syncContextToSet);
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
            tcs.SetResult(true);
        }, cancellationToken);

        return tcs.Task;
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
            _onDispose?.Invoke();
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
}

internal sealed class AsyncDisposingStream : Stream
{
    private readonly Stream _inner;
    private readonly Action _onAsyncDispose;
    private readonly SynchronizationContext? _syncContextToSet;

    public AsyncDisposingStream(Stream inner, Action onAsyncDispose, SynchronizationContext? syncContextToSet = null)
    {
        _inner = inner;
        _onAsyncDispose = onAsyncDispose;
        _syncContextToSet = syncContextToSet;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    public override async ValueTask DisposeAsync()
    {
        _onAsyncDispose();
        if (_syncContextToSet is not null)
        {
            SynchronizationContext.SetSynchronizationContext(_syncContextToSet);
        }

        await Task.Delay(5).ConfigureAwait(false);
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class YieldingPrintDocument : IPrintDocument
{
    private readonly byte[] _bytes;
    private readonly SynchronizationContext? _syncContextToSet;

    public YieldingPrintDocument(byte[] bytes, string name = "YieldingDoc", SynchronizationContext? syncContextToSet = null)
    {
        _bytes = bytes;
        DocumentName = name;
        _syncContextToSet = syncContextToSet;
    }

    public string DocumentName { get; }
    public bool IsEmpty => false;
    public byte[] GetBytes() => _bytes;

    public ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (_syncContextToSet is not null)
        {
            SynchronizationContext.SetSynchronizationContext(_syncContextToSet);
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(_bytes, cancellationToken).ConfigureAwait(false);
            tcs.SetResult(true);
        }, cancellationToken);

        return new ValueTask(tcs.Task);
    }
}

internal sealed class HangingPrintDocument : IPrintDocument
{
    public string DocumentName => "HangingDoc";
    public bool IsEmpty => false;
    public byte[] GetBytes() => [0x1B, 0x40];

    public async ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
    }
}

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
