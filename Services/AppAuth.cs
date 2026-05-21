namespace RonekaiImageFramer.Services;

public static class AppAuth
{
    private const string AccessPassword = "3556";

    public static bool Verify(string? password) =>
        string.Equals(password?.Trim(), AccessPassword, StringComparison.Ordinal);
}
