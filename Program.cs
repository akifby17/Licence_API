using Microsoft.EntityFrameworkCore;
using LicenseAPI.Data;
using LicenseAPI.Services;
using System.Reflection;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Database
builder.Services.AddDbContext<LicenseDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<ILicenseValidationService, LicenseValidationService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "License API",
        Version = "v1",
        Description = "Flutter uygulamaları için lisans doğrulama API'si",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "License System",
            Email = "support@example.com"
        }
    });

    // XML Comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Custom configurations
    c.DescribeAllParametersInCamelCase();
});

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "License API v1");
        c.RoutePrefix = string.Empty; // Swagger'ı root'ta göster
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
    });
}

// CORS
app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthorization();
//app.Urls.Add("https://0.0.0.0:7270");

app.MapControllers();

// Ensure database created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("Database connection successful");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database connection failed");
    }
}

app.Logger.LogInformation("License API started successfully");

app.Run();