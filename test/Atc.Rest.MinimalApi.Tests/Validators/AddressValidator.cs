namespace Atc.Rest.MinimalApi.Tests.Validators;

public class AddressValidator<T> : AbstractValidator<T>
    where T : IAddress
{
    public AddressValidator()
    {
        RuleFor(x => x.CountryCodeA3)
            .NotEmpty()
            .Length(3)
            .WithMessage("CountryCodeA3 must be 3 characters long.");

        RuleFor(x => x.Place)
            .NotEmpty()
            .WithMessage("Place is required.");

        RuleFor(x => x.Street)
            .NotEmpty()
            .WithMessage("Street is required.");

        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .WithMessage("PostalCode is required.");

        RuleFor(x => x.City)
            .NotEmpty()
            .WithMessage("City is required.");
    }
}