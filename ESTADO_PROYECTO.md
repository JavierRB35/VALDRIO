# ESTADO_PROYECTO

Fecha de revisión: 2026-10-04  
Repositorio: `JavierRB35/VALDRIO`

## Corrección de fuente

El paquete `VALDRIO_Paquete_Desarrollo_v0_4.zip` **sí está disponible**.

- SHA-256: `1d8f8c21e3386117426f0eede257508a0bae30ed8c48cec7d2fb0e8ac68d7ced`.
- 59 entradas.
- `sha256sum -c SHA256SUMS.txt`: todas las entradas enumeradas **OK**.
- `python3 casos/verificar_referencias.py`: **passed, 39/39**.
- El verificador matemático no prueba la aplicación C#, SQLite ni WPF.

La afirmación anterior de que el paquete faltaba queda revocada.

## Estado de tareas

### T00 — Acordar el producto y sus políticas

Estado: **PENDIENTE DE REVISIÓN DEL PROPIETARIO**.

Completado documentalmente:

- lectura y reconciliación de especificaciones 01-05;
- revisión de CM01, CM02, CM08, CM09, CM10, CM12, CM15 y CM16;
- recorrido de estudio descrito;
- D01-D12 reconciliadas con la fuente;
- ejemplos C01, C15, C18, C20, C22 y C23 preparados para revisión;
- criterios I01, I02, I04 e I13 identificados sin declararlos pruebas de aplicación;
- identidad VALDRIO e internacionalización desde fundamentos conservadas;
- integridad del paquete y 39 referencias verificadas.

Pendiente para cierre formal:

- revisión del propietario de `docs/revision/T00_REVISION_USUARIO.md`.

Las propuestas D04-D07 **no necesitan aceptarse ahora** para comprender/cerrar T00; conservan sus puertas de decisión posteriores. D03 requiere un ejemplo real antes de T06 productivo.

### T01 — Repositorio, solución y compilación Windows

Estado técnico: **IMPLEMENTADA Y REVALIDADA**.  
Estado de secuencia: **CIERRE FORMAL PENDIENTE DE T00**.

La primera implementación se adelantó antes de reconciliar la ficha real. La comparación detectó y corrigió:

- textos WPF hardcodeados → recursos neutral inglés + español;
- ausencia de validación de catálogos → pruebas de claves/placeholders;
- Runner solo compilado → Runner ejecutado en CI;
- publish framework-dependent → publish `win-x64` autocontenido.

Evidencia de revalidación:

- run `37217066778`;
- build: success;
- tests: **4/4 passed**;
- Runner smoke: success;
- publish self-contained: success;
- WPF smoke: success;
- artefacto `valdrio-t01-win-x64`, ID `11308518710`, 68.358.723 bytes.

No se declara revisión visual humana ni funcionalidad de T01A.

## Decisiones

Fuente reconciliada: `docs/decisiones/DECISIONES_PENDIENTES.md`.

- D01, D04-D07, D10-D11: propuestas.
- D02: supuesto.
- D03: pendiente de ejemplo.
- D08: pendiente de operación.
- D09: pendiente comercial.
- D12: pendiente avanzado.

Ninguna propuesta económica ha sido promovida a regla definitiva.

## Entorno y evidencia

Host local de esta sesión:
- Linux x86_64;
- Git `2.47.3`;
- Python `3.13.5`;
- sin `dotnet` ni PowerShell locales.

Verificación Windows:
- GitHub Actions `windows-latest`;
- .NET 10;
- compilación, pruebas, Runner, publish y smoke WPF ejecutados remotamente.

Documentos:
- `docs/evidencias/T00_PAQUETE_2026-10-04.md`
- `docs/revision/T00_REVISION_USUARIO.md`
- `docs/tareas/T00.md`
- `docs/tareas/T01.md`
- `docs/evidencias/T01_REVALIDACION_PAQUETE_2026-10-04.md`
- `docs/decisiones/DECISIONES_PENDIENTES.md`
- `docs/fuentes/FUENTES_DISPONIBLES.md`

## Continuidad

Primera acción pendiente: **revisión del propietario de T00**.

Después de esa revisión:
1. cerrar formalmente T00;
2. promover T01 de implementada/revalidada a cerrada en secuencia;
3. comenzar T01A, sin adelantar T02/T03.
