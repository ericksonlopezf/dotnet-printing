// Copyright © Erickson Lopez. MIT License.
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Printing.Tests;

public sealed class PooledTcpPrinterClientTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var act = () => new PooledTcpPrinterClient(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public async Task PrintAsync_NullDocument_ThrowsArgumentNullException()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions());
        var act = () => client.PrintAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PrintAsync_EmptyDocument_ReturnsSuccessImmediately()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions());
        var doc = new RawPrintDocument(Array.Empty<byte>());

        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task PrintAsync_ValidTcpServer_ReusesPersistentConnectionAcrossJobs()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var acceptedConnectionCount = 0;
            var serverTask = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                Interlocked.Increment(ref acceptedConnectionCount);
                using var stream = serverClient.GetStream();

                var receivedData = new byte[20];
                var totalBytesRead = 0;

                while (totalBytesRead < 10)
                {
                    var read = await stream.ReadAsync(receivedData.AsMemory(totalBytesRead, 10 - totalBytesRead));
                    if (read == 0)
                    {
                        break;
                    }
                    totalBytesRead += read;
                }

                return receivedData[..totalBytesRead];
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            byte[] job1 = [0x01, 0x02, 0x03, 0x04, 0x05];
            byte[] job2 = [0x06, 0x07, 0x08, 0x09, 0x0A];

            var result1 = await client.PrintAsync(new RawPrintDocument(job1, "Job1"));
            var result2 = await client.PrintAsync(new RawPrintDocument(job2, "Job2"));

            result1.IsSuccess.Should().BeTrue();
            result2.IsSuccess.Should().BeTrue();

            var serverReceived = await serverTask;
            acceptedConnectionCount.Should().Be(1);
            serverReceived.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A });
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_DisposedClient_ThrowsObjectDisposedException()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions());
        client.Dispose();

        var act = () => client.PrintAsync(new RawPrintDocument([0x01]));
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_DisposesCleanlyAndThrowsObjectDisposedExceptionOnPrint()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions());
        await client.DisposeAsync();

        var act = () => client.PrintAsync(new RawPrintDocument([0x01]));
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PrintAsync_UnreachableHost_ReturnsSocketError()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "invalid_host_that_does_not_exist",
            Port = 9100,
            TimeoutMs = 3000
        });

        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Data"));
        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SocketError");
    }

    [Fact]
    public async Task PrintAsync_ConcurrentCalls_AreSerializedSafely()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var serverTask = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                using var stream = serverClient.GetStream();
                var buffer = new byte[1024];
                var total = 0;
                while (total < 100)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(total, 100 - total));
                    if (read == 0)
                    {
                        break;
                    }
                    total += read;
                }
                return total;
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 5000
            });

            var tasks = new Task[10];
            for (var i = 0; i < 10; i++)
            {
                var payload = new byte[10];
                Array.Fill(payload, (byte)i);
                tasks[i] = Task.Run(() => client.PrintAsync(new RawPrintDocument(payload)));
            }

            await Task.WhenAll(tasks);
            var bytesReceived = await serverTask;
            bytesReceived.Should().Be(100);
        }
        finally
        {
            listener.Stop();
        }
    }
}
