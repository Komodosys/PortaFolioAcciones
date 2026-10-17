# `features` — las pantallas, por funcionalidad

Una carpeta por funcionalidad, y cada una con lo suyo: sus componentes, su servicio de datos y sus
tipos. Previstas para el MVP:

- `auth/` — entrar, cambiar la contraseña obligatoria, salir
- `portfolios/` — lista de carteras y creación
- `positions/` — la tabla de posiciones, que es la pantalla principal
- `admin/` — gestión de personas, restablecer contraseñas y consultar la auditoría

Regla: **una funcionalidad no importa de otra**. Si dos necesitan lo mismo, eso sube a `shared`.

Y una regla que no se negocia: **aquí no se calcula nada financiero.** El backend manda los importes
ya calculados; el frontend los formatea y los presenta. Es lo que evita que dos sitios den cifras
distintas de lo mismo.
