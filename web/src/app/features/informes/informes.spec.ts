import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Informes } from './informes';

/**
 * Los filtros del informe.
 *
 * Existe por un fallo real: los filtros estaban como **campos normales** y se leían dentro de un `computed`.
 * Un `computed` solo se recalcula cuando cambia una **señal**, así que cambiar el desplegable no recalculaba
 * nada y las cifras se quedaban quietas. El compilador no lo caza y la pantalla parece correcta: los
 * desplegables cambian, pero los totales no.
 *
 * Esta prueba habría fallado en cuanto el filtro dejara de ser una señal.
 */
describe('Informes', () => {
  let informe: Informes;
  let http: HttpTestingController;

  const filas = [
    { portfolioId: 'a', name: 'A', ownerDisplayName: 'Titular A', registrationType: 'RegisteredRetirement', currency: 'CAD', marketValue: 100, cashValue: 10, costBasis: 50, unrealizedGain: 50, contributed: 80, withdrawn: 0, realizedGain: null, snapshotDate: '2026-10-18', hasOpening: true },
    { portfolioId: 'b', name: 'B', ownerDisplayName: 'Titular A', registrationType: 'RegisteredSavings', currency: 'CAD', marketValue: 200, cashValue: 0, costBasis: 100, unrealizedGain: 100, contributed: 150, withdrawn: 0, realizedGain: null, snapshotDate: '2026-10-18', hasOpening: true },
    { portfolioId: 'c', name: 'C', ownerDisplayName: 'Titular B', registrationType: 'RegisteredRetirement', currency: 'CAD', marketValue: 300, cashValue: 0, costBasis: 200, unrealizedGain: 100, contributed: 250, withdrawn: 0, realizedGain: null, snapshotDate: '2026-10-18', hasOpening: true },
  ];

  beforeEach(async () => {
    TestBed.configureTestingModule({
      // El componente va como proveedor para poder instanciarlo sin renderizar la plantilla: lo que se prueba
      // es su logica de filtrado, no como se pinta.
      providers: [Informes, provideHttpClient(), provideHttpClientTesting()],
    });

    informe = TestBed.inject(Informes);
    http = TestBed.inject(HttpTestingController);

    // La primera carga, la de la construccion.
    http.expectOne('/api/reports/performance').flush({ rows: filas, totals: null, currencies: ['CAD'] });
    await new Promise((listo) => setTimeout(listo, 0));
  });

  afterEach(() => {
    http.verify();

    // Sin esto, vitest no reinicia el TestBed entre pruebas y la segunda falla con un error que no se parece
    // a la causa. Esta documentado en la prueba de la sesion, que se dio de bruces con lo mismo.
    TestBed.resetTestingModule();
  });

  it('sin filtros los enseña todos', () => {
    expect(informe.filas().length).toBe(3);
  });

  it('filtrar por persona cambia las filas', () => {
    // Esta es la que habria cazado el fallo: con un campo normal, `filas()` seguiria devolviendo 3.
    informe.persona.set('Titular B');

    expect(informe.filas().length).toBe(1);
    expect(informe.filas()[0].portfolioId).toBe('c');
  });

  it('filtrar por tipo cambia las filas', () => {
    informe.tipo.set('RegisteredRetirement');

    expect(informe.filas().length).toBe(2);
  });

  it('los dos filtros se combinan', () => {
    informe.persona.set('Titular A');
    informe.tipo.set('RegisteredSavings');

    expect(informe.filas().length).toBe(1);
    expect(informe.filas()[0].portfolioId).toBe('b');
  });

  it('los totales se recalculan sobre lo filtrado', () => {
    expect(informe.totales()?.valor).toBe(600);

    informe.persona.set('Titular B');

    // Solo la cartera C: 300 en posiciones.
    expect(informe.totales()?.valor).toBe(300);
  });

  it('limpiar los filtros vuelve a enseñarlos todos', () => {
    informe.persona.set('Titular B');
    informe.tipo.set('RegisteredRetirement');

    informe.limpiarFiltros();

    expect(informe.filas().length).toBe(3);
  });
});
