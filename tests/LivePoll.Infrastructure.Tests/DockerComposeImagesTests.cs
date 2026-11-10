namespace LivePoll.Infrastructure.Tests;

public sealed class DockerComposeImagesTests
{
    [Fact]
    public void Ruvio_Image_IsReadFromDockerComposeYml()
    {
        var image = DockerComposeImages.Ruvio;

        Assert.False(string.IsNullOrWhiteSpace(image));
        Assert.Contains(':', image);
    }
}