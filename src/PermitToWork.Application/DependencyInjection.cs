using Microsoft.Extensions.DependencyInjection;
using PermitToWork.Application.Employees;
using PermitToWork.Application.Permits;
using PermitToWork.Application.Teams;

namespace PermitToWork.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IPermitService, PermitService>();
        services.AddScoped<IPermitExpiryService, PermitExpiryService>();
        services.AddScoped<IFacilityApproverService, FacilityApproverService>();

        return services;
    }
}
