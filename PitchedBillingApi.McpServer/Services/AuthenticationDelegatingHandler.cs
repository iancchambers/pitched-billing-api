namespace PitchedBillingApi.McpServer.Services;

public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly AzureAuthService _authService;

    public AuthenticationDelegatingHandler(AzureAuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get access token (user's token from device code flow) and add to request
        var token = await _authService.GetAccessTokenAsync();
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Send request
        var response = await base.SendAsync(request, cancellationToken);

        // If we get 401 Unauthorized, clear cache and try one more time
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _authService.ClearCacheAsync();

            // Get new token and retry
            var newToken = await _authService.GetAccessTokenAsync();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }
}
