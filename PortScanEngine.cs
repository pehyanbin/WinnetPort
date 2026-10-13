using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PortScanner.WinForms;

internal sealed record PortScanResult(int Port, string Status, long ElapsedMs, string Details);

internal static class PortScanEngine
{
    // A modest concurrency cap avoids flooding the destination with connections.
    private const int MaxParallelConnections = 24;

    public static async Task<IPAddress> ResolveHostAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var address))
            return address;

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        return addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("The host did not resolve to an IP address.");
    }

    public static Task ScanAsync(
        IPAddress target,
        IReadOnlyList<int> ports,
        int timeoutMs,
        IProgress<PortScanResult> progress,
        CancellationToken cancellationToken)
    {
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = MaxParallelConnections
        };

        return Parallel.ForEachAsync(
            ports,
            options,
            async (port, token) =>
            {
                var result = await ProbeAsync(target, port, timeoutMs, token);
                progress.Report(result);
            });
    }

    private static async Task<PortScanResult> ProbeAsync(
        IPAddress target, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        using var client = new TcpClient(target.AddressFamily);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        var stopwatch = Stopwatch.StartNew();
        string status;
        string details;

        try
        {
            await client.ConnectAsync(target, port, timeoutCts.Token);
            status = "Open";
            details = "TCP connection accepted";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            status = "Timed out";
            details = "No connection before timeout";
        }
        catch (SocketException ex)
        {
            (status, details) = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => ("Closed", "Connection refused"),
                SocketError.TimedOut => ("Timed out", "Socket connection timeout"),
                SocketError.HostUnreachable or SocketError.NetworkUnreachable
                    or SocketError.HostDown or SocketError.NetworkDown
                    => ("Unreachable", ex.SocketErrorCode.ToString()),
                _ => ("Error", ex.SocketErrorCode.ToString())
            };
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            status = "Error";
            details = ex.GetType().Name;
        }

        stopwatch.Stop();
        return new PortScanResult(port, status, stopwatch.ElapsedMilliseconds, details);
    }
}
