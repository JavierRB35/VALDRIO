# T00 — revisión del propietario

Estado: **REVISADA Y ACEPTADA PARA CIERRE DE T00**  
Fecha: 2026-10-04

## Flujo aceptado

1. Se crea/abre un archivo local de empresa. La empresa **propone una moneda predeterminada** para nuevos estudios.
2. Se crea un modelo y un estudio para un periodo/escenario. **Cada estudio fija su moneda funcional**.
3. Los originales pueden conservar otras monedas; toda conversión es explícita y los tipos utilizados quedan congelados por ejecución.
4. Una importación se prepara y previsualiza con perfil explícito; solo la confirmación altera entradas.
5. Los recursos conservan origen, ajustes, partes y rutas; no se duplican entre directo/pool/exclusión/pendiente.
6. El cálculo recibe un snapshot tipado, sin DbContext, CultureInfo actual ni ViewModels.
7. Solo una ejecución completada y compatible con la revisión vigente se publica como resultado actual.
8. Cada importe publicado debe poder explicarse hasta su origen.
9. Capacidad, órdenes y márgenes se añaden con políticas y perímetros visibles, no con fórmulas ocultas en UI/SQL.

## Decisiones registradas por el propietario

- **D01 ACEPTADA**: R1 para gestión de costes, capacidad, productos, órdenes y rentabilidad; existencias/PdC en T32.
- **D02 ACEPTADA**: un editor por archivo; local; sin edición simultánea multiequipo.
- **D03 PENDIENTE**: se decidirá con archivo representativo; no inferir modalidad/signos por idioma.
- **D04-D07 PROPUESTAS**: se revisarán en sus tareas con datos, fórmula, resultado y alternativas.
- **D08, D09 y D12**: se resolverán en hitos previstos y no bloquean trabajo independiente actual.

## Interpretación internacional aceptada

Idioma de interfaz, formato regional, perfil de origen, moneda funcional y zona horaria son dimensiones independientes. Cambiar presentación no cambia ledger ni `EconomicInputHash`.

La moneda funcional pertenece al estudio. La moneda sugerida por la empresa es un valor predeterminado de creación, no una imposición sobre todos los estudios.

## Cierre

El propietario confirma el alcance inicial y la separación entre modelo, estudio, ejecución y presentación internacional. Esto satisface el criterio de T00 de comprensión del flujo y de identificación de decisiones que afectan reglas y arquitectura.

La aceptación de T00 **no** promueve D04-D07 a políticas definitivas.
