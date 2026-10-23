using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;

namespace Quizware.Application.Reporting;

/// <summary>P13-05: the program's audit log, newest first, filterable by
/// entity (type and/or id), action, actor (user id or user name) and a UTC
/// date range.</summary>
public sealed record GetAuditLogQuery(AuditLogFilter Filter) : IRequest<AuditLogPage>;

public sealed class GetAuditLogQueryValidator : AbstractValidator<GetAuditLogQuery>
{
    public const int MaxPageSize = 500;

    public GetAuditLogQueryValidator()
    {
        RuleFor(x => x.Filter.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(x => x.Filter.Action)
            .Must(a => a is null || a is "Insert" or "Update" or "Delete")
            .WithMessage("Action must be Insert, Update or Delete.");
        RuleFor(x => x.Filter)
            .Must(f => f.FromUtc is null || f.ToUtc is null || f.FromUtc <= f.ToUtc)
            .WithMessage("'from' must not be after 'to'.");
    }
}

public sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, AuditLogPage>
{
    private readonly IAuditLogReader _reader;

    public GetAuditLogQueryHandler(IAuditLogReader reader)
    {
        _reader = reader;
    }

    public Task<AuditLogPage> Handle(GetAuditLogQuery request, CancellationToken cancellationToken) =>
        _reader.QueryAsync(request.Filter, cancellationToken);
}
