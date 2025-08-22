# License API

Flutter uygulamaları için ASP.NET Core Web API tabanlı lisans doğrulama sistemi.

## Özellikler

### 🔐 **Lisans Doğrulama**
- Lisans prefix ve anahtar doğrulaması
- SHA256 + Salt ile güvenli hash kontrolü
- Geçerlilik tarihi kontrolü
- Lisans durumu kontrolü (Aktif, Suspended, vb.)

### 📱 **Cihaz Yönetimi**
- **Otomatik Cihaz Atama**: İlk kullanımda cihaz otomatik atanır
- **Tek Cihaz Kısıtlaması**: Bir lisans sadece bir cihazda çalışır
- **Cihaz Bilgisi Güncelleme**: Cihaz bilgileri otomatik güncellenir
- **Cihaz Değişikliği Engelleme**: Farklı cihaz erişimi reddedilir

### 📊 **API Endpoints**

#### 1. POST `/api/license/validate`
Lisans doğrulama ve cihaz yönetimi

**Request:**
```json
{
  "licenseKey": "MYAPP-A1B2-C3D4-E5F6-G7H8",
  "deviceId": "unique_device_identifier",
  "deviceInfo": "iPhone 14 Pro - iOS 16.0"
}
```

**Response (Başarılı):**
```json
{
  "success": true,
  "message": "Lisans doğrulama tamamlandı",
  "data": {
    "isValid": true,
    "message": "Lisans geçerli.",
    "licenseInfo": {
      "licensePrefix": "MYAPP",
      "companyName": "ABC Şirketi",
      "createdAt": "2025-01-20T10:00:00Z",
      "expiresAt": "2026-01-20T10:00:00Z",
      "status": "Aktif",
      "assignedDeviceId": "unique_device_identifier",
      "assignedAt": "2025-01-20T10:30:00Z"
    },
    "expiresAt": "2026-01-20T10:00:00Z",
    "daysRemaining": 365,
    "isDeviceAssigned": true,
    "assignedAt": "2025-01-20T10:30:00Z"
  },
  "timestamp": "2025-01-20T10:30:00Z"
}
```

**Response (Hata):**
```json
{
  "success": true,
  "message": "Lisans doğrulama tamamlandı",
  "data": {
    "isValid": false,
    "message": "Bu lisans başka bir cihaza atanmış. (Atanmış Cihaz: other_device_id)",
    "errorCode": "DEVICE_MISMATCH",
    "isDeviceAssigned": true,
    "assignedAt": "2025-01-15T09:00:00Z"
  },
  "timestamp": "2025-01-20T10:30:00Z"
}
```

#### 2. GET `/api/license/status/{licensePrefix}`
Lisans durumu sorgulama (cihaz ataması yapmaz)

#### 3. GET `/api/license/health`
API sağlık kontrolü

## Hata Kodları

| Kod | Açıklama |
|-----|----------|
| `LICENSE_NOT_FOUND` | Lisans bulunamadı |
| `INVALID_LICENSE_KEY` | Geçersiz lisans anahtarı |
| `LICENSE_NOT_ACTIVE` | Lisans aktif değil |
| `LICENSE_EXPIRED` | Lisans süresi dolmuş |
| `DEVICE_MISMATCH` | Farklı cihaz erişimi |
| `DEVICE_ASSIGNMENT_ERROR` | Cihaz atama hatası |
| `VALIDATION_ERROR` | Genel doğrulama hatası |

## Kurulum

### 1. Veritabanı Bağlantısı
`appsettings.json` dosyasında connection string'i düzenleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your_server;Database=LicenseDB;User Id=sa;Password=your_password;..."
  }
}
```

### 2. Paketleri Yükle
```bash
dotnet restore
```

### 3. Çalıştır
```bash
dotnet run
```

### 4. Swagger'a Erişim
Uygulama çalıştıktan sonra: `https://localhost:7270`

## Flutter Entegrasyonu

### Dart HTTP Client Örneği

```dart
import 'dart:convert';
import 'package:http/http.dart' as http;

class LicenseService {
  static const String baseUrl = 'https://your-api-domain.com/api';
  
  static Future<LicenseValidationResult> validateLicense({
    required String licensePrefix,
    required String licenseKey,
    required String deviceId,
    String? deviceInfo,
    String? platform,
    String? appVersion,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/license/validate'),
      headers: {
        'Content-Type': 'application/json',
      },
      body: jsonEncode({
        'licensePrefix': licensePrefix,
        'licenseKey': licenseKey,
        'deviceId': deviceId,
        'deviceInfo': deviceInfo,
        'platform': platform,
        'appVersion': appVersion,
      }),
    );

    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      return LicenseValidationResult.fromJson(data['data']);
    } else {
      throw Exception('License validation failed');
    }
  }
}

class LicenseValidationResult {
  final bool isValid;
  final String message;
  final String? errorCode;
  final LicenseInfo? licenseInfo;
  
  LicenseValidationResult({
    required this.isValid,
    required this.message,
    this.errorCode,
    this.licenseInfo,
  });
  
  factory LicenseValidationResult.fromJson(Map<String, dynamic> json) {
    return LicenseValidationResult(
      isValid: json['isValid'],
      message: json['message'],
      errorCode: json['errorCode'],
      licenseInfo: json['licenseInfo'] != null 
          ? LicenseInfo.fromJson(json['licenseInfo']) 
          : null,
    );
  }
}
```

### Cihaz ID Alma

```dart
import 'package:device_info_plus/device_info_plus.dart';
import 'dart:io';

Future<String> getDeviceId() async {
  final deviceInfo = DeviceInfoPlugin();
  
  if (Platform.isAndroid) {
    final androidInfo = await deviceInfo.androidInfo;
    return androidInfo.id; // Android ID
  } else if (Platform.isIOS) {
    final iosInfo = await deviceInfo.iosInfo;
    return iosInfo.identifierForVendor ?? '';
  }
  
  return 'unknown';
}

Future<String> getDeviceInfo() async {
  final deviceInfo = DeviceInfoPlugin();
  
  if (Platform.isAndroid) {
    final androidInfo = await deviceInfo.androidInfo;
    return '${androidInfo.brand} ${androidInfo.model} - Android ${androidInfo.version.release}';
  } else if (Platform.isIOS) {
    final iosInfo = await deviceInfo.iosInfo;
    return '${iosInfo.name} - iOS ${iosInfo.systemVersion}';
  }
  
  return 'Unknown Device';
}
```

## Güvenlik

- ✅ SQL Injection koruması (Entity Framework)
- ✅ Model validasyonu
- ✅ HTTPS zorunluluğu
- ✅ CORS yapılandırması
- ✅ Hata detaylarının gizlenmesi
- ✅ Logging ve monitoring

## Deployment

### IIS Deployment
1. `dotnet publish -c Release -o ./publish`
2. IIS'e application pool oluşturun (.NET Core)
3. Publish klasörünü IIS'e deploy edin
4. Connection string'i production değerleriyle güncelleyin

### Docker Deployment
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY ./publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "LicenseAPI.dll"]
```

## Monitoring

API otomatik olarak şu bilgileri loglar:
- Lisans doğrulama istekleri
- Cihaz atama işlemleri
- Hata detayları
- Performans metrikleri

Loglar console ve debug output'ta görüntülenir.
