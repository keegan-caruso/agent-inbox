using System.Security.Cryptography;
using System.Text;

namespace AgentInbox.Security;

internal static class CapabilityTokens
{
    public static string Generate() => Guid.CreateVersion7().ToString();

    public static string Hash(string capabilityToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(capabilityToken));
        return Convert.ToHexStringLower(hash);
    }
}
