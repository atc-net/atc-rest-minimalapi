namespace Atc.Rest.MinimalApi.Tests.Validators;

public sealed class ParameterAttributeOnlyParametersValidator : AbstractValidator<ParameterAttributeOnlyParameters>
{
    public ParameterAttributeOnlyParametersValidator()
        => RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
}