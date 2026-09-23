// Copyright © Erickson Lopez. MIT License.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Printing.Serial.Tests;

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

internal sealed class FlushTrackingStream : Stream
{
    private readonly MemoryStream _inner = new();
    private readonly Action? _onFlush;
    private readonly SynchronizationContext? _syncContextToSet;

    public FlushTrackingStream(Action? onFlush = null, SynchronizationContext? syncContextToSet = null)
    {
        _onFlush = onFlush;
        _syncContextToSet = syncContextToSet;
    }

    public int FlushCount { get; private set; }
    public byte[] ToArray() => _inner.ToArray();

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public bool YieldOnFlush { get; set; }

    public override void Flush()
    {
        FlushCount++;
        _onFlush?.Invoke();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        _onFlush?.Invoke();
        if (_syncContextToSet is not null)
        {
            SynchronizationContext.SetSynchronizationContext(_syncContextToSet);
        }

        if (YieldOnFlush)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Task.Run(async () =>
            {
                await Task.Delay(5, cancellationToken).ConfigureAwait(false);
                await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
                tcs.SetResult(true);
            }, cancellationToken);
            return tcs.Task;
        }

        return Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(buffer, cancellationToken);
}

internal sealed class YieldingPrintDocument : IPrintDocument
{
    private readonly byte[] _bytes;
    private readonly int _delayMs;
    private readonly SynchronizationContext? _syncContextToSet;

    public YieldingPrintDocument(byte[] bytes, string documentName = "YieldingJob", int delayMs = 10, SynchronizationContext? syncContextToSet = null)
    {
        _bytes = bytes;
        DocumentName = documentName;
        _delayMs = delayMs;
        _syncContextToSet = syncContextToSet;
    }

    public string DocumentName { get; }
    public string? IdempotencyKey => null;
    public bool IsEmpty => _bytes.Length == 0;
    public bool IsIdempotent => false;
    public byte[] GetBytes() => _bytes;
    public ReadOnlyMemory<byte> GetMemory() => _bytes.AsMemory();

    public ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (_syncContextToSet is not null)
        {
            SynchronizationContext.SetSynchronizationContext(_syncContextToSet);
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(_bytes, cancellationToken).ConfigureAwait(false);
            tcs.SetResult(true);
        }, cancellationToken);

        return new ValueTask(tcs.Task);
    }
}

internal sealed class DelegatingPrintDocument : IPrintDocument
{
    private readonly Func<Stream, CancellationToken, Task> _writeAction;

    public DelegatingPrintDocument(Func<Stream, CancellationToken, Task> writeAction, string documentName = "DelegatingJob")
    {
        _writeAction = writeAction;
        DocumentName = documentName;
    }

    public string DocumentName { get; }
    public string? IdempotencyKey => null;
    public bool IsEmpty => false;
    public bool IsIdempotent => false;
    public byte[] GetBytes() => [0x1B, 0x40];
    public ReadOnlyMemory<byte> GetMemory() => new byte[] { 0x1B, 0x40 };

    public async ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await _writeAction(stream, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class FailingStream : Stream
{
    private readonly Exception _exceptionToThrow;

    public FailingStream(Exception exceptionToThrow)
    {
        _exceptionToThrow = exceptionToThrow;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => 0;
    public override long Position { get; set; }

    public override void Flush() => throw _exceptionToThrow;
    public override Task FlushAsync(CancellationToken cancellationToken) => throw _exceptionToThrow;
    public override int Read(byte[] buffer, int offset, int count) => 0;
    public override long Seek(long offset, SeekOrigin origin) => 0;
    public override void SetLength(long value) { }
    public override void Write(byte[] buffer, int offset, int count) => throw _exceptionToThrow;
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw _exceptionToThrow;
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw _exceptionToThrow;
}
