# ESTADO_PROYECTO

Fecha de revisión: 2026-10-04  
Repositorio: `JavierRB35/VALDRIO`  
Rama: `main`

## Estado observado

- Estado inicial de esta sesión: un único commit (`3244d14ecbdb77a192209654ef9cf13eceb10110`) con solo `README.md`.
- T00.1 creó la línea base documental, arquitectura inicial y registro de decisiones pendientes.
- T01 se implementó y verificó mediante PR #1.
- Merge de T01 en `main`: `04812f7972a77d276ce700cbcbca5e77deebf678`.

## Estado de tareas

### T00 - Acuerdo de finalidad y políticas

Estado: **EN CURSO**.  
Subentrega T00.1: **COMPLETADA**.

T00 no se declara cerrada porque el prompt maestro referencia `VALDRIO_Paquete_Desarrollo_v0_4.zip` con fichas T00-T33, contratos CM01-CM16, decisiones, casos JSON, verificador y plantillas que no están entre los seis archivos fuente disponibles en esta sesión.

Las decisiones D01-D09 siguen registradas como **PROPUESTA / PENDIENTE** salvo aceptación explícita posterior.

### T01 - Repositorio, SDK y CI Windows

Estado: **COMPLETADA**.

Resultado comprobado:
- solución modular `Valdrio.slnx`;
- SDK .NET 10 fijado en `10.0.401`;
- paquetes centralizados;
- shell WPF mínima;
- fixture de desarrollo no económico;
- pruebas de límites básicos de Domain/Calculation;
- CI sobre `windows-latest`;
- build Release correcto;
- 2/2 pruebas correctas;
- publicación win-x64 correcta;
- smoke WPF correcto: proceso vivo durante 6 segundos tras abrir el fixture;
- artefacto `valdrio-t01-win-x64` publicado por GitHub Actions.

Evidencia: `docs/evidencias/T01_CI_2026-10-04.md`.

La verificación anterior prueba compilación, tests, publicación y arranque automatizado en Windows. **No equivale a revisión visual humana de la ventana.**

## Línea base de alcance

- Nombre operativo del producto: **VALDRIO**.
- La comprobación registral/dominios de la marca sigue pendiente; no se interpreta el nombre operativo como registro concedido ni dominio adquirido.
- R0 es un demostrador verificable. R1 es la primera edición profesional prevista.
- Primera plataforma: Windows x64, escritorio local.
- Internacionalización es transversal desde los fundamentos: idioma, región, moneda funcional/original y zona horaria son ámbitos independientes.
- Las reglas económicas propuestas no se consideran aceptadas hasta que se revisen sus ejemplos.
- Precio, presupuesto de marketing y retorno publicitario permanecen pendientes; no se inventan.

## Entorno comprobado

Host de trabajo local:
- Linux x86_64.
- Git `2.47.3`.
- Python `3.13.5`.
- Sin `dotnet` local.
- Sin PowerShell local.

Alternativa ejecutada:
- GitHub Actions `windows-latest` con .NET 10.
- T01 compilada y ejecutada automáticamente en Windows mediante CI.

## Evidencia y documentación

- ADR técnico inicial: `docs/adr/ADR-0001-arquitectura-base.md`.
- Decisiones pendientes: `docs/decisiones/DECISIONES_PENDIENTES.md`.
- Fuentes y huellas: `docs/fuentes/FUENTES_DISPONIBLES.md`.
- T00: `docs/tareas/T00.md`.
- T01: `docs/tareas/T01.md`.
- Evidencia T01: `docs/evidencias/T01_CI_2026-10-04.md`.
- Dependencias fijadas: `docs/dependencias/DEPENDENCIAS.md`.

## Continuidad

Primera tarea técnica siguiente cuyas dependencias técnicas están satisfechas: **T01A - primera ventana bilingüe, preferencias independientes, recursos y pruebas de layout/traducción**.

Límites antes de avanzar:
1. T01A puede avanzar como fundamento técnico independiente.
2. No se deben construir pantallas funcionales posteriores antes de T01A.
3. T00 sigue pendiente de reconciliación con el paquete de desarrollo completo y de aceptación de las políticas económicas pertinentes.
4. No se implementarán fórmulas de costes como definitivas mientras sus reglas correspondientes sigan sin aceptación.
