using FluentValidation;

namespace DASHBOARD.Application.Capacity.Commands.UpsertCapacityMember;

/// <summary>Validates <see cref="UpsertCapacityMemberCommand"/> inputs.</summary>
internal sealed class UpsertCapacityMemberCommandValidator : AbstractValidator<UpsertCapacityMemberCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpsertCapacityMemberCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.SprintId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.HoursPerDay).InclusiveBetween(0, 12);
        RuleFor(x => x.OvertimeHoursPerDay).InclusiveBetween(0, 6);
    }
}
