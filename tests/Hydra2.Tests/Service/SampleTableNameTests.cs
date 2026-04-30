using Hydra2.Service;

namespace Hydra2.Tests.Service;

public class SampleTableNameTests
{
    [Theory]
    [InlineData(0,   "Sample-000")]
    [InlineData(1,   "Sample-001")]
    [InlineData(64,  "Sample-064")]
    [InlineData(259, "Sample-259")]
    [InlineData(650, "Sample-650")]
    [InlineData(999, "Sample-999")]
    public void ForStation_pads_to_three_digits(int id, string expected)
    {
        SampleTableName.ForStation(id).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void ForStation_rejects_invalid_ids(int id)
    {
        var act = () => SampleTableName.ForStation(id);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
