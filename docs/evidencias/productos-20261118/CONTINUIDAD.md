# Continuidad de Catálogo y Composición — 18/11/2026

Estado: implementación en curso; entrega en PR borrador. No integrar en main.

## Origen y destino

Repositorio canónico: https://github.com/JavierRB35/VALDRIO
Rama conservada: feature/products-catalog-composition-ux-20261118.
Base remota verificada: main 35e5b457bd5de4a15449b3429a657b6cd95159da.
Punto de continuación remoto: 4a864ebcc3f69dfe2515ae27e51214b33a45b5ba.
Los commits anteriores se conservan; no se ha reiniciado ni reescrito el historial.

## Trabajo realizado

Se leyó íntegramente el protocolo maestro del proyecto y AGENTS.md. Se auditó el diff parcial, se corrigió el error WPF de Application.Current y se continuaron guardado, selección, decimales, importación, ayuda ES/EN e históricos.

Por instrucción del propietario, la implementación posterior se delegó en los agentes OpenCode revisor-codigo, valdrio-costes y arquitecto-pruebas. Los tres recibieron explícitamente el modelo opencode/deepseek-v4.1-flash-free. Codex quedó como supervisor.

Cambios principales de esa continuación:
- Guardado efectivo al salir/cambiar de pestaña y tratamiento del cambio de producto con edición pendiente.
- Copias de trabajo temporales sin sobrescribir snapshots publicados; recuperación de la composición guardada al descartar.
- Consulta histórica de referencias inactivas en solo lectura, sin nuevos clones ni nuevos componentes inactivos.
- Detección de auxiliares y bases por orden también en semielaborados anidados.
- Coste parcial distinguido del completo; conservación del manual y retirada del cálculo anterior obsoleto.
- CSV/XLSX, códigos textuales, fórmulas con valor cacheado, ayudas y documentación ES/EN.
- Ampliación de pruebas y del recorrido WPF. Este último sigue pendiente de ejecución final y ajuste.

## Evidencia ejecutada y sus límites

- Build local Windows .NET SDK 10.0.403: ejecución inicial corregida, 0 errores y 0 advertencias. Es anterior a las últimas modificaciones.
- Primera selección de pruebas relacionadas: 43 superadas, 0 fallidas y 0 omitidas. Es anterior a las últimas modificaciones.
- El arquitecto ejecutó después otro build intermedio: 0 errores y 0 advertencias.
- Su selección intermedia de 28 pruebas produjo 27 superadas y 1 fallida: ProductWorkflowNestedPerOrderInputDoesNotPublishStableUnitCost. Demostró que una base por orden anidada se estaba tratando como coste unitario estable.
- El revisor aplicó después el uso de CompositionPreview.HasOrderDependentBasis. La prueba fallida NO se ha repetido sobre esa corrección; no se declara resuelta por ejecución.
- Revisión estática ES/EN: XML válido, claves alineadas; el supuesto duplicado de recursos era un artefacto del conteo de líneas, no un duplicado XML.
- git diff --check sin errores de espacios; advertencias de normalización LF/CRLF.
- No se ejecutó la verificación final WPF ES/EN/AURORA ni se declara probado el estado final de esta rama.
- No se activó GitHub Actions. El workflow sigue limitado a workflow_dispatch.

## Bloqueo real

OpenCode detuvo al agente arquitecto-pruebas con HTTP 429, tipo provider.free-limit / FreeUsageLimitError: Free usage limit reached. El proveedor indicó retryAfterMs=20721000 en ese momento. No se cambió a un modelo de pago ni a otro proveedor, respetando la instrucción de usar los tres agentes DeepSeek gratuitos.

Los agentes técnico y contable entregaron sus cambios; el de pruebas quedó interrumpido durante la última ronda. Se preserva su trabajo parcial para continuarlo, no como evidencia de aceptación.

## Próximos pasos obligatorios

1. Retomar esta misma rama desde GitHub con los agentes autorizados cuando exista cuota gratuita.
2. Compilar el estado final y repetir primero ProductWorkflowNestedPerOrderInputDoesNotPublishStableUnitCost; después las pruebas de catálogo/composición/órdenes afectadas.
3. Terminar y ejecutar el smoke WPF ES/EN: pestañas materializadas, guardar/descartar/cancelar, cambio A con edición a B, borrador guardado y transitorio, inactivas con y sin composición, coma/punto y precisión, ComboBox, importación, ayudas y navegación por teclado.
4. Comprobar fórmulas XLSX con caché/sin caché/error; auxiliares anidados; invariantes de snapshots y órdenes históricas; costes por orden y redondeos.
5. Ejecutar los recorridos sobre una copia consistente de AURORA; nunca escribir en la empresa original. Verificar su hash y revisar capturas en ES/EN y escalado.
6. Revisar el diff final y actualizar la evidencia de la PR. Solo entonces considerar merge, sujeto a la autorización vigente.

Los dos pendientes transversales permanecen documentados en docs/pendientes/PEND-UX-DECIMALES-Y-AYUDA-2026-11-18.md. No se presentan como terminados fuera de este módulo.

## Corrección de compilación tras prueba del propietario

El propietario comunicó dos errores CS8602 en `MainWindow.CompositionSmoke.cs` (líneas 230 y 234 del commit 8577d94). Autorizó excepcionalmente a Codex a corregirlos porque DeepSeek gratuito seguía bloqueado por cuota. Se captura la superficie WPF en una variable local con comprobación explícita de ausencia antes de las comprobaciones de escala; no se suprimen advertencias ni se cambia lógica económica.

Verificación ejecutada tras la corrección: `dotnet build Valdrio.slnx -c Release --no-restore`, Windows/.NET 10, resultado **0 errores y 0 advertencias**. Las pruebas automatizadas y el recorrido visual final siguen pendientes de los resultados del propietario. Este build sustituye únicamente la limitación de compilación descrita anteriormente; no acredita los flujos WPF ni los cálculos.
