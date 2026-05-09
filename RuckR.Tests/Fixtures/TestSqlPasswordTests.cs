namespace RuckR.Tests.Fixtures;

public class TestSqlPasswordTests
{
    [Fact]
    public void Create_WithoutConfiguredPassword_ReturnsSqlServerCompliantPassword()
    {
        var originalPassword = Environment.GetEnvironmentVariable("RUCKR_TEST_DB_PASSWORD");

        try
        {
            Environment.SetEnvironmentVariable("RUCKR_TEST_DB_PASSWORD", null);

            var password = TestSqlPassword.Create();

            Assert.True(password.Length >= 8);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, c => !char.IsLetterOrDigit(c));
        }
        finally
        {
            Environment.SetEnvironmentVariable("RUCKR_TEST_DB_PASSWORD", originalPassword);
        }
    }
}
