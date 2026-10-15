# Matriz de aceptación de la aplicación real — preparación del manual definitivo

**Corte de definición:** 15/10/2026. **Base de inventario:** [REALIDAD_FUNCIONAL_2026-10-15.md](REALIDAD_FUNCIONAL_2026-10-15.md). Este archivo es un **plan ejecutable en Windows**, no un acta de pruebas realizadas hoy.

## Condiciones antes de marcar una fila como superada

- Emplear un clon limpio del SHA que se quiera documentar. Registrar `git rev-parse HEAD`, fecha, `dotnet --info`, sistema Windows, escala DPI, idioma y cultura.
- Ejecutar el binario **WPF real**, no solo servicios y mocks; usar el mismo commit para controles, pruebas, capturas y material del manual. Un smoke automatizado puede complementar, pero no sustituir el recorrido de una persona.
- Trabajar con **copias** de archivos `.valdrio`; conservar intactos los dos ejemplos de referencia bajo `Synthetic Projects/` y sus comprobantes. Nunca alterar datos de producción para documentar.
- Probar ES y EN, importes positivos, negativos y cero, dos tamaños de ventana, zoom 125 % y 200 %, cambios guardados/reapertura y teclado. No usar una captura aislada como prueba de todos los estados.
- En cada caso archivar: ruta y hash de captura(s), entrada, acción, mensaje visible, importe esperado/obtenido, persistencia tras reabrir, estado `OK` / `INCIDENCIA` / `NO EJECUTADO`, SHA y responsable. **Ningún caso está marcado OK por esta auditoría Linux**.
- GitHub Actions solo es manual y **no debe lanzarse** sin instrucción expresa; validar primero en PC Windows local o self-hosted sin consumo facturable.

## Recorrido dirigido: casos funcionales por pantalla

| ID | Flujo a ejecutar físicamente | Comprobación visible y persistente | Estado de este corte |
|---|---|---|---|
| A01 | Iniciar VALDRIO sin proyecto | Proyecto accesible, fases con puertas de acceso correctas, preferencias aparte, explicación al intentar avanzar | NO EJECUTADO |
| A02 | Crear un proyecto, cancelar otro, abrirlo y cerrarlo | Nombre sugerido en guardado, moneda, destino, mensajes, sesión activa y archivo real intacto tras cancelar | NO EJECUTADO |
| A03 | Cargar ejemplo y elegir destino ocupado | Crea copia nueva al confirmar; destino ocupado/cancelado no sobrescribe nada; abre un proyecto utilizable | NO EJECUTADO |
| A04 | Copia, restauración y actualización de archivo heredado | Nunca sustituye original, explica cambios y exige destino nuevo | NO EJECUTADO |
| B01 | Crear centro(s), nodo principal y auxiliar, pool/unidad; guardar y reabrir | La tabla y el formulario reflejan los cambios persistidos | NO EJECUTADO |
| B02 | Publicar estructura, intentar editarla y clonar nueva versión | Publicado conserva definición; edición legítima crea versión, no reescribe el histórico | NO EJECUTADO |
| C01 | Crear análisis para estructura publicada; abrir otro período | Periodos, moneda, escenario, análisis activo y separación de importaciones | NO EJECUTADO |
| C02 | Capturar instantánea, cerrar y reabrir estudio | Estados coherentes y revisión; comprobar que ejecuciones anteriores T12 no se muestran vigentes sin validar compatibilidad | NO EJECUTADO |
| D01 | Importar CSV con código/descripcion, valor único, ceros y negativos | Mapeo, vista previa, interpretación numérica, selección y confirmación con diferencias conciliadas | NO EJECUTADO |
| D02 | Importar XLSX con columnas Debe/Haber, formatos y cuentas duplicadas | Código textual y ceros iniciales, descripción, agrupación explícita, procedencia de cada origen, conversión | NO EJECUTADO |
| D03 | Cancelar importación y reabrir análisis | Vista previa cancelada no produce asiento/lote confirmado; un origen antiguo no se pisa inadvertidamente | NO EJECUTADO |
| E01 | Corregir importe manual en tabla, deshacer sin guardar y volver a abrir | Cambio persistido tras confirmación; importe efectivo, diferencia y saldo conciliados | NO EJECUTADO |
| E02 | Dividir cuenta en partes; asignar a nodo, objeto directo y exclusión | Suma de partes, pendientes y exclusiones, sin doble imputación; conservar tras reabrir | NO EJECUTADO |
| E03 | Crear regla extracontable por cuenta o prefijo; simular y cancelar | No se aplica al cancelar; ficha guardada no crea impacto económico automáticamente | NO EJECUTADO |
| E04 | Aplicar regla con confirmación, corregir manual en cuenta afectada, deshacer | Modales, independencia de corrección/regla, compensación, identidad analítica, revisión de destinos | NO EJECUTADO |
| E05 | Crear regla sin cuenta origen y abrir otro período | Recurso analítico sin compensación; no reaplicación automática a otro análisis; reversión si estado lo permite | NO EJECUTADO |
| F01 | Crear/publicar clave primaria, regla exacta/prefijo y asignación masiva | Clave y regla diferenciadas, cobertura, porcentaje/inductor, cuenta con descripción y ruta por origen | NO EJECUTADO |
| F02 | Ver resultados primarios, seleccionar cuenta, cerrar y reabrir | Tabla, flujo **de cuenta seleccionada**, conciliación, bloqueo de edición y efecto en secundario | NO EJECUTADO |
| G01 | Configurar secundarios (autoprestación y auxiliares recíprocos) | Diagnóstico de viabilidad, solución LU, liquidación bruta no aditiva, costes terminales por principal | NO EJECUTADO |
| G02 | Provocar un circuito 100 % auxiliar sin salida; reparar clave/regla | Error comprensible con acción de reparación, no publicaciones numéricas falsas | NO EJECUTADO |
| G03 | Ejecutar y cerrar secundario; reabrir | Revisión, bloqueo de primario, histórico, distinción entre brutos y finales | NO EJECUTADO |
| H01 | Calcular ejecución T12, seleccionar filas/arcos y consultar gráficos | Origen→primario→secundario→final; tablas de contribución/conciliación, sumas exactas | NO EJECUTADO |
| H02 | Repetir ejecución sin cambios, consultar una antigua y modificar el estudio | Historial inmutable, revisiones, vigente/histórica, sin recálculo al seleccionar una antigua | NO EJECUTADO |
| H03 | Cancelación y error en ejecución con datos incompletos | Diagnóstico, sin resultado parcialmente publicado ni falsa condición vigente | NO EJECUTADO |
| I01 | Cambiar ES/EN, cultura/formato y zona horaria durante flujos | Traducciones completas, importes fechados, formato de edición controlado y ayuda contextual | NO EJECUTADO |
| I02 | Navegar con Tab/Enter/Escape, menú compacto, tablas densas, 125/200 % | Foco, expansión de fases, diálogos cancelables, accesibilidad visual, desbordamientos registrados | NO EJECUTADO |

## Casos económicos patrón (no inventar el valor observado)

En `Synthetic Projects/` existen dos empresas sintéticas de referencia y [pruebas T13](../tareas/T13.md) para corroborar el motor real de importación, cierres, publicación y reapertura:

| Conjunto | Total terminal esperado según casos versionados | Qué contrastar en pantalla |
|---|---:|---|
| Service Company | **17.500,00 EUR** | Dos principales, dos auxiliares, cuenta negativa/cero, autoprestación y recíprocos |
| Industrial Company | **36.000,00 EUR** | Tres principales, dos auxiliares, lote/hora, costes y descuentos, autoprestación/recíprocos |
| Ejemplo de conciliación | 1.000 EUR = 300 directo + 500 repartible + 200 excluido | Los **500 EUR** terminales de nodos no se confunden con el total contable de 1.000 EUR |

Los valores anteriores son **expectativas de los casos**, no cifras extraídas por esta auditoría de una ejecución interactiva actual. Los ficheros `.valdrio` originales v9 se mantienen para verificar migración **a copia** v10; pueden requerir preparar ejecuciones T12 en archivos derivados.

## Automatizaciones locales existentes (complementarias)

Scripts disponibles en el repositorio: [Verify-Windows.ps1](../../.github/scripts/Verify-Windows.ps1) y [Verify-T13-T12.ps1](../../.github/scripts/Verify-T13-T12.ps1). Consultar sus parámetros antes de ejecutarlos; pueden exigir directorio nuevo y árbol limpio.

```powershell
git rev-parse HEAD
dotnet restore Valdrio.slnx
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore
# Ejecución posterior y controlada del smoke WPF en Windows, con ruta nueva:
# pwsh -NoProfile -File .github/scripts/Verify-Windows.ps1
```

**Evidencia histórica a reutilizar sin sobreafirmar:** [auditoría y recorrido WPF 14/10](../auditorias/2026-10-14/VALIDACION.md), [continuación visual, 564 checks WPF](../auditorias/2026-10-14-visual/CONTINUACION.md), [ajustes reutilizables y sus incrementos](../evidencias/2026-10-14-ajustes-reutilizables/INFORME.md), [T12 aceptada, 692 pruebas](../tareas/T12.md), [T13/T12 Windows y casos](../tareas/T13.md). Cada acta valida **el SHA y escenarios que declara**, no cualquier versión posterior.

## Regla editorial de salida

Una sección puede pasar al futuro manual de instrucciones solo cuando su fila esté **OK sobre el mismo binario que se distribuya**, incluya captura real ES/EN, entradas, acción, resultado esperado/observado y mensaje de error/recuperación. Si cambia el código después, volver a comprobar los flujos afectados. Un apartado sin prueba queda en inventario técnico como «implementado / no verificado», y una función T14/R1–R4 no integrada queda únicamente en hoja de ruta.

**No se ha realizado ninguna prueba de aplicación nativa, cambio funcional, workflow ni cargo de GitHub Actions durante la generación de este documento.**
