using RentifyxIdentity.Domain.Enums;

namespace RentifyxIdentity.Domain.ValueObjects;

public sealed class TaxDocument
{
    public string RawValue { get; }
    public TaxDocumentType DocumentType { get; }

    private TaxDocument(string rawValue, TaxDocumentType documentType)
    {
        RawValue = rawValue;
        DocumentType = documentType;
    }

    public static TaxDocument Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string raw = value
            .Replace(".", "")
            .Replace("-", "")
            .Replace("/", "")
            .ToUpperInvariant();

        return raw.Length switch
        {
            11 => new TaxDocument(raw, TaxDocumentType.Cpf),
            14 => new TaxDocument(raw, TaxDocumentType.Cnpj),
            _ => throw new ArgumentException("Invalid TaxId format.")
        };
    }

    internal static TaxDocument CreateAnonymized() =>
        new("ANONYMIZED", TaxDocumentType.Cpf);

    public override string ToString() => DocumentType switch
    {
        TaxDocumentType.Cpf => "***.***.***-**",
        TaxDocumentType.Cnpj => "**.***.***/****-**",
        _ => RawValue
    };
}
