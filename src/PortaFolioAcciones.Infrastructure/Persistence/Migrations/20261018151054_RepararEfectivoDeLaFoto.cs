using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Repara la moneda del efectivo en las fotos que ya existían.
    ///
    /// Al añadir la columna, EF la creó como obligatoria con <c>defaultValue: ""</c>, así que las catorce fotos
    /// anteriores se quedaron con la <b>moneda vacía</b>. El importe era cero y no hacía daño, pero la moneda no
    /// es un número: al leer la fila, el valor de la moneda se valida y una cadena vacía lanza. El informe
    /// devolvía un 500 sin decir por qué.
    ///
    /// Es el mismo fallo que ya apareció con el tipo de registro de la cartera y con los desdoblamientos: un
    /// valor por defecto que satisface a la base de datos y no al dominio. La lección está escrita, y aun así se
    /// repitió; por eso las migraciones que añaden columnas obligatorias a una tabla con filas se revisan a mano.
    ///
    /// Se rellena con la moneda base de cada foto, que es la que le corresponde: el efectivo de una cartera está
    /// en su moneda base.
    /// </summary>
    public partial class RepararEfectivoDeLaFoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "PortfolioSnapshots"
                SET "CashValue_Currency" = "BaseCurrency"
                WHERE "CashValue_Currency" = '' OR "CashValue_Currency" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se deshace: volver a dejar la moneda vacía sería volver a romper la lectura, y la reparación no
            // cambia ningún importe.
        }
    }
}