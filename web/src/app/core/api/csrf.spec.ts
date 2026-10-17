import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Csrf } from './csrf';

/**
 * El testigo CSRF.
 *
 * Es la pieza con mas riesgo de todo el cliente: si se pide de mas, cada accion lleva una ida y vuelta
 * extra; si se guarda el que no toca, **toda accion despues de entrar falla** con un error que no tiene
 * nada que ver con lo que la persona esta haciendo. Ese fallo aparecio de verdad en el servidor.
 */
describe('Csrf', () => {
  let csrf: Csrf;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    csrf = TestBed.inject(Csrf);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('la primera vez lo pide a la API', async () => {
    const promesa = csrf.asegurar();
    http.expectOne('/api/auth/csrf').flush({ token: 'T1' });

    expect(await promesa).toBe('T1');
    expect(csrf.actual()).toBe('T1');
  });

  it('la segunda vez NO vuelve a pedirlo', async () => {
    const primera = csrf.asegurar();
    http.expectOne('/api/auth/csrf').flush({ token: 'T1' });
    await primera;

    expect(await csrf.asegurar()).toBe('T1');

    // Si esto fallara, cada accion de la pantalla gastaria una peticion de mas.
    http.expectNone('/api/auth/csrf');
  });

  it('dos peticiones a la vez comparten UNA sola', async () => {
    const primera = csrf.asegurar();
    const segunda = csrf.asegurar();

    const peticiones = http.match('/api/auth/csrf');

    // Sin esto, abrir dos pantallas a la vez pediria dos testigos y el segundo pisaria al primero, con lo
    // que la peticion que ya iba en camino llevaria un testigo que acaba de dejar de valer.
    expect(peticiones.length).toBe(1);

    peticiones[0].flush({ token: 'T1' });

    expect(await primera).toBe('T1');
    expect(await segunda).toBe('T1');
  });

  it('guardar nulo lo olvida, y la siguiente vez lo vuelve a pedir', async () => {
    const primera = csrf.asegurar();
    http.expectOne('/api/auth/csrf').flush({ token: 'T1' });
    await primera;

    // Es lo que pasa al cerrar sesion: el testigo pertenecia a la sesion que acaba de terminar.
    csrf.guardar(null);
    expect(csrf.actual()).toBeNull();

    const segunda = csrf.asegurar();
    http.expectOne('/api/auth/csrf').flush({ token: 'T2' });

    expect(await segunda).toBe('T2');
  });

  it('acepta directamente el testigo que devuelve la API al entrar', async () => {
    // La API devuelve uno nuevo en la respuesta del inicio de sesion porque el anterior, pedido estando
    // sin sesion, deja de valer en cuanto se entra.
    csrf.guardar('EL-NUEVO');

    expect(await csrf.asegurar()).toBe('EL-NUEVO');
    http.expectNone('/api/auth/csrf');
  });
});
