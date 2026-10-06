using System.Security.Cryptography;
using System.Text;
namespace MealGeniusBackend.Services.Auth;
public static class TokenHash
{
    public static string Compute(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
