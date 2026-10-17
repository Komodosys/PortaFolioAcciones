using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OwnerUserId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "OwnerUserId", "OccurredAtUtc" });

            // ------------------------------------------------------------------------------------
            // El libro es de SOLO ANADIR, y lo garantiza la base de datos, no la aplicacion.
            //
            // Esto va aqui y no en el codigo por un motivo concreto: si la proteccion viviera en la
            // aplicacion, cualquiera con acceso a la base de datos podria reescribir el libro y no
            // quedaria rastro de que lo hizo. Una tabla que se puede borrar no es un libro de
            // auditoria: es una tabla.
            //
            // Se usa un disparador y no una regla porque tiene que FALLAR de forma ruidosa. Una regla
            // con "DO INSTEAD NOTHING" ignoraria el intento en silencio, y quien lo intentara creeria
            // que lo ha conseguido.
            // ------------------------------------------------------------------------------------
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION portafolio_auditoria_solo_anadir()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION
                        'El libro de auditoria es de solo anadir: no se puede modificar ni borrar (tabla %, operacion %).',
                        TG_TABLE_NAME, TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER auditoria_solo_anadir
                    BEFORE UPDATE OR DELETE ON "AuditLogs"
                    FOR EACH ROW
                    EXECUTE FUNCTION portafolio_auditoria_solo_anadir();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El disparador se va con la tabla, pero la funcion hay que quitarla a mano: no depende de
            // ninguna tabla.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS portafolio_auditoria_solo_anadir();");

            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
