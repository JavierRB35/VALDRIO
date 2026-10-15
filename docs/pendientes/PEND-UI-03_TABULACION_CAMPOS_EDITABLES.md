# PEND-UI-03 — Tabulación directa entre campos y celdas editables

Fecha: 2026-10-15. Estado: pendiente de implementación.

## Requisito del propietario

Agilizar la introducción de datos sin utilizar el ratón. En formularios y tablas, Tab debe llevar al siguiente campo o celda editable y Mayús+Tab al anterior, evitando recorrer campos de solo lectura y partes no editables de la tabla.

El propietario observa paradas innecesarias en la navegación actual. El requisito es transversal e incluye expresamente la definición de destinos en tablas de reparto.

## Trabajo pendiente

- Inventariar el recorrido de foco en los formularios y tablas reales, incluidos los repartos.
- Excluir del recorrido de entrada de datos las etiquetas, contenedores, cabeceras, selectores de fila y celdas de solo lectura que hoy producen paradas innecesarias.
- Respetar la editabilidad efectiva de cada campo o celda según el estado de la operación, permisos y fila, sin limitarse a la configuración estática de la columna.
- Conservar el acceso por teclado a acciones necesarias, listas desplegables y otros controles interactivos, así como la lectura accesible de la información no editable. No convertir esos controles en inaccesibles para reducir paradas.

## Criterios de aceptación

1. Tab y Mayús+Tab recorren los campos editables en un orden lógico y predecible, con foco visible, sin detenerse en contenido meramente informativo o deshabilitado.
2. En tablas, el recorrido pasa directamente entre celdas editables, salta las de solo lectura y continúa a la fila siguiente o anterior según corresponda.
3. Al terminar o empezar la tabla, se puede continuar el recorrido por teclado sin quedar atrapado ni requerir un clic.
4. La navegación conserva el valor introducido y respeta las validaciones, sin descartar datos, confirmar operaciones completas ni crear filas involuntariamente.
5. Un error de validación ofrece una explicación visible y una vía de corrección mediante teclado; se conserva el comportamiento de los modales existentes y se aplica PEND-UI-02 a los mensajes contextuales de error.
6. Es posible rellenar un formulario y definir destinos en una tabla de reparto, corregir valores y ejecutar las acciones necesarias sin usar el ratón.
7. Se comprueban localmente recorridos hacia delante y atrás, filas con distinta editabilidad, tablas vacías y con varias filas, y estados de edición y solo lectura.

## Evidencia pendiente

Registrar las pantallas revisadas y el resultado real de cada recorrido. La tarea queda documentada; todavía no se ha implementado ni validado este ajuste. No ejecutar GitHub Actions.
