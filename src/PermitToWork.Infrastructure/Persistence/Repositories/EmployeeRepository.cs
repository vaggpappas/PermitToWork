using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Application.Employees;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.ValueObjects;

namespace PermitToWork.Infrastructure.Persistence.Repositories;

internal sealed class EmployeeRepository(PermitToWorkDbContext context) : IEmployeeRepository
{
    public Task<Employee?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Employees
            .Include(e => e.Certifications)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<PagedResult<EmployeeSummaryDto>> SearchAsync(
        EmployeeSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = context.Employees.AsNoTracking();

        if (request.CompanyId is { } companyId)
        {
            query = query.Where(e => e.CompanyId == companyId);
        }

        if (request.TradeId is { } tradeId)
        {
            query = query.Where(e => e.TradeId == tradeId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        var term = request.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term}%";

            // The badge number is stored through a value converter, so LIKE cannot reach
            // inside it — the column is an EmployeeNumber as far as the model is concerned.
            // Equality does translate, so a search term that happens to be a well-formed
            // number is matched exactly, and one that isn't skips that clause entirely.
            var number = EmployeeNumber.TryCreate(term);

            query = number is null
                ? query.Where(e => EF.Functions.Like(e.Name.First, pattern)
                                   || EF.Functions.Like(e.Name.Last, pattern)
                                   || EF.Functions.Like(e.Contact.Email, pattern))
                : query.Where(e => EF.Functions.Like(e.Name.First, pattern)
                                   || EF.Functions.Like(e.Name.Last, pattern)
                                   || EF.Functions.Like(e.Contact.Email, pattern)
                                   || e.Number == number);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<EmployeeSummaryDto>.Empty(request.PageSize);
        }

        var page = query
            .OrderBy(e => e.Name.Last)
            .ThenBy(e => e.Name.First)
            .Skip(request.Skip)
            .Take(request.PageSize);

        // Joined rather than navigated: Employee holds a TradeId, not a Trade, because an
        // aggregate references other aggregates by identity. The join happens in SQL and
        // costs one query, where lazy navigation would cost one per row.
        var items = await (
                from e in page
                join t in context.Trades on e.TradeId equals t.Id
                join c in context.Companies on e.CompanyId equals c.Id
                select new EmployeeSummaryDto(
                    e.Id,
                    e.Number.Value,
                    e.Name.First,
                    e.Name.Last,
                    e.Contact.Email,
                    e.JobTitle,
                    t.Name,
                    c.Name,
                    e.Status,
                    e.UserId != null))
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeSummaryDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<EmployeeDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await (
                from e in context.Employees.AsNoTracking()
                join t in context.Trades on e.TradeId equals t.Id
                join c in context.Companies on e.CompanyId equals c.Id
                where e.Id == id
                select new
                {
                    Employee = e,
                    TradeName = t.Name,
                    CompanyName = c.Name,
                    // Left join: the manager may be missing, or may be at another company
                    // and hidden by the scope filter. Either way the name is simply absent.
                    ManagerName = context.Employees
                        .Where(m => m.Id == e.ManagerId)
                        .Select(m => m.Name.First + " " + m.Name.Last)
                        .FirstOrDefault()
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var certifications = await (
                from cert in context.Set<Certification>().AsNoTracking()
                join type in context.CertificationTypes on cert.CertificationTypeId equals type.Id
                where cert.EmployeeId == id
                orderby cert.ExpiresOn descending
                select new CertificationDto(
                    cert.Id,
                    cert.CertificationTypeId,
                    type.Name,
                    cert.IssuedBy,
                    cert.IssuedOn,
                    cert.ExpiresOn,
                    cert.ReferenceNumber))
            .ToListAsync(cancellationToken);

        var employee = row.Employee;

        return new EmployeeDetailDto(
            employee.Id,
            employee.Number.Value,
            employee.Name.First,
            employee.Name.Last,
            employee.Contact.Email,
            employee.Contact.PhoneNumber,
            employee.Address is { } address
                ? new AddressDto(address.Street, address.City, address.PostalCode, address.Country)
                : null,
            employee.DateOfBirth,
            employee.JobTitle,
            employee.TradeId,
            row.TradeName,
            employee.CompanyId,
            row.CompanyName,
            employee.ManagerId,
            row.ManagerName,
            employee.HireDate,
            employee.Status,
            employee.UserId is not null,
            certifications);
    }

    // Uniqueness is a property of the whole table, not of what the caller may see.
    // Without IgnoreQueryFilters a contractor could be told a badge number is free when it
    // belongs to another company, and the insert would then fail on the unique index with
    // a 500 instead of a clear message.
    public Task<bool> NumberIsTakenAsync(EmployeeNumber number, CancellationToken cancellationToken = default) =>
        context.Employees.IgnoreQueryFilters().AnyAsync(e => e.Number == number, cancellationToken);

    public Task<bool> EmailIsTakenAsync(
        string email,
        Guid? exceptEmployeeId = null,
        CancellationToken cancellationToken = default) =>
        context.Employees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Contact.Email == email && (exceptEmployeeId == null || e.Id != exceptEmployeeId), cancellationToken);

    public void Add(Employee employee) => context.Employees.Add(employee);
}

internal sealed class UnitOfWork(PermitToWorkDbContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
