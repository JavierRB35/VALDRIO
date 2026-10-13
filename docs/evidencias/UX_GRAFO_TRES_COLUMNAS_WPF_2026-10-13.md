# VALDRIO — Visualización WPF del grafo secundario en tres columnas
Fecha: 2026-10-13 · PR #44 · Pendiente validación Windows

## Solicitud de aceptación
El usuario rechazó el diseño de filas de transferencia y compartió imágenes de referencia
con tarjetas redondeadas, textos breves, valores EUR y conectores Bézier. No fue posible
recuperar los enlaces externos de Gemini; se respetaron las imágenes y requisitos visibles.

## Diseño implementado
- **Primario**: se conserva un diagrama por la cuenta elegida. No se agrupan todas las
  cuentas en el mismo gráfico.
- **Secundario**: grafo completo sin selección, dispuesto en tres columnas:
  1. **Nodos tras reparto primario**: todos los nodos activos, incluidos los de
     coste cero, con el coste primario recibido.
  2. **Auxiliares**: una tarjeta por auxiliar, con importe bruto de circulación
     y coste primario en tooltip.
  3. **Principales**: una tarjeta por principal con su **coste final conciliado**
     del ledger T11; desglose primario/secundario en tooltip.
- Se mantienen identificadores **canónicos de nodo** en los arcos, para no
  confundir códigos o nombres repetidos.
- Los arcos del mismo par origen/destino y etapa se **agregan visualmente**
  entre grupos, conservando los importes y grupos individuales en el tooltip.
- Conectores WPF `PathGeometry`/`BezierSegment`; grosor proporcional al importe
  absoluto, sin mezclar importes internos y finales.
- Las asignaciones primarias directas a un principal pasan por una banda
  superior gris discontinua (sin fingir que transitan por un auxiliar).
- Las prestaciones recíprocas `A→B`, `B→A` y `A→A` vuelven por el lateral
  de la columna auxiliar en **curvas azules discontinuas dirigidas**.
- Las salidas auxiliar→principal usan curvas verdes. Se diferencian del
  coste primario directo y del volumen recíproco.
- Una tarjeta por nodo y por etapa, no una tarjeta por transferencia; las
  tarjetas muestran etiquetas cortas y el nombre completo en tooltip.
- Hover sobre una tarjeta **destaca sus conexiones** de entrada y salida,
  atenuando las restantes, para redes complejas.
- El `Canvas` se dimensiona según el máximo número de nodos por columna;
  el scroll vertical interno permanece desactivado. El ancho completo
  admite desplazamiento horizontal cuando no quepa en la ventana.
- Traducciones ES/EN y recursos de marca existentes; no se añade dependencia
  gráfica externa.

## Integridad de cálculo
- El motor de prestaciones recíprocas por factorización matricial **no cambia**.
- El grafo visualiza los arcos que ya deriva
  `AllocationWorkspaceViewModel.SecondaryFlow.cs`:
  `Gross[i] × Q[i,j]`, `Gross[i] × B[i,m]` y hechos primarios.
- El coste final en la columna derecha proviene de `PrincipalCosts`,
  no de sumar nuevamente prestaciones internas.
- Si los coeficientes guardados no reconcilian con la publicación,
  se suprime el dibujo parcial y se muestra aviso.
- Si falla el cálculo, se limpian arcos y nodos de gráficos anteriores.

## Comprobaciones añadidas
- Smoke WPF de tarjeta única por nodo y etapa: total =
  nodos primarios + auxiliares + principales = 2 × nodos activos.
- El smoke comprueba conectores **BezierSegment**, y que el gráfico
  sigue existiendo sin principal seleccionado.
- Las pruebas matemáticas existentes mantienen reciprocidad dirigida,
  autoasignación y orden canónico de identificadores.
- Revisión estática: XAML y recursos ES/EN con etiquetas completas
  y sin diferencias de claves.
- **No hay confirmación todavía de compilación ni smoke WPF Windows
  para este último commit.**

## Prueba de aceptación local
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

Validar sobre estudio real con auxiliares recíprocos:
1. Tres columnas visibles, sin selección de nodo, cada nombre aparece una vez
   por etapa y con importe pertinente.
2. Flechas A→B y B→A separadas, A→A como bucle si se configura; tooltip con grupo
   y valor; coste bruto no suma dos veces.
3. El coste final de principales coincide con el resumen, incluyendo primario
   ya absorbido.
4. Hover destaca conexiones y desatura las restantes; ancho/alto correctos.
5. Resultado publicado recuperado sin volver a calcular; prueba de error.
6. Validar español, inglés, DPI y nombres extensos.

PR #44 sigue en borrador, no se toca main y no se ejecutan GitHub Actions.
Objetivo facturable: 0 USD.
