using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PortaFolioAcciones.Infrastructure.MarketData.Resilience;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// La red con una fuente que falla: reintentos, interruptor de circuito y cuota.
///
/// Lo que se prueba aqui no es «hay reintentos», sino las decisiones que los hacen utiles: que <b>no</b> se
/// reintenta lo que no puede salir bien, que el interruptor <b>prueba a recuperarse</b> —si no, se quedaria
/// abierto para siempre— y que la cuota se corta <b>antes</b> de agotarla.
/// </summary>
public sealed class ManejadorDeResilienciaTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Una fuente de mentira que devuelve lo que se le diga, en el orden que se le diga.</summary>
    private sealed class FuenteFalsa : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _preparadas = new();
        private Func<HttpResponseMessage>? _ultima;

        public int Llamadas { get; private set; }

        public void RespondeCon(HttpStatusCode codigo)
        {
            _preparadas.Enqueue(() => new HttpResponseMessage(codigo));
        }

        public void RespondeBien()
        {
            _preparadas.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));
        }

        public void FallaDeRed()
        {
            _preparadas.Enqueue(() => throw new HttpRequestException("la red se callo"));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Llamadas++;

            // Cada respuesta preparada se gasta una vez, y a partir de ahi se repite la ULTIMA. Asi una
            // prueba de «falla siempre» prepara una sola, y una de «falla, luego va bien» prepara dos y la
            // segunda se queda puesta.
            if (_preparadas.Count > 0)
            {
                _ultima = _preparadas.Dequeue();
            }

            var responder = _ultima ?? (() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            return Task.FromResult(responder());
        }
    }

    private static (HttpClient Cliente, FuenteFalsa Fuente, GuardaDeLaFuente Guarda, RelojFalso Reloj) Montar(
        OpcionesDeResiliencia? opciones = null)
    {
        // Espera CERO: las pruebas no esperan de verdad. Lo que se comprueba es cuantas veces se llama y
        // cuando se corta, no cuanto se tarda. La duracion de la espera se prueba aparte, como funcion pura.
        var ajustes = opciones ?? new OpcionesDeResiliencia { EsperaBase = TimeSpan.Zero };

        var reloj = new RelojFalso();
        var guarda = new GuardaDeLaFuente("fuente-de-prueba", ajustes, reloj);
        var fuente = new FuenteFalsa();

        var manejador = new ManejadorDeResiliencia(guarda, ajustes, reloj, NullLogger.Instance)
        {
            InnerHandler = fuente,
        };

        return (new HttpClient(manejador), fuente, guarda, reloj);
    }

    [Fact]
    public async Task UnFalloPasajeroSeReintentaYSaleBien()
    {
        var (cliente, fuente, _, _) = Montar();

        fuente.RespondeCon(HttpStatusCode.ServiceUnavailable);
        fuente.RespondeBien();

        var respuesta = await cliente.GetAsync("https://ejemplo/prueba", Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task SiSigueFallandoSeRindeTrasLosIntentos()
    {
        var (cliente, fuente, _, _) = Montar();

        fuente.RespondeCon(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => cliente.GetAsync("https://ejemplo/prueba", Ct));

        // Tres intentos: la primera vez y dos reintentos. Insistir mas seria gastar cuota en algo que no
        // esta respondiendo.
        Assert.Equal(3, fuente.Llamadas);
    }

    [Fact]
    public async Task UnSimboloQueNoExisteNoSeReintenta()
    {
        var (cliente, fuente, _, _) = Montar();

        fuente.RespondeCon(HttpStatusCode.NotFound);

        var respuesta = await cliente.GetAsync("https://ejemplo/prueba", Ct);

        // El 404 se DEVUELVE, no se convierte en excepcion: que un simbolo no exista es una respuesta
        // legitima, y quien llama sabe mejor que nadie que hacer con ella.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        // Y no se reintenta: no va a existir al tercer intento.
        Assert.Equal(1, fuente.Llamadas);
    }

    [Fact]
    public async Task UnaCaidaDeRedTambienSeReintenta()
    {
        var (cliente, fuente, _, _) = Montar();

        fuente.FallaDeRed();
        fuente.RespondeBien();

        var respuesta = await cliente.GetAsync("https://ejemplo/prueba", Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public void LaEsperaSeDuplicaEnCadaIntento()
    {
        var base_ = TimeSpan.FromMilliseconds(500);

        // Medio segundo, uno y dos. Esperar siempre lo mismo no ayuda: si la fuente esta saturada,
        // insistir cada medio segundo la satura mas.
        Assert.Equal(TimeSpan.FromMilliseconds(500), ManejadorDeResiliencia.EsperaDelIntento(1, base_));
        Assert.Equal(TimeSpan.FromSeconds(1), ManejadorDeResiliencia.EsperaDelIntento(2, base_));
        Assert.Equal(TimeSpan.FromSeconds(2), ManejadorDeResiliencia.EsperaDelIntento(3, base_));
    }

    [Fact]
    public async Task ElInterruptorCortaTrasVariosFallosSeguidos()
    {
        var (cliente, fuente, guarda, _) = Montar(
            new OpcionesDeResiliencia { EsperaBase = TimeSpan.Zero, FallosParaAbrir = 3 });

        fuente.RespondeCon(HttpStatusCode.InternalServerError);

        // Tres peticiones, tres intentos cada una: nueve fallos, y el interruptor salta.
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync("https://ejemplo", Ct));
        }

        Assert.True(guarda.EstaAbierto);

        var llamadasAntes = fuente.Llamadas;

        // Y ahora se corta SIN salir a la red. Eso es lo que protege a la fuente y lo que hace que la
        // aplicacion no se quede esperando diez segundos por algo que ya se sabe que va a fallar.
        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync("https://ejemplo", Ct));

        Assert.Equal(llamadasAntes, fuente.Llamadas);
    }

    [Fact]
    public async Task ElInterruptorDejaPasarUnaPruebaYSeCierraSiSaleBien()
    {
        var (cliente, fuente, guarda, reloj) = Montar(
            new OpcionesDeResiliencia
            {
                EsperaBase = TimeSpan.Zero,
                FallosParaAbrir = 3,
                DuracionAbierto = TimeSpan.FromMinutes(5),
            });

        fuente.RespondeCon(HttpStatusCode.InternalServerError);

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync("https://ejemplo", Ct));
        }

        Assert.True(guarda.EstaAbierto);

        // Pasa el tiempo de espera y la fuente ya responde.
        reloj.Avanzar(TimeSpan.FromMinutes(6));

        fuente.RespondeBien();

        var respuesta = await cliente.GetAsync("https://ejemplo", Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        // ESTA es la parte que hace que el interruptor sirva: sin la llamada de prueba, no tendria forma de
        // enterarse de que la fuente ha vuelto, y se quedaria abierto para siempre.
        Assert.False(guarda.EstaAbierto);
    }

    [Fact]
    public async Task LaCuotaDiariaCortaLasLlamadas()
    {
        var (cliente, fuente, guarda, _) = Montar(
            new OpcionesDeResiliencia { EsperaBase = TimeSpan.Zero, LlamadasAlDia = 3 });

        fuente.RespondeBien();

        for (var i = 0; i < 3; i++)
        {
            await cliente.GetAsync("https://ejemplo", Ct);
        }

        Assert.Equal(3, fuente.Llamadas);
        Assert.Equal(3, guarda.LlamadasHoy);

        // La cuarta se corta. Pasarse de cuota no da un error claro: la fuente empieza a rechazar, y ese
        // dia no hay precios para nadie. Mejor dejar de pedir y decirlo.
        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync("https://ejemplo", Ct));

        Assert.Equal(3, fuente.Llamadas);
    }

    [Fact]
    public async Task LaCuotaSeRenuevaAlDiaSiguiente()
    {
        var (cliente, fuente, _, reloj) = Montar(
            new OpcionesDeResiliencia { EsperaBase = TimeSpan.Zero, LlamadasAlDia = 1 });

        fuente.RespondeBien();

        await cliente.GetAsync("https://ejemplo", Ct);

        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync("https://ejemplo", Ct));

        reloj.Avanzar(TimeSpan.FromDays(1));

        await cliente.GetAsync("https://ejemplo", Ct);

        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task UnExitoBorraLosFallosSeguidos()
    {
        // El umbral se pone en 4 para que TRES fallos no abran el interruptor. Asi lo que se prueba es que
        // el contador se reinicia, y no el interruptor, que ya tiene sus propias pruebas. Con el umbral en
        // 3, el exito no llegaria siquiera a intentarse: estaria cortado, y con razon.
        var (cliente, fuente, guarda, _) = Montar(
            new OpcionesDeResiliencia { EsperaBase = TimeSpan.Zero, FallosParaAbrir = 4 });

        // Tres fallos, uno por intento. No llegan a abrir el interruptor.
        fuente.RespondeCon(HttpStatusCode.InternalServerError);
        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync("https://ejemplo", Ct));
        Assert.False(guarda.EstaAbierto);

        // Un exito, que borra la cuenta.
        fuente.RespondeBien();
        await cliente.GetAsync("https://ejemplo", Ct);

        // Y otros tres fallos. Si el exito NO hubiera borrado la cuenta, se sumarian a los tres anteriores
        // y el interruptor se abriria.
        fuente.RespondeCon(HttpStatusCode.InternalServerError);
        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync("https://ejemplo", Ct));

        Assert.False(guarda.EstaAbierto);
    }
}
