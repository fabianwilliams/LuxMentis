using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;

public class BearerAuthenticationProviderWithCancellationToken
{
    private readonly IPublicClientApplication _client;
    private readonly ILogger<BearerAuthenticationProviderWithCancellationToken> _logger;

    public BearerAuthenticationProviderWithCancellationToken(IConfiguration configuration, ILogger<BearerAuthenticationProviderWithCancellationToken> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var clientId = configuration["MSGraph:ClientId"];
        var tenantId = configuration["MSGraph:TenantId"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId))
        {
            throw new InvalidOperationException("Please provide valid MSGraph configuration in appsettings.Development.json file.");
        }

        this._client = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .WithDefaultRedirectUri()
            .Build();
    }

    public async Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var token = await this.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var scopes = new string[] { "https://graph.microsoft.com/.default" };
        try
        {
            _logger.LogInformation("Attempting to acquire token silently.");
            var authResult = await this._client.AcquireTokenSilent(scopes, (await this._client.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault()).ExecuteAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Token acquired silently.");
            return authResult.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Silent token acquisition failed: {ex.Message}. Attempting device code flow.");
            var authResult = await this._client.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
            {
                Console.WriteLine(deviceCodeResult.Message);
                return Task.CompletedTask;
            }).ExecuteAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Token acquired via device code flow.");
            return authResult.AccessToken;
        }
    }
}