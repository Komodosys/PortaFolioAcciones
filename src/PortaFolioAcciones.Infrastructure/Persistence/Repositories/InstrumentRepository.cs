using Microsoft.EntityFrameworkCore;
using PortaFolioAcciones.Application.Abstractions;
using PortaFolioAcciones.Application.Instruments;using PortaFolioAcciones.Domain.Entities;
using PortaFolioAcciones.Domain.ValueObjects;
using PortaFolioAcciones.Infrastructure.Persistence;

namespace PortaFolioAcciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// El catálogo de instrumentos.
///
/// Es <b>compartido</b>: un instrumento no es de nadie. Por eso no lleva filtro por persona, y en su
/// lugar lleva una regla distinta: <b>cualquiera puede añadir</b> —hace falta para dar de alta la
/// posición que se acaba de comprar— pero <b>solo el administrador puede modificar</b>, porque cambiar
/// un instrumento afecta a las carteras de todos a la vez.
/// </summary>
internal sealed class InstrumentRepository : IInstrumentRepository
{
    private const int MaxResultados = 50;

    /// <summary>
    /// Tope de filas que se traen para filtrar en memoria. No es un limite de producto: es un cinturon
    /// para que, si el catalogo creciera sin que nadie revisara esto, no se traiga la tabla entera.
    /// </summary>
    private const int MaxCatalogoEnMemoria = 5000;

    private readonly PortafolioDbContext _context;
    private readonly ICurrentUser _currentUser;

    public InstrumentRepository(PortafolioDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<InstrumentSummary>> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        // Se trae el catalogo activo y se filtra EN MEMORIA, y conviene explicar por que.
        //
        // El simbolo es un objeto de valor con conversor, y EF **no sabe traducir** el acceso a sus
        // miembros: `i.Symbol.Value.Contains(...)` revienta en tiempo de ejecucion, y `EF.Property`
        // tampoco sirve porque el parametro acaba pasando por el conversor y no cuadra. Las salidas
        // serian cambiar el mapeo a tipo complejo —tocando el esquema de todo el modelo— o escribir SQL
        // a mano.
        //
        // Para un catalogo familiar, que son los instrumentos que esta casa tiene o ha tenido, son unos
        // cientos de filas: traerlas y filtrar aqui es correcto, simple y cabe de sobra en memoria.
        //
        // SI ALGUN DIA EL CATALOGO PASA DE UNOS MILES: esto hay que cambiarlo por una columna de texto
        // del simbolo, mantenida junto al objeto de valor. Se deja dicho aqui para que no haya que
        // descubrirlo midiendo.
        var instrumentos = await _context.Instruments
            .AsNoTracking()
            .Where(i => i.IsActive)
            .Take(MaxCatalogoEnMemoria)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(query))
        {
            // Se compara en minusculas a proposito: buscar «apple» tiene que encontrar «Apple Inc.».
            var texto = query.Trim().ToLowerInvariant();

            instrumentos = [.. instrumentos.Where(i =>
                i.Symbol.Value.ToLowerInvariant().Contains(texto, StringComparison.Ordinal) ||
                i.Name.ToLowerInvariant().Contains(texto, StringComparison.Ordinal) ||
                (i.BrokerCode?.ToLowerInvariant().Contains(texto, StringComparison.Ordinal) ?? false))];
        }

        return
        [
            .. instrumentos
                .OrderBy(i => i.Symbol.Value, StringComparer.Ordinal)
                .Take(MaxResultados)
                .Select(AResumen)
        ];
    }

    public async Task<InstrumentSummary?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var instrumento = await _context.Instruments
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

        return instrumento is null ? null : AResumen(instrumento);
    }

    public async Task<InstrumentResult> AddAsync(
        CreateInstrumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!TryBuild(command, out var symbol, out var currency, out var error))
        {
            return InstrumentResult.Failed(error);
        }

        var exchange = command.Exchange.Trim().ToUpperInvariant();

        // La identidad de un instrumento es (simbolo, bolsa, moneda), que es justo el indice unico de la
        // tabla. Si ya existe, se devuelve el que hay: dar de alta dos veces el mismo valor es lo que
        // pasa cuando dos personas de la casa compran lo mismo, y lo correcto es que las dos posiciones
        // apunten al mismo instrumento y no a dos copias que acabarian con precios distintos.
        //
        // Se comparan los OBJETOS DE VALOR enteros, no sus miembros: con conversor, EF solo sabe
        // comparar el valor completo, porque traduce la comparacion a la columna.
        var existente = await _context.Instruments
            .SingleOrDefaultAsync(
                i => i.Symbol == symbol!
                     && i.Exchange == exchange
                     && i.QuoteCurrency == currency!,
                cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (existente is not null)
        {
            return InstrumentResult.Ok(AResumen(existente));
        }

        if (!TryParseEnums(command, out var assetType, out var valuationMode, out var frequency, out error))
        {
            return InstrumentResult.Failed(error);
        }

        var instrumento = Instrument.Create(
            symbol!,
            command.Name,
            assetType,
            currency!,
            exchange,
            valuationMode,
            frequency,
            now,
            command.Country);

        // El ISIN se valida en el dominio, con su digito de control. Un ISIN mal tecleado que no se
        // detecta es un dato malo que se queda para siempre.
        if (!string.IsNullOrWhiteSpace(command.Isin))
        {
            try
            {
                instrumento.SetIsin(command.Isin, now);
            }
            catch (ArgumentException)
            {
                return InstrumentResult.Failed("instrument.invalid_isin");
            }
        }

        if (!string.IsNullOrWhiteSpace(command.ProviderKey))
        {
            instrumento.SetProviderKey(command.ProviderKey, now);
        }

        if (!string.IsNullOrWhiteSpace(command.BrokerCode))
        {
            instrumento.SetBrokerCode(command.BrokerCode, now);
        }

        _context.Instruments.Add(instrumento);
        await _context.SaveChangesAsync(cancellationToken);

        return InstrumentResult.Ok(AResumen(instrumento));
    }

    public async Task<InstrumentResult> UpdateAsync(
        Guid id,
        CreateInstrumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Modificar el catalogo compartido es una decision de administracion: el cambio lo notan todas
        // las carteras a la vez.
        if (_currentUser.IsAdmin is false)
        {
            return InstrumentResult.Failed("instrument.not_allowed");
        }

        var instrumento = await _context.Instruments
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (instrumento is null)
        {
            return InstrumentResult.Failed("instrument.not_found");
        }

        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            try
            {
                instrumento.Rename(command.Name, now);
            }
            catch (ArgumentException)
            {
                return InstrumentResult.Failed("instrument.invalid_name");
            }
        }

        if (!string.IsNullOrWhiteSpace(command.ProviderKey))
        {
            instrumento.SetProviderKey(command.ProviderKey, now);
        }

        if (!string.IsNullOrWhiteSpace(command.BrokerCode))
        {
            instrumento.SetBrokerCode(command.BrokerCode, now);
        }

        if (!string.IsNullOrWhiteSpace(command.Isin))
        {
            try
            {
                instrumento.SetIsin(command.Isin, now);
            }
            catch (ArgumentException)
            {
                return InstrumentResult.Failed("instrument.invalid_isin");
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return InstrumentResult.Ok(AResumen(instrumento));
    }

    private static InstrumentSummary AResumen(Instrument instrumento) =>
        new(
            instrumento.Id,
            instrumento.Symbol.Value,
            instrumento.Name,
            instrumento.AssetType.ToString(),
            instrumento.QuoteCurrency.Code,
            instrumento.Exchange,
            instrumento.Isin,
            instrumento.ValuationMode.ToString(),
            instrumento.PricingFrequency.ToString(),
            instrumento.ProviderKey,
            instrumento.BrokerCode,
            instrumento.IsActive,
            [.. instrumento.Splits.Select(split => new SplitSummary(split.Date, split.Ratio))]);

    /// <summary>Comprueba el símbolo y la moneda, que son los dos datos sin los que no hay instrumento.</summary>
    private static bool TryBuild(
        CreateInstrumentCommand command,
        out Ticker? symbol,
        out Currency? currency,
        out string error)
    {
        symbol = null;
        currency = null;
        error = string.Empty;

        try
        {
            symbol = Ticker.From(command.Symbol);
        }
        catch (ArgumentException)
        {
            error = "instrument.invalid_symbol";
            return false;
        }

        try
        {
            currency = Currency.FromCode(command.QuoteCurrency);
        }
        catch (ArgumentException)
        {
            error = "instrument.invalid_currency";
            return false;
        }

        return true;
    }

    /// <summary>Convierte los textos que llegan de fuera a los enumerados del dominio.</summary>
    private static bool TryParseEnums(
        CreateInstrumentCommand command,
        out AssetType assetType,
        out ValuationMode valuationMode,
        out PricingFrequency frequency,
        out string error)
    {
        error = string.Empty;

        // Se inicializan antes de nada: si el primer analisis falla y se sale, el compilador exige que
        // todos los parametros de salida lleven un valor. Dejarlos sin asignar seria devolver basura.
        assetType = default;
        valuationMode = default;
        frequency = default;

        if (!Enum.TryParse(command.AssetType, ignoreCase: true, out assetType))
        {
            error = "instrument.invalid_asset_type";
            return false;
        }

        if (!Enum.TryParse(command.ValuationMode, ignoreCase: true, out valuationMode))
        {
            error = "instrument.invalid_valuation_mode";
            return false;
        }

        if (!Enum.TryParse(command.PricingFrequency, ignoreCase: true, out frequency))
        {
            error = "instrument.invalid_pricing_frequency";
            return false;
        }

        return true;
    }
}
