namespace QuickInventory.Verifactu.Tests;

public class ErrorCatalogTests
{
    [Theory]
    [InlineData(4102, AeatErrorKind.RejectsSubmission)]
    [InlineData(1100, AeatErrorKind.RejectsRecord)]
    [InlineData(3000, AeatErrorKind.RejectsRecord)]
    [InlineData(2000, AeatErrorKind.AcceptedWithErrors)]
    [InlineData(2007, AeatErrorKind.AcceptedWithErrors)]
    [InlineData(9999, AeatErrorKind.Unknown)]
    public void Clasifica_los_codigos(int code, AeatErrorKind kind) => Assert.Equal(kind, ErrorCatalog.KindOf(code));

    [Fact]
    public void Lee_las_tildes_del_archivo() =>
        Assert.Equal("Error técnico al obtener el certificado.", ErrorCatalog.Describe(4108));

    [Fact]
    public void Codigo_desconocido() => Assert.Null(ErrorCatalog.Describe(9999));
}
