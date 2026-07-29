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
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public string DefaultScheme { get; set; } = "AzureAD";
}
