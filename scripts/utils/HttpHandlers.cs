using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public static class HttpHandlers
{
    public static SocketsHttpHandler Create()
    {
        return new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        string host = context.DnsEndPoint.Host;
        int port = context.DnsEndPoint.Port;

        if (IPAddress.TryParse(host, out IPAddress literalAddress))
        {
            return await ConnectToFirstReachableAsync(new[] { literalAddress }, port, cancellationToken)
                ?? throw new SocketException((int)SocketError.HostUnreachable);
        }

        var pendingLookups = new List<Task<IPAddress[]>>
        {
            Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cancellationToken),
            Dns.GetHostAddressesAsync(host, AddressFamily.InterNetworkV6, cancellationToken)
        };

        foreach (Task<IPAddress[]> lookup in pendingLookups)
        {
            _ = lookup.ContinueWith(faulted => _ = faulted.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        Exception lastFailure = null;
        bool anyAddressFound = false;

        while (pendingLookups.Count > 0)
        {
            Task<IPAddress[]> finishedLookup = await Task.WhenAny(pendingLookups);
            pendingLookups.Remove(finishedLookup);

            if (!finishedLookup.IsCompletedSuccessfully || finishedLookup.Result.Length == 0)
            {
                continue;
            }

            anyAddressFound = true;

            try
            {
                Stream connection = await ConnectToFirstReachableAsync(finishedLookup.Result, port, cancellationToken);

                if (connection != null)
                {
                    return connection;
                }
            }

            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                lastFailure = failure;
            }
        }

        if (!anyAddressFound)
        {
            Stream fallbackConnection = await ConnectToFirstReachableAsync(await Dns.GetHostAddressesAsync(host, cancellationToken), port, cancellationToken);

            if (fallbackConnection != null)
            {
                return fallbackConnection;
            }
        }

        throw lastFailure ?? new SocketException((int)SocketError.HostUnreachable);
    }

    private static async Task<Stream> ConnectToFirstReachableAsync(IPAddress[] addresses, int port, CancellationToken cancellationToken)
    {
        Exception lastFailure = null;

        foreach (IPAddress address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }

            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                socket.Dispose();
                lastFailure = failure;
            }
        }

        if (lastFailure != null)
        {
            throw lastFailure;
        }

        return null;
    }
}
