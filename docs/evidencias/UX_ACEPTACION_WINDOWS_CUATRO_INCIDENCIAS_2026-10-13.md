# VALDRIO — Incidencias de aceptación Windows (2026-10-13)

## Contexto y estado
Tras la compilación Windows de la versión anterior, el propietario detectó cuatro problemas de uso real.
Esta intervención continúa el PR #44, **sin fusionar en main**. La última suite comunicada
por el propietario (640/640) es anterior a estos nuevos cambios; **no demuestra todavía su validez**.

## 1. Nombre sugerido al crear empresa
- `CompanyFileViewModel.Create` pasa `CompanyName` al selector de guardar.
- `CompanyFileDialogs` rellena `SaveFileDialog.FileName` sin decidir la ubicación ni sobrescribir.
- Se quitan caracteres inválidos y nombres reservados de Windows; el cuadro conserva la extensión `.valdrio`.
- Una sobrecarga por defecto en `ICompanyFileDialogs` preserva dobles de prueba preexistentes.
- Nuevo MSTest: `CompanyDialogFilenameTests`; smoke WPF de nombres seguros.

## 2. Recuperar la configuración secundaria cuando el cálculo es imposible
- La validación matemática sigue mostrando el fallo; nunca publica resultados incompletos.
- El error señala una ruta secundaria guardada, cuando los argumentos de diagnóstico identifican
  los nodos auxiliares, y ofrece una acción «Corregir clave o asignación secundaria».
- La acción expande la biblioteca y la pestaña de reglas secundarias, selecciona la asignación
  y carga la clave a la que hace referencia.
- Desde ahí se puede **retirar la asignación secundaria** o elegir/crear otra clave. El botón
  «Nueva versión» expone el comando existente para claves publicadas.
- No se permite borrar ni reescribir versiones metodológicas ya referenciadas por hechos históricos.
  No se debilitan bloqueos de `KEY_IMMUTABLE` ni la publicación.
- WPF smoke ampliado: circuito cerrado debe mostrar error, regla seleccionada, eliminación disponible,
  ningún coste final ni gráfico obsoleto.

## 3 y 4. Resumen y diagrama del coste final por centro principal
- En la pantalla de reparto secundario, el cuadro «Costes de centros principales» ahora
  ofrece selección individual. A su derecha aparece un **cuadro de procedencia** agrupado
  por cuenta de origen y nodo del reparto primario, con tipo de ruta, importes y claves.
- Los importes se proyectan desde las contribuciones finales comprobables ya calculadas/
  publicadas, y se concilian exactamente con el total visible del principal seleccionado.
- Debajo aparece un **diagrama WPF con flechas**:
  `Cuentas de origen → Nodos de reparto primario → Centro principal seleccionado`.
  Los importes en las tarjetas y detalles al pasar el ratón se extraen de los mismos
  registros que alimentan el cuadro de procedencia.
- Seleccionar otro centro actualiza al mismo tiempo tabla y diagrama.
- Si no hay resultado válido, no se muestra un dibujo con datos anteriores.
- Etiquetas ES/EN. Las tablas conservan scroll horizontal para anchos reducidos.
- Comprobaciones WPF ES/EN: selección y conciliación por centro, diagrama no vacío,
  cambio de centro y ausencia de gráficos en fallo; capturas de la visualización.
- **Límite deliberado**: el diagrama del PR #44 no es la integración del T12 completo.
  El PR #36 `feature/t12-cost-flow-history` tiene un flujo histórico de cuatro etapas,
  cálculos persistidos y circulación auxiliar agregada, pero su formato 10 y la base
  divergen de este PR. No se han copiado ni mezclado sus migraciones de forma insegura.
  Requiere integración/revisión específica después de la validación de #44.
- La circulación interna entre auxiliares no se atribuye artificialmente a cada cuenta;
  el gráfico de #44 representa **contribuciones al coste final**, no cada paso recíproco.

## Validación requerida en Windows (SIN GitHub-hosted Actions)

```powershell
cd C:\Users\cjrui\source\repos\VALDRIO
git status --short
git switch fix/ux-consolidated-legacy-open-20261013
git pull --ff-only origin fix/ux-consolidated-legacy-open-20261013
git log -1 --oneline
dotnet restore Valdrio.slnx
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release
dotnet run --project src/Valdrio.Desktop/Valdrio.Desktop.csproj -c Release --no-build
```

Pruebas manuales prioritarias:
1. Crear empresa «Obrador del Norte»; el selector debe sugerir «Obrador del Norte.valdrio».
2. En estudio con auxiliares, crear regla que provoque `GRAPH_CLOSED_COMPONENT`
   (ciclo 100% cerrado), pulsar Calcular, comprobar mensaje y reparar asociación
   desde «Corregir clave o asignación secundaria». Crear nueva versión publicada y
   reasignarla. Recalcular; solo cerrar si el cálculo es viable.
3. En un reparto secundario válido, seleccionar cada centro principal. Comprobar
   que cuadro de orígenes y diagrama cambian y concilian con su total. Revisar ES/EN.
4. Reabrir una empresa con reparto publicado y repetir cuadro+diagrama sin recalcular.
5. Confirmar que un cálculo fallido no mantiene datos de una ejecución anterior.
6. Repetir caso Horizonte (5.500 €; dos nodos de 2.750 €) y T13 servicios/industria.

Prueba WPF extendida: `MainWindow.ResultsScrollSmoke.cs`; solo se ejecuta cuando
se invoca el modo de captura/smoke correspondiente, no con `dotnet test` a secas.
No se ha podido ejecutar .NET/WPF Windows desde el entorno de edición.

## Control económico
0 workflows ejecutados. No se han activado runners GitHub-hosted ni cambiado límites.
Presupuesto predeterminado de GitHub Actions: **0 USD sin autorización explícita**.
