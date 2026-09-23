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
            result1.Value.Should().BeTrue();
            result2.IsSuccess.Should().BeTrue();
            result2.Value.Should().BeTrue();

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
        var logger = Substitute.For<ILogger<PooledTcpPrinterClient>>();
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "invalid_host_that_does_not_exist",
            Port = 9100,
            TimeoutMs = 3000
        }, logger);

        var doc = new RawPrintDocument(Encoding.UTF8.GetBytes("Data"));
        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Printer.SocketError");
        client.Client.Should().BeNull();
        client.Stream.Should().BeNull();

        logger.Received(1).Log(
            LogLevel.Error,
            PrintingEventIds.TcpPrintFailed,
            Arg.Is<object>(o => o.ToString()!.Contains("Socket error")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
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

    [Fact]
    public void CreateTcpClient_SocketOptions_ConfiguredCorrectly()
    {
        var clientZero = new PooledTcpPrinterClient(new TcpPrinterClientOptions { LingerSeconds = 0, NoDelay = true });
        using var tcpClientZero = clientZero.CreateTcpClient();
        tcpClientZero.NoDelay.Should().BeTrue();
        tcpClientZero.LingerState.Should().NotBeNull();
        tcpClientZero.LingerState!.Enabled.Should().BeTrue();
        tcpClientZero.LingerState.LingerTime.Should().Be(0);

        var clientPos = new PooledTcpPrinterClient(new TcpPrinterClientOptions { LingerSeconds = 5, NoDelay = false });
        using var tcpClientPos = clientPos.CreateTcpClient();
        tcpClientPos.NoDelay.Should().BeFalse();
        tcpClientPos.LingerState.Should().NotBeNull();
        tcpClientPos.LingerState!.Enabled.Should().BeTrue();
        tcpClientPos.LingerState.LingerTime.Should().Be(5);

        var clientNeg = new PooledTcpPrinterClient(new TcpPrinterClientOptions { LingerSeconds = -1 });
        using var tcpClientNeg = clientNeg.CreateTcpClient();
        (tcpClientNeg.LingerState == null || !tcpClientNeg.LingerState.Enabled).Should().BeTrue();
    }

    [Fact]
    public async Task WarmupAsync_ValidServer_PreEstablishesConnection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                await Task.Delay(500);
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            client.Client.Should().BeNull();
            client.Stream.Should().BeNull();

            await client.WarmupAsync();

            client.Client.Should().NotBeNull();
            client.Stream.Should().NotBeNull();
            client.Client!.Connected.Should().BeTrue();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task WarmupAsync_DisposedClient_ThrowsObjectDisposedException()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions());
        client.Dispose();

        var act = () => client.WarmupAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task WarmupAsync_UnreachableHost_ThrowsSocketException()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "invalid_host_that_does_not_exist",
            Port = 9100,
            TimeoutMs = 3000
        });

        var act = () => client.WarmupAsync();
        await act.Should().ThrowAsync<SocketException>();
    }

    [Fact]
    public async Task PrintAsync_TransmissionError_ReturnsTransmissionErrorAndResetsConnection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var buffer = new byte[10];
                _ = await serverClient.GetStream().ReadAsync(buffer);
            });

            var logger = Substitute.For<ILogger<PooledTcpPrinterClient>>();
            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            }, logger);

            var throwingDoc = new ThrowingPrintDocument();

            var result = await client.PrintAsync(throwingDoc);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(PrintingErrorCodes.TransmissionError);
            result.Error.Description.Should().Contain("Simulated network stream broken");

            client.Client.Should().BeNull();
            client.Stream.Should().BeNull();

            logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Network I/O transmission error")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_WhenCallerCancellationTokenIsCanceled_ReturnsCanceledAndResetsConnection()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "127.0.0.1",
            Port = 9100,
            TimeoutMs = 5000
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var doc = new RawPrintDocument([0x1B, 0x40], "CanceledDoc");
        var result = await client.PrintAsync(doc, cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PrintingErrorCodes.Canceled);
        result.Error.Description.Should().Be("Print operation was canceled by the caller.");

        client.Client.Should().BeNull();
        client.Stream.Should().BeNull();
    }

    [Fact]
    public async Task PrintAsync_WhenConnectingTimesOut_ReturnsTimeoutErrorAndResetsConnection()
    {
        var logger = Substitute.For<ILogger<PooledTcpPrinterClient>>();
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "10.255.255.1",
            Port = 9100,
            TimeoutMs = 15
        }, logger);

        var doc = new RawPrintDocument([0x1B, 0x40], "TimeoutDoc");
        var result = await client.PrintAsync(doc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().BeOneOf(PrintingErrorCodes.Timeout, PrintingErrorCodes.SocketError);

        client.Client.Should().BeNull();
        client.Stream.Should().BeNull();

        if (result.Error.Code == PrintingErrorCodes.Timeout)
        {
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Timed out after")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }

    [Fact]
    public void IsSocketDisconnected_Behaviors_CorrectlyDetected()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect("127.0.0.1", port);
            using var serverSocket = listener.AcceptSocket();

            // Connected socket without pending reads and not disconnected
            PooledTcpPrinterClient.IsSocketDisconnected(clientSocket).Should().BeFalse();

            // When server sends data, Available is > 0, so Poll with Available == 0 is false
            serverSocket.Send([0x01, 0x02]);
            Thread.Sleep(50);
            clientSocket.Available.Should().BeGreaterThan(0);
            PooledTcpPrinterClient.IsSocketDisconnected(clientSocket).Should().BeFalse();

            // Remote shutdown / disconnect causes Poll to be true and Available == 0
            var buf = new byte[10];
            clientSocket.Receive(buf);
            serverSocket.Shutdown(SocketShutdown.Both);
            serverSocket.Close();
            Thread.Sleep(50);
            PooledTcpPrinterClient.IsSocketDisconnected(clientSocket).Should().BeTrue();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_WhenServerClosesConnection_DetectsDisconnectAndReconnectsCleanly()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var acceptedCount = 0;
            var serverTask = Task.Run(async () =>
            {
                // First client connection: read and close immediately
                var s1 = await listener.AcceptTcpClientAsync();
                Interlocked.Increment(ref acceptedCount);
                var b1 = new byte[10];
                _ = await s1.GetStream().ReadAsync(b1);
                s1.Client.Shutdown(SocketShutdown.Both);
                s1.Close();

                // Second client connection: accept and read
                var s2 = await listener.AcceptTcpClientAsync();
                Interlocked.Increment(ref acceptedCount);
                var b2 = new byte[10];
                var read2 = await s2.GetStream().ReadAsync(b2);
                s2.Close();
                return b2[..read2];
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            var r1 = await client.PrintAsync(new RawPrintDocument([0x01, 0x02]));
            r1.IsSuccess.Should().BeTrue();

            // Give OS time to propagate FIN
            await Task.Delay(100);

            var r2 = await client.PrintAsync(new RawPrintDocument([0x03, 0x04]));
            r2.IsSuccess.Should().BeTrue();

            var secondData = await serverTask;
            acceptedCount.Should().Be(2);
            secondData.Should().BeEquivalentTo(new byte[] { 0x03, 0x04 });
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_IsIdempotent()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions());
        client.Dispose();
        client.Dispose();

        client.Client.Should().BeNull();
        client.Stream.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_WhenConnected_DisposesStreamAndIsIdempotent()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                await Task.Delay(1000);
            });

            var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            await client.WarmupAsync();
            client.Stream.Should().NotBeNull();
            client.Client.Should().NotBeNull();

            await client.DisposeAsync();
            client.Stream.Should().BeNull();
            client.Client.Should().BeNull();

            await client.DisposeAsync();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_EmptyDocument_LogsDebugAndReturnsSuccessTrue()
    {
        var logger = Substitute.For<ILogger<PooledTcpPrinterClient>>();
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "127.0.0.1",
            Port = 9100,
            TimeoutMs = 3000
        }, logger);

        var doc = new RawPrintDocument([], "EmptyDoc");
        var result = await client.PrintAsync(doc);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        logger.Received(1).Log(
            LogLevel.Debug,
            PrintingEventIds.TcpPrintSuccess,
            Arg.Is<object>(o => o.ToString()!.Contains("Empty print document")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PrintAsync_Success_LogsStartedAndSuccessAndReturnsTrue()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var buffer = new byte[100];
                _ = await serverClient.GetStream().ReadAsync(buffer);
            });

            var logger = Substitute.For<ILogger<PooledTcpPrinterClient>>();
            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            }, logger);

            var result = await client.PrintAsync(new RawPrintDocument([0x1B, 0x40], "ValidDoc"));
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();

            logger.Received(1).Log(
                LogLevel.Information,
                PrintingEventIds.TcpPrintStarted,
                Arg.Is<object>(o => o.ToString()!.Contains("Initiating print job")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());

            logger.Received(1).Log(
                LogLevel.Information,
                PrintingEventIds.TcpPrintSuccess,
                Arg.Is<object>(o => o.ToString()!.Contains("Successfully completed pooled print job")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task WarmupAsync_WhenConnected_LogsInformationAndReleasesGate()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var buffer = new byte[100];
                while (await serverClient.GetStream().ReadAsync(buffer) > 0) { }
            });

            var logger = Substitute.For<ILogger<PooledTcpPrinterClient>>();
            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            }, logger);

            await client.WarmupAsync();

            logger.Received(1).Log(
                LogLevel.Information,
                PrintingEventIds.TcpPrintStarted,
                Arg.Is<object>(o => o.ToString()!.Contains("Warmup: pre-established")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());

            // If _gate.Release() was mutated away in WarmupAsync, PrintAsync would deadlock/timeout!
            var printResult = await client.PrintAsync(new RawPrintDocument([0x1B, 0x40], "Doc"));
            printResult.IsSuccess.Should().BeTrue();
            printResult.Value.Should().BeTrue();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task WarmupAsync_WhenUnreachableWithShortTimeout_TimesOutAndCancels()
    {
        var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
        {
            Host = "10.255.255.1",
            Port = 9100,
            TimeoutMs = 15
        });

        var act = async () => await client.WarmupAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResetConnection_DisposesStreamAndClient()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var buffer = new byte[100];
                _ = await serverClient.GetStream().ReadAsync(buffer);
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            await client.WarmupAsync();
            var oldStream = client.Stream;
            var oldClient = client.Client;

            oldStream.Should().NotBeNull();
            oldClient.Should().NotBeNull();

            // Calling PrintAsync with ThrowingPrintDocument triggers ResetConnection() in catch
            var result = await client.PrintAsync(new ThrowingPrintDocument());
            result.IsFailure.Should().BeTrue();

            client.Stream.Should().BeNull();
            client.Client.Should().BeNull();

            var actStream = () => oldStream!.Read([1]);
            actStream.Should().Throw<ObjectDisposedException>();

            var actClient = () => oldClient!.GetStream();
            actClient.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task DisposeAsync_DisposesStreamAndClientAsynchronously()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                await Task.Delay(500);
            });

            var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            await client.WarmupAsync();
            var oldStream = client.Stream;
            var oldClient = client.Client;

            await client.DisposeAsync();

            client.Stream.Should().BeNull();
            client.Client.Should().BeNull();

            var actStream = () => oldStream!.Read([1]);
            actStream.Should().Throw<ObjectDisposedException>();

            var actClient = () => oldClient!.GetStream();
            actClient.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_FlushesStreamAndUsesConfigureAwaitFalse()
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
                var buffer = new byte[100];
                _ = await stream.ReadAsync(buffer);
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            var flushCount = 0;
            client.StreamFactory = c => new FlushTrackingStream(c.GetStream(), () => flushCount++);

            var result = await Task.Run(async () =>
            {
                var syncContext = new DisallowingSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);
                try
                {
                    var doc = new YieldingPrintDocument([0x1B, 0x40], "YieldingDoc");
                    return await client.PrintAsync(doc).ConfigureAwait(false);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
            flushCount.Should().Be(1);

            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task EnsureConnectedAsync_WhenStreamIsNullWhileClientConnected_Reconnects()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var secondAccepted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var acceptedCount = 0;
            var serverTask = Task.Run(async () =>
            {
                try
                {
                    while (acceptedCount < 2)
                    {
                        var s = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        if (Interlocked.Increment(ref acceptedCount) >= 2)
                        {
                            secondAccepted.TrySetResult(true);
                        }

                        _ = Task.Run(async () =>
                        {
                            using (s)
                            {
                                var b = new byte[100];
                                while (await s.GetStream().ReadAsync(b).ConfigureAwait(false) > 0)
                                {
                                }
                            }
                        });
                    }
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            await client.WarmupAsync();
            var firstClient = client.Client;
            firstClient.Should().NotBeNull();
            client.Stream.Should().NotBeNull();

            // Simulate stream being null while client is connected
            client.InvalidateStreamForTesting();

            var r = await client.PrintAsync(new RawPrintDocument([0x1B, 0x40]));
            r.IsSuccess.Should().BeTrue();
            r.Value.Should().BeTrue();

            await secondAccepted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            acceptedCount.Should().Be(2);
            client.Client.Should().NotBeSameAs(firstClient);

            var act = () => firstClient!.GetStream();
            act.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_WhenSocketExceptionDuringWrite_CallsResetConnection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var buffer = new byte[100];
                _ = await serverClient.GetStream().ReadAsync(buffer);
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            await client.WarmupAsync();
            client.Client.Should().NotBeNull();

            var result = await client.PrintAsync(new SocketThrowingPrintDocument());
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(PrintingErrorCodes.SocketError);

            client.Client.Should().BeNull();
            client.Stream.Should().BeNull();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_WhenCanceledDuringWrite_CallsResetConnection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var buffer = new byte[100];
                _ = await serverClient.GetStream().ReadAsync(buffer);
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            await client.WarmupAsync();
            client.Client.Should().NotBeNull();

            var result = await client.PrintAsync(new CanceledThrowingPrintDocument());
            result.IsFailure.Should().BeTrue();

            client.Client.Should().BeNull();
            client.Stream.Should().BeNull();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Dispose_WhenConnected_CallsResetConnectionAndDisposesClient()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                await Task.Delay(500);
            });

            var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            FlushTrackingStream? trackingStream = null;
            client.StreamFactory = c =>
            {
                trackingStream = new FlushTrackingStream(c.GetStream());
                return trackingStream;
            };

            await client.WarmupAsync();
            var oldClient = client.Client;
            oldClient.Should().NotBeNull();
            trackingStream.Should().NotBeNull();
            trackingStream!.IsDisposed.Should().BeFalse();

            client.Dispose();

            client.Client.Should().BeNull();
            client.Stream.Should().BeNull();
            trackingStream.IsDisposed.Should().BeTrue();

            var act = () => oldClient!.GetStream();
            act.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task DisposeAsync_UsesAsyncDisposingStreamAndConfigureAwaitFalse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                await Task.Delay(500);
            });

            var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            var asyncDisposed = false;
            var syncContext = new DisallowingSynchronizationContext();
            client.StreamFactory = c => new AsyncDisposingStream(c.GetStream(), () => asyncDisposed = true, syncContextToSet: syncContext);

            await client.WarmupAsync();

            await Task.Run(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(syncContext);
                try
                {
                    await client.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            asyncDisposed.Should().BeTrue();
            client.Stream.Should().BeNull();
            client.Client.Should().BeNull();
            syncContext.WasViolated.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task WarmupAsync_WhenGateContended_UsesConfigureAwaitFalse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                await Task.Delay(500);
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            await client.Gate.WaitAsync();

            var syncContext = new DisallowingSynchronizationContext();
            var warmupTask = Task.Run(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(syncContext);
                try
                {
                    await client.WarmupAsync().ConfigureAwait(false);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            await Task.Delay(50);
            client.Gate.Release();

            await warmupTask;
            client.Client.Should().NotBeNull();
            syncContext.WasViolated.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task WarmupAsync_WhenCalled_UsesConfigureAwaitFalse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                await Task.Delay(500);
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            var syncContext = new DisallowingSynchronizationContext();
            await Task.Run(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(syncContext);
                try
                {
                    await client.WarmupAsync().ConfigureAwait(false);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            client.Client.Should().NotBeNull();
            syncContext.WasViolated.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_WhenGateContended_UsesConfigureAwaitFalse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var b = new byte[100];
                while (await serverClient.GetStream().ReadAsync(b) > 0) { }
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            await client.Gate.WaitAsync();

            var syncContext = new DisallowingSynchronizationContext();
            var printTask = Task.Run(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(syncContext);
                try
                {
                    return await client.PrintAsync(new RawPrintDocument([0x1B, 0x40])).ConfigureAwait(false);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            await Task.Delay(50);
            client.Gate.Release();

            var result = await printTask;
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
            syncContext.WasViolated.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_WhenCalledWithoutWarmup_UsesConfigureAwaitFalse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var b = new byte[100];
                while (await serverClient.GetStream().ReadAsync(b) > 0) { }
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            var syncContext = new DisallowingSynchronizationContext();
            var result = await Task.Run(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(syncContext);
                try
                {
                    return await client.PrintAsync(new RawPrintDocument([0x1B, 0x40])).ConfigureAwait(false);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
            syncContext.WasViolated.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_WhenWriteToAsyncAndFlushAsync_EnforcesConfigureAwaitFalse()
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
                var buffer = new byte[100];
                _ = await stream.ReadAsync(buffer);
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 3000
            });

            var flushCount = 0;
            var syncContext = new DisallowingSynchronizationContext();
            client.StreamFactory = c => new FlushTrackingStream(c.GetStream(), () => flushCount++, syncContextToSet: syncContext);

            await client.WarmupAsync();

            var result = await Task.Run(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(syncContext);
                try
                {
                    var doc = new YieldingPrintDocument([0x1B, 0x40], "YieldingDoc", syncContextToSet: syncContext);
                    return await client.PrintAsync(doc).ConfigureAwait(false);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
            flushCount.Should().Be(1);
            syncContext.WasViolated.Should().BeFalse();

            await serverTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task PrintAsync_WhenOperationExceedsTimeoutMs_ReturnsTimeoutError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            _ = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                var b = new byte[100];
                while (await serverClient.GetStream().ReadAsync(b) > 0) { }
            });

            await using var client = new PooledTcpPrinterClient(new TcpPrinterClientOptions
            {
                Host = "127.0.0.1",
                Port = port,
                TimeoutMs = 50
            });

            var result = await client.PrintAsync(new HangingPrintDocument());
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(PrintingErrorCodes.Timeout);
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class SocketThrowingPrintDocument : IPrintDocument
    {
        public string DocumentName => "SocketThrowingDoc";
        public bool IsEmpty => false;
        public byte[] GetBytes() => [0x1B, 0x40];
        public ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new SocketException((int)SocketError.ConnectionReset));
    }

    private sealed class CanceledThrowingPrintDocument : IPrintDocument
    {
        public string DocumentName => "CanceledThrowingDoc";
        public bool IsEmpty => false;
        public byte[] GetBytes() => [0x1B, 0x40];
        public ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
            => ValueTask.FromCanceled(new CancellationToken(true));
    }

    private sealed class ThrowingPrintDocument : IPrintDocument
    {
        public string DocumentName => "ThrowingDoc";
        public bool IsEmpty => false;
        public byte[] GetBytes() => [0x1B, 0x40];
        public ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new IOException("Simulated network stream broken"));
    }
}
