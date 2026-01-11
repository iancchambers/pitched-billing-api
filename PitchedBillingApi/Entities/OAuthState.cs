using System.ComponentModel.DataAnnotations;

namespace PitchedBillingApi.Entities;

/// <summary>
/// Stores OAuth state tokens for CSRF protection during OAuth flows.
/// States are single-use and expire after 15 minutes.
/// </summary>
public class OAuthState
{
    /// <summary>
    /// The state token (GUID). Primary key.
    /// </summary>
    [Key]
    [MaxLength(450)]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// When this state token was created
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// When this state token expires (typically 15 minutes after creation)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// The OAuth provider this state is for (e.g., "QuickBooks")
    /// </summary>
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;
}
