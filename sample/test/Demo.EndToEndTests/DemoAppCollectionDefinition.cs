namespace Demo.EndToEndTests;

[CollectionDefinition(Name)]
public sealed class DemoAppCollectionDefinition : ICollectionFixture<DemoAppFixture>
{
    public const string Name = "Demo app";
}