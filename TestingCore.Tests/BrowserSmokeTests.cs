using Microsoft.Playwright.Xunit;

namespace TestingCore.Tests;

public sealed class BrowserSmokeTests : PageTest
{
    [Fact]
    public async Task PageTest_provides_an_isolated_page()
    {
        await Page.GotoAsync("data:text/html,<h1>Fixture ready</h1>");
        Assert.Equal("Fixture ready", await Page.Locator("h1").InnerTextAsync());
    }
}