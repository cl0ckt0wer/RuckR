namespace RuckR.Tests.Fixtures;

[Xunit.CollectionDefinition(nameof(TestCollection), DisableParallelization = true)]
public class TestCollection : Xunit.ICollectionFixture<CustomWebApplicationFactory>
{
}
