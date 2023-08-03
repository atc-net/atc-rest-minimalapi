namespace Atc.Rest.MinimalApi.Tests.Validators;

public sealed class CreateLocationRequestWithoutJsonPropertyNamesValidator : AbstractValidator<CreateLocationRequestWithoutJsonPropertyNames>
{
    public CreateLocationRequestWithoutJsonPropertyNamesValidator()
    {
        RuleFor(x => x.Address).SetValidator(new AddressValidator<AddressWithoutJsonPropertyNames>());

        RuleFor(x => x.Telephone)
            .NotEmpty()
            .WithMessage("Telephone is required.");
    }
}