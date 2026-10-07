# Continuidad T06A — puente de moneda tras T06

Base: formato de empresa v6, perfiles v1/v2/v3, preparados v1/v2/v3 e inputs v1/v2/v3/v4.
Mantener sus lectores y bytes históricos; cualquier nueva semántica persistida necesita
una versión propia y actualización a copia, no reescritura de los mappings congelados.

1. Implementar las subfases de la ficha canónica T06A: origen multimoneda, RateSet aplicado,
   originales/conversiones explícitas, conciliación por moneda y base funcional, persistencia
   atómica e interpretación independiente de idioma/región.
2. No sumar nominales de monedas distintas ni reutilizar cotizaciones de demostración.
   Por ahora T06 bloquea fuentes distintas de la moneda funcional.
3. Integrar conversiones con agrupación por código y selección de registros. Mantener todos
   los enlaces y ambos documentos cuando se trate de diferencias de acumulados.
4. Tratar sustituciones mediante `ActiveBatches`; nunca sumar recursos de lotes retirados.
   Preservar las fotos de revisiones anteriores y la idempotencia de `ConfirmImport`.
5. Probar nuevas fuentes CSV/XLSX, tipos ausentes y orientación de cotizaciones, cancelación,
   rollback, snapshots, runner y WPF, más UTC/Madrid y recursos EN/ES en el commit aceptado.
6. D03 productiva no está aprobada: el propietario ha identificado su Excel como ejemplo
   de prueba con cierre declarado 31/10/2026. No usarlo para inferir reglas económicas reales.

No declarar revaloración completa ni preparación para cálculo antes de integrar las
dependencias de particiones, consumos y políticas de T07 y posteriores.
