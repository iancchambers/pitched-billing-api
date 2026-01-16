using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PitchedBillingApi.Data;
using PitchedBillingApi.Entities;
using PitchedBillingApi.Models;

namespace PitchedBillingApi.Services;

public interface IQuickBooksAuthService
{
    Task<string> GetAuthorizationUrlAsync(string state);
    Task<bool> ValidateAndConsumeStateAsync(string state);
    Task<QuickBooksTokenResponse> ExchangeCodeForTokensAsync(string code, string realmId);
    Task<QuickBooksTokenResponse> RefreshAccessTokenAsync();
    Task<string> GetValidAccessTokenAsync();
    bool IsConnected { get; }
    string? RealmId { get; }
}

public class QuickBooksAuthService : IQuickBooksAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QuickBooksAuthService> _logger;
    private readonly BillingDbContext _dbContext;
    private readonly ITokenEncryptionService _encryptionService;

    private const string AuthorizationEndpoint = "https://appcenter.intuit.com/connect/oauth2";
    private const string TokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    // Cache for current request - loaded from DB on first access
    private QuickBooksToken? _cachedToken;

    public bool IsConnected => GetTokenFromDb() != null;
    public string? RealmId => GetTokenFromDb()?.RealmId;

    public QuickBooksAuthService(
        HttpClient httpClient,
        IConfiguration configuration,
        BillingDbContext dbContext,
        ILogger<QuickBooksAuthService> logger,
        ITokenEncryptionService encryptionService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    private QuickBooksToken? GetTokenFromDb()
    {
        if (_cachedToken != null)
            return _cachedToken;

        var dbToken = _dbContext.QuickBooksTokens
            .OrderByDescending(t => t.ModifiedDate)
            .FirstOrDefault();

        if (dbToken != null)
        {
            try
            {
                // Decrypt tokens after retrieving from database
                _cachedToken = new QuickBooksToken
                {
                    Id = dbToken.Id,
                    RealmId = dbToken.RealmId,
                    AccessToken = _encryptionService.Decrypt(dbToken.AccessToken),
                    RefreshToken = _encryptionService.Decrypt(dbToken.RefreshToken),
                    AccessTokenExpiresAt = dbToken.AccessTokenExpiresAt,
                    RefreshTokenExpiresAt = dbToken.RefreshTokenExpiresAt,
                    CreatedDate = dbToken.CreatedDate,
                    ModifiedDate = dbToken.ModifiedDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt QuickBooks tokens. Tokens may have been created before encryption was enabled. Please reconnect to QuickBooks.");
                // Return null to indicate no valid token - forces re-authentication
                return null;
            }
        }

        return _cachedToken;
    }

    public async Task<string> GetAuthorizationUrlAsync(string state)
    {
        var clientId = _configuration["quickbooks-client-id"];
        var redirectUri = _configuration["quickbooks-redirect-uri"];
        var scope = "com.intuit.quickbooks.accounting";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            throw new InvalidOperationException("QuickBooks client ID or redirect URI not configured");
        }

        // Store state in database for CSRF protection
        var oauthState = new OAuthState
        {
            State = state,
            CreatedDate = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // 15 minute expiration
            Provider = "QuickBooks"
        };

        _dbContext.OAuthStates.Add(oauthState);
        await _dbContext.SaveChangesAsync();

        // Cleanup expired states (older than 1 hour)
        await CleanupExpiredStatesAsync();

        var url = $"{AuthorizationEndpoint}?" +
                  $"client_id={Uri.EscapeDataString(clientId)}&" +
                  $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                  $"response_type=code&" +
                  $"scope={Uri.EscapeDataString(scope)}&" +
                  $"state={Uri.EscapeDataString(state)}";

        return url;
    }

    public async Task<bool> ValidateAndConsumeStateAsync(string state)
    {
        if (string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("OAuth state validation failed: state is null or empty");
            return false;
        }

        var oauthState = await _dbContext.OAuthStates
            .FirstOrDefaultAsync(s => s.State == state && s.Provider == "QuickBooks");

        if (oauthState == null)
        {
            _logger.LogWarning("OAuth state validation failed: state not found - {State}", state);
            return false;
        }

        if (oauthState.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("OAuth state validation failed: state expired - {State}", state);
            _dbContext.OAuthStates.Remove(oauthState);
            await _dbContext.SaveChangesAsync();
            return false;
        }

        // State is valid - consume it (delete from database to prevent replay)
        _dbContext.OAuthStates.Remove(oauthState);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("OAuth state validated successfully - {State}", state);
        return true;
    }

    private async Task CleanupExpiredStatesAsync()
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-1);
        var expiredStates = await _dbContext.OAuthStates
            .Where(s => s.ExpiresAt < cutoffTime)
            .ToListAsync();

        if (expiredStates.Any())
        {
            _dbContext.OAuthStates.RemoveRange(expiredStates);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired OAuth states", expiredStates.Count);
        }
    }

    public async Task<QuickBooksTokenResponse> ExchangeCodeForTokensAsync(string code, string realmId)
    {
        var clientId = _configuration["quickbooks-client-id"];
        var clientSecret = _configuration["quickbooks-client-secret"];
        var redirectUri = _configuration["quickbooks-redirect-uri"];

        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri! }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

        var authBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(authBytes));

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to exchange code for tokens: {Error}", errorContent);
            throw new InvalidOperationException($"Failed to exchange code for tokens: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var tokens = JsonSerializer.Deserialize<QuickBooksTokenResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize token response");

        // Store tokens in database (encrypted)
        var existingToken = await _dbContext.QuickBooksTokens.FirstOrDefaultAsync(t => t.RealmId == realmId);

        if (existingToken != null)
        {
            // Encrypt tokens before saving to database
            existingToken.AccessToken = _encryptionService.Encrypt(tokens.AccessToken);
            existingToken.RefreshToken = _encryptionService.Encrypt(tokens.RefreshToken);
            existingToken.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
            existingToken.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(100);
            existingToken.ModifiedDate = DateTime.UtcNow;
        }
        else
        {
            var newToken = new QuickBooksToken
            {
                RealmId = realmId,
                // Encrypt tokens before saving to database
                AccessToken = _encryptionService.Encrypt(tokens.AccessToken),
                RefreshToken = _encryptionService.Encrypt(tokens.RefreshToken),
                AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn),
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(100),
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };
            _dbContext.QuickBooksTokens.Add(newToken);
        }

        await _dbContext.SaveChangesAsync();
        _cachedToken = null; // Clear cache

        _logger.LogInformation("QuickBooks tokens encrypted and stored in database for realm {RealmId}", realmId);

        return tokens;
    }

    public async Task<QuickBooksTokenResponse> RefreshAccessTokenAsync()
    {
        var token = GetTokenFromDb();
        if (token == null || string.IsNullOrEmpty(token.RefreshToken))
        {
            throw new InvalidOperationException("No refresh token available. Please re-authorize.");
        }

        var clientId = _configuration["quickbooks-client-id"];
        var clientSecret = _configuration["quickbooks-client-secret"];

        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", token.RefreshToken }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

        var authBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(authBytes));

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to refresh token: {Error}", errorContent);

            // Clear tokens on refresh failure (expired refresh token)
            _dbContext.QuickBooksTokens.Remove(token);
            await _dbContext.SaveChangesAsync();
            _cachedToken = null;

            throw new InvalidOperationException($"Failed to refresh token: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var tokens = JsonSerializer.Deserialize<QuickBooksTokenResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize token response");

        // Update stored tokens (encrypt before saving)
        // Note: 'token' is the decrypted in-memory object, need to update the database record
        var dbToken = await _dbContext.QuickBooksTokens.FirstOrDefaultAsync(t => t.RealmId == token.RealmId);
        if (dbToken != null)
        {
            dbToken.AccessToken = _encryptionService.Encrypt(tokens.AccessToken);
            dbToken.RefreshToken = _encryptionService.Encrypt(tokens.RefreshToken);
            dbToken.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
            dbToken.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(100);
            dbToken.ModifiedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            _cachedToken = null; // Clear cache

            _logger.LogInformation("QuickBooks tokens refreshed and encrypted successfully");
        }

        return tokens;
    }

    public async Task<string> GetValidAccessTokenAsync()
    {
        var token = GetTokenFromDb();
        if (token == null)
        {
            throw new InvalidOperationException("Not connected to QuickBooks. Please authorize first.");
        }

        // Refresh token if expired or about to expire (5 minute buffer)
        if (DateTime.UtcNow >= token.AccessTokenExpiresAt.AddMinutes(-5))
        {
            _logger.LogInformation("Access token expired or expiring soon, refreshing...");
            await RefreshAccessTokenAsync();
            token = GetTokenFromDb()!;
        }

        return token.AccessToken;
    }
}
