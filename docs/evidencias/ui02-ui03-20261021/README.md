# UI-02 y UI-03 — entrega candidata de accesibilidad y entrada de datos

**Base:** main de 21/10/2026. **Estado:** candidato a validación en Windows; no declarar aceptado ni cerrar #59/#60 antes del recorrido real.

## UI-02 — Errores contextuales

- Se añade `Text.ContextualStatus` al tema VALDRIO: texto rojo `Brand.Red`, con énfasis **solo** cuando `HasStatusError` es verdadero; éxito e información siguen neutros.
- Los ViewModels de empresa, estudio y preparación de recursos exponen `HasStatusError` derivado del diagnóstico de dominio. El reparto ya lo exponía y reutiliza el estilo. No se cambia la lógica de diagnóstico.
- Los errores de validación ya rojos (modelo, filas secundarias, importaciones, actividad física, etc.) conservan su tratamiento; se centraliza el estilo de errores explícitos en la fila secundaria.
- Los diálogos modales existentes permanecen intactos. No se cambia cuándo aparecen.
- Queda por revisar en Windows el inventario completo de mensajes de operación, campos y estados para certificar cobertura transversal del #59.

## UI-03 — Tabulación

- Los `TextBox` de solo lectura con estilo global de VALDRIO dejan de consumir Tab; mantienen su contenido y acceso mediante mouse/lector de pantalla.
- Una navegación de edición optativa recorre los controles realmente disponibles en las plantillas de **líneas de clave (incluida selección «Incluir»), composición de cuentas, asignación secundaria a nodos, asignaciones absolutas y cobertura primaria**.
- El orden de búsqueda atraviesa filas y columnas en ambos sentidos; omite controles deshabilitados, ocultos o de solo lectura. No escribe ni valida valores por sí mismo.
- Las tablas de selección o presentación sin este comportamiento conservan sus mecanismos WPF y navegación por flechas.
- La ordenación pura de celdas cuenta con pruebas unitarias; el comportamiento WPF exige validación en escritorio real, especialmente con virtualización, combos abiertos y scroll.

## Recorrido mínimo de aceptación local (no sobre datos reales)

1. `dotnet build Valdrio.slnx -c Release` y `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release`, en un Windows con SDK .NET 10.
2. ES/EN: provocar errores en empresa, estudio, recursos, definición de claves y secundaria; confirmar texto rojo contextual, explicación y recuperación. Un estado de éxito/información no debe teñirse de rojo. Comprobar que siguen abriéndose los mismos diálogos.
3. En los dos repartos, usar Tab/Mayús+Tab entre casillas, porcentajes, asignaciones y desplegables de filas consecutivas. El foco no debe quedar en etiquetas ni celdas de solo lectura; comprobar el recorrido en ambos extremos, la salida al control siguiente y que no se crean filas ni se pierden datos.
4. Comprobar filas editables/no editables, tabla vacía, varias filas, combos desplegados, ordenación/selección, teclado sin ratón y scroll con 100/125/150 % de escalado. Validar también edición de correcciones de recursos, importación y otras tablas con editores WPF estándar, que no usan el comportamiento optativo.
5. No cerrar issues ni fusionar hasta registrar los resultados y resolver cualquier error de compilación o foco.

**GitHub Actions:** sin ejecuciones ni autorización de coste; objetivo 0 USD.

## Incidencia de aceptación UI-03 y corrección candidata (21/10/2026)

La prueba real en Windows reporta que Tab desde la casilla `Incluir` no llega a `Peso / %`. El screenshot confirma por separado que la validación de nombre vacío aparece en rojo (UI-02 en este caso).

Se cambia el manejador de Tab para escuchar eventos de previsualización ya tratados por WPF y para examinar cada editor de `CellTemplate` **sin descartar la columna porque su DataGrid padre sea de solo lectura**. Se comprueba existencia, visibilidad, habilitación y carácter editable real del `TextBox`. La dirección inversa y el paso a otra fila comparten la misma lógica. El cursor debe abandonar la tabla al llegar al extremo.

**Requisito adicional explícito del propietario:** al llegar por Tab/Mayús+Tab a cualquier `TextBox` editable, seleccionar todo su valor, permitiendo reemplazarlo o dejarlo intacto sin ratón. El comportamiento se centraliza en el tema de inputs; el clic del ratón conserva la colocación normal del cursor. No seleccionar campos deshabilitados ni de solo lectura. Para `ComboBox`, se conserva la interacción nativa.

**Pendiente:** repetir compilación y pruebas Windows sobre el nuevo commit y verificar a mano la secuencia exacta `Incluir → Peso / % → Incluir de la fila siguiente → Peso / % siguiente`, su regreso con Mayús+Tab y la selección completa del texto. Confirmar el mismo patrón en repartos primario/secundario y que Tab nunca modifica ni guarda automáticamente el contenido.
