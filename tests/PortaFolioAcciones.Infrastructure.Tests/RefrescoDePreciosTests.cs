using PortaFolioAcciones.Infrastructure.MarketData;
using PortaFolioAcciones.Infrastructure.Tests.Ayudas;

namespace PortaFolioAcciones.Infrastructure.Tests;

/// <summary>
/// El estado de los refrescos pedidos a mano: la generación que tira la caché y el freno que protege a la
/// fuente.
///
/// Los dos importan por motivos opuestos. La generación es lo que hace que el botón signifique algo —sin
/// ella, pulsar «actualizar» devolvería el precio guardado—, y el freno es lo que evita que el botón se
/// convierta en una forma de que la fuente nos bloquee.
/// </summary>
public sealed class RefrescoDePreciosTests
{
    [Fact]
    public void El_primero_pasa()
    {
        var refrescos = new RefrescoDePrecios(new RelojFalso());

        Assert.Null(refrescos.EsperarPara());
    }

    [Fact]
    public void El_segundo_inmediato_no_pasa_y_dice_cuanto_falta()
    {
        var reloj = new RelojFalso();
        var refrescos = new RefrescoDePrecios(reloj);

        Assert.Null(refrescos.EsperarPara());

        var falta = refrescos.EsperarPara();

        Assert.NotNull(falta);

        // «Menor o igual» y no «menor»: con el reloj quieto, lo que falta es exactamente la espera entera.
        Assert.True(falta <= RefrescoDePrecios.EsperaMinima);
        Assert.True(falta > TimeSpan.Zero);
    }

    [Fact]
    public void Pasada_la_espera_vuelve_a_pasar()
    {
        var reloj = new RelojFalso();
        var refrescos = new RefrescoDePrecios(reloj);

        refrescos.EsperarPara();
        reloj.Avanzar(RefrescoDePrecios.EsperaMinima + TimeSpan.FromSeconds(1));

        Assert.Null(refrescos.EsperarPara());
    }

    [Fact]
    public void Justo_en_el_limite_todavia_no()
    {
        // El limite es «al menos», no «mas de»: a los 119 segundos sigue sin poder y a los 121 si.
        var reloj = new RelojFalso();
        var refrescos = new RefrescoDePrecios(reloj);

        refrescos.EsperarPara();
        reloj.Avanzar(RefrescoDePrecios.EsperaMinima - TimeSpan.FromSeconds(1));

        Assert.NotNull(refrescos.EsperarPara());
    }

    [Fact]
    public void Invalidar_la_cache_sube_la_generacion()
    {
        var refrescos = new RefrescoDePrecios(new RelojFalso());
        var antes = refrescos.Generacion;

        refrescos.InvalidarCache();

        Assert.Equal(antes + 1, refrescos.Generacion);
    }

    [Fact]
    public void Invalidar_no_libera_del_freno()
    {
        // Son dos cosas distintas: tirar la cache no es permiso para volver a llamar a la fuente. Si lo
        // fuera, bastaria invalidar en bucle para machacarla.
        var refrescos = new RefrescoDePrecios(new RelojFalso());

        refrescos.EsperarPara();
        refrescos.InvalidarCache();

        Assert.NotNull(refrescos.EsperarPara());
    }
}
