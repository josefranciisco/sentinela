namespace Sentinela.ScreenCapture.Core;

public interface IScreenCapturePolicyService
{
    Task<ScreenCapturePolicy> CreatePolicyAsync(ScreenCapturePolicy policy, CancellationToken ct = default);
    Task<ScreenCapturePolicy?> GetPolicyAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ScreenCapturePolicy>> GetAllPoliciesAsync(CancellationToken ct = default);
    Task<ScreenCapturePolicy> UpdatePolicyAsync(ScreenCapturePolicy policy, CancellationToken ct = default);
    Task DeletePolicyAsync(Guid id, CancellationToken ct = default);
    Task<bool> ValidatePolicyAsync(ScreenCapturePolicy policy, CancellationToken ct = default);
    Task<CaptureRequest> CreateCaptureRequestAsync(CaptureRequest request, CancellationToken ct = default);
    Task<CaptureRequest> ApproveCaptureRequestAsync(Guid requestId, Guid approvedBy, CancellationToken ct = default);
    Task<CaptureRequest> DenyCaptureRequestAsync(Guid requestId, Guid deniedBy, CancellationToken ct = default);
    Task<bool> CanCaptureAsync(Guid computerId, Guid userId, Guid policyId, CancellationToken ct = default);
    Task<IReadOnlyList<ScreenCaptureRecord>> GetCaptureHistoryAsync(Guid computerId, DateTimeOffset? since = null, DateTimeOffset? until = null, CancellationToken ct = default);
}

public class ScreenCapturePolicyService : IScreenCapturePolicyService
{
    private readonly ILogger<ScreenCapturePolicyService> _logger;
    private readonly IOptions<ScreenCaptureOptions> _options;
    private readonly List<ScreenCapturePolicy> _policies = new();
    private readonly List<CaptureRequest> _requests = new();
    private readonly List<ScreenCaptureRecord> _records = new();

    public ScreenCapturePolicyService(ILogger<ScreenCapturePolicyService> logger, IOptions<ScreenCaptureOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    public Task<ScreenCapturePolicy> CreatePolicyAsync(ScreenCapturePolicy policy, CancellationToken ct = default)
    {
        policy.Id = Guid.NewGuid();
        policy.CreatedAt = DateTimeOffset.UtcNow;
        policy.UpdatedAt = policy.CreatedAt;

        if (!ValidatePolicyConfiguration(policy))
            throw new InvalidOperationException("Policy configuration is invalid.");

        _policies.Add(policy);
        _logger.LogInformation("Screen capture policy created: {PolicyId} - {Name}", policy.Id, policy.Name);
        return Task.FromResult(policy);
    }

    public Task<ScreenCapturePolicy?> GetPolicyAsync(Guid id, CancellationToken ct = default)
    {
        var policy = _policies.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(policy);
    }

    public Task<IReadOnlyList<ScreenCapturePolicy>> GetAllPoliciesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ScreenCapturePolicy>>(_policies.ToList());
    }

    public Task<ScreenCapturePolicy> UpdatePolicyAsync(ScreenCapturePolicy policy, CancellationToken ct = default)
    {
        var existing = _policies.FirstOrDefault(p => p.Id == policy.Id)
            ?? throw new KeyNotFoundException($"Policy {policy.Id} not found.");

        if (!ValidatePolicyConfiguration(policy))
            throw new InvalidOperationException("Policy configuration is invalid.");

        policy.UpdatedAt = DateTimeOffset.UtcNow;
        policy.CreatedAt = existing.CreatedAt;
        policy.CreatedBy = existing.CreatedBy;

        var index = _policies.IndexOf(existing);
        _policies[index] = policy;

        _logger.LogInformation("Screen capture policy updated: {PolicyId} - {Name}", policy.Id, policy.Name);
        return Task.FromResult(policy);
    }

    public Task DeletePolicyAsync(Guid id, CancellationToken ct = default)
    {
        var policy = _policies.FirstOrDefault(p => p.Id == id)
            ?? throw new KeyNotFoundException($"Policy {id} not found.");

        _policies.Remove(policy);
        _logger.LogInformation("Screen capture policy deleted: {PolicyId}", id);
        return Task.CompletedTask;
    }

    public Task<bool> ValidatePolicyAsync(ScreenCapturePolicy policy, CancellationToken ct = default)
    {
        return Task.FromResult(ValidatePolicyConfiguration(policy));
    }

    private bool ValidatePolicyConfiguration(ScreenCapturePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Name))
        {
            _logger.LogWarning("Policy validation failed: Name is required.");
            return false;
        }

        if (policy.Quality is < 0 or > 100)
        {
            _logger.LogWarning("Policy validation failed: Quality must be between 0 and 100.");
            return false;
        }

        if (policy.MaxWidth <= 0 || policy.MaxHeight <= 0)
        {
            _logger.LogWarning("Policy validation failed: Max dimensions must be positive.");
            return false;
        }

        if (policy.Mode == CaptureMode.Scheduled && policy.IntervalSeconds <= 0)
        {
            _logger.LogWarning("Policy validation failed: Scheduled mode requires a positive interval.");
            return false;
        }

        if (policy.RetentionPeriod <= TimeSpan.Zero)
        {
            _logger.LogWarning("Policy validation failed: Retention period must be positive.");
            return false;
        }

        var opts = _options.Value;
        if (policy.Mode == CaptureMode.OnDemand && !opts.AllowOnDemand)
        {
            _logger.LogWarning("Policy validation failed: On-demand captures are not allowed.");
            return false;
        }

        if (policy.Mode == CaptureMode.Scheduled && !opts.AllowScheduled)
        {
            _logger.LogWarning("Policy validation failed: Scheduled captures are not allowed.");
            return false;
        }

        if (policy.Mode == CaptureMode.EventDriven && !opts.AllowEventDriven)
        {
            _logger.LogWarning("Policy validation failed: Event-driven captures are not allowed.");
            return false;
        }

        return true;
    }

    public Task<CaptureRequest> CreateCaptureRequestAsync(CaptureRequest request, CancellationToken ct = default)
    {
        request.Id = Guid.NewGuid();
        request.RequestedAt = DateTimeOffset.UtcNow;
        request.Status = CaptureRequestStatus.Pending;

        _requests.Add(request);
        _logger.LogInformation("Capture request created: {RequestId} by {RequestedBy}", request.Id, request.RequestedBy);
        return Task.FromResult(request);
    }

    public Task<CaptureRequest> ApproveCaptureRequestAsync(Guid requestId, Guid approvedBy, CancellationToken ct = default)
    {
        var request = _requests.FirstOrDefault(r => r.Id == requestId)
            ?? throw new KeyNotFoundException($"Capture request {requestId} not found.");

        request.Status = CaptureRequestStatus.Approved;
        request.ApprovedBy = approvedBy;
        request.ApprovedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Capture request approved: {RequestId} by {ApprovedBy}", requestId, approvedBy);
        return Task.FromResult(request);
    }

    public Task<CaptureRequest> DenyCaptureRequestAsync(Guid requestId, Guid deniedBy, CancellationToken ct = default)
    {
        var request = _requests.FirstOrDefault(r => r.Id == requestId)
            ?? throw new KeyNotFoundException($"Capture request {requestId} not found.");

        request.Status = CaptureRequestStatus.Denied;
        request.ApprovedBy = deniedBy;
        request.ApprovedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Capture request denied: {RequestId} by {DeniedBy}", requestId, deniedBy);
        return Task.FromResult(request);
    }

    public Task<bool> CanCaptureAsync(Guid computerId, Guid userId, Guid policyId, CancellationToken ct = default)
    {
        var policy = _policies.FirstOrDefault(p => p.Id == policyId);
        if (policy == null || !policy.Enabled)
            return Task.FromResult(false);

        if (policy.TargetComputers.Length > 0 && !policy.TargetComputers.Contains(computerId.ToString()))
            return Task.FromResult(false);

        if (policy.ExcludedComputers.Contains(computerId.ToString()))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<ScreenCaptureRecord>> GetCaptureHistoryAsync(Guid computerId, DateTimeOffset? since = null, DateTimeOffset? until = null, CancellationToken ct = default)
    {
        var query = _records.AsEnumerable();

        query = query.Where(r => r.ComputerId == computerId);

        if (since.HasValue)
            query = query.Where(r => r.CapturedAt >= since.Value);

        if (until.HasValue)
            query = query.Where(r => r.CapturedAt <= until.Value);

        return Task.FromResult<IReadOnlyList<ScreenCaptureRecord>>(query.OrderByDescending(r => r.CapturedAt).ToList());
    }
}
