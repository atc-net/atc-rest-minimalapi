namespace Demo.Domain.Users.Handlers;

public sealed class UpdateUserByIdHandler : IUpdateUserByIdHandler
{
    private readonly DemoDbContext dbContext;

    public UpdateUserByIdHandler(
        DemoDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Results<Ok<User>, BadRequest<string>, NotFound, Conflict<string>>> ExecuteAsync(
        UpdateUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync(
            parameters.UserId,
            cancellationToken);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var existingUserByEmail = await dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Email.Equals(parameters.Request.Email, StringComparison.OrdinalIgnoreCase) &&
                     x.Id != parameters.UserId,
                cancellationToken);

        if (existingUserByEmail is not null)
        {
            return TypedResults.Conflict($"A user already exists with the email '{parameters.Request.Email}'");
        }

        if (!IsModified(parameters.Request, user))
        {
            return TypedResults.Ok(user.Adapt<User>());
        }

        user.FirstName = parameters.Request.FirstName;
        user.LastName = parameters.Request.LastName;
        user.Gender = parameters.Request.Gender;
        user.Email = parameters.Request.Email;
        user.WorkAddress = MapAddress(parameters.Request.WorkAddress);

        var saveChangesResult = await dbContext.SaveChangesAsync(cancellationToken);

        return saveChangesResult > 0
            ? TypedResults.Ok(user.Adapt<User>())
            : TypedResults.BadRequest("Could not update user.");
    }

    private static bool IsModified(
        UpdateUserRequest request,
        UserEntity user)
        => !request.FirstName.Equals(user.FirstName, StringComparison.Ordinal) ||
           !request.LastName.Equals(user.LastName, StringComparison.Ordinal) ||
           request.Gender != user.Gender ||
           !request.Email.Equals(user.Email, StringComparison.Ordinal) ||
           IsAddressModified(request.WorkAddress, user.WorkAddress);

    private static bool IsAddressModified(
        Address? request,
        AddressEntity? entity)
    {
        if (request is null && entity is null)
        {
            return false;
        }

        if (request is null || entity is null)
        {
            return true;
        }

        return !string.Equals(request.StreetName, entity.StreetName, StringComparison.Ordinal) ||
               !string.Equals(request.StreetNumber, entity.StreetNumber, StringComparison.Ordinal) ||
               !string.Equals(request.PostalCode, entity.PostalCode, StringComparison.Ordinal) ||
               !string.Equals(request.CityName, entity.CityName, StringComparison.Ordinal) ||
               IsCountryModified(request.Country, entity.Country);
    }

    private static bool IsCountryModified(
        Country? request,
        CountryEntity? entity)
    {
        if (request is null && entity is null)
        {
            return false;
        }

        if (request is null || entity is null)
        {
            return true;
        }

        return !string.Equals(request.Name, entity.Name, StringComparison.Ordinal) ||
               !string.Equals(request.Alpha2Code, entity.Alpha2Code, StringComparison.Ordinal) ||
               !string.Equals(request.Alpha3Code, entity.Alpha3Code, StringComparison.Ordinal);
    }

    private static AddressEntity? MapAddress(Address? address)
    {
        if (address is null)
        {
            return null;
        }

        return new AddressEntity
        {
            StreetName = address.StreetName,
            StreetNumber = address.StreetNumber,
            PostalCode = address.PostalCode,
            CityName = address.CityName,
            Country = MapCountry(address.Country),
        };
    }

    private static CountryEntity? MapCountry(Country? country)
    {
        if (country is null)
        {
            return null;
        }

        return new CountryEntity
        {
            Name = country.Name,
            Alpha2Code = country.Alpha2Code,
            Alpha3Code = country.Alpha3Code,
        };
    }
}