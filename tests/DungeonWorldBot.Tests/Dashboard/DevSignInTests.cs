using DungeonWorldBot.Dashboard.Services.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace DungeonWorldBot.Tests.Dashboard;

public class DevSignInTests
{
    private static WebApplicationBuilder Builder(string environment) =>
        WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment,
            Args = ["--DevSignInAs=900000000000000001"],
        });

    [Fact]
    public void InDevelopment_SignsEveryoneInAsThePlayerSet()
    {
        Assert.True(Builder(Environments.Development).AddDevSignIn());
    }

    // The shared dashboard runs as Production, so a stray setting there must never let anyone in as someone
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AnywhereElse_IsNeverOn(string environment)
    {
        Assert.False(Builder(environment).AddDevSignIn());
    }
}
