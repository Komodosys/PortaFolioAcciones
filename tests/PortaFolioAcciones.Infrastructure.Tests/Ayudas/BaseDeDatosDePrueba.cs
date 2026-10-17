using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Tests.Ayudas;

/// <summary>
/// Quien hace la peticion, de mentira.
///
/// Se puede elegir el identificador y si es administrador, porque <b>el aislamiento es justo lo que hay
/// que probar</b>: una prueba que siempre fuera administrador no comprobaria nada de eso.
/// </summary>
internal sealed class UsuarioFalso : ICurrentUser
{
    public Guid? Id { get; set; }

    public bool IsAuthenticated => Id is not null;

    public bool IsAdmin { get; set; }
}

/// <summary>
/// Una base de datos de verdad, en memoria, para probar las consultas de los repositorios.
///
/// <b>Por que existe esto.</b> Las pruebas de infraestructura solo cubrian los proveedores de datos de
/// mercado, y un fallo en una <b>consulta</b> no lo puede ver una prueba que no consulta nada. Por eso
/// pasó lo de las posiciones cerradas: cerraban una y seguia contando en los totales, porque el lector de
/// la vista no filtraba. Cada pieza por separado estaba bien.
///
/// <b>SQLite y no el proveedor «en memoria»</b>, aunque los dos sirvan para esto: el de en memoria no es
/// una base de datos —no aplica restricciones ni traduce a SQL—, asi que una consulta que PostgreSQL
/// rechazaria pasaria la prueba tan contenta.
///
/// La conexion se mantiene <b>abierta</b> mientras vive la prueba: una base SQLite en memoria se borra al
/// cerrarse la ultima conexion, y cerrarla antes de tiempo dejaria la prueba sin datos a mitad.
/// </summary>
internal sealed class BaseDeDatosDePrueba : IDisposable
{
    private readonly SqliteConnection _conexion;

    private BaseDeDatosDePrueba(SqliteConnection conexion, UsuarioFalso usuario)
    {
        _conexion = conexion;
        Usuario = usuario;
    }

    public UsuarioFalso Usuario { get; }

    /// <summary>Crea una base vacia, con el esquema hecho a partir del modelo.</summary>
    /// <param name="esAdministrador">Si quien consulta administra. Cambia por completo el aislamiento.</param>
    public static BaseDeDatosDePrueba Nueva(bool esAdministrador = false)
    {
        // `DataSource=:memory:` es una base en memoria; la conexion abierta es la que la mantiene viva.
        var conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        var usuario = new UsuarioFalso { IsAdmin = esAdministrador };
        var base_ = new BaseDeDatosDePrueba(conexion, usuario);

        // Se usa EnsureCreated y no las migraciones: las migraciones son de PostgreSQL —tipos,
        // `numeric(28,4)`, indices parciales— y no se pueden aplicar aqui. El esquema que interesa probar
        // es el que sale del MODELO, que es el mismo en los dos sitios.
        using var contexto = base_.CrearContexto();
        contexto.Database.EnsureCreated();

        return base_;
    }

    /// <summary>Un contexto nuevo sobre la misma base. Se tira al terminar, como el de verdad.</summary>
    public PortafolioDbContext CrearContexto() =>
        new(
            new DbContextOptionsBuilder<PortafolioDbContext>()
                .UseSqlite(_conexion)
                .Options,
            Usuario);

    public void Dispose() => _conexion.Dispose();
}
