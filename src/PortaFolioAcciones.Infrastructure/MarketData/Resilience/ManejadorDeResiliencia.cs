using System.Net;
using Microsoft.Extensions.Logging;

namespace PortaFolioAcciones.Infrastructure.MarketData.Resilience;

/// <summary>
/// Envuelve las llamadas a una fuente de datos de mercado: reintenta lo que merece la pena, corta cuando
/// la fuente lleva rato fallando y no se pasa de cuota.
///
/// Va en un <see cref="DelegatingHandler"/> y no dentro de cada adaptador porque <b>es una decision sobre
/// la red, no sobre los precios</b>. Dentro de cada adaptador habria que escribirla dos veces —una para las
/// cotizaciones y otra para los tipos de cambio— y la segunda quedaria distinta.
///
/// Tres decisiones que hacen que sirva de algo:
///
/// <list type="number">
///   <item><description><b>Solo se reintenta lo que puede salir bien a la segunda.</b> Un 404 porque el
///   simbolo no existe no va a existir tampoco al tercer intento: reintentarlo es gastar cuota para volver
///   a recibir lo mismo.</description></item>
///   <item><description><b>El interruptor prueba a recuperarse.</b> Despues del tiempo de espera deja pasar
///   <b>una</b> llamada: si sale bien se cierra, y si falla se vuelve a abrir. Sin esa prueba, el
///   interruptor no tendria forma de enterarse de que la fuente ha vuelto y se quedaria abierto para
///   siempre.</description></item>
///   <item><description><b>La cuota se corta antes de agotarla.</b> Pasarse no da un error claro: la fuente
///   empieza a rechazar, y ese dia no hay precios para nadie. Es mejor dejar de pedir y decirlo.</description></item>
/// </list>
///
/// Se usa el <see cref="TimeProvider"/> para las esperas y para el reloj, y no el del sistema: probar que
/// entre el primer y el segundo intento se espera el doble exigiria esperar de verdad, y esa prueba no se
/// escribiria nunca.
/// </summary>
internal sealed class ManejadorDeResiliencia : DelegatingHandler
{
    private readonly GuardaDeLaFuente _guarda;
    private readonly OpcionesDeResiliencia _opciones;
    private readonly TimeProvider _reloj;
    private readonly ILogger _registro;

    public ManejadorDeResiliencia(
        GuardaDeLaFuente guarda,
        OpcionesDeResiliencia opciones,
        TimeProvider reloj,
        ILogger registro)
    {
        _guarda = guarda;
        _opciones = opciones;
        _reloj = reloj;
        _registro = registro;
    }

    /// <summary>
    /// Los codigos que <b>si</b> merece la pena reintentar: la fuente esta saturada o tiene un problema
    /// pasajero.
    /// </summary>
    private static bool EsTransitorio(HttpStatusCode codigo) =>
        codigo is HttpStatusCode.RequestTimeout          // 408
            or HttpStatusCode.TooManyRequests            // 429
            or HttpStatusCode.InternalServerError        // 500
            or HttpStatusCode.BadGateway                 // 502
            or HttpStatusCode.ServiceUnavailable         // 503
            or HttpStatusCode.GatewayTimeout;            // 504

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage peticion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        for (var intento = 1; ; intento++)
        {
            if (!_guarda.PermiteLlamar(out var motivo))
            {
                // No se llama, y se dice por que. Un fallo silencioso aqui haria que la cartera apareciera
                // sin precios sin que nadie supiera que la aplicacion decidio no preguntar.
                _registro.LogWarning("No se consulta {Fuente}: {Motivo}", _guarda.Nombre, motivo);

                throw new HttpRequestException($"No se consulta {_guarda.Nombre}: {motivo}.");
            }

            HttpResponseMessage? respuesta = null;
            Exception? fallo = null;
            TimeSpan? esperaPedida = null;

            try
            {
                respuesta = await base.SendAsync(peticion, cancellationToken);

                if (!EsTransitorio(respuesta.StatusCode))
                {
                    // Un 4xx es una respuesta, no un fallo de la fuente: el simbolo no existe, o la
                    // peticion esta mal. Se devuelve tal cual y quien llama decide.
                    _guarda.AnotarExito();

                    return respuesta;
                }

                esperaPedida = EsperaQuePideLaFuente(respuesta);

                fallo = new HttpRequestException(
                    $"{_guarda.Nombre} respondio {(int)respuesta.StatusCode}.");
            }
            catch (HttpRequestException error)
            {
                fallo = error;
            }
            catch (TaskCanceledException error) when (!cancellationToken.IsCancellationRequested)
            {
                // Un `TaskCanceledException` que NO viene de nuestra cancelacion es un tiempo de espera
                // agotado. Distinguirlos importa: uno es un fallo de la fuente y el otro es que ya no
                // hace falta la respuesta.
                fallo = error;
            }

            // A partir de aqui, hubo un fallo transitorio.
            respuesta?.Dispose();

            var seAbrioAhora = _guarda.AnotarFallo();

            if (seAbrioAhora)
            {
                _registro.LogError(
                    "{Fuente} lleva {Fallos} fallos seguidos: se deja de consultar {Duracion}.",
                    _guarda.Nombre,
                    _opciones.FallosParaAbrir,
                    _opciones.DuracionAbierto);
            }

            if (intento >= _opciones.IntentosMaximos)
            {
                _registro.LogWarning(
                    "{Fuente} fallo en los {Intentos} intentos: {Motivo}",
                    _guarda.Nombre,
                    _opciones.IntentosMaximos,
                    fallo.Message);

                throw fallo;
            }

            var espera = esperaPedida ?? EsperaDelIntento(intento, _opciones.EsperaBase);

            _registro.LogInformation(
                "Reintento {Intento} de {Total} contra {Fuente} dentro de {Espera}.",
                intento + 1,
                _opciones.IntentosMaximos,
                _guarda.Nombre,
                espera);

            await Task.Delay(espera, _reloj, cancellationToken);
        }
    }

    /// <summary>
    /// Lo que hay que esperar antes del intento siguiente: el doble cada vez.
    ///
    /// Esperar siempre lo mismo sirve de poco. Si la fuente esta saturada, insistir cada medio segundo la
    /// satura mas, y el reintento pasa de ayudar a estorbar.
    ///
    /// Es una funcion pura y por eso se puede probar sola: la alternativa seria cronometrar una prueba de
    /// red, que es lenta y falla sola de vez en cuando.
    /// </summary>
    /// <param name="intento">El intento que acaba de fallar. El primero es 1.</param>
    /// <param name="esperaBase">Lo que se espera tras el primer fallo.</param>
    internal static TimeSpan EsperaDelIntento(int intento, TimeSpan esperaBase) =>
        esperaBase * Math.Pow(2, intento - 1);

    /// <summary>
    /// Si la fuente dice cuanto hay que esperar, se la hace caso.
    ///
    /// Es lo que manda cuando se ha pedido demasiado: la fuente sabe cuando va a poder atender, y insistir
    /// antes de tiempo es la forma mas rapida de que cierre el grifo del todo.
    /// </summary>
    private static TimeSpan? EsperaQuePideLaFuente(HttpResponseMessage respuesta)
    {
        var cabecera = respuesta.Headers.RetryAfter;

        if (cabecera?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (cabecera?.Date is { } fecha)
        {
            var espera = fecha - DateTimeOffset.UtcNow;

            return espera > TimeSpan.Zero ? espera : TimeSpan.Zero;
        }

        return null;
    }
}
