using Microsoft.AspNetCore.Mvc;
using LicenseAPI.DTOs;
using LicenseAPI.Services;

namespace LicenseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class LicenseController : ControllerBase
    {
        private readonly ILicenseValidationService _licenseService;
        private readonly ILogger<LicenseController> _logger;

        public LicenseController(ILicenseValidationService licenseService, ILogger<LicenseController> logger)
        {
            _licenseService = licenseService;
            _logger = logger;
        }

        /// <summary>
        /// Lisans doğrulama ve cihaz yönetimi
        /// </summary>
        /// <remarks>
        /// Lisans anahtarı formatı: PREFIX-XXXX-XXXX-XXXX-XXXX (örn: MYAPP-A1B2-C3D4-E5F6-G7H8)
        /// 
        /// Bu endpoint:
        /// - Lisans anahtarından prefix'i otomatik çıkarır
        /// - Lisans geçerliliğini kontrol eder  
        /// - Cihaz ataması yapar (ilk kullanımda)
        /// - Farklı cihaz erişimini engeller
        /// </remarks>
        /// <param name="request">Lisans doğrulama isteği (LicenseKey, DeviceId, DeviceInfo)</param>
        /// <returns>Lisans doğrulama sonucu</returns>
        /// <response code="200">Lisans doğrulama tamamlandı (başarılı veya başarısız)</response>
        /// <response code="400">Geçersiz istek</response>
        /// <response code="500">Sunucu hatası</response>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ApiResponse<LicenseValidationResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 500)]
        public async Task<ActionResult<ApiResponse<LicenseValidationResponse>>> ValidateLicense([FromBody] LicenseValidationRequest request)
        {
            _logger.LogInformation("License validation request received for key: {LicenseKey}", request.LicenseKey?.Substring(0, Math.Min(10, request.LicenseKey?.Length ?? 0)) + "...");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                
                _logger.LogWarning("Invalid license validation request: {Errors}", string.Join(", ", errors));
                
                return BadRequest(ApiResponse<LicenseValidationResponse>.ErrorResult(
                    $"Geçersiz istek: {string.Join(", ", errors)}", 
                    "INVALID_REQUEST"));
            }

            try
            {
                var result = await _licenseService.ValidateLicenseAsync(request);
                
                return Ok(ApiResponse<LicenseValidationResponse>.SuccessResult(
                    result, 
                    "Lisans doğrulama tamamlandı"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing license validation request");
                
                return StatusCode(500, ApiResponse<LicenseValidationResponse>.ErrorResult(
                    "Sunucu hatası oluştu", 
                    "INTERNAL_ERROR"));
            }
        }

        /// <summary>
        /// Lisans durumunu sorgula (sadece bilgi için, cihaz ataması yapmaz)
        /// </summary>
        /// <param name="licensePrefix">Lisans prefix'i</param>
        /// <returns>Lisans bilgileri</returns>
        [HttpGet("status/{licensePrefix}")]
        [ProducesResponseType(typeof(ApiResponse<LicenseInfo>), 200)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<ApiResponse<LicenseInfo>>> GetLicenseStatus(string licensePrefix)
        {
            _logger.LogInformation("License status request for prefix: {LicensePrefix}", licensePrefix);

            var license = await _licenseService.GetLicenseByPrefixAsync(licensePrefix);
            if (license == null)
            {
                return NotFound(ApiResponse<LicenseInfo>.ErrorResult(
                    "Lisans bulunamadı", 
                    "LICENSE_NOT_FOUND"));
            }

            var licenseInfo = new LicenseInfo
            {
                LicensePrefix = license.LicensePrefix,
                CompanyName = license.CompanyName,
                CreatedAt = license.CreatedAt,
                ExpiresAt = license.ExpiresAt,
                Status = GetStatusDisplayName(license.Status),
                AssignedDeviceId = license.AssignedDeviceId,
                AssignedAt = license.AssignedAt
            };

            return Ok(ApiResponse<LicenseInfo>.SuccessResult(
                licenseInfo, 
                "Lisans bilgileri alındı"));
        }

        /// <summary>
        /// API sağlık kontrolü
        /// </summary>
        /// <returns>API durumu</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        public ActionResult<ApiResponse> HealthCheck()
        {
            return Ok(ApiResponse.SuccessResult("API çalışıyor"));
        }

        private static string GetStatusDisplayName(Models.LicenseStatus status)
        {
            return status switch
            {
                Models.LicenseStatus.Active => "Aktif",
                Models.LicenseStatus.Expired => "Süresi Dolmuş",
                Models.LicenseStatus.Suspended => "Askıya Alınmış",
                Models.LicenseStatus.Revoked => "İptal Edilmiş",
                _ => "Bilinmeyen"
            };
        }
    }
}
