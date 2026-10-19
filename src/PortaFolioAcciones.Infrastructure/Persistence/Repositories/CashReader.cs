using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Portfolios;
using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Calcula el saldo de efectivo de una cartera.
///
/// **No lo guarda.** Se suma cada vez: un saldo almacenado puede dejar de coincidir con los movimientos que lo
/// produjeron, y entonces hay dos verdades. Calculado, cuadra por construcción.
/// </summary>
/// <remarks>
/// <b>La apertura es «lo que habías puesto», no «el efectivo que había».</b> Esa distinción es la que hace que
/// el cálculo salga bien. Si la apertura fuera solo el dinero de la cuenta, las posiciones que ya existían no
/// estarían pagadas por nadie y el efectivo saldría inflado justo en lo que costaron.
///
/// Con la apertura entendida así, el saldo es:
///
/// <code>
/// efectivo = apertura + aportaciones − retiros + traspasos recibidos − traspasos enviados
///            + ventas + dividendos − comisiones − TODO lo comprado
/// </code>
///
/// Y entonces lo ganado —total menos aportado neto— sale igual a la ganancia no realizada más la realizada,
/// que es la comprobación de que el modelo está bien montado.
///
/// **Los traspasos se suman al efectivo y son aparte de las aportaciones.** El dinero se movió —por eso entran
/// en esta cuenta—, pero no lo puso nadie: ya era del titular, en otra cuenta. Por eso no van dentro de
/// <c>aportado</c>, que es lo que hace de contrapeso del total para calcular lo ganado.
/// </remarks>
internal sealed class CashReader : ICashReader
{
    private readonly PortafolioDbContext _context;

    public CashReader(PortafolioDbContext context)
    {
        _context = context;
    }

    public async Task<CashBalanceSummary?> GetAsync(
        Guid portfolioId,
        CancellationToken cancellationToken = default)
    {
        var cartera = await _context.Portfolios
            .AsNoTracking()
            .SingleOrDefaultAsync(portfolio => portfolio.Id == portfolioId, cancellationToken);

        if (cartera is null)
        {
            return null;
        }

        var moneda = cartera.BaseCurrency ?? await BaseDelDuenoAsync(cartera.OwnerUserId, cancellationToken);

        var movimientos = await _context.Movements
            .AsNoTracking()
            .Where(movimiento => movimiento.PortfolioId == portfolioId)
            .ToListAsync(cancellationToken);

        // La apertura: la última declarada. Si se corrige, se declara otra vez y vale la nueva.
        var apertura = movimientos
            .Where(movimiento => movimiento.Kind is MovementKind.Opening)
            .OrderByDescending(movimiento => movimiento.OccurredOn)
            .ThenByDescending(movimiento => movimiento.CreatedAtUtc)
            .FirstOrDefault();

        var desde = apertura?.OccurredOn;
        var sinConvertir = 0;

        // Solo lo que paso DESDE la apertura. Un deposito anterior ya esta dentro de lo declarado, y sumarlo
        // otra vez lo contaria dos veces: el mismo fallo que tenia la apertura consigo misma.
        decimal SumaDesde(MovementKind tipo)
        {
            var total = 0m;

            foreach (var movimiento in movimientos.Where(m => m.Kind == tipo))
            {
                if (desde is { } dia && movimiento.OccurredOn < dia)
                {
                    continue;
                }

                if (movimiento.AmountIn(moneda) is { } importe)
                {
                    total += importe.Amount;
                }
                else
                {
                    sinConvertir++;
                }
            }

            return total;
        }

        // **Una sola apertura, la ultima.** Sumarlas seria sumar la correccion a lo corregido: declarar dos
        // veces el mismo dinero lo contaria dos veces, y el saldo saldria al doble.
        var declarado = 0m;

        if (apertura is not null)
        {
            if (apertura.AmountIn(moneda) is { } importeDeLaApertura)
            {
                declarado = importeDeLaApertura.Amount;
            }
            else
            {
                sinConvertir++;
            }
        }
        var aportado = SumaDesde(MovementKind.Deposit);
        var retirado = SumaDesde(MovementKind.Withdrawal);

        // Los traspasos mueven efectivo igual que una aportacion o un retiro, pero NO son dinero puesto: el
        // dinero ya era del titular y solo cambio de cuenta. Se suman aqui, y se dejan FUERA de `aportado`, que
        // es contra lo que se mide lo ganado.
        var transferidoEntrada = SumaDesde(MovementKind.TransferIn);
        var transferidoSalida = SumaDesde(MovementKind.TransferOut);

        var vendido = SumaDesde(MovementKind.Sale);

        // Un dividendo entra y una comision sale. Ninguno es dinero aportado: el dividendo es ganancia y la
        // comision es un coste, y por eso no pasan por lo aportado sino por el efectivo.
        var dividendos = SumaDesde(MovementKind.Dividend);
        var comisiones = SumaDesde(MovementKind.Commission);

        // Lo comprado: lo que todavía se tiene, MÁS lo que se vendió.
        //
        // Lo que se tiene son los lotes; lo vendido ya no está en los lotes, pero su coste salió de la cuenta
        // igual, y sin contarlo el efectivo saldría alto en justo esa cantidad.
        var comprado = 0m;

        // Lo que queda en cartera.
        var lotes = await (
                from lote in _context.Lots.AsNoTracking()
                join posicion in _context.Positions.AsNoTracking() on lote.PositionId equals posicion.Id
                where posicion.PortfolioId == portfolioId
                select lote)
            .ToListAsync(cancellationToken);

        foreach (var lote in lotes)
        {
            if (lote.CostIn(moneda) is { } coste)
            {
                comprado += coste.Amount;
            }
            else
            {
                sinConvertir++;
            }
        }

        // Y lo ya vendido, con el coste que tenía cuando se vendió.
        foreach (var venta in movimientos.Where(m => m.Kind is MovementKind.Sale))
        {
            if (venta.Quantity is { } unidades && venta.CostPerUnit is { } costeUnitario)
            {
                var costeVendido = unidades * costeUnitario;

                if (costeVendido.Currency == moneda)
                {
                    comprado += costeVendido.Amount;
                }
                else if (venta.RateUsed is { } tipo)
                {
                    comprado += costeVendido.Amount * tipo;
                }
                else
                {
                    sinConvertir++;
                }
            }
            else
            {
                // Una venta sin coste guardado: no se sabe cuánto costó lo vendido. Se cuenta como no
                // convertida y el saldo dirá que no se puede dar como bueno.
                sinConvertir++;
            }
        }

        // ---------------------------------------------------------------------------------------------
        // El saldo de verdad: una bolsa por moneda.
        //
        // La suma de los flujos de arriba —cada uno al tipo de su dia— **no es el saldo de una cuenta con dos
        // monedas**: mezcla el dinero que entro en coronas con el que salio en dolares como si fueran la misma
        // bolsa. En la cuenta real eso daba a la aplicacion unos 21.500 CAD de dinero que ya no estaba.
        //
        // La cuenta tiene dos bolsas. El saldo es el de cada una, y el total es la suma llevada al cambio de hoy,
        // que es lo que hace el broker cuando informa de un solo saldo combinado.
        // ---------------------------------------------------------------------------------------------
        var bolsas = await BolsasAsync(movimientos, desde, moneda, cancellationToken);

        var saldo = bolsas.Total;

        // La vista en dolares, solo informativa. Se calcula al cambio de hoy y no se guarda: manana valdra otra
        // cosa, y guardarla seria tener un numero que envejece sin decirlo. El tipo se ensena al lado para que la
        // cifra se pueda comprobar.
        decimal? saldoEnUsd = null;
        decimal? tipoDeHoy = null;

        if (moneda != Currency.Usd)
        {
            var hoy = await _context.DailyFxRates
                .AsNoTracking()
                .Where(tipo => tipo.From == Currency.Usd && tipo.To == moneda)
                .OrderByDescending(tipo => tipo.RateDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (hoy is { Rate: > 0m })
            {
                tipoDeHoy = hoy.Rate;
                saldoEnUsd = saldo / hoy.Rate;
            }
        }

        return new CashBalanceSummary(
            portfolioId,
            moneda.Code,
            declarado,
            aportado,
            retirado,
            vendido,
            comprado,
            saldo,
            desde,
            sinConvertir + bolsas.SinConvertir,
            dividendos,
            comisiones,
            saldoEnUsd,
            tipoDeHoy,
            transferidoEntrada,
            transferidoSalida,
            bolsas.Detalle);
    }

    /// <summary>
    /// El saldo de cada bolsa de moneda, y el total llevado al cambio de hoy.
    /// </summary>
    /// <remarks>
    /// <b>Las compras van en la moneda del instrumento, no en la de la cartera.</b> Es el fondo del asunto: quien
    /// compra en dólares gasta dólares, y apuntarlo como si gastara coronas deja la bolsa de coronas llena para
    /// siempre. Se resta de la bolsa en la que de verdad se pagó.
    /// </remarks>
    private async Task<(decimal Total, int SinConvertir, IReadOnlyList<CashBalanceByCurrency> Detalle)> BolsasAsync(
        IReadOnlyList<Movement> movimientos,
        DateOnly? desde,
        Currency moneda,
        CancellationToken cancellationToken)
    {
        var bolsas = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var sinConvertir = 0;

        void Sumar(Currency divisa, decimal importe) =>
            bolsas[divisa.Code] = bolsas.GetValueOrDefault(divisa.Code) + importe;

        // Lo que entra y sale, pata a pata. Un cambio de moneda aporta dos patas, una por bolsa.
        foreach (var movimiento in movimientos)
        {
            if (desde is { } dia && movimiento.OccurredOn < dia)
            {
                continue;
            }

            foreach (var (divisa, importe) in movimiento.CashLegs())
            {
                Sumar(divisa, importe);
            }
        }

        // Lo que se pagó por lo que se tiene, en la moneda en que se pagó.
        var cartera = movimientos.Count > 0 ? movimientos[0].PortfolioId : Guid.Empty;

        var lotes = await (
                from lote in _context.Lots.AsNoTracking()
                join posicion in _context.Positions.AsNoTracking() on lote.PositionId equals posicion.Id
                where posicion.PortfolioId == cartera
                select lote)
            .ToListAsync(cancellationToken);

        foreach (var lote in lotes)
        {
            Sumar(lote.CostPerUnit.Currency, -(lote.Quantity.Value * lote.CostPerUnit.Amount));
        }

        // Y lo que se pagó por lo que ya se vendió, que salió de la cuenta igual.
        foreach (var venta in movimientos.Where(m => m.Kind is MovementKind.Sale))
        {
            if (venta.Quantity is { } unidades && venta.CostPerUnit is { } costeUnitario)
            {
                Sumar(costeUnitario.Currency, -(unidades.Value * costeUnitario.Amount));
            }
            else
            {
                sinConvertir++;
            }
        }

        // El cambio de hoy para cada bolsa, en una sola consulta. La agrupación se hace en memoria y no en SQL:
        // el «ultimo tipo por moneda» no se traduce igual en Postgres que en SQLite, y una consulta que solo
        // funciona en uno de los dos motores es una prueba que no prueba nada.
        var tipos = (await _context.DailyFxRates
                .AsNoTracking()
                .Where(tipo => tipo.To == moneda)
                .ToListAsync(cancellationToken))
            .GroupBy(tipo => tipo.From.Code)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.OrderByDescending(tipo => tipo.RateDate).First().Rate);

        var detalle = new List<CashBalanceByCurrency>(bolsas.Count);
        var total = 0m;

        foreach (var (divisa, importe) in bolsas.OrderBy(par => par.Key, StringComparer.Ordinal))
        {
            decimal? tipo = divisa == moneda.Code ? 1m : tipos.GetValueOrDefault(divisa);

            if (tipo is null or 0m)
            {
                sinConvertir++;
                detalle.Add(new CashBalanceByCurrency(divisa, importe, null, null));
                continue;
            }

            var enBase = importe * tipo.Value;
            total += enBase;
            detalle.Add(new CashBalanceByCurrency(divisa, importe, tipo, enBase));
        }

        return (total, sinConvertir, detalle);
    }

    private async Task<Currency> BaseDelDuenoAsync(Guid dueno, CancellationToken cancellationToken)
    {
        var baseDelPerfil = await _context.UserProfiles
            .AsNoTracking()
            .Where(perfil => perfil.Id == dueno)
            .Select(perfil => (Currency?)perfil.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);

        return baseDelPerfil ?? Currency.Cad;
    }
}
