# Usabilidad — decisión permanente del propietario (2026-10-07)

«Una aplicación de este tipo ya es lo suficientemente compleja en su metodología
como para complicarla en su uso y funcionamiento».

Usabilidad y sistema visual aprobado se aplican conjuntamente. La presentación sigue
[el sistema visual V1](../diseno/SISTEMA_VISUAL_VALDRIO_V1.md), sin reducir la claridad del flujo. Conservar todas las funcionalidades y
reglas económicas, sin obligar a conocer la arquitectura interna para trabajar.

- La aplicación proporciona el orden de trabajo y explica el siguiente paso.
- Mostrar solo el contexto del paso actual; las opciones avanzadas siguen accesibles.
- Antes de pedir datos, explicar su finalidad y los requisitos previos. No permitir
  llegar al final de una importación para descubrir que falta guardar el estudio.
- Cada campo debe tener ayuda contextual, disponible con ratón y teclado. Los
  requisitos críticos también son visibles: no esconderlos únicamente en tooltips.
- Distinguir explícitamente revisión, cambios pendientes y datos guardados. Mostrar
  destino y resultado del guardado. No perder formularios al cambiar de paso.
- Un bloqueo explica qué falta y cómo resolverlo. Los códigos técnicos son apoyo,
  no sustituyen la explicación. No inventar unidades, roles ni criterios económicos.
- Verificar el recorrido desde empresa vacía hasta importación guardada, reapertura,
  retroceso, corrección de errores y opciones avanzadas. Mantener localización y teclado.

T07 fue autorizada para implementación completa el 2026-10-07; el recorrido añade
Recursos después de Importación. Esta prioridad no elimina funcionalidades
ni sustituye las validaciones del dominio; reorganiza cómo se presentan y ejecutan.

## Edición directa en tablas — decisión del propietario (2026-10-09)

Todo lo que pueda consultarse o modificarse en una tabla se hará en la propia tabla.
No duplicar campos en formularios externos ni añadir paneles de edición para datos
que ya aparecen en filas. Priorizar simplicidad, controles por celda y un guardado
inequívoco que solo se habilite cuando existan cambios pendientes.

En reparto primario: comportamiento Fijo/Variable/Mixto mediante combo por cuenta.
Los porcentajes fijo y variable pertenecen a esa fila; solo se muestran como campos
editables para Mixto. Fijo equivale a 100/0 y Variable a 0/100. Guardar confirma los
cambios de la tabla y vuelve a desactivarse hasta la siguiente modificación.
