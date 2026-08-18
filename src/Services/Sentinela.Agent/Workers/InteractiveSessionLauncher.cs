using System.Diagnostics;

namespace Sentinela.Agent.Workers;

/// <summary>
/// Processo do serviço (Sessão 0) não consegue capturar a tela.
/// Sobe o mesmo executável na sessão do usuário logado, onde gravação e remoto funcionam.
/// </summary>
public sealed class InteractiveSessionLauncher : BackgroundService
{
    private readonly ILogger<InteractiveSessionLauncher> _logger;
    private int _childPid;

    public InteractiveSessionLauncher(ILogger<InteractiveSessionLauncher> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var exe = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "Sentinela.Agent.exe");
        var workingDir = AppContext.BaseDirectory;

        _logger.LogInformation(
            "Serviço na Sessão 0: o agente de captura será iniciado na sessão do usuário logado ({Exe})",
            exe);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EnsureChild(exe, workingDir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Aguardando sessão interativa para captura de tela / remoto / gravação");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        StopChild();
    }

    private void EnsureChild(string exe, string workingDir)
    {
        var existing = UserSessionProcess.FindInteractiveAgent(Environment.ProcessId);
        if (existing is int runningPid)
        {
            _childPid = runningPid;
            return;
        }

        var pid = UserSessionProcess.StartInteractiveAgent(exe, workingDir);
        _childPid = pid;
        _logger.LogInformation("Agente interativo iniciado (PID {Pid}) na sessão do usuário", pid);
    }

    private void StopChild()
    {
        if (_childPid <= 0)
            return;

        try
        {
            using var process = Process.GetProcessById(_childPid);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
            // already gone
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao encerrar o agente interativo PID {Pid}", _childPid);
        }
        finally
        {
            _childPid = 0;
        }
    }

    public override void Dispose()
    {
        StopChild();
        base.Dispose();
    }
}
