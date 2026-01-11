using Microsoft.Identity.Client;
using System.Text.Json;

namespace PitchedBillingApi.McpServer.Services;

public class AzureAuthService
{
    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;
    private readonly string _tokenCacheFile;
    private AuthenticationResult? _cachedResult;

    public AzureAuthService(string tenantId, string clientId, string[] scopes)
    {
        _scopes = scopes;

        // Token cache file location (in user's local app data)
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheDir = Path.Combine(appDataPath, "PitchedBillingApiMcp");
        Directory.CreateDirectory(cacheDir);
        _tokenCacheFile = Path.Combine(cacheDir, "msal_token_cache.json");

        _app = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .WithRedirectUri("http://localhost") // For device code flow
            .Build();

        // Set up token cache serialization
        SetupTokenCache();
    }

    public async Task<string> GetAccessTokenAsync()
    {
        // Try to get token silently first (from cache)
        try
        {
            var accounts = await _app.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            if (firstAccount != null)
            {
                var result = await _app.AcquireTokenSilent(_scopes, firstAccount)
                    .ExecuteAsync();
                _cachedResult = result;
                return result.AccessToken;
            }
        }
        catch (MsalUiRequiredException)
        {
            // Silent acquisition failed, need interactive login
        }

        // If silent acquisition fails, use device code flow
        return await AcquireTokenInteractiveAsync();
    }

    private async Task<string> AcquireTokenInteractiveAsync()
    {
        try
        {
            var result = await _app.AcquireTokenWithDeviceCode(_scopes, deviceCodeResult =>
            {
                // Display the device code to the user
                Console.WriteLine();
                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║          Azure Authentication Required                         ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
                Console.WriteLine(deviceCodeResult.Message);
                Console.WriteLine();
                Console.WriteLine("Waiting for authentication...");
                Console.WriteLine();
                return Task.CompletedTask;
            }).ExecuteAsync();

            _cachedResult = result;

            Console.WriteLine("✓ Authentication successful!");
            Console.WriteLine($"  Authenticated as: {result.Account.Username}");
            Console.WriteLine();

            return result.AccessToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Authentication failed: {ex.Message}");
            throw;
        }
    }

    private void SetupTokenCache()
    {
        // Load cache from file if it exists
        if (File.Exists(_tokenCacheFile))
        {
            try
            {
                var cacheData = File.ReadAllBytes(_tokenCacheFile);
                (_app.UserTokenCache as ITokenCacheSerializer)?.DeserializeMsalV3(cacheData);
            }
            catch
            {
                // If cache is corrupted, delete it
                File.Delete(_tokenCacheFile);
            }
        }

        // Save cache to file whenever it changes
        _app.UserTokenCache.SetBeforeAccess(args =>
        {
            if (File.Exists(_tokenCacheFile))
            {
                try
                {
                    var cacheData = File.ReadAllBytes(_tokenCacheFile);
                    args.TokenCache.DeserializeMsalV3(cacheData);
                }
                catch
                {
                    // Ignore errors
                }
            }
        });

        _app.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged)
            {
                try
                {
                    var cacheData = args.TokenCache.SerializeMsalV3();
                    File.WriteAllBytes(_tokenCacheFile, cacheData);
                }
                catch
                {
                    // Ignore errors
                }
            }
        });
    }

    public async Task ClearCacheAsync()
    {
        var accounts = await _app.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _app.RemoveAsync(account);
        }

        if (File.Exists(_tokenCacheFile))
        {
            File.Delete(_tokenCacheFile);
        }

        _cachedResult = null;
        Console.WriteLine("✓ Token cache cleared");
    }
}
