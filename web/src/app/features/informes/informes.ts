import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../core/api/interceptores';
import { Idioma } from '../../core/i18n/idioma';
import { formatearImporte } from '../../shared/formato/numeros';
import { FilaDeRendimiento, InformeDeRendimiento, InformesApi } from '../../core/api/informes.api';
import { CarterasApi } from '../carteras/carteras.api';
import { EvolucionDeCartera } from '../carteras/modelos';
import { Linea, PuntoDeEvolucion } from '../../shared/graficos/linea/linea';

/**
 * El informe de rendimiento: coste contra valor, por cartera.
 *
 * Los filtros son **en memoria** y no llamadas al servidor: el informe ya trae todas las filas que la persona
 * puede ver —el aislamiento lo decide el servidor— así que filtrar es elegir cuáles se enseñan. Pedir el
 * filtrado al servidor sería un viaje por cada cambio de desplegable.
 */
@Component({
  selector: 'app-informes',
  imports: [FormsModule, TranslocoDirective, Linea],
  templateUrl: './informes.html',
  host: { class: 'block' },
})
export class Informes {
  private readonly api = inject(InformesApi);
  private readonly carteras = inject(CarterasApi);
  private readonly idioma = inject(Idioma);

  readonly informe = signal<InformeDeRendimiento | null>(null);
  readonly cargando = signal(true);
  readonly fallo = signal<string | null>(null);

  /**
   * La cartera cuya evolución se está mirando.
   *
   * Vacío significa «ninguna todavía». No se elige una por defecto: enseñar el gráfico de la primera cartera de
   * la lista haría creer que esa es la que se ha pedido.
   */
  readonly elegida = signal('');

  readonly evolucion = signal<EvolucionDeCartera | null>(null);
  readonly cargandoEvolucion = signal(false);

  /**
   * El periodo del gráfico.
   *
   * **El valor es el nombre que viaja en la dirección**, no una fecha: la traducción a fechas la hace el
   * servidor, porque «lo que va de año» es una regla de calendario y no una resta de días.
   */
  readonly rango = signal('todo');

  /** Los periodos que se ofrecen, en el orden en que se leen: de más corto a más largo. */
  readonly rangos = [
    { valor: '5d', clave: 'informe.rango5d' },
    { valor: '15d', clave: 'informe.rango15d' },
    { valor: '1m', clave: 'informe.rango1m' },
    { valor: '1a', clave: 'informe.rango1a' },
    { valor: 'ytd', clave: 'informe.rangoYtd' },
    { valor: 'todo', clave: 'informe.rangoTodo' },
  ];

  /**
   * Los puntos del gráfico, con la fecha ya formateada y el valor total.
   *
   * **Nada se calcula aquí**: el valor y lo aportado llegan hechos del servidor. Esta función solo los pone en la
   * forma que espera el dibujo.
   */
  readonly puntosDeEvolucion = computed<readonly PuntoDeEvolucion[]>(() => {
    const serie = this.evolucion();

    if (!serie) {
      return [];
    }

    return serie.points.map((punto) => ({
      fecha: punto.date,
      valor: punto.totalValue,
      aportado: punto.contributed,
    }));
  });

  /**
   * Los filtros. Vacío significa «todas».
   *
   * **Tienen que ser senales.** Se leen dentro de un computed, y un computed solo vuelve a calcularse
   * cuando cambia una senal: con campos normales, cambiar el desplegable no recalculaba nada y las cifras se
   * quedaban quietas. Es el error clasico de las senales, y no lo caza el compilador.
   */
  readonly persona = signal('');
  readonly tipo = signal('');

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.fallo.set(null);

    try {
      this.informe.set(await this.api.rendimiento());
    } catch (error: unknown) {
      this.informe.set(null);
      this.fallo.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.cargando.set(false);
    }
  }

  /** Las personas que aparecen, para el desplegable. */
  readonly personas = computed(() => {
    const nombres = (this.informe()?.rows ?? []).map((fila) => fila.ownerDisplayName);
    return [...new Set(nombres)].sort((a, b) => a.localeCompare(b));
  });

  /** Los tipos que aparecen. */
  readonly tipos = computed(() => {
    const valores = (this.informe()?.rows ?? []).map((fila) => fila.registrationType);
    return [...new Set(valores)].sort((a, b) => a.localeCompare(b));
  });

  readonly filas = computed(() =>
    (this.informe()?.rows ?? []).filter(
      (fila) =>
        (this.persona() === '' || fila.ownerDisplayName === this.persona()) &&
        (this.tipo() === '' || fila.registrationType === this.tipo()),
    ),
  );

  /**
   * Pide la evolución de una cartera y la deja lista para dibujar.
   *
   * Un fallo aquí **no tumba el informe**: la tabla ya está cargada y es lo que la persona estaba mirando.
   */
  async verEvolucion(portfolioId: string): Promise<void> {
    this.elegida.set(portfolioId);

    if (portfolioId === '') {
      this.evolucion.set(null);
      return;
    }

    this.cargandoEvolucion.set(true);

    try {
      this.evolucion.set(await this.carteras.evolucion(portfolioId, this.rango()));
    } catch {
      this.evolucion.set(null);
    } finally {
      this.cargandoEvolucion.set(false);
    }
  }

  /**
   * Cambia el periodo y vuelve a pedir la serie.
   *
   * Sin cartera elegida no se pide nada: el periodo se guarda y se aplicará cuando se elija una.
   */
  async cambiarRango(rango: string): Promise<void> {
    this.rango.set(rango);

    if (this.elegida() !== '') {
      await this.verEvolucion(this.elegida());
    }
  }

  /** Los totales de lo que se está viendo, sumados aquí porque el servidor solo da los de todo. */
  readonly totales = computed(() => {
    const filas = this.filas();

    if (filas.length === 0) {
      return null;
    }

    const suma = (campo: keyof FilaDeRendimiento): number =>
      filas.reduce((total, fila) => total + Number(fila[campo] ?? 0), 0);

    // La ganancia realizada solo se suma si TODAS la tienen: si a una le falta, sumar las demás se leería como
    // «lo ganado» y le faltaría una parte. Es la misma regla que aplica el servidor.
    const realizado = filas.every((fila) => fila.realizedGain === null)
      ? null
      : filas.some((fila) => fila.realizedGain === null)
        ? null
        : suma('realizedGain');

    // Y lo aportado y lo retirado, igual: si a una cartera no se le pudo sumar entero, el total no se da.
    const aportado = filas.some((fila) => fila.contributed === null) ? null : suma('contributed');
    const retirado = filas.some((fila) => fila.withdrawn === null) ? null : suma('withdrawn');

    // Los traspasos, aparte. No son aportacion ni ganancia: son dinero que cambio de cuenta.
    const traspasosEntrada = filas.some((fila) => fila.transfersIn === null) ? null : suma('transfersIn');
    const traspasosSalida = filas.some((fila) => fila.transfersOut === null) ? null : suma('transfersOut');
    const traspasosNeto =
      traspasosEntrada === null || traspasosSalida === null ? null : traspasosEntrada - traspasosSalida;

    // La moneda de los totales: la que declara el servidor. Si no hay, la de la primera fila.
    const moneda = this.informe()?.totals?.currency ?? filas[0]?.currency ?? 'CAD';

    const valor = suma('marketValue');
    const efectivo = suma('cashValue');

    return {
      moneda,
      valor,
      efectivo,
      total: valor + efectivo,
      coste: suma('costBasis'),
      noRealizada: suma('unrealizedGain'),
      aportado,
      retirado,
      aportadoNeto: aportado === null || retirado === null ? null : aportado - retirado,
      traspasosNeto,

      // Lo ganado de verdad: el total menos el dinero que entró sin ser ganancia. El efectivo ya incorpora lo
      // cobrado en las ventas, asi que la ganancia realizada no se suma aparte: se contaria dos veces.
      // Sin apertura declarada, el total menos lo aportado no es la ganancia: saldria enorme y falsa. Se dice
      // que no se sabe en vez de dar un numero. Y si alguna parte no se pudo sumar entera, tampoco.
      //
      // **Los traspasos se restan**: un traspaso desde otra cuenta del titular no es ganancia, igual que no es
      // una aportacion. Sin restarlos, esta cifra diria que se gano todo lo que se movio de sitio.
      ganado:
        filas.some((fila) => !fila.hasOpening) ||
        aportado === null ||
        retirado === null ||
        traspasosNeto === null
          ? null
          : valor + efectivo - (aportado - retirado) - traspasosNeto,
      realizado,
    };
  });

  /** Un importe como número, para poder sumarlo. */
  numero(valor: string | number | null): number {
    return valor === null ? 0 : Number(valor);
  }

  /**
   * Un importe escrito como se escribe el dinero en el idioma de la pantalla: dos decimales y el código de la
   * moneda.
   *
   * Se usa el formateador compartido y no uno propio: si esta pantalla formateara a su manera, la misma cifra
   * saldria distinta aquí y en el detalle de la cartera, y alguien lo tomaría por un error de cuentas.
   */
  importe(valor: string | number | null, moneda: string): string {
    return formatearImporte(this.numero(valor), moneda, this.idioma.activo());
  }

  limpiarFiltros(): void {
    this.persona.set('');
    this.tipo.set('');
  }
}
