# VALDRIO — Resultados explicativos, claves protegidas y reciprocidad (2026-10-13)

## Alcance aprobado
Continuación incremental del PR #44. El propietario autorizó implementar únicamente las
mejoras que respetan la arquitectura T08/T09/T10/T11 y el método recíproco existente.

**Sin cambio de motor de cálculo.** El modelo matricial por factorización LU y las
políticas de redondeo existentes siguen siendo autoridad económica. No se crea otro
método directo, escalonado o iterativo con tolerancias arbitrarias, ni se atribuye
al centro principal más grande un resto residual.

## Biblioteca de claves
- El botón «Eliminar clave» explica por qué está disponible o bloqueado:
  fase cerrada, ediciones pendientes, regla actualmente guardada, cualquier referencia
  histórica (incluidas reglas inactivas), o versiones descendientes.
- Una clave sin referencias ni descendientes puede eliminarse mediante el flujo
  habitual. Regresión de ViewModel que publica, selecciona y elimina la clave.
- Se añade `ProtectedKeyIds` al snapshot **solo de lectura/UI**, distinto de
  `UsedKeyIds`: el primero incluye todas las claves recogidas en recibos
  históricos; el segundo identifica usos económicos con reglas activas.
- La infraestructura obtiene ambas listas de un único recorrido de recibos
  de todas las revisiones y estudios. El control de escritura ya exigía que
  ninguna referencia histórica fuese borrada, y se mantiene intacto.
- Regresión: referencia en regla primaria inactiva -> usada económicamente = no,
  pero protegida de eliminación = sí.
- **No se implementa todavía una entidad persistente «archivada»**: incorporarla
  sin contrato de migración/lectura podría ocultar claves necesitadas por otros
  estudios. La interfaz informa del bloqueo y permite reparar/desvincular
  reglas actuales; el historial permanece íntegro.

## Resultados del reparto primario
- KPI de recursos efectivos, base repartible a nodos, importes destinados a nodos,
  pendientes, exclusiones e imputaciones directas a objetos; no se confunde el
  total contable con el importe elegible para repartir.
- Nueva tabla agrupada por cuenta con base elegible, auxiliares, principales,
  importe asignado, diferencia y porcentaje, con detalle desplegable al
  seleccionar fila. Se conserva la tabla técnica de hechos primarios original.
- Cada detalle usa la clave/criterio que realmente gobernó ese hecho de reparto,
  su peso/unidad y participación, sin recalcular el ledger.
- Diagrama read-only ligado a la cuenta seleccionada; grosor de flecha proporcional
  al importe mostrado. Limitación explícita a los 32 flujos mayores por rendimiento;
  la tabla contiene el universo completo de hechos.
- Proyección económica: los importes de hechos usan el mismo redondeo de
  presentación que la tabla existente. Por cuenta se proyecta el **residuo real**
  respecto a esos importes ya presentados; evita descuadres falsos de un céntimo
  sin cambiar el cálculo ni distribuir costes a destinos arbitrarios.

## Resultados del reparto secundario
- Nueva tabla de liquidación por nodo auxiliar y grupo: coste primario,
  recepción recíproca **neta deducida matemáticamente**, volumen bruto
  distribuido y saldo **teórico** de liquidación. Añade residuo máximo numérico
  del cálculo por grupo como control separado.
- Se alimenta de `SecondaryGroupCirculation.Solution` y de los hechos primarios
  del mismo estudio, tanto para un cálculo nuevo como para una publicación
  recuperada. No se ejecuta el solver al abrir un estudio ya cerrado.
- La circulación auxiliar bruta **no se suma** al coste final de principales.
  No se simula un historial por cuenta de cada salto auxiliar: T11 no lo guarda.
- Un cálculo fallido limpia liquidaciones y gráficos anteriores.
- Se conserva el resumen previo de coste final por principal con procedencia
  paralela y diagrama seleccionable (PR #44 previo).

## Fuera de alcance de esta integración
- No se importan los contratos/migraciones de T12 PR #36, ni se presenta un
  falso historial de rondas iterativas. La integración de las ejecuciones
  completas T12 requiere revisión propia cuando su formato y base sean compatibles.
- No se fija una política distinta de residuo `0,01 €` ni se escoge un
  principal receptor residual arbitrario.
- No se añade todavía un selector de metodología, un heatmap completo ni
  un sankey multiperiodo: requerirían reglas de datos/agrupación adicionales.

## Pruebas pendientes en Windows
La suite de **641/641** comunicada anteriormente y la compilación del commit
`f575fe8` (si se realizó) **no validan estos nuevos cambios**.

Validar sobre el nuevo HEAD del PR #44:
```powershell
cd C:\Users\cjrui\source\repos\VALDRIO
git status --short
git fetch origin
git switch fix/ux-consolidated-legacy-open-20261013
git pull --ff-only origin fix/ux-consolidated-legacy-open-20261013
git log -1 --oneline
dotnet build Valdrio.slnx -c Release
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build
dotnet run --project src/Valdrio.Desktop/Valdrio.Desktop.csproj -c Release --no-build
```

Comprobar manualmente:
1. Crear y eliminar una clave secundaria de prueba **nunca referenciada**, durante
   la fase secundaria abierta. El botón debe activarse al seleccionar la fila.
2. Guardar una asignación (incluso inactiva), retirarla del estudio actual
   y comprobar la explicación de protección histórica.
3. Reparto primario: expandir una cuenta, verificar importes por destino;
   cambiar de cuenta actualiza el diagrama. Total por cuenta =
   principales + auxiliares + diferencia; total de hechos = total por nodos.
4. Reparto secundario: después de un cálculo viable, comprobar la liquidación
   auxiliar y los totales de principales, sin sumar circulación interna dos veces.
5. Forzar `GRAPH_CLOSED_COMPONENT`: no publicar ni mostrar liquidación obsoleta;
   corregir clave/regla y recalcular.
6. Reabrir un estudio cerrado: verificar la publicación y procedencias
   sin ejecutar cálculo nuevo.
7. ES/EN, anchuras 780/960/1600 y DPI 125/150 %.

Smokes WPF de `MainWindow.ResultsScrollSmoke.cs` ampliados para comprobar:
tabla primaria/total nodos; flujo seleccionado, auxiliar bruto, error sin
datos antiguos. Estos smokes requieren ejecución en WPF Windows; no son
invocados automáticamente por `dotnet test`.

## Control económico
No se ha ejecutado GitHub Actions ni se han cambiado workflows ni facturación.
Objetivo y gasto provocado: **0 USD**. PR en borrador, `main` intacta.
