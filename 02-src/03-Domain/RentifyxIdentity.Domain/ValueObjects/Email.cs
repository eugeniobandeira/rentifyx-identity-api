namespace RentifyxIdentity.Domain.ValueObjects;

public sealed class Email
{
    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com",
        "guerrillamail.com",
        "tempmail.com",
        "throwam.com",
        "yopmail.com"
    };

    public string Value { get; }

    private Email(string value)
        => Value = value;

    public static Email Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = value.Trim().ToLowerInvariant();

        int atIndex = normalized.IndexOf('@');
        if (atIndex > 0)
        {
            string domain = normalized[(atIndex + 1)..];
            if (DisposableDomains.Contains(domain))
                throw new ArgumentException("Disposable email domains are not allowed.", nameof(value));
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
