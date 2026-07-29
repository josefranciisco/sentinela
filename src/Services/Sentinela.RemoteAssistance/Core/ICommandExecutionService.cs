namespace Sentinela.RemoteAssistance.Core;

public interface ICommandExecutionService
{
    Task<RemoteCommand> IssueCommandAsync(Guid computerId, CommandType type, string? parameters, string issuedBy);
    Task<RemoteCommand?> GetCommandAsync(Guid commandId);
    Task<IEnumerable<RemoteCommand>> GetPendingCommandsAsync(Guid computerId);
    Task<bool> UpdateCommandStatusAsync(Guid commandId, CommandStatus status, string? result = null, string? error = null);
    Task<bool> CancelCommandAsync(Guid commandId);
    Task<IEnumerable<RemoteCommand>> GetCommandHistoryAsync(Guid computerId, int count = 50);
    Task<bool> ValidateCommandAsync(CommandType type, string issuedBy);
}
