namespace Sentinela.RemoteAssistance.Configuration;

public class RemoteAssistanceOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxConcurrentSessions { get; set; } = 50;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public int MaxFileTransferSizeMB { get; set; } = 500;
    public string[] AllowedCommandTypes { get; set; } = Array.Empty<string>();
    public bool RequireJustification { get; set; } = true;
    public bool RequireEndUserConsent { get; set; } = true;
    public bool FullAuditEnabled { get; set; } = true;
    public int ScreenFrameQuality { get; set; } = 40;
    public int ScreenFps { get; set; } = 10;
}
