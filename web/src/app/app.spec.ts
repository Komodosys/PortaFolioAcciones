import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { provideRouter } from '@angular/router';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('se crea', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('solo tiene el hueco de la ruta: la cabecera vive en el marco', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const compilado = fixture.nativeElement as HTMLElement;

    // La raiz NO pinta cabecera: eso es cosa de `shared/marco`, para que la pantalla de entrada pueda
    // ocupar la ventana entera sin ella.
    expect(compilado.querySelector('header')).toBeNull();
    expect(compilado.querySelector('router-outlet')).toBeTruthy();
  });
});
