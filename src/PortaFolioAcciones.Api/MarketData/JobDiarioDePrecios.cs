using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Domain.Services;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Api.MarketData;

/// <summary>
/// El trabajo diario: recoge los precios despues de que cierren las bolsas donde hay posiciones.
///
/// <b>Espera a que cierren todas</b>, y esa es la decision que lo define. Recoger en cuanto cierra Nueva
/// York guardaria la cartera con los precios de alla y sin los de Toronto, y la foto del dia seria la de
/// un momento en el que media cartera tenia el precio de ayer.
///
/// <b>Y comprueba si la sesion ha terminado, no si el mercado esta abierto.</b> A las siete de la mañana
/// tampoco esta abierto, y recoger entonces daria los precios de ayer con la fecha de hoy. Por eso se
/// pregunta por <b>la hora del ultimo cierre</b>, no por el estado.
///
/// Se ejecuta cada cuarto de hora y <b>no hace nada si ya recogio hoy</b>. Que sea barato preguntar es lo
/// que permite no tener que acertar con la hora exacta: un equipo domestico no esta encendido a todas
/// horas, y al arrancar por la noche se pone al dia solo.
/// </summary>
internal sealed class JobDiarioDePrecios : BackgroundService
{
    /// <summary>
    /// Cada cuanto se mira si ha llegado el momento.
    ///
    /// No es cada cuanto se recogen los precios, que es una vez al dia: es cada cuanto se mira el reloj.
    /// Mirarlo no cuesta nada, y recoger solo ocurre una vez.
    /// </summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _alcances;
    private readonly TimeProvider _reloj;
    private readonly ILogger<JobDiarioDePrecios> _registro;

    /// <summary>El ultimo dia que se recogio, para no repetirlo en cada vuelta.</summary>
    private DateOnly? _ultimoDia;

    public JobDiarioDePrecios(
        IServiceScopeFactory alcances,
        TimeProvider reloj,
        ILogger<JobDiarioDePrecios> registro)
    {
        _alcances = alcances;
        _reloj = reloj;
        _registro = registro;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Una primera vuelta al arrancar: si el equipo estuvo apagado a la hora del cierre, esto lo pone al
        // dia sin esperar al siguiente cuarto de hora.
        await IntentarAsync(stoppingToken);

        using var latido = new PeriodicTimer(Intervalo, _reloj);

        while (await latido.WaitForNextTickAsync(stoppingToken))
        {
            await IntentarAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Mira si toca recoger, y recoge si toca. Un fallo aqui <b>no tumba el servicio</b>: se avisa y se
    /// espera a la vuelta siguiente. Un trabajo que se muere al primer fallo de red no vuelve solo.
    /// </summary>
    private async Task IntentarAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ahora = _reloj.GetUtcNow();
            var dia = DateOnly.FromDateTime(ahora.UtcDateTime);

            if (_ultimoDia == dia)
            {
                return;
            }

            var bolsas = await BolsasConPosicionesAsync(cancellationToken);

            if (bolsas.Count == 0)
            {
                // Sin posiciones no hay nada que recoger, y no se marca el dia: mañana puede haberlas.
                return;
            }

            var sinCerrar = bolsas.Where(bolsa => !MarketCalendar.HasClosedToday(bolsa, ahora)).ToList();

            if (sinCerrar.Count > 0)
            {
                _registro.LogDebug(
                    "Todavia no han cerrado {Bolsas}: no se recogen precios.",
                    string.Join(", ", sinCerrar));

                return;
            }

            using var alcance = _alcances.CreateScope();
            var colector = alcance.ServiceProvider.GetRequiredService<IMarketDataCollector>();

            await colector.CollectAsync(dia, cancellationToken);

            _ultimoDia = dia;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Se esta parando la aplicacion: no es un fallo.
        }
        catch (Exception error)
        {
            _registro.LogError(error, "Fallo la recoleccion diaria. Se reintentara en la proxima vuelta.");
        }
    }

    /// <summary>
    /// Las bolsas donde alguien tiene algo abierto.
    ///
    /// Solo esas: esperar al cierre de una bolsa donde no se tiene nada dejaria la recoleccion parada sin
    /// motivo. Y da igual de quien sea cada posicion: para saber <b>que plazas</b> hay que esperar no hace
    /// falta saber de quien es cada una.
    /// </summary>
    private async Task<IReadOnlyList<string>> BolsasConPosicionesAsync(CancellationToken cancellationToken)
    {
        using var alcance = _alcances.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<PortafolioDbContext>();

        return await contexto.Positions
            .Where(posicion => !posicion.IsClosed)
            .Select(posicion => posicion.InstrumentId)
            .Distinct()
            .Join(
                contexto.Instruments.Select(instrumento => new { instrumento.Id, instrumento.Exchange }),
                id => id,
                instrumento => instrumento.Id,
                (_, instrumento) => instrumento.Exchange)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
