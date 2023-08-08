namespace Demo.Domain.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder InitializeDatabase(
        this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        var serviceProvider = serviceScope.ServiceProvider;
        var options = serviceProvider.GetRequiredService<DbContextOptions<DemoDbContext>>();
        using var context = new DemoDbContext(options);

        if (context.Users.Any())
        {
            return app;
        }

        var users = GenerateUsersData();

        context.Users.Add(ConstructWellKnownUser());

        context.Users.AddRange(users);

        context.SaveChanges();

        return app;
    }

    private static UserEntity ConstructWellKnownUser()
        => new Faker<UserEntity>()
            .CustomInstantiator(f => new UserEntity
            {
                Id = Guid.Parse(Constants.WellKnownUserId),
                Gender = f.PickRandom<GenderType>(),
                FirstName = "James",
                LastName = "Bond",
                Email = Constants.WellKnownUserEmail,
                Telephone = f.Phone.PhoneNumber(),
                HomePage = new Uri(f.Internet.Url()),
                HomeAddress = new AddressEntity
                {
                    StreetName = f.Address.StreetName(),
                    StreetNumber = f.Address.BuildingNumber(),
                    PostalCode = f.Address.ZipCode(),
                    CityName = f.Address.City(),
                    Country = new CountryEntity
                    {
                        Name = f.Address.Country(),
                        Alpha2Code = "DK",
                        Alpha3Code = "DNK",
                    },
                },
                WorkAddress = new AddressEntity
                {
                    StreetName = f.Address.StreetName(),
                    StreetNumber = f.Address.BuildingNumber(),
                    PostalCode = f.Address.ZipCode(),
                    CityName = f.Address.City(),
                    Country = new CountryEntity
                    {
                        Name = f.Address.Country(),
                        Alpha2Code = "DK",
                        Alpha3Code = "DNK",
                    },
                },
            })
            .Generate(1)[0];

    private static IEnumerable<UserEntity> GenerateUsersData()
        => new Faker<UserEntity>()
            .CustomInstantiator(f => new UserEntity
            {
                Id = Guid.NewGuid(),
                Gender = f.PickRandom<GenderType>(),
                FirstName = f.Person.FirstName,
                LastName = f.Person.LastName,
                Email = f.Internet.Email(f.Person.FirstName, f.Person.LastName),
                Telephone = f.Phone.PhoneNumber(),
                HomePage = new Uri(f.Internet.Url()),
                HomeAddress = new AddressEntity
                {
                    StreetName = f.Address.StreetName(),
                    StreetNumber = f.Address.BuildingNumber(),
                    PostalCode = f.Address.ZipCode(),
                    CityName = f.Address.City(),
                    Country = new CountryEntity
                    {
                        Name = f.Address.Country(),
                        Alpha2Code = "DK",
                        Alpha3Code = "DNK",
                    },
                },
                WorkAddress = new AddressEntity
                {
                    StreetName = f.Address.StreetName(),
                    StreetNumber = f.Address.BuildingNumber(),
                    PostalCode = f.Address.ZipCode(),
                    CityName = f.Address.City(),
                    Country = new CountryEntity
                    {
                        Name = f.Address.Country(),
                        Alpha2Code = "DK",
                        Alpha3Code = "DNK",
                    },
                },
            })
            .Generate(10);
}