using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Domain.Tests.Entities;

/// <summary>
/// Los movimientos de una cartera: dinero que entra, dinero que sale y ventas.
///
/// Lo que se prueba no es que guarden campos, sino las tres cosas que hacen que sirvan para calcular algo:
/// que una venta <b>calcula</b> su importe en vez de aceptarlo, que un importe no puede ser cero ni
/// negativo, y sobre todo que <b>una venta no cuenta como dinero aportado</b>.
/// </summary>
public sealed class MovementTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Cartera = Guid.NewGuid();
    private static readonly Guid Posicion = Guid.NewGuid();
    private static readonly DateOnly Dia = new(2026, 10, 3);

    private static Money Dolar(decimal importe) => Money.Of(importe, Currency.Usd);
    private static Money Canadiense(decimal importe) => Money.Of(importe, Currency.Cad);

    [Fact]
    public void Una_aportacion_guarda_el_dinero_y_la_fecha()
    {
        var movimiento = Movement.Deposit(Cartera, Canadiense(5_000m), Dia, "traspaso", Ahora);

        Assert.Equal(MovementKind.Deposit, movimiento.Kind);
        Assert.Equal(5_000m, movimiento.Amount.Amount);
        Assert.Equal(Dia, movimiento.OccurredOn);
        Assert.Null(movimiento.PositionId);
        Assert.Null(movimiento.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Un_importe_que_no_es_positivo_se_rechaza(int importe)
    {
        // Cero no es una aportación de cero: es un error de quien lo teclea. Aceptarlo llenaría el registro de
        // filas que no significan nada y que después hay que limpiar a mano.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Movement.Deposit(Cartera, Canadiense(importe), Dia, null, Ahora));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Movement.Withdrawal(Cartera, Canadiense(importe), Dia, null, Ahora));
    }

    [Fact]
    public void Una_venta_calcula_el_importe()
    {
        var venta = Movement.Sale(Cartera, Posicion, Quantity.Of(10m), Dolar(50m), Dolar(40m), Dia, null, Ahora);

        Assert.Equal(500m, venta.Amount.Amount);
        Assert.Equal("USD", venta.Amount.Currency.Code);
        Assert.Equal(10m, venta.Quantity?.Value);
        Assert.Equal(Posicion, venta.PositionId);
    }

    [Fact]
    public void Una_venta_sin_cantidad_positiva_se_rechaza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Movement.Sale(Cartera, Posicion, Quantity.Of(0m), Dolar(50m), Dolar(40m), Dia, null, Ahora));
    }

    [Fact]
    public void Un_movimiento_sin_cartera_se_rechaza_en_los_tres_tipos()
    {
        // La primera version solo lo comprobaba en la venta. Se comprueba en un solo sitio para que no se
        // olvide otra vez.
        Assert.Throws<ArgumentException>(
            () => Movement.Deposit(Guid.Empty, Canadiense(1m), Dia, null, Ahora));

        Assert.Throws<ArgumentException>(
            () => Movement.Withdrawal(Guid.Empty, Canadiense(1m), Dia, null, Ahora));

        Assert.Throws<ArgumentException>(
            () => Movement.Sale(Guid.Empty, Posicion, Quantity.Of(1m), Dolar(1m), Dolar(1m), Dia, null, Ahora));
    }

    [Fact]
    public void Solo_las_aportaciones_y_los_retiros_son_dinero()
    {
        Assert.True(Movement.Deposit(Cartera, Canadiense(1m), Dia, null, Ahora).IsCash);
        Assert.True(Movement.Withdrawal(Cartera, Canadiense(1m), Dia, null, Ahora).IsCash);
        Assert.False(Movement.Sale(Cartera, Posicion, Quantity.Of(1m), Dolar(1m), Dolar(1m), Dia, null, Ahora).IsCash);
    }

    [Fact]
    public void Un_traspaso_tambien_es_dinero_que_se_mueve()
    {
        // Entra o sale dinero de la cuenta, asi que para el SALDO es dinero. Lo que no cambia es lo aportado, y
        // esa es la prueba siguiente.
        Assert.True(Movement.TransferIn(Cartera, Canadiense(1m), Dia, null, Ahora).IsCash);
        Assert.True(Movement.TransferOut(Cartera, Canadiense(1m), Dia, null, Ahora).IsCash);
    }

    [Fact]
    public void Un_traspaso_NO_cambia_lo_aportado()
    {
        // Es la razon de existir del tipo: si fuera un deposito, el informe diria que se puso dinero que no se
        // puso, y la ganancia saldria corta justo en lo que valga el traspaso. En una cuenta de jubilacion no es
        // un matiz: una aportacion consume espacio de cotizacion y un traspaso no.
        var recibido = Movement.TransferIn(Cartera, Canadiense(12_064.86m), Dia, "traspaso desde otra cuenta", Ahora);
        var enviado = Movement.TransferOut(Cartera, Canadiense(900m), Dia, "traspaso a otra cuenta", Ahora);

        Assert.True(recibido.CashFlow().IsZero);
        Assert.True(enviado.CashFlow().IsZero);

        // Y para que la prueba signifique algo, el mismo importe como aportacion SI mueve lo aportado.
        Assert.Equal(12_064.86m, Movement.Deposit(Cartera, Canadiense(12_064.86m), Dia, null, Ahora).CashFlow().Amount);
        Assert.Equal(-900m, Movement.Withdrawal(Cartera, Canadiense(900m), Dia, null, Ahora).CashFlow().Amount);
    }

    [Fact]
    public void Un_traspaso_exige_importe_mayor_que_cero()
    {
        // El signo lo pone el tipo, no el importe: un traspaso «de menos 900» seria un traspaso en la direccion
        // contraria escrito al reves, y dos formas de decir lo mismo acaban discrepando.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Movement.TransferIn(Cartera, Canadiense(0m), Dia, null, Ahora));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Movement.TransferOut(Cartera, Canadiense(-1m), Dia, null, Ahora));
    }

    [Fact]
    public void El_flujo_de_caja_suma_lo_que_entra_y_resta_lo_que_sale()
    {
        var aportacion = Movement.Deposit(Cartera, Canadiense(5_000m), Dia, null, Ahora);
        var retiro = Movement.Withdrawal(Cartera, Canadiense(1_200m), Dia, null, Ahora);

        Assert.Equal(5_000m, aportacion.CashFlow().Amount);
        Assert.Equal(-1_200m, retiro.CashFlow().Amount);
    }

    [Fact]
    public void La_ganancia_realizada_es_lo_cobrado_menos_lo_que_costo()
    {
        var venta = Movement.Sale(Cartera, Posicion, Quantity.Of(10m), Dolar(50m), Dolar(40m), Dia, null, Ahora);

        Assert.Equal(100m, venta.RealizedGain()?.Amount);
        Assert.Equal("USD", venta.RealizedGain()?.Currency.Code);
    }

    [Fact]
    public void Una_venta_a_perdida_da_ganancia_negativa()
    {
        // Negativa y no cero: una pérdida es una pérdida, y esconderla con un cero sería mentir en el informe.
        var venta = Movement.Sale(Cartera, Posicion, Quantity.Of(10m), Dolar(30m), Dolar(40m), Dia, null, Ahora);

        Assert.Equal(-100m, venta.RealizedGain()?.Amount);
    }

    [Fact]
    public void Una_aportacion_no_tiene_ganancia_realizada()
    {
        // Nula y no cero: una aportación no gana ni pierde nada, y un cero diría que se vendió sin ganar.
        Assert.Null(Movement.Deposit(Cartera, Canadiense(1_000m), Dia, null, Ahora).RealizedGain());
    }

    [Fact]
    public void Una_venta_con_coste_negativo_se_rechaza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Movement.Sale(Cartera, Posicion, Quantity.Of(1m), Dolar(50m), Dolar(-1m), Dia, null, Ahora));
    }
    [Fact]
    public void Una_apertura_no_cuenta_como_aportacion()
    {
        // El saldo inicial no es dinero que se aportara: es el punto de partida de una cuenta que ya tenia
        // dinero. Contarlo como aportacion haria que el informe dijera que se puso mucho mas de lo que se puso.
        var apertura = Movement.Opening(Cartera, Canadiense(3_500m), Dia, "lo que habia", Ahora);

        Assert.Equal(MovementKind.Opening, apertura.Kind);
        Assert.True(apertura.CashFlow().IsZero);
    }

    [Fact]
    public void Un_movimiento_en_la_moneda_de_la_cartera_no_necesita_tipo()
    {
        var aportacion = Movement.Deposit(Cartera, Canadiense(1_000m), Dia, null, Ahora);

        Assert.Null(aportacion.RateUsed);
        Assert.Equal(1_000m, aportacion.AmountIn(Currency.Cad)?.Amount);
    }

    [Fact]
    public void Un_movimiento_en_otra_moneda_se_lleva_a_la_de_la_cartera_con_su_tipo()
    {
        // Una venta en dolares, en una cartera en dolares canadienses. El efectivo que mueve son los CAD que
        // entraron en la cuenta, no los USD que se cobraron.
        var venta = Movement.Sale(
            Cartera, Posicion, Quantity.Of(10m), Dolar(50m), Dolar(40m), Dia, null, Ahora, rateUsed: 1.4m);

        Assert.Equal(700m, venta.AmountIn(Currency.Cad)?.Amount);
        Assert.Equal("CAD", venta.AmountIn(Currency.Cad)?.Currency.Code);

        // Y el importe propio sigue siendo el que se cobro, en su moneda.
        Assert.Equal(500m, venta.Amount.Amount);
    }

    [Fact]
    public void Sin_tipo_no_se_inventa_la_conversion()
    {
        // Nulo y no el importe tal cual: devolver 500 «CAD» cuando son 500 USD seria un saldo falso con
        // aspecto de exacto.
        var venta = Movement.Sale(Cartera, Posicion, Quantity.Of(10m), Dolar(50m), Dolar(40m), Dia, null, Ahora);

        Assert.Null(venta.AmountIn(Currency.Cad));
    }
    [Fact]
    public void Una_venta_no_cambia_lo_aportado()
    {
        // Es la prueba que más importa de este fichero. Una venta no aporta dinero: el dinero ya estaba
        // dentro y solo cambió de forma. Contarla como aportación haría que un informe de rentabilidad
        // dijera que se ha puesto mucho más de lo que se puso, y la ganancia saldría mal.
        var venta = Movement.Sale(Cartera, Posicion, Quantity.Of(10m), Dolar(50m), Dolar(40m), Dia, null, Ahora);

        Assert.True(venta.CashFlow().IsZero);
    }
}
