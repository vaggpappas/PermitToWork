using Microsoft.Extensions.DependencyInjection;
using PermitToWork.Application.Employees;
using PermitToWork.Application.Teams;

namespace PermitToWork.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ITeamService, TeamService>();

        return services;
    }
}
