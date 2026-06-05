using ApiInadimplencia.Domain.SerasaPefin;
using FluentAssertions;
using Xunit;

namespace ApiInadimplencia.Domain.Tests.SerasaPefin;

/// <summary>
/// Cobertura do value object <see cref="SerasaPefinBaixaMotivo"/>:
/// whitelist Serasa (1, 2, 3, 4, 19, 43, 45), descrições canônicas e igualdade estrutural.
/// </summary>
public sealed class SerasaPefinBaixaMotivoTests
{
    [Theory]
    [InlineData(1, "PAGAMENTO DA DIVIDA")]
    [InlineData(2, "RENEGOCIACAO DA DIVIDA")]
    [InlineData(3, "POR SOLICITACAO DO CLIENTE")]
    [InlineData(4, "ORDEM JUDICIAL")]
    [InlineData(19, "RENEGOCIACAO DA DIVIDA POR ACORDO")]
    [InlineData(43, "BAIXA POR NEGOCIACAO")]
    [InlineData(45, "CONTESTACAO")]
    public void From_CodigoNaWhitelist_RetornaInstanciaComDescricaoCorreta(byte codigo, string descricao)
    {
        var motivo = SerasaPefinBaixaMotivo.From(codigo);

        motivo.Codigo.Should().Be(codigo);
        motivo.Descricao.Should().Be(descricao);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(44)]
    [InlineData(99)]
    public void From_CodigoForaDaWhitelist_DeveLancarArgumentException(byte codigo)
    {
        Action act = () => SerasaPefinBaixaMotivo.From(codigo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*motivo*");
    }

    [Fact]
    public void Igualdade_DoisMotivosComMesmoCodigo_SaoIguais()
    {
        var a = SerasaPefinBaixaMotivo.From(3);
        var b = SerasaPefinBaixaMotivo.From(3);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Igualdade_MotivosDistintos_SaoDiferentes()
    {
        var a = SerasaPefinBaixaMotivo.From(3);
        var b = SerasaPefinBaixaMotivo.From(43);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Whitelist_DeveExporExatamenteSeteCodigos()
    {
        SerasaPefinBaixaMotivo.CodigosValidos.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 19, 43, 45 });
    }
}
