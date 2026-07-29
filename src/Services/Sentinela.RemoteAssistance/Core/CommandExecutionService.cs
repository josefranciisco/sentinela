using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinela.RemoteAssistance.Configuration;

namespace Sentinela.RemoteAssistance.Core;

public class CommandExecutionService : ICommandExecutionService
{
    private readonly ConcurrentDictionary<Guid, RemoteCommand> _commands = new();
    private readonly ILogger<CommandExecutionService> _logger;
    private readonly RemoteAssistanceOptions _options;

    private static readonly HashSet<CommandType> DangerousCommands = new()
    {
        CommandType.Restart, CommandType.Shutdown, CommandType.Logoff, CommandType.Lock,
        CommandType.KillProcess, CommandType.StopService, CommandType.RegistryWrite
    };

    public CommandExecutionService(IOptions<RemoteAssistanceOptions> options, ILogger<CommandExecutionService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<RemoteCommand> IssueCommandAsync(Guid computerId, CommandType type, string? parameters, string issuedBy)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Remote assistance is disabled.");

        if (!ValidateCommandInternal(type, issuedBy))
            throw new UnauthorizedAccessException($"Command {type} is not allowed for user {issuedBy}.");

        var command = new RemoteCommand
        {
            Id = Guid.NewGuid(),
            ComputerId = computerId,
            Type = type,
            Parameters = parameters,
            IssuedBy = issuedBy,
            IssuedAt = DateTimeOffset.UtcNow,
            Status = CommandStatus.Pending
        };

        _commands.TryAdd(command.Id, command);
        _logger.LogInformation("Command issued: {CommandId} type={CommandType} on {ComputerId} by {IssuedBy}",
            command.Id, type, computerId, issuedBy);

        return Task.FromResult(command);
    }

    public Task<RemoteCommand?> GetCommandAsync(Guid commandId)
    {
        _commands.TryGetValue(commandId, out var command);
        return Task.FromResult(command);
    }

    public Task<IEnumerable<RemoteCommand>> GetPendingCommandsAsync(Guid computerId)
    {
        var pending = _commands.Values
            .Where(c => c.ComputerId == computerId && c.Status == CommandStatus.Pending)
            .OrderBy(c => c.IssuedAt);
        return Task.FromResult(pending);
    }

    public Task<bool> UpdateCommandStatusAsync(Guid commandId, CommandStatus status, string? result = null, string? error = null)
    {
        if (!_commands.TryGetValue(commandId, out var command))
            return Task.FromResult(false);

        command.Status = status;

        if (result is not null)
            command.Result = result;

        if (error is not null)
            command.Error = error;

        if (status is CommandStatus.Completed or CommandStatus.Failed)
            command.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Command {CommandId} status updated to {Status}", commandId, status);
        return Task.FromResult(true);
    }

    public Task<bool> CancelCommandAsync(Guid commandId)
    {
        if (!_commands.TryGetValue(commandId, out var command))
            return Task.FromResult(false);

        if (command.Status is CommandStatus.Completed or CommandStatus.Failed)
            return Task.FromResult(false);

        command.Status = CommandStatus.Failed;
        command.Error = "Cancelled by user";
        command.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Command {CommandId} cancelled", commandId);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<RemoteCommand>> GetCommandHistoryAsync(Guid computerId, int count = 50)
    {
        var history = _commands.Values
            .Where(c => c.ComputerId == computerId)
            .OrderByDescending(c => c.IssuedAt)
            .Take(count);
        return Task.FromResult(history);
    }

    public Task<bool> ValidateCommandAsync(CommandType type, string issuedBy)
    {
        return Task.FromResult(ValidateCommandInternal(type, issuedBy));
    }

    private bool ValidateCommandInternal(CommandType type, string issuedBy)
    {
        var allowed = _options.AllowedCommandTypes;
        if (allowed is { Length: > 0 } && !allowed.Contains(type.ToString(), StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public static bool IsDangerousCommand(CommandType type) => DangerousCommands.Contains(type);
}
