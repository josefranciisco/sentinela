public class ScreenCaptureOptions
{
    public string StorageProvider { get; set; } = "Local";
    public string StoragePath { get; set; } = "C:\\ProgramData\\Sentinela\\Captures";
    public string EncryptionKey { get; set; } = string.Empty;
    public int DefaultQuality { get; set; } = 50;
    public int MaxWidth { get; set; } = 1920;
    public int MaxHeight { get; set; } = 1080;
    public bool EnableAudit { get; set; } = true;
    public int RetentionDays { get; set; } = 30;
    public bool AllowOnDemand { get; set; } = true;
    public bool AllowScheduled { get; set; }
    public bool AllowEventDriven { get; set; }
}
