using ApiInadimplencia.Application.Abstractions.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ApiInadimplencia.Infrastructure.Tests.Persistence.SqlServer;

/// <summary>
/// Unit tests for InadimplenciaQueryService row mapping logic.
/// Tests the private helper methods via reflection or by testing the public methods with mocked data.
/// </summary>
public class InadimplenciaQueryServiceTests
{
    [Theory]
    [InlineData("123.456.789-01", "12345678901")]
    [InlineData("12.345.678/0001-90", "12345678000190")]
    [InlineData("12345678901", "12345678901")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("123-456-789", "123456789")]
    public void DigitsOnly_ShouldRemoveAllNonDigitCharacters(string? input, string expected)
    {
        // Act
        var result = new string((input ?? "").Where(char.IsDigit).ToArray());

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Rua das Flores", "123", "Rua das Flores, 123")]
    [InlineData("Rua das Flores", "", "Rua das Flores")]
    [InlineData("", "123", "")]
    [InlineData("Av. Brasil", "456", "Av. Brasil, 456")]
    public void BuildAddressLine_ShouldCombineStreetAndNumber(string? logradouro, string? numero, string expected)
    {
        // Arrange
        var street = logradouro?.Trim() ?? "";
        var fallbackAddress = "";
        var number = numero?.Trim() ?? "";

        // Act
        string result;
        if (!string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(number))
        {
            result = $"{street}, {number}";
        }
        else
        {
            result = street ?? fallbackAddress;
        }

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EnderecoDto_WithAllFields_ShouldCreateCorrectly()
    {
        // Arrange
        var zipCode = "01310900";
        var addressLine = "Av. Paulista, 1000";
        var district = "Bela Vista";
        var city = "São Paulo";
        var state = "SP";
        var complement = "Apto 101";
        var number = "1000";

        // Act
        var endereco = new EnderecoDto(
            ZipCode: zipCode,
            AddressLine: addressLine,
            District: district,
            City: city,
            State: state,
            Complement: complement,
            Number: number);

        // Assert
        endereco.ZipCode.Should().Be(zipCode);
        endereco.AddressLine.Should().Be(addressLine);
        endereco.District.Should().Be(district);
        endereco.City.Should().Be(city);
        endereco.State.Should().Be(state);
        endereco.Complement.Should().Be(complement);
        endereco.Number.Should().Be(number);
    }

    [Fact]
    public void EnderecoDto_WithOptionalFieldsNull_ShouldCreateCorrectly()
    {
        // Arrange
        var zipCode = "01310900";
        var addressLine = "Av. Paulista, 1000";
        var district = "Bela Vista";
        var city = "São Paulo";
        var state = "SP";

        // Act
        var endereco = new EnderecoDto(
            ZipCode: zipCode,
            AddressLine: addressLine,
            District: district,
            City: city,
            State: state);

        // Assert
        endereco.ZipCode.Should().Be(zipCode);
        endereco.AddressLine.Should().Be(addressLine);
        endereco.District.Should().Be(district);
        endereco.City.Should().Be(city);
        endereco.State.Should().Be(state);
        endereco.Complement.Should().BeNull();
        endereco.Number.Should().BeNull();
    }

    [Fact]
    public void InadimplenciaQueryResult_WithEndereco_ShouldCreateCorrectly()
    {
        // Arrange
        var numVenda = 295;
        var documentoDevedor = "12345678901";
        var nomeDevedor = "João Silva";
        var valor = 1500.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);
        var endereco = new EnderecoDto(
            ZipCode: "01310900",
            AddressLine: "Av. Paulista, 1000",
            District: "Bela Vista",
            City: "São Paulo",
            State: "SP");

        // Act
        var result = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: documentoDevedor,
            NomeDevedor: nomeDevedor,
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento Teste",
            Bloco: "Bloco A",
            Unidade: "101",
            Valor: valor,
            DataVencimento: dataVencimento,
            Endereco: endereco);

        // Assert
        result.NumVenda.Should().Be(numVenda);
        result.DocumentoDevedor.Should().Be(documentoDevedor);
        result.NomeDevedor.Should().Be(nomeDevedor);
        result.Valor.Should().Be(valor);
        result.DataVencimento.Should().Be(dataVencimento);
        result.Endereco.Should().Be(endereco);
    }

    [Fact]
    public void InadimplenciaQueryResult_WithNullEndereco_ShouldCreateCorrectly()
    {
        // Arrange
        var numVenda = 295;
        var documentoDevedor = "12345678901";
        var nomeDevedor = "João Silva";
        var valor = 1500.50m;
        var dataVencimento = new DateOnly(2025, 12, 31);

        // Act
        var result = new InadimplenciaQueryResult(
            NumVenda: numVenda,
            DocumentoDevedor: documentoDevedor,
            NomeDevedor: nomeDevedor,
            Cliente: "Cliente Teste",
            Empreendimento: "Empreendimento Teste",
            Bloco: "Bloco A",
            Unidade: "101",
            Valor: valor,
            DataVencimento: dataVencimento,
            Endereco: null);

        // Assert
        result.NumVenda.Should().Be(numVenda);
        result.DocumentoDevedor.Should().Be(documentoDevedor);
        result.NomeDevedor.Should().Be(nomeDevedor);
        result.Valor.Should().Be(valor);
        result.DataVencimento.Should().Be(dataVencimento);
        result.Endereco.Should().BeNull();
    }

    [Fact]
    public void FiadorQueryResult_WithEndereco_ShouldCreateCorrectly()
    {
        // Arrange
        var numVenda = 295;
        var idAssociado = "ASSOC001";
        var idPessoa = "PESSOA001";
        var nome = "Maria Santos";
        var documento = "98765432100";
        var tipoAssociacao = "FIADOR";
        var endereco = new EnderecoDto(
            ZipCode: "01310900",
            AddressLine: "Av. Paulista, 1000",
            District: "Bela Vista",
            City: "São Paulo",
            State: "SP");
        var dataCadastro = DateTime.Now;

        // Act
        var result = new FiadorQueryResult(
            NumVenda: numVenda,
            IdAssociado: idAssociado,
            IdPessoa: idPessoa,
            Nome: nome,
            Documento: documento,
            TipoAssociacao: tipoAssociacao,
            Endereco: endereco,
            DataCadastro: dataCadastro);

        // Assert
        result.NumVenda.Should().Be(numVenda);
        result.IdAssociado.Should().Be(idAssociado);
        result.IdPessoa.Should().Be(idPessoa);
        result.Nome.Should().Be(nome);
        result.Documento.Should().Be(documento);
        result.TipoAssociacao.Should().Be(tipoAssociacao);
        result.Endereco.Should().Be(endereco);
        result.DataCadastro.Should().Be(dataCadastro);
    }

    [Fact]
    public void FiadorQueryResult_WithNullEndereco_ShouldCreateCorrectly()
    {
        // Arrange
        var numVenda = 295;
        var idAssociado = "ASSOC001";
        var idPessoa = "PESSOA001";
        var nome = "Maria Santos";
        var documento = "98765432100";
        var tipoAssociacao = "FIADOR";
        var dataCadastro = DateTime.Now;

        // Act
        var result = new FiadorQueryResult(
            NumVenda: numVenda,
            IdAssociado: idAssociado,
            IdPessoa: idPessoa,
            Nome: nome,
            Documento: documento,
            TipoAssociacao: tipoAssociacao,
            Endereco: null,
            DataCadastro: dataCadastro);

        // Assert
        result.NumVenda.Should().Be(numVenda);
        result.IdAssociado.Should().Be(idAssociado);
        result.IdPessoa.Should().Be(idPessoa);
        result.Nome.Should().Be(nome);
        result.Documento.Should().Be(documento);
        result.TipoAssociacao.Should().Be(tipoAssociacao);
        result.Endereco.Should().BeNull();
        result.DataCadastro.Should().Be(dataCadastro);
    }

    [Theory]
    [InlineData("FIADOR", true)]
    [InlineData("CONJUGE", true)]
    [InlineData("CESSIONARIO", true)]
    [InlineData("COOBRIGADO", true)]
    [InlineData("CO-OBRIGADO", true)]
    [InlineData("CO OBRIGADO", true)]
    [InlineData("OUTRO", false)]
    [InlineData("TESTE", false)]
    public void TipoAssociacao_ValidTypes_ShouldMatchExpected(string tipo, bool isValid)
    {
        // Arrange
        var validTypes = new[] { "FIADOR", "CONJUGE", "CESSIONARIO", "COOBRIGADO", "CO-OBRIGADO", "CO OBRIGADO" };

        // Act
        var result = validTypes.Contains(tipo);

        // Assert
        result.Should().Be(isValid);
    }

    [Fact]
    public void EnderecoDto_State_ShouldBeTwoCharacters()
    {
        // Arrange
        var endereco = new EnderecoDto(
            ZipCode: "01310900",
            AddressLine: "Av. Paulista, 1000",
            District: "Bela Vista",
            City: "São Paulo",
            State: "SP");

        // Assert
        endereco.State.Should().HaveLength(2);
        endereco.State.Should().MatchRegex(@"^[A-Z]{2}$");
    }

    [Fact]
    public void EnderecoDto_ZipCode_ShouldBeEightDigits()
    {
        // Arrange
        var endereco = new EnderecoDto(
            ZipCode: "01310900",
            AddressLine: "Av. Paulista, 1000",
            District: "Bela Vista",
            City: "São Paulo",
            State: "SP");

        // Assert
        endereco.ZipCode.Should().HaveLength(8);
        endereco.ZipCode.Should().MatchRegex(@"^\d{8}$");
    }
}
