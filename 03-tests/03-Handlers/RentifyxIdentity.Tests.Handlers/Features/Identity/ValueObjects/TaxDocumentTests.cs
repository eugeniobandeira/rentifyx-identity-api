using FluentAssertions;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.ValueObjects;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity.ValueObjects;

public sealed class TaxDocumentTests
{
    [Fact]
    public void Create_ValidCpf_WithFormatting_Succeeds()
    {
        TaxDocument taxDocument = TaxDocument.Create("529.982.247-25");

        taxDocument.DocumentType.Should().Be(TaxDocumentType.Cpf);
        taxDocument.RawValue.Should().Be("52998224725");
    }

    [Fact]
    public void Create_ValidCnpj_WithFormatting_Succeeds()
    {
        TaxDocument taxDocument = TaxDocument.Create("11.222.333/0001-81");

        taxDocument.DocumentType.Should().Be(TaxDocumentType.Cnpj);
        taxDocument.RawValue.Should().Be("11222333000181");
    }

    // Alphanumeric CNPJ (IN RFB 2229/2024): root "1A2B3C4D", branch "0001", check digits "47"
    [Fact]
    public void Create_ValidAlphanumericCnpj_Succeeds()
    {
        TaxDocument taxDocument = TaxDocument.Create("1A.2B3.C4D/0001-47");

        taxDocument.DocumentType.Should().Be(TaxDocumentType.Cnpj);
        taxDocument.RawValue.Should().Be("1A2B3C4D000147");
    }

    [Fact]
    public void Create_ValidAlphanumericCnpj_LowercaseInput_NormalizesToUppercase()
    {
        TaxDocument taxDocument = TaxDocument.Create("1a.2b3.c4d/0001-47");

        taxDocument.RawValue.Should().Be("1A2B3C4D000147");
    }

    [Fact]
    public void Create_WrongLength_Throws()
    {
        Action act = () => TaxDocument.Create("12345");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_Cpf_ReturnsMasked()
    {
        TaxDocument taxDocument = TaxDocument.Create("529.982.247-25");

        taxDocument.ToString().Should().Be("***.***.***-**");
    }

    [Fact]
    public void ToString_Cnpj_ReturnsMasked()
    {
        TaxDocument taxDocument = TaxDocument.Create("11.222.333/0001-81");

        taxDocument.ToString().Should().Be("**.***.***/****-**");
    }
}
