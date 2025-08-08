/*
 * Copyright (c) HeyBaldur 2025
 *
 * License Agreement for HeyBaldur/GoConnect.dev
 *
 * 1. Grant of License
 * This software, including the HeyBaldur/GoConnect.dev and all associated documentation 
 * ("Software"), is licensed solely for development and testing purposes under the terms 
 * of this License Agreement ("Agreement"). This Software is provided to you ("Licensee") 
 * by HeyBaldur ("Licensor") under the following conditions:
 * 
 * - The Software may only be used in non-production environments for the purpose of 
 *   developing and testing applications that interact with the Software.
 * - Use of the Software in production environments or for any commercial purpose 
 *   requires a valid commercial license issued by HeyBaldur.
 * - The Software is provided "as is" without any warranties, express or implied.
 *
 * 2. Restrictions on Use
 * Licensee shall not:
 * - Use the Software in a production environment without obtaining a valid commercial 
 *   license from HeyBaldur.
 * - Modify, reverse-engineer, decompile, or create derivative works of the Software 
 *   without prior written consent from HeyBaldur.
 * - Redistribute, sublicense, rent, lease, or otherwise transfer the Software to any 
 *   third party.
 * - Use the Software to process personal data in violation of the General Data Protection 
 *   Regulation (GDPR) (EU) 2016/679 or other applicable data protection laws.
 *
 * 3. GDPR Compliance
 * The Software may process personal data as defined under GDPR. Licensee agrees to:
 * - Use the Software in compliance with GDPR and all applicable data protection laws, 
 *   including but not limited to lawful processing, data minimization, and purpose limitation.
 * - Implement appropriate technical and organizational measures to ensure the security 
 *   of personal data processed via the Software, as required by Article 32 of GDPR.
 * - Notify HeyBaldur immediately in the event of a personal data breach 
 *   involving the Software, as required by Article 33 of GDPR.
 * - If acting as a data controller, ensure a lawful basis for processing personal data 
 *   and provide necessary transparency to data subjects under Articles 13 and 14 of GDPR.
 * - If acting as a data processor, comply with all obligations under Article 28 of GDPR, 
 *   including entering into a data processing agreement with HeyBaldur if required.
 *
 * 4. Intellectual Property
 * The Software, including all code, documentation, and associated intellectual property, 
 * is owned by HeyBaldur. This Agreement does not transfer any ownership rights 
 * to Licensee. All rights not expressly granted herein are reserved by HeyBaldur.
 *
 * 5. Disclaimer of Warranties
 * The Software is provided "AS IS" without warranties of any kind, whether express, implied, 
 * statutory, or otherwise, including but not limited to warranties of merchantability, 
 * fitness for a particular purpose, or non-infringement. HeyBaldur does not 
 * warrant that the Software will meet Licensee’s requirements or operate without interruption 
 * or errors.
 *
 * 6. Limitation of Liability
 * To the maximum extent permitted by law, HeyBaldur shall not be liable for any 
 * direct, indirect, incidental, consequential, or punitive damages arising out of or related 
 * to the use of the Software, including but not limited to data loss, breaches of GDPR, or 
 * other regulatory non-compliance. Licensee assumes full responsibility for ensuring GDPR 
 * compliance when using the Software.
 *
 * 7. Termination
 * This Agreement is effective until terminated. HeyBaldur may terminate this 
 * Agreement immediately if Licensee breaches any of its terms. Upon termination, Licensee 
 * must cease all use of the Software and destroy all copies in its possession or control.
 *
 * 8. Unauthorized Use
 * Unauthorized use of the Software in production environments, redistribution, or 
 * modification without a valid license is strictly prohibited and may result in legal action, 
 * including but not limited to claims for damages, injunctive relief, and recovery of legal 
 * fees.
 *
 * 9. Governing Law and Jurisdiction
 * This Agreement shall be governed by and construed in accordance with the laws of 
 * [Your Jurisdiction, e.g., Ireland]. Any disputes arising under this Agreement shall be 
 * subject to the exclusive jurisdiction of the courts of [Your Jurisdiction].
 *
 * 10. Contact Information
 * For inquiries regarding commercial licensing, GDPR compliance, or other matters related 
 * to this Agreement, please contact:
 * By using the Software, Licensee acknowledges that they have read, understood, and agree 
 * to be bound by the terms and conditions of this Agreement.
 */

using GdprApi.AuthHelpers;
using GdprApi.LicenseHelpers;
using GdprConfigurations;
using GdprServices.Audience;
using GdprServices.AuditLogs;
using GdprServices.Auth;
using GdprServices.DataExporter;
using GdprServices.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Repositories;
using Repositories.Interfaces;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true; // Opcional, pero recomendado
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GdprApi",
        Version = "v1",
        Description = $"GDPR-compliant interface for managing tenant operations in a multi-tenant system. " +
        $"It supports tenant registration, secure JWT-based authentication, and data retrieval with strict authorization controls. " +
        $"Key features include pseudonymization of sensitive data (email, full name) using SHA-256, " +
        $"encryption of original values in a dedicated PseudonymMappings collection, " +
        $"and comprehensive audit logging of all operations to ensure traceability and compliance " +
        $"with GDPR Articles 7, 25, 30, and 32. The API ensures data minimization, explicit consent capture, " +
        $"and secure access restricted to authorized tenants via tenant ID and client ID validation.\r\n\r\n"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection(nameof(MongoDbSettings)));

builder.Services.AddSingleton<IMongoDbSettings>(sp =>
                sp.GetRequiredService<IOptions<MongoDbSettings>>().Value);

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IMongoDbSettings>();
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    var configuration = builder.Services
    .BuildServiceProvider()
    .GetRequiredService<IConfiguration>();
    var signingKey = configuration["AppSettings:Token"];

    if (string.IsNullOrEmpty(signingKey) || Encoding.ASCII.GetBytes(signingKey).Length < 32)
    {
        throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(signingKey)) // Match the key used in AuthenticateTenantAsync
    };
});

builder.Services.AddTenantPolicies();

// Services
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAuditLogs, AuditLogsService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDataFormatter, DataFormatter>();
builder.Services.AddScoped<ITenantAudience, TenantAudienceService>();

// Repositories
builder.Services.AddScoped<ITenantAudienceRepository, TenantAudienceRepository>();
builder.Services.AddScoped<IAuditLogsRepository, AuditLogsRepository>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.UseMiddleware<LicenseValidatorMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.Run();
