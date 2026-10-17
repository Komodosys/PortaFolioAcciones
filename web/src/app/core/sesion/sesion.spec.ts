import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { of } from 'rxjs';
import { Csrf } from '../api/csrf';
import { Sesion } from './sesion';

/**
 * Un servicio de idiomas de mentira que **apunta** los idiomas que se le piden.
 *
 * Se escribe a mano y no con un espia de la libreria de pruebas a proposito: importar utilidades de
 * `vitest` en este fichero impide que Angular reinicie el `TestBed` entre pruebas, y el fallo que
 * produce —«el modulo ya esta instanciado»— no se parece en nada a la causa.
 */
function idiomasFalsos() {
  const pedidos: string[] = [];

  return {
    pedidos,
    setActiveLang: (codigo: string) => {
      pedidos.push(codigo);
    },
    getActiveLang: () => 'es',
    langChanges$: of('es'),
  };
}

const PERFIL = {
  id: '11111111-1111-1111-1111-111111111111',
  displayName: 'Persona De Ejemplo',
  baseCurrency: 'CAD',
  language: 'fr',
  isAdmin: false,
};

describe('Sesion', () => {
  let sesion: Sesion;
  let csrf: Csrf;
  let http: HttpTestingController;
  let idiomas: ReturnType<typeof idiomasFalsos>;

  beforeEach(() => {
    idiomas = idiomasFalsos();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: TranslocoService, useValue: idiomas },
      ],
    });

    sesion = TestBed.inject(Sesion);
    csrf = TestBed.inject(Csrf);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /**
   * Deja correr lo que estaba esperando.
   *
   * Hace falta porque entre una respuesta y la peticion siguiente **hay un salto**: al entrar, la peticion
   * del perfil no se emite hasta que se resuelve la del inicio de sesion, y eso ocurre en el siguiente
   * turno. Sin esperar, la prueba pediria una peticion que todavia no existe.
   */
  function siguienteTurno(): Promise<void> {
    return new Promise((resolver) => setTimeout(resolver, 0));
  }

  /** Responde al inicio de sesion y a la peticion del perfil. */
  async function responderEntrada(
    csrfToken: string,
    mustChangePassword = false,
    perfil: typeof PERFIL = PERFIL,
  ): Promise<void> {
    http.expectOne('/api/auth/login').flush({ status: 'ok', csrfToken, mustChangePassword });

    await siguienteTurno();

    http.expectOne('/api/auth/me').flush(perfil);
  }

  it('al entrar guarda el testigo NUEVO que devuelve la API', async () => {
    const promesa = sesion.entrar('ana', 'clave');
    await responderEntrada('TESTIGO-NUEVO');

    expect(await promesa).toBe('ok');

    // Es lo importante de esta prueba. El testigo pedido estando sin sesion deja de valer al entrar, asi
    // que si se guardara el viejo, la primera accion despues de entrar fallaria con «csrf.invalid» aunque
    // la sesion funcionara — un error desconcertante que aparecio de verdad.
    expect(csrf.actual()).toBe('TESTIGO-NUEVO');
  });

  it('al entrar deja la sesion abierta con el perfil', async () => {
    const promesa = sesion.entrar('ana', 'clave');
    await responderEntrada('T1');

    await promesa;

    expect(sesion.haySesion()).toBe(true);
    expect(sesion.usuario()?.displayName).toBe('Persona De Ejemplo');
    expect(sesion.esAdministrador()).toBe(false);
  });

  it('al entrar pone el idioma de la persona, no el de la instalacion', async () => {
    const promesa = sesion.entrar('ana', 'clave');
    await responderEntrada('T1');

    await promesa;

    // El perfil dice frances, asi que la interfaz tiene que cambiar sola.
    expect(idiomas.pedidos).toContain('fr');
  });

  it('avisa cuando la contrasena es temporal', async () => {
    const promesa = sesion.entrar('ana', 'temporal');
    await responderEntrada('T1', true);

    await promesa;

    expect(sesion.debeCambiarContrasena()).toBe(true);
  });

  it('traduce el fallo de credenciales a un codigo, no a un texto', async () => {
    const promesa = sesion.entrar('ana', 'mal');
    http
      .expectOne('/api/auth/login')
      .flush(
        { title: 'auth.invalid_credentials', status: 401 },
        { status: 401, statusText: 'Unauthorized' },
      );

    // Un codigo: el texto lo pone la pantalla en el idioma de la persona.
    expect(await promesa).toBe('credenciales');
    expect(sesion.haySesion()).toBe(false);
  });

  it('distingue la cuenta bloqueada de las credenciales mal', async () => {
    const promesa = sesion.entrar('ana', 'mal');
    http
      .expectOne('/api/auth/login')
      .flush({ title: 'auth.locked_out' }, { status: 423, statusText: 'Locked' });

    // Son dos cosas distintas y se arreglan de forma distinta: una es esperar y la otra es recordar la
    // contrasena. Decir «credenciales incorrectas» a quien esta bloqueado le hace intentarlo mas.
    expect(await promesa).toBe('bloqueado');
  });

  it('distingue la contrasena temporal caducada', async () => {
    const promesa = sesion.entrar('ana', 'temporal');
    http
      .expectOne('/api/auth/login')
      .flush(
        { title: 'auth.temporary_password_expired' },
        { status: 401, statusText: 'Unauthorized' },
      );

    expect(await promesa).toBe('contrasenaCaducada');
  });

  it('al salir lo olvida todo, incluido el testigo', async () => {
    const entrada = sesion.entrar('ana', 'clave');
    await responderEntrada('T1');
    await entrada;

    const salida = sesion.salir();
    http.expectOne('/api/auth/logout').flush({});

    await salida;

    expect(sesion.haySesion()).toBe(false);
    expect(sesion.debeCambiarContrasena()).toBe(false);
    expect(csrf.actual()).toBeNull();
  });

  it('si la API falla al salir, la sesion local se cierra igual', async () => {
    const entrada = sesion.entrar('ana', 'clave');
    await responderEntrada('T1');
    await entrada;

    const salida = sesion.salir();
    http
      .expectOne('/api/auth/logout')
      .flush({ title: 'comun.error' }, { status: 500, statusText: 'Server Error' });

    await salida;

    // Dejar a alguien «dentro» porque el servidor no respondio seria lo peor de los dos mundos: cree que
    // ha salido y sigue dentro.
    expect(sesion.haySesion()).toBe(false);
  });

  it('recupera la sesion al recargar, y solo pregunta una vez', async () => {
    const primera = sesion.restaurar();
    http.expectOne('/api/auth/me').flush(PERFIL);
    await primera;

    await sesion.restaurar();

    // Si preguntara en cada navegacion, cada cambio de pantalla costaria una peticion.
    http.expectNone('/api/auth/me');
    expect(sesion.haySesion()).toBe(true);
  });

  it('si la cookie ya no vale, no hay sesion', async () => {
    const promesa = sesion.restaurar();
    http.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });

    await promesa;

    expect(sesion.haySesion()).toBe(false);
  });
});
