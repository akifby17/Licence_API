using System.ComponentModel.DataAnnotations;

namespace LicenseAPI.DTOs
{
    public class LicenseValidationRequest
    {
        [Required]
        [StringLength(256)]
        public string LicenseKey { get; set; } = string.Empty;

        [Required]
        [StringLength(128)]
        public string DeviceId { get; set; } = string.Empty;

        [StringLength(500)]
        public string? DeviceInfo { get; set; }
    }
}
