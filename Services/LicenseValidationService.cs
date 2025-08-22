using Microsoft.EntityFrameworkCore;
using LicenseAPI.Data;
using LicenseAPI.Models;
using LicenseAPI.DTOs;
using BCrypt.Net;
using System.Security.Cryptography;
using System.Text;

namespace LicenseAPI.Services
{
    public interface ILicenseValidationService
    {
        Task<LicenseValidationResponse> ValidateLicenseAsync(LicenseValidationRequest request);
        Task<License?> GetLicenseByPrefixAsync(string licensePrefix);
        bool VerifyLicenseKey(string licenseKey, string hash, string salt);
    }

    public class LicenseValidationService : ILicenseValidationService
    {
        private readonly LicenseDbContext _context;
        private readonly ILogger<LicenseValidationService> _logger;

        public LicenseValidationService(LicenseDbContext context, ILogger<LicenseValidationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<LicenseValidationResponse> ValidateLicenseAsync(LicenseValidationRequest request)
        {
            try
            {
                // Lisans anahtarından prefix'i çıkar (MYAPP-A1B2-C3D4-E5F6-G7H8 formatında)
                var licensePrefix = ExtractPrefixFromLicenseKey(request.LicenseKey);
                if (string.IsNullOrEmpty(licensePrefix))
                {
                    return new LicenseValidationResponse
                    {
                        IsValid = false,
                        Message = "Geçersiz lisans anahtarı formatı.",
                        ErrorCode = "INVALID_LICENSE_FORMAT"
                    };
                }

                _logger.LogInformation("License validation requested for prefix: {LicensePrefix}, Device: {DeviceId}", 
                    licensePrefix, request.DeviceId);

                // 1. Lisansı bul
                var license = await GetLicenseByPrefixAsync(licensePrefix);
                if (license == null)
                {
                    _logger.LogWarning("License not found: {LicensePrefix}", licensePrefix);
                    return new LicenseValidationResponse
                    {
                        IsValid = false,
                        Message = "Lisans bulunamadı.",
                        ErrorCode = "LICENSE_NOT_FOUND"
                    };
                }

                // 2. Lisans anahtarını doğrula
                if (!VerifyLicenseKey(request.LicenseKey, license.LicenseKeyHash, license.Salt))
                {
                    _logger.LogWarning("Invalid license key for prefix: {LicensePrefix}", licensePrefix);
                    return new LicenseValidationResponse
                    {
                        IsValid = false,
                        Message = "Geçersiz lisans anahtarı.",
                        ErrorCode = "INVALID_LICENSE_KEY"
                    };
                }

                // 3. Lisans durumunu kontrol et
                if (license.Status != LicenseStatus.Active)
                {
                    _logger.LogWarning("License not active. Status: {Status}, Prefix: {LicensePrefix}", 
                        license.Status, licensePrefix);
                    return new LicenseValidationResponse
                    {
                        IsValid = false,
                        Message = $"Lisans durumu: {GetStatusDisplayName(license.Status)}",
                        ErrorCode = "LICENSE_NOT_ACTIVE"
                    };
                }

                // 4. Geçerlilik süresini kontrol et
                if (license.IsExpired)
                {
                    _logger.LogWarning("License expired: {LicensePrefix}, Expired at: {ExpiresAt}", 
                        licensePrefix, license.ExpiresAt);
                    return new LicenseValidationResponse
                    {
                        IsValid = false,
                        Message = "Lisansın süresi dolmuş.",
                        ErrorCode = "LICENSE_EXPIRED",
                        ExpiresAt = license.ExpiresAt
                    };
                }

                // 5. Cihaz kontrolü ve atama
                var deviceResult = await HandleDeviceAssignmentAsync(license, request);
                if (!deviceResult.IsValid)
                {
                    return deviceResult;
                }

                // 6. Başarılı sonuç
                var daysRemaining = (int)(license.ExpiresAt - DateTime.UtcNow).TotalDays;
                
                _logger.LogInformation("License validation successful for prefix: {LicensePrefix}, Device: {DeviceId}", 
                    licensePrefix, request.DeviceId);

                return new LicenseValidationResponse
                {
                    IsValid = true,
                    Message = "Lisans geçerli.",
                    LicenseInfo = new LicenseInfo
                    {
                        LicensePrefix = license.LicensePrefix,
                        CompanyName = license.CompanyName,
                        CreatedAt = license.CreatedAt,
                        ExpiresAt = license.ExpiresAt,
                        Status = GetStatusDisplayName(license.Status),
                        AssignedDeviceId = license.AssignedDeviceId,
                        AssignedAt = license.AssignedAt
                    },
                    ExpiresAt = license.ExpiresAt,
                    DaysRemaining = daysRemaining > 0 ? daysRemaining : 0,
                    IsDeviceAssigned = license.IsAssigned,
                    AssignedAt = license.AssignedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during license validation for key: {LicenseKey}", request.LicenseKey?.Substring(0, Math.Min(10, request.LicenseKey?.Length ?? 0)) + "...");
                return new LicenseValidationResponse
                {
                    IsValid = false,
                    Message = "Lisans doğrulama sırasında hata oluştu.",
                    ErrorCode = "VALIDATION_ERROR"
                };
            }
        }

        private async Task<LicenseValidationResponse> HandleDeviceAssignmentAsync(License license, LicenseValidationRequest request)
        {
            // Eğer lisans hiç atanmamışsa, bu cihaza ata
            if (!license.IsAssigned)
            {
                license.AssignedDeviceId = request.DeviceId;
                license.DeviceInfo = request.DeviceInfo;
                license.AssignedAt = DateTime.UtcNow;

                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Device assigned to license. Prefix: {LicensePrefix}, Device: {DeviceId}", 
                        license.LicensePrefix, request.DeviceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to assign device to license: {LicensePrefix}", license.LicensePrefix);
                    return new LicenseValidationResponse
                    {
                        IsValid = false,
                        Message = "Cihaz atama sırasında hata oluştu.",
                        ErrorCode = "DEVICE_ASSIGNMENT_ERROR"
                    };
                }
            }
            // Eğer farklı bir cihaz gönderirse reddet
            else if (license.AssignedDeviceId != request.DeviceId)
            {
                _logger.LogWarning("Different device attempted to use license. Prefix: {LicensePrefix}, Assigned: {AssignedDevice}, Requested: {RequestedDevice}", 
                    license.LicensePrefix, license.AssignedDeviceId, request.DeviceId);
                
                return new LicenseValidationResponse
                {
                    IsValid = false,
                    Message = $"Bu lisans başka bir cihaza atanmış. (Atanmış Cihaz: {license.AssignedDeviceId})",
                    ErrorCode = "DEVICE_MISMATCH",
                    IsDeviceAssigned = true,
                    AssignedAt = license.AssignedAt
                };
            }
            // Aynı cihazsa devam et (cihaz bilgilerini güncelle)
            else
            {
                // Cihaz bilgilerini güncelle
                if (!string.IsNullOrEmpty(request.DeviceInfo) && license.DeviceInfo != request.DeviceInfo)
                {
                    license.DeviceInfo = request.DeviceInfo;
                    try
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Device info updated for license: {LicensePrefix}", license.LicensePrefix);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update device info for license: {LicensePrefix}", license.LicensePrefix);
                        // Bu hata kritik değil, devam et
                    }
                }
            }

            return new LicenseValidationResponse { IsValid = true };
        }

        public async Task<License?> GetLicenseByPrefixAsync(string licensePrefix)
        {
            return await _context.Licenses
                .FirstOrDefaultAsync(l => l.LicensePrefix == licensePrefix);
        }

        public bool VerifyLicenseKey(string licenseKey, string hash, string salt)
        {
            try
            {
                var keyBytes = Encoding.UTF8.GetBytes(licenseKey + salt);
                using var sha256 = SHA256.Create();
                var computedHashBytes = sha256.ComputeHash(keyBytes);
                var computedHash = Convert.ToBase64String(computedHashBytes);
                return computedHash == hash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying license key");
                return false;
            }
        }

        private static string ExtractPrefixFromLicenseKey(string licenseKey)
        {
            try
            {
                // MYAPP-A1B2-C3D4-E5F6-G7H8 formatında lisans anahtarından prefix'i çıkar
                if (string.IsNullOrEmpty(licenseKey))
                    return string.Empty;

                var parts = licenseKey.Split('-');
                if (parts.Length >= 5) // En az 5 parça olmalı (PREFIX-XXXX-XXXX-XXXX-XXXX)
                {
                    return parts[0]; // İlk parça prefix
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetStatusDisplayName(LicenseStatus status)
        {
            return status switch
            {
                LicenseStatus.Active => "Aktif",
                LicenseStatus.Expired => "Süresi Dolmuş",
                LicenseStatus.Suspended => "Askıya Alınmış",
                LicenseStatus.Revoked => "İptal Edilmiş",
                _ => "Bilinmeyen"
            };
        }
    }
}
