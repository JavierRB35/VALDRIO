# Auditoría integral de usabilidad — recorrido del analista de costes

Fecha: 2026-10-08  
Rama: `codex/end-to-end-usability-audit`  
Base funcional: `codex/t07-resource-rules`

## 1. Perspectiva utilizada

La auditoría se realiza desde la perspectiva de un analista de costes que necesita:

1. iniciar un proyecto nuevo;
2. reabrir un proyecto existente;
3. modificar su estructura cuando sea necesario;
4. preparar un nuevo periodo reutilizando la estructura vigente;
5. importar saldos o movimientos;
6. corregir, clasificar y distribuir las cuentas importadas;
7. dejar la información preparada para las fases de cálculo todavía no implementadas.

La interfaz no debe exigir conocer nombres de clases, versiones de esquema, identificadores,
revisiones, lotes, snapshots ni contratos internos para completar esas tareas.

## 2. Recorrido de negocio visible

El recorrido principal queda expresado como:

**Proyecto → Diseñar estructura → Periodo / análisis → Importar saldos → Preparar cuentas**

Las preferencias quedan fuera del recorrido económico.

El usuario no está obligado a recorrer todos los pasos cada vez. Al abrir un proyecto ya
existente se muestra un centro de tareas con accesos directos a:

- diseñar o modificar la estructura;
- abrir o crear un periodo de análisis;
- continuar el análisis actual, llevándolo a importación o preparación de cuentas según su estado.

## 3. Proyecto de costes

### Problemas encontrados

- La pantalla se presentaba como «Archivo de empresa», aunque para el analista es el proyecto completo.
- Crear, abrir, cerrar, copiar, restaurar y actualizar aparecían con la misma jerarquía.
- El formulario de creación competía visualmente con la acción habitual de reabrir un proyecto.
- Los mensajes de error exponían códigos internos.
- Al reabrir un proyecto no existía una elección explícita de la tarea a realizar.

### Cambios

- «Archivo de empresa» pasa a **Proyecto de costes**.
- Se separan dos tareas principales:
  - **Retomar un proyecto existente**.
  - **Empezar un proyecto nuevo**.
- El botón Crear permanece deshabilitado hasta introducir un nombre.
- Copias, restauración, actualización y cierre quedan en **Archivo, copias y recuperación (opcional)**.
- Los detalles de identificadores/esquema quedan dentro de **Detalles técnicos (soporte)**.
- Los errores visibles muestran el mensaje humano; el código se conserva en `DiagnosticCode`.
- Al abrir un proyecto aparece un centro de tareas en la cabecera.

## 4. Diseñar la estructura de costes

Se conserva el rediseño ya aplicado antes de esta auditoría:

- «Modelo» se presenta como **Estructura de costes**.
- «Publicar» se presenta como **Finalizar estructura para usarla**.
- centros: formulario + tabla permanente;
- el formulario se limpia después de un guardado confirmado;
- los centros históricos se desactivan/reactivan en lugar de borrarse;
- nodos: formulario + tabla permanente;
- guardar nodo persiste realmente el borrador;
- la unidad de actividad puede crearse dentro del propio nodo;
- el catálogo general de unidades queda como opción secundaria;
- fallos de persistencia conservan el formulario y el estado realmente guardado.

### Resultado para el analista

La estructura responde a «cómo reparto costes» y no a «qué datos tiene este mes».
Una versión finalizada queda fija para poder reutilizarla en varios periodos y conservar
la reproducibilidad histórica.

## 5. Periodo / análisis de costes

### Problemas encontrados

- Abrir un análisis existente y crear uno nuevo compartían la misma superficie sin una separación clara.
- Snapshots, cierre, captura y operaciones de soporte tenían demasiado peso visual.
- «Preparar otro análisis» borraba también la selección de estructura, moneda y escenario, aunque
  el caso habitual es abrir el mes siguiente con la misma estructura.
- El resumen visible incluía revisión e identificadores internos que no ayudan al trabajo diario.
- La revisión de un archivo sin guardar estaba demasiado cerca del flujo principal.

### Cambios

La pantalla queda separada en:

1. **Retomar un análisis guardado**.
2. **Preparar un nuevo periodo / editar el análisis actual**.
3. **Datos importados en este análisis**.
4. **Opciones avanzadas del análisis**.

Al elegir **Nuevo periodo con esta estructura** se conservan:

- estructura de costes;
- moneda;
- escenario.

Se limpian deliberadamente:

- nombre;
- fecha inicial;
- fecha final;
- importaciones y snapshots del periodo anterior.

Así no se puede crear accidentalmente otro análisis con las fechas anteriores.

Las operaciones de snapshot, cierre, descarte y revisión de archivo sin guardar siguen disponibles,
pero quedan en la sección avanzada.

## 6. Importar saldos

Se conserva el rediseño de importación realizado antes de esta auditoría.

Flujo visible:

1. **Elegir archivo**.
2. **Indicar columnas**.
3. **Elegir registros**.
4. **Explicar los importes**.
5. **Revisar y guardar**.

### Principios preservados

- cuenta e importe siguen siendo obligatorios;
- no se convierten vacíos o importes inválidos en cero;
- ceros iniciales de cuentas se preservan;
- signos, moneda y periodo se validan;
- movimientos, saldos de cierre y acumulados mantienen semánticas diferentes;
- las filas originales y su procedencia se conservan;
- los botones internos «leer» y «validar» no compiten con la acción principal;
- un bloqueo explica qué falta;
- los códigos `IMP_*` no sustituyen la explicación humana.

Después de guardar una importación nueva, el flujo entra directamente en preparación de cuentas.

## 7. Preparar cuentas — T07

### Problemas encontrados

- El acceso ya era funcional tras el rediseño anterior, pero faltaba una visión del progreso global.
- Seleccionar una cuenta y después buscar el botón de edición era más lento de lo necesario.

### Cambios

- La pantalla se presenta como **Preparar cuentas**.
- La tabla muestra importe importado, correcciones, importe efectivo, pendiente y diferencia.
- Se añade un resumen:
  - cuentas conciliadas;
  - cuentas con importe pendiente de asignar;
  - cuentas con diferencia sin explicar.
- Una cuenta puede abrirse con doble clic.
- Se mantiene el botón **Revisar / ajustar cuenta seleccionada** para teclado y accesibilidad.
- Todos los controles, comandos y operaciones avanzadas existentes se conservan.
- El importe original nunca se reescribe: las correcciones se registran como ajustes reversibles.

## 8. Navegación global

### Antes

Los seis botones superiores eran navegables aunque no existiera contexto suficiente; al pulsarlos la
aplicación podía redirigir a otra pantalla. Esto es técnicamente correcto pero poco predecible.

### Ahora

- Proyecto siempre disponible.
- Diseñar estructura y Periodo/análisis se habilitan cuando existe proyecto.
- Importar saldos se habilita cuando existe un análisis abierto y guardado.
- Preparar cuentas se habilita cuando existe al menos una importación guardada.
- Los pasos no disponibles muestran su requisito como ayuda.
- La cabecera muestra el contexto actual:
  **Proyecto · Estructura · Análisis**.
- Al reabrir un proyecto se muestra un centro de tareas para evitar recorrer pasos ya completados.

## 9. Recorridos prioritarios resultantes

### Proyecto nuevo

**Crear proyecto → Diseñar estructura → Finalizar estructura → Crear periodo/análisis → Importar saldos → Preparar cuentas**

### Retomar un proyecto

**Abrir proyecto → elegir tarea en el centro de tareas → continuar desde el estado real**

### Modificar estructura

**Abrir proyecto → Diseñar/modificar estructura → crear versión editable si la vigente está finalizada → finalizar nueva versión**

Los análisis históricos siguen vinculados a la versión anterior.

### Importar otro periodo

**Abrir proyecto → abrir el análisis anterior → Nuevo periodo con esta estructura → introducir nuevo nombre y fechas → guardar → importar saldos**

### Continuar preparación

Si el análisis actual ya tiene importaciones:

**Abrir proyecto → Continuar preparando las cuentas importadas**

sin pasar de nuevo por estructura o importación.

## 10. Funcionalidad preservada

La reorganización no elimina:

- creación/apertura/cierre de archivos;
- backup, restore y upgrade;
- centros y unidades;
- versionado y finalización de estructuras;
- escenarios y moneda por análisis;
- snapshots y cierre/reapertura;
- revisión de archivos sin guardarlos;
- importación completa T06/T06A;
- sustitución de importaciones;
- FX;
- ajustes, reversión, particiones, asignaciones, exclusiones y pendientes T07;
- historial y revaloración;
- accesibilidad por teclado y ayuda contextual.

Comparación estática de superficies modificadas:

- Company: mismos 17 controles nombrados y 8 comandos.
- Study: mismos 20 controles nombrados y 9 comandos.
- Resource/T07: mismos 56 controles nombrados y 20 comandos, más doble clic.
- MainWindow: no pierde controles ni comandos; añade 7 elementos de contexto/tareas.

## 11. Validación realizada

Sin GitHub Actions:

- XAML y RESX modificados: estructura XML válida.
- recursos: 963 claves ES / 963 EN.
- 0 claves faltantes.
- 0 claves duplicadas.
- inventario de controles/comandos comparado contra `codex/t07-resource-rules`.
- pruebas WPF actualizadas para:
  - diagnóstico técnico separado del texto visible;
  - nuevo periodo que reutiliza estructura/moneda/escenario sin copiar fechas;
  - centro de tareas al reabrir;
  - reanudación directa en preparación de cuentas.

No se ha ejecutado una compilación Windows de esta rama porque esta auditoría no incluye autorización
para GitHub-hosted runners y no hay un checkout Windows local disponible desde este entorno.

## 12. Límite actual

La auditoría termina en T07 porque T08+ todavía no forma parte del producto realizado. «Preparado»
significa preparado según las reglas T07 existentes; no se inventan requisitos de cálculo de fases
que aún no están implementadas.
