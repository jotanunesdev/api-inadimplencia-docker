using ApiInadimplencia.Application.Features.SerasaPefin.Payloads;
using FluentAssertions;
using System.Text.Json;

namespace ApiInadimplencia.Application.Tests.Features.SerasaPefin.Payloads;

public class SerasaPefinPayloadBuilderTests
{
    private readonly SerasaPefinPayloadBuilder _builder;
    private readonly SerasaPefinPayloadBuilder.Options _options;

    public SerasaPefinPayloadBuilderTests()
    {
        _builder = new SerasaPefinPayloadBuilder();
        _options = new SerasaPefinPayloadBuilder.Options(
            UatEnabled: false,
            AreaInformante: "TEST_AREA",
            CategoryId: "CAT_001");
    }

    [Fact]
    public void BuildMainDebt_WithParcela_UsesParcelaDataInPayload()
    {
        // Arrange
        var parcela = new ParcelaInput(
            Valor: 1500.50m,
            Vencimento: new DateOnly(2026, 6, 15),
            Numero: 2,
            IdOrigem: "parcela-123");

        var input = new MainDebtInput(
            Parcela: parcela,
            ContractNumber: "CONTRATO-001",
            DebtorDocument: "123.456.789-01",
            DebtorName: "João Silva",
            DebtorAddress: new SerasaAddress(
                "01310-100",
                "Av. Paulista",
                "Bela Vista",
                "São Paulo",
                "SP",
                Complement: "Apto 101",
                Number: "1000"),
            CreditorDocument: "12.345.678/0001-90");

        // Act
        var (payload, json) = _builder.BuildMainDebt(input, _options);

        // Assert
        payload.Should().NotBeNull();
        var payloadDict = payload as Dictionary<string, object?>;
        payloadDict.Should().NotBeNull();
        payloadDict.Should().ContainKey("value");
        payloadDict!["value"].Should().Be(1500.50m);
        payloadDict.Should().ContainKey("dueDate");
        payloadDict["dueDate"].Should().Be("2026-06-15");
        payloadDict.Should().ContainKey("contractNumber");
        payloadDict["contractNumber"].Should().Be("CONTRATO-001-P2");
    }

    [Fact]
    public void BuildMainDebt_WithDifferentParcelaNumbers_GeneratesDifferentContractNumbers()
    {
        // Arrange
        var parcela1 = new ParcelaInput(
            Valor: 1000m,
            Vencimento: new DateOnly(2026, 5, 15),
            Numero: 1,
            IdOrigem: "parcela-1");

        var parcela2 = new ParcelaInput(
            Valor: 1000m,
            Vencimento: new DateOnly(2026, 6, 15),
            Numero: 2,
            IdOrigem: "parcela-2");

        var input1 = new MainDebtInput(
            Parcela: parcela1,
            ContractNumber: "CONTRATO-001",
            DebtorDocument: "123.456.789-01",
            DebtorName: "João Silva",
            DebtorAddress: new SerasaAddress(
                "01310-100",
                "Av. Paulista",
                "Bela Vista",
                "São Paulo",
                "SP"),
            CreditorDocument: "12.345.678/0001-90");

        var input2 = new MainDebtInput(
            Parcela: parcela2,
            ContractNumber: "CONTRATO-001",
            DebtorDocument: "123.456.789-01",
            DebtorName: "João Silva",
            DebtorAddress: new SerasaAddress(
                "01310-100",
                "Av. Paulista",
                "Bela Vista",
                "São Paulo",
                "SP"),
            CreditorDocument: "12.345.678/0001-90");

        // Act
        var (payload1, _) = _builder.BuildMainDebt(input1, _options);
        var (payload2, _) = _builder.BuildMainDebt(input2, _options);

        // Assert
        var payloadDict1 = payload1 as Dictionary<string, object?>;
        var payloadDict2 = payload2 as Dictionary<string, object?>;
        payloadDict1!["contractNumber"].Should().Be("CONTRATO-001-P1");
        payloadDict2!["contractNumber"].Should().Be("CONTRATO-001-P2");
        payloadDict1["contractNumber"].Should().NotBe(payloadDict2["contractNumber"]);
    }

    [Fact]
    public void BuildMainDebt_WithParcela_DerivesValueAndDueDateFromParcela()
    {
        // Arrange
        var parcela = new ParcelaInput(
            Valor: 2500.75m,
            Vencimento: new DateOnly(2026, 7, 20),
            Numero: 3,
            IdOrigem: "parcela-456");

        var input = new MainDebtInput(
            Parcela: parcela,
            ContractNumber: "CONTRATO-002",
            DebtorDocument: "987.654.321-00",
            DebtorName: "Maria Santos",
            DebtorAddress: new SerasaAddress(
                "20040-002",
                "Rua da Assembleia",
                "Centro",
                "Rio de Janeiro",
                "RJ"),
            CreditorDocument: "98.765.432/0001-10");

        // Act
        var (payload, _) = _builder.BuildMainDebt(input, _options);

        // Assert
        input.Value.Should().Be(2500.75m);
        input.DueDate.Should().Be(new DateOnly(2026, 7, 20));
        var payloadDict = payload as Dictionary<string, object?>;
        payloadDict!["value"].Should().Be(2500.75m);
        payloadDict["dueDate"].Should().Be("2026-07-20");
    }

    [Fact]
    public void BuildGuarantor_WithParcela_UsesParcelaDataInPayload()
    {
        // Arrange
        var parcela = new ParcelaInput(
            Valor: 3000m,
            Vencimento: new DateOnly(2026, 8, 10),
            Numero: 1,
            IdOrigem: "parcela-789");

        var input = new GuarantorInput(
            Parcela: parcela,
            ContractNumber: "CONTRATO-003",
            DebtorDocument: "111.222.333-44",
            CreditorDocument: "11.222.333/0001-55",
            GuarantorDocument: "555.666.777-88",
            GuarantorName: "Carlos Fiador",
            GuarantorAddress: new SerasaAddress(
                "01000-000",
                "Praça da Sé",
                "Sé",
                "São Paulo",
                "SP",
                Number: "1"));

        // Act
        var (payload, json) = _builder.BuildGuarantor(input, _options);

        // Assert
        payload.Should().NotBeNull();
        var payloadDict = payload as Dictionary<string, object?>;
        payloadDict.Should().NotBeNull();
        payloadDict.Should().ContainKey("value");
        payloadDict!["value"].Should().Be(3000m);
        payloadDict.Should().ContainKey("dueDate");
        payloadDict["dueDate"].Should().Be("2026-08-10");
        payloadDict.Should().ContainKey("contractNumber");
        payloadDict["contractNumber"].Should().Be("CONTRATO-003-P1");
    }

    [Fact]
    public void BuildGuarantor_WithDifferentParcelaNumbers_GeneratesDifferentContractNumbers()
    {
        // Arrange
        var parcela1 = new ParcelaInput(
            Valor: 500m,
            Vencimento: new DateOnly(2026, 9, 1),
            Numero: 1,
            IdOrigem: "parcela-g1");

        var parcela2 = new ParcelaInput(
            Valor: 500m,
            Vencimento: new DateOnly(2026, 10, 1),
            Numero: 2,
            IdOrigem: "parcela-g2");

        var input1 = new GuarantorInput(
            Parcela: parcela1,
            ContractNumber: "CONTRATO-004",
            DebtorDocument: "123.456.789-01",
            CreditorDocument: "12.345.678/0001-90",
            GuarantorDocument: "999.888.777-66",
            GuarantorName: "Ana Fiadora",
            GuarantorAddress: new SerasaAddress(
                "01410-001",
                "Rua Oscar Freire",
                "Jardins",
                "São Paulo",
                "SP"));

        var input2 = new GuarantorInput(
            Parcela: parcela2,
            ContractNumber: "CONTRATO-004",
            DebtorDocument: "123.456.789-01",
            CreditorDocument: "12.345.678/0001-90",
            GuarantorDocument: "999.888.777-66",
            GuarantorName: "Ana Fiadora",
            GuarantorAddress: new SerasaAddress(
                "01410-001",
                "Rua Oscar Freire",
                "Jardins",
                "São Paulo",
                "SP"));

        // Act
        var (payload1, _) = _builder.BuildGuarantor(input1, _options);
        var (payload2, _) = _builder.BuildGuarantor(input2, _options);

        // Assert
        var payloadDict1 = payload1 as Dictionary<string, object?>;
        var payloadDict2 = payload2 as Dictionary<string, object?>;
        payloadDict1!["contractNumber"].Should().Be("CONTRATO-004-P1");
        payloadDict2!["contractNumber"].Should().Be("CONTRATO-004-P2");
        payloadDict1["contractNumber"].Should().NotBe(payloadDict2["contractNumber"]);
    }

    [Fact]
    public void BuildGuarantor_WithParcela_DerivesValueAndDueDateFromParcela()
    {
        // Arrange
        var parcela = new ParcelaInput(
            Valor: 1750.25m,
            Vencimento: new DateOnly(2026, 11, 30),
            Numero: 5,
            IdOrigem: "parcela-g5");

        var input = new GuarantorInput(
            Parcela: parcela,
            ContractNumber: "CONTRATO-005",
            DebtorDocument: "444.555.666-77",
            CreditorDocument: "44.555.666/0001-88",
            GuarantorDocument: "777.888.999-00",
            GuarantorName: "Pedro Fiador",
            GuarantorAddress: new SerasaAddress(
                "30130-010",
                "Av. Afonso Pena",
                "Centro",
                "Belo Horizonte",
                "MG"));

        // Act
        var (payload, _) = _builder.BuildGuarantor(input, _options);

        // Assert
        input.Value.Should().Be(1750.25m);
        input.DueDate.Should().Be(new DateOnly(2026, 11, 30));
        var payloadDict = payload as Dictionary<string, object?>;
        payloadDict!["value"].Should().Be(1750.25m);
        payloadDict["dueDate"].Should().Be("2026-11-30");
    }

    [Fact]
    public void BuildMainDebt_SerializesToValidJson()
    {
        // Arrange
        var parcela = new ParcelaInput(
            Valor: 1000m,
            Vencimento: new DateOnly(2026, 12, 15),
            Numero: 1,
            IdOrigem: "parcela-json");

        var input = new MainDebtInput(
            Parcela: parcela,
            ContractNumber: "CONTRATO-JSON",
            DebtorDocument: "123.456.789-01",
            DebtorName: "Test User",
            DebtorAddress: new SerasaAddress(
                "01310-100",
                "Av. Paulista",
                "Bela Vista",
                "São Paulo",
                "SP"),
            CreditorDocument: "12.345.678/0001-90");

        // Act
        var (payload, json) = _builder.BuildMainDebt(input, _options);

        // Assert
        json.Should().NotBeNullOrEmpty();
        var payloadDict = payload as Dictionary<string, object?>;
        payloadDict.Should().NotBeNull();
        payloadDict.Should().ContainKey("value");
        payloadDict.Should().ContainKey("dueDate");
        payloadDict.Should().ContainKey("contractNumber");
    }

    [Fact]
    public void BuildGuarantor_SerializesToValidJson()
    {
        // Arrange
        var parcela = new ParcelaInput(
            Valor: 2000m,
            Vencimento: new DateOnly(2026, 12, 15),
            Numero: 1,
            IdOrigem: "parcela-json-g");

        var input = new GuarantorInput(
            Parcela: parcela,
            ContractNumber: "CONTRATO-JSON-G",
            DebtorDocument: "123.456.789-01",
            CreditorDocument: "12.345.678/0001-90",
            GuarantorDocument: "999.888.777-66",
            GuarantorName: "Test Guarantor",
            GuarantorAddress: new SerasaAddress(
                "01310-100",
                "Av. Paulista",
                "Bela Vista",
                "São Paulo",
                "SP"));

        // Act
        var (payload, json) = _builder.BuildGuarantor(input, _options);

        // Assert
        json.Should().NotBeNullOrEmpty();
        var payloadDict = payload as Dictionary<string, object?>;
        payloadDict.Should().NotBeNull();
        payloadDict.Should().ContainKey("value");
        payloadDict.Should().ContainKey("dueDate");
        payloadDict.Should().ContainKey("contractNumber");
    }
}
