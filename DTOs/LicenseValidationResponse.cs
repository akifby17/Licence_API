namespace LicenseAPI.DTOs
{
    public class LicenseValidationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public LicenseInfo? LicenseInfo { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? DaysRemaining { get; set; }
        public bool IsDeviceAssigned { get; set; }
        public DateTime? AssignedAt { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class LicenseInfo
    {
        public string LicensePrefix { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AssignedDeviceId { get; set; }
        public DateTime? AssignedAt { get; set; }
    }
}
