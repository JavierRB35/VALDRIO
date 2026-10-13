# VALDRIO — Visualización completa del reparto secundario por nodos
Fecha: 2026-10-13 · PR #44 · Validación Windows pendiente

## Decisión UX del propietario

- **Primario**: la persona selecciona una cuenta contable; se visualiza exclusivamente el
  flujo de esa cuenta hacia sus nodos. No se muestra un laberinto de todas las cuentas.
- **Secundario**: no requiere selección. El diagrama completo parte **de los nodos**
  que ya recibieron costes primarios (principales y auxiliares), muestra las
  prestaciones recíprocas entre auxiliares y termina en los principales.
- No se ejecuta un segundo motor iterativo, ni se sustituye la resolución LU existente.

## Algoritmo de visualización implementado

`AllocationWorkspaceViewModel.SecondaryFlow.cs` proyecta tres fases de arcos
dirigidos utilizando los resultados económicos existentes:

1. **Entrada primaria**: de los hechos de `PrimaryAllocationService` por grupo de
   reparto y nodo receptor, sin enumerar cuentas individuales en el diagrama
   secundario. Los nodos principales pasan con su coste primario propio.
2. **Auxiliar → auxiliar**: de los coeficientes `Q[i][j]` reconstruidos desde las claves
   secundarias publicadas y el coste bruto `solution.Gross[i]` de cada solución
   publicada/calculada por grupo. Importe de arco = `Gross[i] * Q[i][j]`.
   Aparecen A→B, B→A, A→A (si existen importes no nulos), sin simular iteraciones.
3. **Auxiliar → principal**: de los coeficientes `B[i][m]` y el mismo importe bruto:
   `Gross[i] * B[i][m]`. Por cada principal/grupo se comprueba que
   `PrimaryDirect[m] + ΣAuxToPrincipal[m]` reconcilia con
   `solution.Final[m]` dentro de la tolerancia numérica. En caso de
   incoherencia se omite el gráfico incompleto y se muestra aviso.

En la parte inferior se muestran los **totales finales de los nodos principales**
obtenidos de las filas T11, por separado de los movimientos auxiliares brutos.

**No sumar las transferencias internas a los costes finales**: son circulación
bruta/relaciones de coste, no hechos económicos nuevos. Los importes de las
flechas son proyecciones informativas y no sustituyen al ledger publicado, cuya
precisión/redondeo se conserva en las tablas. Se reconstruyen los coeficientes
desde las claves del estudio, no se vuelven a resolver las ecuaciones LU.

## Decisiones visuales

- Tres secciones + bloque de coste final, con todos los arcos no nulos de cada
  fase, **sin limitar a ocho o 32 arcos y sin selección previa**.
- Cada transferencia tiene **su propio carril horizontal, dirección, importe
  y tooltip con nodo completo/grupo/valor numérico**. Se evitan las
  intersecciones de curvas entre grupos, causantes de ilegibilidad.
- Las tarjetas muestran nombres cortos `Centro / Nodo`, la descripción completa
  sigue disponible en tooltip.
- Altura del canvas calculada según el número total de transferencias. El
  ScrollViewer interior no se desplaza verticalmente; la página permite
  desplazarse por todo el diagrama cuando hay muchos nodos.
- El reparto primario mantiene su comportamiento de una cuenta seleccionada.

## Validaciones añadidas

- Nuevas pruebas MSTest `SecondaryFullFlowProjectorTests`: reciprocidad A⇄B,
  salida final de 15.000 €, autoasignación A→A y orden no canónico de IDs.
- Smoke WPF: hay arcos de entrada primaria y de salida final en un estudio
  publicado; el gráfico ocupa la altura necesaria y **sigue visible al retirar
  la selección de principal**.
- Ante cálculo imposible se vacían todos los arcos; queda un aviso y no una red
  económica ficticia.
- Revisión estática de traducciones ES/EN y enlaces del ViewModel.

## Validación manual requerida en Windows

El commit de esta tanda **NO tiene todavía una compilación o prueba Windows confirmada**.

```powershell
cd C:\Users\cjrui\source\repos\VALDRIO
git status --short
git fetch origin
git pull --ff-only origin fix/ux-consolidated-legacy-open-20261013
git log -1 --oneline
dotnet build Valdrio.slnx -c Release
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build
dotnet run --project src/Valdrio.Desktop/Valdrio.Desktop.csproj -c Release --no-build
```

Aceptación específica:
1. Primario: selecciona cuenta A y B; nunca mezcla ambas en su gráfico.
2. Secundario: nada seleccionado; visualiza entradas de nodos, traspasos
   A→B/B→A, destinos principales y coste final por principal.
3. Un ejemplo con autoasignación debe mostrar A→A.
4. Estudio cerrado: misma red en lectura, sin calcular de nuevo.
5. Reparto imposible `GRAPH_CLOSED_COMPONENT`: se muestra error y no
   se conserva el gráfico anterior.
6. ES/EN, zoom 125/150 %, desplazamiento global de la página, nombres largos
   y varios grupos de componentes.

No se fusiona a `main`. No se ejecuta GitHub Actions:
coste facturable provocado 0 USD, sin autorización de runners alojados.
