using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Sentinela.Agent.Commands;

public interface IIpcService
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;
}

public class IpcService : IIpcService, IDisposable
{
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cts;
    private readonly ILogger<IpcService> _logger;
    private bool _running;
    private const string PipeName = "SentinelaAgentPipe";

    public event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;

    public IpcService(ILogger<IpcService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _running = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _ = Task.Run(() => ListenLoop(_cts.Token), _cts.Token);
        _logger.LogInformation("IPC service started on pipe: {PipeName}", PipeName);
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _running)
        {
            try
            {
                _pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                await _pipeServer.WaitForConnectionAsync(ct);
                _logger.LogInformation("IPC client connected");

                _ = Task.Run(() => HandleClientAsync(_pipeServer, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IPC server error");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            var buffer = new byte[4096];
            var ms = new MemoryStream();

            while (pipe.IsConnected && !ct.IsCancellationRequested)
            {
                var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, ct);
                if (bytesRead == 0) break;

                ms.Write(buffer, 0, bytesRead);

                if (pipe.IsMessageComplete)
                {
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    ms.SetLength(0);

                    try
                    {
                        var message = JsonSerializer.Deserialize<IpcMessage>(json);
                        if (message != null)
                        {
                            MessageReceived?.Invoke(this, new IpcMessageReceivedEventArgs(message));

                            var response = JsonSerializer.Serialize(new IpcMessage
                            {
                                Type = "Response",
                                Payload = "OK"
                            });
                            var responseBytes = Encoding.UTF8.GetBytes(response);
                            await pipe.WriteAsync(responseBytes, 0, responseBytes.Length, ct);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid IPC message");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IPC client handler error");
        }
        finally
        {
            pipe.Dispose();
        }
    }

    public Task StopAsync()
    {
        _running = false;
        _cts?.Cancel();
        _pipeServer?.Dispose();
        _pipeServer = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _pipeServer?.Dispose();
    }
}

public class IpcMessage
{
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class IpcMessageReceivedEventArgs : EventArgs
{
    public IpcMessage Message { get; }
    public IpcMessageReceivedEventArgs(IpcMessage message) => Message = message;
}
