namespace RuckR.Tests.Fixtures;

internal static class TestSqlPassword
{
    public static string Create()
    {
        var configuredPassword = Environment.GetEnvironmentVariable("RUCKR_TEST_DB_PASSWORD");

        if (!string.IsNullOrWhiteSpace(configuredPassword))
            return configuredPassword;

        return $"RuckR-{Guid.NewGuid():N}-aA1!";
    }
}
