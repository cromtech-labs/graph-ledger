namespace GraphLedger.Core.Models;

public class AppConfiguration
{
    public AzureSettings Azure { get; set; } = new();

    public SchedulingSettings Scheduling { get; set; } = new();

    public StorageSettings Storage { get; set; } = new();

    public List<string> EnabledWorkloads { get; set; } = new()
    {
        "deviceManagement",
        "conditionalAccess",
        "identityGovernance"
    };
}

public class AzureSettings
{
    public string TenantId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string? CertificateThumbprint { get; set; }

    public string[] Scopes { get; set; } = new[] { "https://graph.microsoft.com/.default" };
}

public class SchedulingSettings
{
    public bool Enabled { get; set; } = true;

    public string CronExpression { get; set; } = "0 0 * * * ?"; // Every hour

    public int RetentionDays { get; set; } = 90;
}

public class StorageSettings
{
    public string DatabasePath { get; set; } = "data/graphledger.db";

    public string ExportPath { get; set; } = "data/exports";
}
