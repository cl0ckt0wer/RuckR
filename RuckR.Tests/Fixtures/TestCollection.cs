namespace RuckR.Tests.Fixtures;

    /// <summary>
    /// Provides access to :.
    /// </summary>
[Xunit.CollectionDefinition(nameof(TestCollection), DisableParallelization = true)]
public class TestCollection : Xunit.ICollectionFixture<CustomWebApplicationFactory>
{
}


