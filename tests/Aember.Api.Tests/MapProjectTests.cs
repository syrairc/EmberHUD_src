namespace Aember.Api.Tests;

public class MapProjectTests
{
    [Fact]
    public void WorldXYBecomesMapXYWithoutMirroring() =>
        Assert.Equal(new Vector2(3, 4), Map.Project(new Vector3(3, 4, 9)));

    [Fact]
    public void HeightIsIgnored() =>
        Assert.Equal(Map.Project(new Vector3(1, 2, 0)), Map.Project(new Vector3(1, 2, 500)));
}
