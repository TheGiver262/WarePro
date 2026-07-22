namespace QuanLyHangHoa.Tests.Infrastructure;

public sealed class MutatingRealDatabaseFactAttributeTests
{
    [Fact]
    public void Test_is_skipped_until_explicitly_enabled()
    {
        var original = Environment.GetEnvironmentVariable(
            MutatingRealDatabaseFactAttribute.OptInEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                MutatingRealDatabaseFactAttribute.OptInEnvironmentVariable,
                null);

            var attribute = new MutatingRealDatabaseFactAttribute();

            Assert.NotNull(attribute.Skip);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                MutatingRealDatabaseFactAttribute.OptInEnvironmentVariable,
                original);
        }
    }
}