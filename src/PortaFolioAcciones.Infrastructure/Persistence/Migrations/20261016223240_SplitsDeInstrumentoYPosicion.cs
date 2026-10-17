using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitsDeInstrumentoYPosicion : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// <b>El valor por omision esta corregido a mano, y hay que dejarlo escrito.</b> Entity Framework
        /// genera <c>defaultValue: ""</c> para una columna de texto no nula, y <b>una cadena vacia NO es
        /// JSON valido</b>: PostgreSQL rechaza el <c>ALTER TABLE</c> en cuanto hay una fila que rellenar, y
        /// esta tabla tiene filas. La migracion no habria llegado ni a aplicarse.
        ///
        /// Se pone la lista vacia, que es lo que el modelo espera para «no hay ninguno»: la conversion lee
        /// ese texto y lo convierte en una lista sin elementos.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliedSplitDates",
                table: "Positions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Splits",
                table: "Instruments",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedSplitDates",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Splits",
                table: "Instruments");
        }
    }
}
