namespace RuckR.Tests.Fixtures;

[Xunit.CollectionDefinition(nameof(TestCollection), DisableParallelization = true)]
    /// <summary>
    /// Provides access to :.
    /// </summary>
public class TestCollection : Xunit.ICollectionFixture<CustomWebApplicationFactory>
{
}


