using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GraphLedger.Core.Graph.Auth;
using GraphLedger.Core.Models.Utcm;

namespace GraphLedger.Core.Graph;

/// <summary>
/// Client for interacting with the Microsoft Graph UTCM
/// (Unified Tenant Configuration Management) APIs.
/// </summary>
public class UtcmClient : IUtcmClient
{
    private const string BaseUrl = "https://graph.microsoft.com/beta/admin/configurationManagement";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly GraphAuthProvider _authProvider;
    private readonly string _tenantId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public UtcmClient(GraphAuthProvider authProvider, string tenantId, HttpClient? httpClient = null)
    {
        _authProvider = authProvider;
        _tenantId = tenantId;
        _httpClient = httpClient ?? new HttpClient();
    }

    #region Snapshot Operations

    public async Task<UtcmSnapshotJob> CreateSnapshotAsync(string displayName, IEnumerable<string> resourceTypes, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        // UTCM API requires lowercase resource type names
        var request = new
        {
            displayName,
            resources = resourceTypes.Select(r => r.ToLowerInvariant()).ToArray()
        };

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        var response = await _httpClient.PostAsJsonAsync(
            $"{BaseUrl}/configurationSnapshots/createSnapshot",
            request,
            JsonOptions,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"UTCM API error ({response.StatusCode}):\n" +
                $"Endpoint: POST {BaseUrl}/configurationSnapshots/createSnapshot\n" +
                $"Request: {requestJson}\n" +
                $"Response: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<UtcmSnapshotJobResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to parse snapshot job response");

        return MapToSnapshotJob(result);
    }

    public async Task<UtcmSnapshotJob> GetSnapshotJobAsync(string jobId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var response = await _httpClient.GetAsync($"{BaseUrl}/configurationSnapshots/{jobId}", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UtcmSnapshotJobResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to parse snapshot job response");

        return MapToSnapshotJob(result);
    }

    public async Task<UtcmSnapshotJob> WaitForSnapshotAsync(string jobId, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var job = await GetSnapshotJobAsync(jobId, ct);

            if (job.Status is UtcmJobStatus.Succeeded or UtcmJobStatus.Failed)
            {
                if (job.Status == UtcmJobStatus.Succeeded && !string.IsNullOrEmpty(job.ResourceLocation))
                {
                    return await FetchSnapshotDataAsync(job, ct);
                }
                return job;
            }

            await Task.Delay(PollInterval, ct);
        }

        throw new TimeoutException($"Snapshot job {jobId} did not complete within the timeout period");
    }

    public async Task<UtcmSnapshotJob> FetchSnapshotDataAsync(UtcmSnapshotJob job, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(job.ResourceLocation))
        {
            return job;
        }

        await EnsureAuthenticatedAsync(ct);

        var response = await _httpClient.GetAsync(job.ResourceLocation, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ODataCollectionResponse<UtcmSnapshotResourceResponse>>(JsonOptions, ct);

        if (result?.Value != null)
        {
            job.SnapshotData = result.Value.Select(r => new UtcmSnapshotResource
            {
                DisplayName = r.DisplayName ?? string.Empty,
                ResourceType = r.ResourceType ?? string.Empty,
                Properties = r.Properties ?? new Dictionary<string, object?>()
            }).ToList();
        }

        return job;
    }

    #endregion

    #region Monitor Operations

    public async Task<UtcmMonitor> CreateMonitorAsync(string displayName, UtcmBaseline baseline, int frequencyInHours = 24, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var request = new
        {
            displayName,
            monitorRunFrequencyInHours = frequencyInHours,
            baseline = new
            {
                displayName = baseline.DisplayName,
                description = baseline.Description,
                // UTCM API requires lowercase resource type names
                resources = baseline.Resources.Select(r => new
                {
                    displayName = r.DisplayName,
                    resourceType = r.ResourceType.ToLowerInvariant(),
                    properties = r.Properties
                }).ToArray()
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{BaseUrl}/monitors",
            request,
            JsonOptions,
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UtcmMonitorResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to parse monitor response");

        return MapToMonitor(result);
    }

    public async Task<IReadOnlyList<UtcmMonitor>> ListMonitorsAsync(CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var response = await _httpClient.GetAsync($"{BaseUrl}/monitors", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ODataCollectionResponse<UtcmMonitorResponse>>(JsonOptions, ct);

        return result?.Value?.Select(MapToMonitor).ToList() ?? [];
    }

    public async Task<UtcmMonitor?> GetMonitorAsync(string monitorId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var response = await _httpClient.GetAsync($"{BaseUrl}/monitors/{monitorId}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UtcmMonitorResponse>(JsonOptions, ct);
        return result != null ? MapToMonitor(result) : null;
    }

    public async Task DeleteMonitorAsync(string monitorId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var response = await _httpClient.DeleteAsync($"{BaseUrl}/monitors/{monitorId}", ct);
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Drift Operations

    public async Task<IReadOnlyList<UtcmDrift>> ListDriftsAsync(string? monitorId = null, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var url = string.IsNullOrEmpty(monitorId)
            ? $"{BaseUrl}/drifts"
            : $"{BaseUrl}/monitors/{monitorId}/drifts";

        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ODataCollectionResponse<UtcmDriftResponse>>(JsonOptions, ct);

        return result?.Value?.Select(MapToDrift).ToList() ?? [];
    }

    public async Task<UtcmDrift?> GetDriftAsync(string driftId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var response = await _httpClient.GetAsync($"{BaseUrl}/drifts/{driftId}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UtcmDriftResponse>(JsonOptions, ct);
        return result != null ? MapToDrift(result) : null;
    }

    #endregion

    #region Utility

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);

            var response = await _httpClient.GetAsync($"{BaseUrl}/monitors?$top=1", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> GetSupportedWorkloads() => UtcmResourceTypeRegistry.GetWorkloads();

    public IReadOnlyList<string> GetResourceTypesForWorkload(string workload) => UtcmResourceTypeRegistry.GetResourceTypes(workload);

    #endregion

    #region Authentication

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var credential = _authProvider.CreateCredential();
        var context = new Azure.Core.TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
        var token = await credential.GetTokenAsync(context, ct);
        return token.Token;
    }

    #endregion

    #region Response Mapping

    private static UtcmSnapshotJob MapToSnapshotJob(UtcmSnapshotJobResponse response)
    {
        return new UtcmSnapshotJob
        {
            Id = response.Id ?? string.Empty,
            Status = ParseJobStatus(response.Status),
            CreatedDateTime = response.CreatedDateTime ?? DateTime.UtcNow,
            CompletedDateTime = response.CompletedDateTime,
            DisplayName = response.DisplayName,
            Resources = response.Resources ?? [],
            ResourceLocation = response.ResourceLocation,
            ErrorMessage = response.ErrorMessage
        };
    }

    private static UtcmMonitor MapToMonitor(UtcmMonitorResponse response)
    {
        return new UtcmMonitor
        {
            Id = response.Id ?? string.Empty,
            DisplayName = response.DisplayName ?? string.Empty,
            Description = response.Description,
            Status = response.Status ?? "active",
            MonitorRunFrequencyInHours = response.MonitorRunFrequencyInHours ?? 24,
            CreatedDateTime = response.CreatedDateTime ?? DateTime.UtcNow,
            Baseline = response.Baseline != null ? MapToBaseline(response.Baseline) : new UtcmBaseline()
        };
    }

    private static UtcmBaseline MapToBaseline(UtcmBaselineResponse response)
    {
        return new UtcmBaseline
        {
            DisplayName = response.DisplayName ?? string.Empty,
            Description = response.Description,
            Resources = response.Resources?.Select(r => new UtcmBaselineResource
            {
                DisplayName = r.DisplayName ?? string.Empty,
                ResourceType = r.ResourceType ?? string.Empty,
                Properties = r.Properties ?? new Dictionary<string, object?>()
            }).ToList() ?? []
        };
    }

    private static UtcmDrift MapToDrift(UtcmDriftResponse response)
    {
        return new UtcmDrift
        {
            Id = response.Id ?? string.Empty,
            MonitorId = response.MonitorId ?? string.Empty,
            ResourceType = response.ResourceType ?? string.Empty,
            BaselineResourceDisplayName = response.BaselineResourceDisplayName ?? string.Empty,
            FirstReportedDateTime = response.FirstReportedDateTime ?? DateTime.UtcNow,
            Status = response.Status ?? "active",
            DriftedProperties = response.DriftedProperties?.Select(p => new UtcmDriftedProperty
            {
                PropertyName = p.PropertyName ?? string.Empty,
                CurrentValue = p.CurrentValue,
                DesiredValue = p.DesiredValue
            }).ToList() ?? []
        };
    }

    private static UtcmJobStatus ParseJobStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "notstarted" => UtcmJobStatus.NotStarted,
            "running" or "inprogress" => UtcmJobStatus.Running,
            "succeeded" or "completed" => UtcmJobStatus.Succeeded,
            "failed" or "error" => UtcmJobStatus.Failed,
            _ => UtcmJobStatus.NotStarted
        };
    }

    #endregion

    #region Response DTOs

    private class ODataCollectionResponse<T>
    {
        [JsonPropertyName("@odata.context")]
        public string? ODataContext { get; set; }

        [JsonPropertyName("value")]
        public List<T>? Value { get; set; }
    }

    private class UtcmSnapshotJobResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public DateTime? CompletedDateTime { get; set; }
        public string? DisplayName { get; set; }
        public List<string>? Resources { get; set; }
        public string? ResourceLocation { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private class UtcmSnapshotResourceResponse
    {
        public string? DisplayName { get; set; }
        public string? ResourceType { get; set; }
        public Dictionary<string, object?>? Properties { get; set; }
    }

    private class UtcmMonitorResponse
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public int? MonitorRunFrequencyInHours { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public UtcmBaselineResponse? Baseline { get; set; }
    }

    private class UtcmBaselineResponse
    {
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public List<UtcmBaselineResourceResponse>? Resources { get; set; }
    }

    private class UtcmBaselineResourceResponse
    {
        public string? DisplayName { get; set; }
        public string? ResourceType { get; set; }
        public Dictionary<string, object?>? Properties { get; set; }
    }

    private class UtcmDriftResponse
    {
        public string? Id { get; set; }
        public string? MonitorId { get; set; }
        public string? ResourceType { get; set; }
        public string? BaselineResourceDisplayName { get; set; }
        public DateTime? FirstReportedDateTime { get; set; }
        public string? Status { get; set; }
        public List<UtcmDriftedPropertyResponse>? DriftedProperties { get; set; }
    }

    private class UtcmDriftedPropertyResponse
    {
        public string? PropertyName { get; set; }
        public string? CurrentValue { get; set; }
        public string? DesiredValue { get; set; }
    }

    #endregion
}
