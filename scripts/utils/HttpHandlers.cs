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

        IPAddress[] addresses = IPAddress.TryParse(host, out IPAddress literalAddress)
            ? new[] { literalAddress }
            : await ResolveFromFirstAnsweringFamilyAsync(host, cancellationToken);

        Exception lastFailure = null;

        foreach (IPAddress address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }

            catch (Exception failure)
            {
                socket.Dispose();
                lastFailure = failure;
            }
        }

        throw lastFailure ?? new SocketException((int)SocketError.HostNotFound);
    }

    private static async Task<IPAddress[]> ResolveFromFirstAnsweringFamilyAsync(string host, CancellationToken cancellationToken)
    {
        var pendingLookups = new List<Task<IPAddress[]>>
        {
            Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cancellationToken),
            Dns.GetHostAddressesAsync(host, AddressFamily.InterNetworkV6, cancellationToken)
        };

        foreach (Task<IPAddress[]> lookup in pendingLookups)
        {
            _ = lookup.ContinueWith(faulted => _ = faulted.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        while (pendingLookups.Count > 0)
        {
            Task<IPAddress[]> finishedLookup = await Task.WhenAny(pendingLookups);
            pendingLookups.Remove(finishedLookup);

            if (finishedLookup.IsCompletedSuccessfully && finishedLookup.Result.Length > 0)
            {
                return finishedLookup.Result;
            }
        }

        return await Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}
