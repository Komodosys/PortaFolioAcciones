import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * La raiz de la aplicacion.
 *
 * Solo tiene el hueco donde se pinta la ruta. La cabecera vive en `shared/marco`, que es la pantalla
 * padre de todo lo que no sea entrar: asi la pantalla de entrada puede ocupar la ventana entera, sin
 * cabecera, que es lo que se espera de una pantalla de acceso.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
