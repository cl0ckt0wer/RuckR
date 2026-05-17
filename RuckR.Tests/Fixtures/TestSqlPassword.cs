namespace RuckR.Tests.Fixtures;

internal static class TestSqlPassword
{
    /// <summary>
    /// Verifies create.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public static string Create()
    {
        var configuredPassword = Environment.GetEnvironmentVariable("RUCKR_TEST_DB_PASSWORD");

        if (!string.IsNullOrWhiteSpace(configuredPassword))
            return configuredPassword;

        return $"RuckR-{Guid.NewGuid():N}-aA1!";
    }
}


