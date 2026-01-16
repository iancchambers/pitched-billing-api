using Microsoft.AspNetCore.DataProtection;

namespace PitchedBillingApi.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive OAuth tokens using ASP.NET Core Data Protection API.
/// Uses AES-256-CBC encryption with keys managed by Azure Key Vault in production.
/// </summary>
public interface ITokenEncryptionService
{
    /// <summary>
    /// Encrypts plain text using AES-256 encryption
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <returns>The encrypted cipher text</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts cipher text using AES-256 encryption
    /// </summary>
    /// <param name="cipherText">The encrypted cipher text</param>
    /// <returns>The decrypted plain text</returns>
    string Decrypt(string cipherText);
}

public class TokenEncryptionService : ITokenEncryptionService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<TokenEncryptionService> _logger;

    public TokenEncryptionService(
        IDataProtectionProvider provider,
        ILogger<TokenEncryptionService> logger)
    {
        // Create a purpose-specific protector for QuickBooks tokens
        // This ensures tokens encrypted for this purpose cannot be decrypted elsewhere
        _protector = provider.CreateProtector("QuickBooksTokenProtection");
        _logger = logger;
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            _logger.LogWarning("Attempted to encrypt null or empty string");
            return plainText;
        }

        try
        {
            var encrypted = _protector.Protect(plainText);
            _logger.LogDebug("Successfully encrypted token (length: {Length})", plainText.Length);
            return encrypted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt token");
            throw;
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            _logger.LogWarning("Attempted to decrypt null or empty string");
            return cipherText;
        }

        try
        {
            var decrypted = _protector.Unprotect(cipherText);
            _logger.LogDebug("Successfully decrypted token");
            return decrypted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt token - this may indicate the token was stored before encryption was enabled or encryption keys have changed");
            throw new InvalidOperationException("Failed to decrypt QuickBooks token. You may need to reconnect to QuickBooks.", ex);
        }
    }
}
