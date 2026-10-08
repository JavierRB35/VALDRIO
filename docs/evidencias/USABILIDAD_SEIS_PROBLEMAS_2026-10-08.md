# Revisión de usabilidad: seis problemas — 2026-10-08

Base contrastada: `origin/codex/local-tests-and-workspace`, commit `770c64024464f633fb94caa921d99923ad5ab066`.
Cambios en `codex/usability-six-issues`, copia aislada; no se modifica la rama abierta en Visual Studio ni `main`.

## Hallazgos y correcciones

| Problema | Causa comprobada | Corrección |
| --- | --- | --- |
| Guardar estructura no confirma el resultado | Crear/guardar/finalizar acababan con estados genéricos. | Mensajes específicos tras recibo confirmado; fallo de refresco conserva el aviso de guardado ya confirmado. |
| Actualizar no explica su función | Acción de recarga junto a creación, sin propósito visible. | «Recargar datos guardados», ayuda visible y tooltip. Relee el archivo; conserva formularios pendientes y no los guarda. |
| Nuevo Centro redundante | El formulario ya queda vacío automáticamente al guardar. El botón solo vacía el formulario. | Acción «Dejar de editar / vaciar formulario» solo cuando existe selección o texto; continúa protegiendo cambios pendientes. |
| Centro y orden de nodos | Ya existía columna, pero mostraba solo código; las filas seguían orden de inserción. | Centro físico primero, código y nombre. Orden explícito centro, código de nodo y papel (Principal antes de Auxiliar). |
| Finalización técnica | Resumen principal incluía revisión, ID de padre, hash y lista plana de maestros. | Resumen funcional y árbol expandido Centro → Nodos con papel, unidad y funciones. Detalles técnicos en expander cerrado; publicación e identidad siguen intactas. |
| Separadores manuales invisibles | Estaban dentro de un expander cerrado. No había detección automática de ambigüedad. | Panel abierto por defecto y explicación explícita. CSV/texto Excel siguen perfil manual; números nativos Excel conservan su valor almacenado. Cambiar separadores invalida validación y aprobación. |
| Paso 4 bloquea 5 | Elección y checkbox duplicados; además exigía ejemplo escrito y aprobación antes de preparar la revisión. | Elegir la modalidad constituye la elección explícita. Paso 5 accesible sin ejemplo ni aprobación previa, incluso pulsando su pestaña. Una aprobación del significado en la revisión final; nota opcional, referencia automática interna si queda vacía. |

## Contraste metodológico

Se contrastaron `specification/v0.4/tareas/T07.md`, `docs/tareas/T07.md`, contratos CM01/CM02/CM16, datos de recursos y documento internacional, además de `docs/directrices/USABILIDAD.md`.

La revisión no escribe datos. El perfil pendiente continúa bloqueado por el dominio con `IMP_APPROVAL_REQUIRED`; la UI permite inspeccionarlo y exige aprobación al guardar. El guardado reconstruye el perfil aprobado cuando hace falta, vuelve a validar y verifica el hash de ambos archivos y la revisión del estudio. Una preparación ya aprobada sin cambios mantiene su fingerprint.

Para acumulados se exige conservar el archivo anterior, fechas fiscales y ámbito comparable. «Conservar archivo anterior» captura expresamente esa evidencia, sin exigir las confirmaciones del lote actual; la transformación completa se aprueba en la revisión final. Monedas distintas continúan necesitando puente FX; las filas inválidas no se convierten en cero.

Tras guardar, el asistente ofrece continuar a **Preparar y ajustar cuentas**. El flujo principal entra en T07, conservando original, convertido, ajustes, efectivo y partes. Siguen vigentes motivo y tipo de ajuste, reversión, validación por recurso, rutas exclusivas, pendientes explícitos y conservación 900 = 300 + 500 + 100. No se aprueban decisiones metodológicas pendientes ni una fuente productiva por pasar fixtures sintéticas.

Los nombres de centros y unidades en una publicación se toman de sus definiciones congeladas: renombrar un maestro vivo no reescribe el árbol histórico. Cambiar idioma/región/zona no cambia importes ni snapshots. Nuevos textos y ayuda en EN/ES.

## Validación

- `dotnet build Valdrio.slnx -c Release --no-restore --nologo -v minimal`: correcto, 0 errores y 0 advertencias.
- `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --logger "trx;LogFileName=verified-final.trx" --results-directory artifacts/tests`: 522 correctas, 0 fallos y 0 omitidas. Incluye las cinco regresiones añadidas y las reglas T07/FX/persistencia existentes.
- Tras completar los últimos textos de ayuda, 17 pruebas de UI/localización y regresiones afectadas también correctas (`final-ui-resources.trx`).
- 975 claves EN/ES coincidentes y sin duplicados. 57 archivos canónicos de especificación conservan su SHA-256.
- WPF: ejecución completa con `--capture-dir=... --preferences-path=...`, fixtures sintéticas, automatización nativa, capturas ES/EN y pseudo/RTL. Resultado: todas las suites correctas, 338 comprobaciones registradas en `artifacts/wpf-accepted/` (incluye 27 de auditoría guiada y 26 de T07).
- `git diff --check`: correcto.

Se conservan TRX y capturas en `artifacts/` (ignoradas por Git). La inspección visual incluyó el árbol de estructura y la revisión final de importación, además de los ensayos de teclado, regreso a pasos anteriores, guardado/reapertura y T07. Los ensayos sintéticos no sustituyen la aceptación con un archivo contable productivo.

Durante la inspección visual se corrigieron estilos de ayuda que no heredaban ajuste de línea y recortaban textos. Se repararon expectativas WPF desfasadas que buscaban IDs en el historial visible, donde la base integrada ya mostraba archivos y totales; las comprobaciones persistentes de IDs/fingerprints/reapertura siguen activas. El ensayo de centro pendiente ahora mantiene la revisión visible: el helper anterior revelaba automáticamente el formulario y deshacía la precondición de redirección.

Economía: compilación, tests y WPF locales. No se ejecuta ni dispara GitHub Actions; coste de Actions de esta tarea: 0 USD. Fuentes sincronizadas de `sources/` intactas.
