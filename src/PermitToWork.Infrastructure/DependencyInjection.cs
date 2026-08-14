using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Accounts;
using PermitToWork.Infrastructure.Identity;
using PermitToWork.Infrastructure.Persistence;
using PermitToWork.Infrastructure.Persistence.Repositories;

namespace PermitToWork.Infrastructure;

/// <summary>
/// The one place the API is allowed to learn that persistence and Identity exist.
/// Everything Infrastructure offers is registered here, so <c>Program.cs</c> stays a list
/// of capabilities rather than a list of implementations.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "PermitToWorkDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
                               ?? throw new InvalidOperationException(
                                   $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<PermitToWorkDbContext>(options => options
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(PermitToWorkDbContext).Assembly.FullName))
            // Employee.Address is an optional owned type whose columns are all nullable,
            // so EF warns that it cannot tell "no address" from "an address of nulls".
            // Here those are the same thing by design — the domain will not construct a
            // partial Address — so the warning is acknowledged and suppressed rather than
            // left to accumulate in the log where real warnings would hide behind it.
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.OptionalDependentWithoutIdentifyingPropertyWarning)));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            // No AddRoles: there are no Identity role tables. Employee.AccessRole is the
            // single source of truth and the token's role claim is issued from it.
            .AddEntityFrameworkStores<PermitToWorkDbContext>();

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<JwtTokenFactory>();

        services.AddScoped<CounterStore>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// Bearer token validation.
    /// <para>
    /// Lives in Infrastructure because this is where the signing key is read, and the
    /// signing key should be handled in exactly one assembly. <c>MapInboundClaims</c> is
    /// turned off so the claims that arrive are the claims that were issued — by default
    /// the handler rewrites <c>sub</c> and <c>role</c> into long WS-Federation URIs, which
    /// is a reliable source of "why is User.FindFirst(\"sub\") null" afternoons.
    /// </para>
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(section);

        var options = section.Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:SigningKey is not configured. " +
                "Set it with: dotnet user-secrets set \"Jwt:SigningKey\" \"<a long random string>\" -p src/PermitToWork.Api");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role"
                };
            });

        return services;
    }
}
