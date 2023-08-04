namespace Atc.Rest.MinimalApi.Tests.Validators;

public sealed class CreateLocationRequestWithJsonPropertyNamesValidator : AbstractValidator<CreateLocationRequestWithJsonPropertyNames>
{
    public CreateLocationRequestWithJsonPropertyNamesValidator()
    {
        RuleFor(x => x.Address).SetValidator(new AddressValidator<AddressWithJsonPropertyNames>());

        RuleFor(x => x.Telephone)
            .NotEmpty()
            .WithMessage("Telephone is required.");
    }
}