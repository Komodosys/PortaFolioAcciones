using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Añade el tipo de registro fiscal a las carteras: REER, CELI o no registrada.
    ///
    /// Va en tres pasos y no en uno, y no es por gusto. EF genera la columna de golpe con
    /// <c>defaultValue: ""</c>, y <b>una cadena vacía no es un valor del enumerado</b>: la aplicación
    /// reventaría al leer la primera cartera. Es el mismo fallo que ya apareció con los desdoblamientos,
    /// donde el valor por defecto de un `jsonb` era `""`, que tampoco es JSON válido.
    ///
    /// Así que: se añade permitiendo nulos, se rellena mirando el nombre, y se cierra como obligatoria.
    /// </summary>
    public partial class TipoDeRegistroDeCartera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Se añade permitiendo nulos: no hay ningún valor por defecto que sea honesto.
            migrationBuilder.AddColumn<string>(
                name: "RegistrationType",
                table: "Portfolios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // 2. Se rellenan las que existen, y el relleno NO es una suposición: las siete carteras que había
            //    se llaman «RRSP ···» o «TFSA ···», porque ese nombre viene del estado de cuenta del bróker.
            //    Ninguna es no registrada, así que el resto, si alguna vez hubiera alguna, cae en
            //    `NotRegistered`.
            migrationBuilder.Sql(
                """
                UPDATE "Portfolios"
                SET "RegistrationType" = CASE
                    WHEN "Name" LIKE 'RRSP%' THEN 'RegisteredRetirement'
                    WHEN "Name" LIKE 'TFSA%' THEN 'RegisteredSavings'
                    ELSE 'NotRegistered'
                END;
                """);

            // 3. Y ahora sí, obligatoria y sin valor por defecto: a partir de aquí lo pone la aplicación.
            migrationBuilder.AlterColumn<string>(
                name: "RegistrationType",
                table: "Portfolios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationType",
                table: "Portfolios");
        }
    }
}
