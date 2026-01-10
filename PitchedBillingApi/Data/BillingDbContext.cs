using Microsoft.EntityFrameworkCore;
using PitchedBillingApi.Entities;

namespace PitchedBillingApi.Data;

public class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options)
    {
    }

    public DbSet<BillingPlan> BillingPlans => Set<BillingPlan>();
    public DbSet<BillingPlanItem> BillingPlanItems => Set<BillingPlanItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<EmailDeliveryStatus> EmailDeliveryStatuses => Set<EmailDeliveryStatus>();
    public DbSet<QuickBooksToken> QuickBooksTokens => Set<QuickBooksToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BillingPlan configuration
        modelBuilder.Entity<BillingPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlanName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.QuickBooksCustomerId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.Items)
                  .WithOne(e => e.BillingPlan)
                  .HasForeignKey(e => e.BillingPlanId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Invoices)
                  .WithOne(e => e.BillingPlan)
                  .HasForeignKey(e => e.BillingPlanId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.QuickBooksCustomerId);
            entity.HasIndex(e => new { e.QuickBooksCustomerId, e.IsActive });
        });

        // BillingPlanItem configuration
        modelBuilder.Entity<BillingPlanItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuickBooksItemId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.Rate).HasPrecision(18, 4);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.QuickBooksTaxCodeId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.VatRate).HasPrecision(5, 2).IsRequired();
        });

        // Invoice configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.QuickBooksInvoiceId).HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.GeneratedDate).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            entity.HasMany(e => e.Items)
                  .WithOne(e => e.Invoice)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.EmailDeliveries)
                  .WithOne(e => e.Invoice)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => e.BillingPlanId);
        });

        // InvoiceItem configuration
        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemDescription).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ItemCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.VatRate).HasPrecision(5, 2);
            entity.Property(e => e.NetAmount).HasPrecision(18, 2);
            entity.Property(e => e.VatAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);

            entity.HasIndex(e => e.InvoiceId);
        });

        // EmailDeliveryStatus configuration
        modelBuilder.Entity<EmailDeliveryStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecipientEmail).IsRequired().HasMaxLength(255);
            entity.Property(e => e.MailgunMessageId).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            entity.HasIndex(e => e.MailgunMessageId);
        });

        // QuickBooksToken configuration
        modelBuilder.Entity<QuickBooksToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RealmId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AccessToken).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.RefreshToken).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.RealmId).IsUnique();
        });
    }
}
