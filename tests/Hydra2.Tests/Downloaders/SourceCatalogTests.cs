using Hydra2.Downloaders;

namespace Hydra2.Tests.Downloaders;

public class SourceCatalogTests
{
    [Fact]
    public void All_sources_have_unique_DownLoadType()
    {
        var ids = SourceCatalog.All.Select(s => s.DownLoadType).ToArray();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_sources_have_unique_name()
    {
        var names = SourceCatalog.All.Select(s => s.Name).ToArray();
        names.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(1, "chmi")]
    [InlineData(2, "pvl")]
    [InlineData(3, "pvlNadrze")]
    [InlineData(4, "plaNadrze")]
    [InlineData(5, "pmoNadrze")]
    [InlineData(6, "pmoToky")]
    public void FindByDownLoadType_returns_expected_source(int downLoadType, string expectedName)
    {
        SourceCatalog.FindByDownLoadType(downLoadType)!.Name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData("chmi")]
    [InlineData("CHMI")]
    [InlineData("Chmi")]
    public void FindByName_is_case_insensitive(string name)
    {
        SourceCatalog.FindByName(name).Should().NotBeNull();
    }

    [Fact]
    public void FindByDownLoadType_returns_null_for_unknown()
    {
        SourceCatalog.FindByDownLoadType(999).Should().BeNull();
    }

    [Fact]
    public void NameFor_returns_unknown_marker_for_invalid_type()
    {
        SourceCatalog.NameFor(42).Should().Be("unknown-42");
    }
}
