using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 12: SignalR clients against the in-process test server,
/// over WebSockets, sending the JWT as <c>?access_token=</c> exactly as a
/// browser has to.</summary>
public static class LiveHubConnections
{
    public static async Task<HubConnection> ConnectAsync(WebApplicationFactory<Program> factory, string path, HttpClient authorizedClient)
    {
        var token = authorizedClient.DefaultRequestHeaders.Authorization!.Parameter!;
        var connection = Build(factory, path, token);
        await connection.StartAsync();
        return connection;
    }

    public static HubConnection Build(WebApplicationFactory<Program> factory, string path, string? token)
    {
        var server = factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, path.TrimStart('/')), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var uri = context.Uri;
                    if (token is not null)
                    {
                        var query = uri.Query.TrimStart('?');
                        uri = new UriBuilder(uri) { Query = (query.Length > 0 ? query + "&" : string.Empty) + "access_token=" + Uri.EscapeDataString(token) }.Uri;
                    }

                    WebSocket socket = await server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
                    return socket;
                };
            })
            .Build();
    }
}
