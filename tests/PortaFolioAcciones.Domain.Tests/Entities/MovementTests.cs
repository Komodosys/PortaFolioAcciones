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
        var venta = Movement.Sale(Cartera, Posicion, Quantity.Of(10m), Dolar(50m), Dia, null, Ahora);

        Assert.Equal(500m, venta.Amount.Amount);
        Assert.Equal("USD", venta.Amount.Currency.Code);
        Assert.Equal(10m, venta.Quantity?.Value);
        Assert.Equal(Posicion, venta.PositionId);
    }

    [Fact]
    public void Una_venta_sin_cantidad_positiva_se_rechaza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Movement.Sale(Cartera, Posicion, Quantity.Of(0m), Dolar(50m), Dia, null, Ahora));
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
            () => Movement.Sale(Guid.Empty, Posicion, Quantity.Of(1m), Dolar(1m), Dia, null, Ahora));
    }

    [Fact]
    public void Solo_las_aportaciones_y_los_retiros_son_dinero()
    {
        Assert.True(Movement.Deposit(Cartera, Canadiense(1m), Dia, null, Ahora).IsCash);
        Assert.True(Movement.Withdrawal(Cartera, Canadiense(1m), Dia, null, Ahora).IsCash);
        Assert.False(Movement.Sale(Cartera, Posicion, Quantity.Of(1m), Dolar(1m), Dia, null, Ahora).IsCash);
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
    public void Una_venta_no_cambia_lo_aportado()
    {
        // Es la prueba que más importa de este fichero. Una venta no aporta dinero: el dinero ya estaba
        // dentro y solo cambió de forma. Contarla como aportación haría que un informe de rentabilidad
        // dijera que se ha puesto mucho más de lo que se puso, y la ganancia saldría mal.
        var venta = Movement.Sale(Cartera, Posicion, Quantity.Of(10m), Dolar(50m), Dia, null, Ahora);

        Assert.True(venta.CashFlow().IsZero);
    }
}
