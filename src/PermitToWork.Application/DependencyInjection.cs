using Microsoft.Extensions.DependencyInjection;
using PermitToWork.Application.Employees;

namespace PermitToWork.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeService, EmployeeService>();

        return services;
    }
}
