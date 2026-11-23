using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace NutriTrackAI.Services;

public static class FoodImageUrl
{
    public static string? Resolve(string name, string? imageUrl, IFileProvider webRoot)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl)) return imageUrl;
        var nameBytes = Encoding.UTF8.GetBytes(name.Trim().Normalize().ToLowerInvariant());
        var hash = Convert.ToHexString(SHA256.HashData(nameBytes)).ToLowerInvariant()[..16];
        var path = $"uploads/foods/generated/food-{hash}.png";
        return webRoot.GetFileInfo(path).Exists ? "/" + path : null;
    }
}
