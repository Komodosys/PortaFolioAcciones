/**
 * La persona que tiene la sesion abierta.
 *
 * Son los datos que devuelve `/api/auth/me`. Se llaman igual que en la API y **no se traducen**: son
 * identificadores del contrato, no textos para nadie.
 */
export interface UsuarioSesion {
  readonly id: string;
  readonly displayName: string;
  readonly baseCurrency: string;
  readonly language: string;
  readonly isAdmin: boolean;
}

/**
 * Como acabo un intento de entrar.
 *
 * Son **codigos, no mensajes**, por el mismo motivo que en la API (ADR-013): el texto lo pone la
 * pantalla en el idioma de la persona. Un «usuario o contrasena incorrectos» escrito aqui obligaria a
 * elegir un idioma antes de saber cual quiere.
 */
export type ResultadoEntrada =
  | 'ok'
  | 'credenciales'
  | 'bloqueado'
  | 'contrasenaCaducada'
  | 'camposFaltantes'
  | 'error';

/** Lo que devuelve la API al entrar. */
export interface RespuestaEntrada {
  readonly status: string;
  readonly csrfToken: string;
  readonly mustChangePassword: boolean;
}
