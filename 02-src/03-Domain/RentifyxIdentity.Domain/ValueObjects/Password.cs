namespace RentifyxIdentity.Domain.ValueObjects;

public sealed class Password
{
    public string HashValue { get; }

    private Password(string hash) => HashValue = hash;

    public static Password FromPlaintext(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        string hash = BCrypt.Net.BCrypt.HashPassword(plaintext, workFactor: 12);
        return new Password(hash);
    }

    public static Password FromHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        return new Password(hash);
    }

    public bool Verify(string plaintext) => BCrypt.Net.BCrypt.Verify(plaintext, HashValue);

    public override string ToString() => "[REDACTED]";
}
