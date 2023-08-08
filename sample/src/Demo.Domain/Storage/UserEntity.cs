namespace Demo.Domain.Storage;

public class UserEntity
{
    public Guid Id { get; set; }

    public GenderType Gender { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Telephone { get; set; } = string.Empty;

    public Uri? HomePage { get; set; }

    public AddressEntity? HomeAddress { get; set; }

    public AddressEntity? WorkAddress { get; set; }

    public override string ToString()
        => $"{nameof(Id)}: {Id}, {nameof(Gender)}: {Gender}, {nameof(FirstName)}: {FirstName}, {nameof(LastName)}: {LastName}, {nameof(Email)}: {Email}, {nameof(Telephone)}: {Telephone}, {nameof(HomePage)}: {HomePage}, {nameof(HomeAddress)}: {HomeAddress}, {nameof(WorkAddress)}: {WorkAddress}";
}