using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace CodeForge.Api.Lsp;

/// <summary>
/// Bridges a browser WebSocket to a language server's stdio.
/// LSP frames (Content-Length headers) are stripped/added at the boundary:
/// each WebSocket message carries exactly one JSON-RPC payload.
/// </summary>
public static class LspBridge
{
    private static readonly IReadOnlyDictionary<string, (string FileName, string Arguments)> Servers =
        new Dictionary<string, (string, string)>
        {
            ["python"] = ("pyright-langserver", "--stdio")
        };

    public static void MapLspEndpoints(this WebApplication app)
    {
        app.Map("/lsp/{language}", async (HttpContext context, string language, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("CodeForge.LspBridge");

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (!Servers.TryGetValue(language, out var server))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { error = $"No language server for: {language}" });
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await BridgeAsync(socket, server.FileName, server.Arguments, logger);
        });
    }

    private static async Task BridgeAsync(WebSocket socket, string fileName, string arguments, ILogger logger)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            },
            EnableRaisingEvents = true
        };
        process.Start();
        _ = ConsumeAsync(process.StandardError); // language server logs; drain so it can't block

        using var cts = new CancellationTokenSource();

        try
        {
            await Task.WhenAny(
                PumpProcessToSocketAsync(process, socket, cts.Token),
                PumpSocketToProcessAsync(socket, process, logger, cts.Token));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "LSP bridge for {Command} ended with an error", fileName);
        }
        finally
        {
            cts.Cancel();
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "session ended", CancellationToken.None);
                }
                catch { /* client already gone */ }
            }
        }
    }

    private static async Task PumpProcessToSocketAsync(
        Process process, WebSocket socket, CancellationToken cancellationToken)
    {
        var stdout = process.StandardOutput.BaseStream;

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReadLspMessageAsync(stdout, cancellationToken);
            if (message is null || socket.State != WebSocketState.Open)
                break;

            await socket.SendAsync(
                Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text,
                endOfMessage: true, cancellationToken);
        }
    }

    private static async Task PumpSocketToProcessAsync(
        WebSocket socket, Process process, ILogger logger, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var stdin = process.StandardInput.BaseStream;

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var body = message.ToArray();
            var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            await stdin.WriteAsync(header, cancellationToken);
            await stdin.WriteAsync(body, cancellationToken);
            await stdin.FlushAsync(cancellationToken);
        }
    }

    private static async Task<string?> ReadLspMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>(128);
        var single = new byte[1];

        while (true)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(single, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            if (read == 0)
                return null; // process exited

            headerBytes.Add(single[0]);
            var count = headerBytes.Count;
            if (count >= 4 &&
                headerBytes[count - 4] == '\r' && headerBytes[count - 3] == '\n' &&
                headerBytes[count - 2] == '\r' && headerBytes[count - 1] == '\n')
                break;
        }

        var contentLength = -1;
        foreach (var line in Encoding.ASCII.GetString(headerBytes.ToArray())
                     .Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(line["Content-Length:".Length..].Trim());
        }

        if (contentLength < 0)
            throw new InvalidDataException("LSP message missing Content-Length header");

        var body = new byte[contentLength];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return Encoding.UTF8.GetString(body);
    }

    private static async Task ConsumeAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync() is not null) { /* drain */ }
        }
        catch { /* process ended */ }
    }
}
