using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LicenseAPI.Models
{
    [Table("MobileLicenses")]
    public class License
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(64)]
        public string LicensePrefix { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string LicenseKeyHash { get; set; } = string.Empty;

        [Required]
        [StringLength(64)]
        public string Salt { get; set; } = string.Empty;

        [StringLength(256)]
        public string? CompanyName { get; set; }

        [StringLength(128)]
        public string? AssignedDeviceId { get; set; }

        public string? DeviceInfo { get; set; }

        public DateTime? AssignedAt { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public LicenseStatus Status { get; set; } = LicenseStatus.Active;

        public string? Notes { get; set; }

        // Computed properties
        [NotMapped]
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        [NotMapped]
        public bool IsAssigned => !string.IsNullOrEmpty(AssignedDeviceId);
    }

    public enum LicenseStatus
    {
        Active = 1,
        Expired = 2,
        Suspended = 3,
        Revoked = 4
    }
}
