using Microsoft.Data.SqlClient;
using NutriTrackAI.Data;
using Xunit;

namespace NutriTrackAI.Tests;

public class LocalDbConnectionTests
{
    [Fact] public async Task StoppedInstanceIsStartedAndItsFreshPipeIsUsed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var calls = new List<string>();
        var result = new SqlConnectionStringBuilder(await LocalDbDevelopmentConnection.ResolveAsync(
            @"Server=(localdb)\MSSQLLocalDB;Database=NutriTrackAIDb;Integrated Security=true", true,
            instance => { calls.Add("info"); return Task.FromResult<string?>(calls.Count == 1 ? "State: Stopped" : @"np:\\.\pipe\LOCALDB#NEW123\tsql\query"); },
            instance => { calls.Add("start"); return Task.CompletedTask; }));
        Assert.Equal(new[] { "info", "start", "info" }, calls);
        Assert.Equal(@"np:\\.\pipe\LOCALDB#NEW123\tsql\query", result.DataSource);
        Assert.Equal("NutriTrackAIDb", result.InitialCatalog);
    }

    [Fact] public async Task StartupFailureIsReportedInsteadOfReturningBrokenAutoStartConnection()
    {
        if (!OperatingSystem.IsWindows()) return;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => LocalDbDevelopmentConnection.ResolveAsync(
            @"Server=(localdb)\MSSQLLocalDB;Database=NutriTrackAIDb;Integrated Security=true", true,
            _ => Task.FromResult<string?>("State: Stopped"), _ => throw new InvalidOperationException("LocalDB startup failed")));
        Assert.Equal("LocalDB startup failed", error.Message);
    }

    [Fact] public async Task MissingPipeAfterStartupIsReported()
    {
        if (!OperatingSystem.IsWindows()) return;
        await Assert.ThrowsAsync<InvalidOperationException>(() => LocalDbDevelopmentConnection.ResolveAsync(
            @"Server=(localdb)\MSSQLLocalDB;Database=NutriTrackAIDb;Integrated Security=true", true,
            _ => Task.FromResult<string?>("State: Stopped"), _ => Task.CompletedTask));
    }
    [Fact] public void PipeIsReadWithoutDependingOnLocalizedLabels()
        => Assert.Equal(@"np:\\.\pipe\LOCALDB#ABC123\tsql\query",
            LocalDbDevelopmentConnection.ParsePipe("Instance pipe name: np:\\\\.\\pipe\\LOCALDB#ABC123\\tsql\\query\r\n"));

    [Theory] [InlineData(null)] [InlineData("")] [InlineData("State: Stopped")]
    [InlineData(@"np:\\remote\pipe\LOCALDB#ABC\tsql\query")]
    public void MissingOrRemotePipeIsNotAccepted(string? output) => Assert.Null(LocalDbDevelopmentConnection.ParsePipe(output));

    [Fact] public async Task ProductionConnectionIsUntouched()
    {
        const string original = @"Server=(localdb)\MSSQLLocalDB;Database=NutriTrackAIDb;Integrated Security=true";
        Assert.Equal(original, await LocalDbDevelopmentConnection.ResolveAsync(original, false,
            _ => throw new Exception("Must not inspect LocalDB in production")));
    }

    [Fact] public async Task NonLocalDbConnectionIsUntouched()
    {
        const string original = "Server=localhost;Database=NutriTrackAIDb;Integrated Security=true";
        Assert.Equal(original, await LocalDbDevelopmentConnection.ResolveAsync(original, true,
            _ => throw new Exception("Must not inspect other SQL servers")));
    }

    [Fact] public async Task DynamicPipePreservesDatabaseAndAuthentication()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string original = @"Server=(localdb)\MSSQLLocalDB;Database=NutriTrackAIDb;Integrated Security=true;TrustServerCertificate=true";
        var result = new SqlConnectionStringBuilder(await LocalDbDevelopmentConnection.ResolveAsync(original, true,
            instance => { Assert.Equal("MSSQLLocalDB", instance); return Task.FromResult<string?>(@"np:\\.\pipe\LOCALDB#123ABC\tsql\query"); }));
        Assert.Equal(@"np:\\.\pipe\LOCALDB#123ABC\tsql\query", result.DataSource);
        Assert.Equal("NutriTrackAIDb", result.InitialCatalog);
        Assert.True(result.IntegratedSecurity);
        Assert.True(result.TrustServerCertificate);
    }
}
