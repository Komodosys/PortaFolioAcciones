import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';

/**
 * El idioma activo, como señal.
 *
 * Existe para poder **recalcular** lo que depende del idioma. Los importes y las fechas no se guardan ya
 * formateados: se formatean al presentar, y cuando la persona cambia de idioma hay que volver a
 * formatearlos. Con una señal, eso ocurre solo.
 *
 * No se lee el idioma desde cada plantilla: se lee aqui una vez y quien lo necesita depende de esta señal.
 */
@Injectable({ providedIn: 'root' })
export class Idioma {
  private readonly idiomas = inject(TranslocoService);

  /** El codigo del idioma activo: `es`, `fr` o `en`. */
  readonly activo = toSignal(this.idiomas.langChanges$, {
    initialValue: this.idiomas.getActiveLang(),
  });
}
