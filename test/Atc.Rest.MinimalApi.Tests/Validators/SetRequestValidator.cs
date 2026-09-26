namespace Atc.Rest.MinimalApi.Tests.Validators;

public sealed class SetRequestValidator : AbstractValidator<SetRequest>
{
    public SetRequestValidator()
        => RuleFor(x => x.FirstName).NotEmpty();
}