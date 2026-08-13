namespace Sentinela.Identity.Configuration;

public class JwtConfiguration
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

public class LdapConfiguration
{
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
    public string BaseDn { get; set; } = string.Empty;
    public string UserSearchBase { get; set; } = string.Empty;
    public string GroupSearchBase { get; set; } = string.Empty;
    public string AdminBindDn { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}

public class SsoConfiguration
{
    public bool Enabled { get; set; }
    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
    public SsoProviderConfiguration Google { get; set; } = new();
    public SsoProviderConfiguration Microsoft { get; set; } = new();

    public SsoProviderConfiguration? GetProvider(string provider) =>
        provider.ToLowerInvariant() switch
        {
            "google" => Enabled && Google.IsConfigured ? Google.WithDefaults("google") : null,
            "microsoft" => Enabled && Microsoft.IsConfigured ? Microsoft.WithDefaults("microsoft") : null,
            _ => null
        };
}

public class SsoProviderConfiguration
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = string.Empty;

    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string UserInfoEndpoint { get; set; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; set; } = new List<string>();

    public bool IsConfigured => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);

    public SsoProviderConfiguration WithDefaults(string provider)
    {
        if (provider == "google")
        {
            AuthorizationEndpoint = string.IsNullOrEmpty(AuthorizationEndpoint)
                ? "https://accounts.google.com/o/oauth2/v2/auth"
                : AuthorizationEndpoint;
            TokenEndpoint = string.IsNullOrEmpty(TokenEndpoint)
                ? "https://oauth2.googleapis.com/token"
                : TokenEndpoint;
            UserInfoEndpoint = string.IsNullOrEmpty(UserInfoEndpoint)
                ? "https://www.googleapis.com/oauth2/v3/userinfo"
                : UserInfoEndpoint;
            Scopes = Scopes.Count == 0 ? new List<string> { "openid", "email", "profile" } : Scopes;
        }
        else if (provider == "microsoft")
        {
            var tenant = "common";
            AuthorizationEndpoint = string.IsNullOrEmpty(AuthorizationEndpoint)
                ? $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize"
                : AuthorizationEndpoint;
            TokenEndpoint = string.IsNullOrEmpty(TokenEndpoint)
                ? $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token"
                : TokenEndpoint;
            UserInfoEndpoint = string.IsNullOrEmpty(UserInfoEndpoint)
                ? "https://graph.microsoft.com/oidc/userinfo"
                : UserInfoEndpoint;
            Scopes = Scopes.Count == 0 ? new List<string> { "openid", "email", "profile" } : Scopes;
        }

        CallbackPath = string.IsNullOrEmpty(CallbackPath) ? $"/api/v1/auth/sso/callback/{provider}" : CallbackPath;
        return this;
    }
}
