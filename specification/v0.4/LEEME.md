# VALDRIO — Paquete de producto internacional v0.4

VALDRIO es la identidad definitiva. La aplicación se construirá desde cero en escritorio Windows, con cálculo local e internacionalización desde los fundamentos. Este paquete especifica el producto y guía su desarrollo por un LLM; no contiene una aplicación nueva implementada.

## Contenido

- VALDRIO_Especificacion_Internacional_v0_4.pdf, informe de producto, arquitectura y hoja de ruta.
- VALDRIO_Prompt_Desarrollo_v0_4.md, prompt maestro para el LLM que implemente.
- especificacion, cinco documentos con 16 contratos, decisiones/diccionario, datos/operaciones e internacionalización/extensión.
- tareas, 38 fichas T00–T33 y T01A/T02A/T06A/T22A, índice legible e índice JSON con dependencias y subentregas.
- casos, 39 referencias numéricas sintéticas, su verificador racional, 15 criterios internacionales y catálogo de cuatro monedas para pruebas.
- plantillas, estado del proyecto, evidencia, decisiones y revisión como usuario.
- MANIFIESTO.json y SHA256SUMS.txt, identidad, alcance, verificación y hashes del contenido.

## Cómo empezar sin programar

1. Lee el informe y comprueba finalidad, trabajo local, ejemplos económicos y preferencias internacionales independientes.
2. Entrega este ZIP y el prompt maestro al LLM de desarrollo. Pídele conservar los documentos en el repositorio nuevo de VALDRIO.
3. Empieza por T00 para registrar criterios y dudas. T01 entrega solución y ventana Windows; T01A incorpora español/inglés antes de formularios funcionales.
4. Continúa por dependencias, una tarea o subentrega por sesión. El LLM mantiene estado, decisiones y evidencias.
5. Revisa resultados mediante el ejecutable, archivo y guion de usuario preparados por el LLM. No debes escribir C#, SQL o pruebas.

Las decisiones de negocio se resuelven con ejemplos. El trabajo independiente puede avanzar y las propuestas pueden usarse en demostración identificadas. Antes del uso definitivo se aceptan las políticas económicas pertinentes. Nombre e internacionalización ya son requisitos del producto.

## Contrato internacional inicial

Idioma de interfaz, formato regional, perfil del archivo de origen, moneda funcional/original y zona horaria son independientes. R0 ofrece español e inglés completos, formatos es-ES/en-US/en-GB/de-DE, fechas de negocio canónicas y moneda tipada. El diseño permite nuevos idiomas y adaptadores sin cambiar el motor.

Una ejecución utiliza una moneda funcional. Originales en otras monedas conservan su importe y se convierten con tipos explícitos, fechados y congelados. El cambio de región no convierte divisa. Monedas de 0, 2 y 3 unidades menores forman parte de los casos. Cálculo, importación, traducción, informes y persistencia tienen límites propios.

## Qué se ha comprobado en este paquete

Se verificaron las 39 referencias numéricas con Fraction de Python, las dependencias y referencias de las 38 tareas y la integridad del paquete. El PDF se renderizó y revisó visualmente. Las 32 referencias económicas originales conservaron sus entradas y salidas; siete referencias nuevas cubren conversión y proyección de moneda.

Los 15 casos internacionales son criterios para la futura aplicación, todavía sin ejecución contra C#, WPF o SQLite. El verificador matemático valida ejemplos, no el motor que se desarrollará. No hay pruebas de aplicación ejecutadas en este paquete.

## Extensión y límites

La primera edición es Windows local, una empresa y un editor por archivo, finalidad de gestión y moneda funcional por estudio. La arquitectura facilita idiomas, fuentes, métodos e informes adicionales; el volumen se medirá durante desarrollo. Edición simultánea entre equipos necesita un modo de concurrencia y permisos adicional, además de abstraer persistencia.

T32 añade valoración productiva/inventario con marco y jurisdicción elegidos; T33 añade fuentes contables concretas mediante adaptadores. Ninguno condiciona el núcleo. No se presume cumplimiento fiscal o contable universal por localizar la interfaz. Las políticas OwnerOnceCapacity y PDEC/PNUM tienen sus puertas de comprobación y aceptación económica.
