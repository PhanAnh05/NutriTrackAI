using Microsoft.Extensions.DependencyInjection;
using NutriTrackAI.Data;

namespace NutriTrackAI.Tests;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (!args.Contains("--serve")) return;
        var previewMealPlans = args.Contains("--meal-plans");
        var url = previewMealPlans ? "http://127.0.0.1:5176" : "http://127.0.0.1:5168";
        var app = await QaApplication.StartAsync(url, preview: true);
        if (previewMealPlans)
        {
            using var scope = app.Services.CreateScope();
            await DbSeeder.SeedAsync(scope.ServiceProvider);
        }
        else if (args.Contains("--food-images"))
        {
            using var scope = app.Services.CreateScope();
            await FoodSeedData.UpsertAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }
        Console.WriteLine($"QA preview: {url} — in-memory synthetic data only.");
        await Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions.WaitForShutdownAsync(app);
    }
}
