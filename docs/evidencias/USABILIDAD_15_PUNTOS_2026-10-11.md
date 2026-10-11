# VALDRIO — 15 mejoras de usabilidad: implementación y aceptación local

Fecha: 2026-10-11. Rama: `codex/usability-project-nodes-import-20261011` (PR #37). Base: rama funcional existente. Sin merge a main.

## Alcance del cambio

1. **Errores en rojo.** El tema WPF muestra fondo y borde rojos para errores de validación, además de Tag=error. Nombre de clave vacío y columnas obligatorias de importación utilizan indicadores propios; una clave incompleta se marca también en la biblioteca. **Pendiente:** repasar los formularios restantes en el recorrido manual para comprobar cada validación de dominio.
2. **Descripción en selección de registros.** Se carga desde la columna de descripción elegida y se enseña tanto en filas individuales como en la agrupación, conservando posibles descripciones diferentes de un mismo código.
3. **Descripción y ordenación en revisión.** Las tablas de importación presentan descripción, cabeceras centradas, divisiones verticales suaves y ordenación. Los totales de importación incluyen propiedad numérica de ordenación.
4. **Altura de preparación.** La previsualización de columnas aumenta hasta un máximo de 590 px, con ajuste por número de filas y desplazamiento.
5. **Foco al crear clave.** El botón «Nueva clave» dirige el foco al nombre desde ambas fases.
6. **Foco amarillo pastel.** Controles de texto y desplegables editables usan el token `Status.WarningBg`.
7. **Tabulación.** Los TextBox de solo lectura dejan de ser parada de tabulación. **Límite deliberado:** las tablas de consulta mantienen navegación por teclado por accesibilidad.
8. **Selección completa.** Los TextBox editables seleccionan todo su contenido al recibir el foco inicial por teclado o ratón, sin impedir un clic posterior para colocar el cursor.
9. **Abrir clave desde tabla y borradores.** Selección directa con comprobación de ediciones pendientes; guardar clave incompleta crea un borrador rojo, no una publicación apta para reparto.
10. **Edición estable de clave aplicada.** La columna primaria evita alternancias de deshabilitación durante guardado asíncrono. La tabla de asignaciones secundarias mantiene identidad de filas al refrescar. Una asignación primaria sigue siendo no editable después de cerrar primario, por coherencia económica.
11. **Cierre explícito primario.** Navegación y cálculo secundario requieren primario validado y cerrado.
12. **Totales por centro/nodo.** Tabla de agregación de hechos primarios reales, con importes firmados, sin recalcular ni duplicar coste.
13. **Orden de pantallas.** Reglas y asignaciones especiales precede a «Asignar claves a cuentas».
14. **Progreso en operaciones.** Barra indeterminada y cursor de espera para no confundir una acción asíncrona con bloqueo. No se inventa tiempo restante.
15. **Clave por auxiliar.** Tabla directa de asignación auxiliar → clave secundaria. Las operaciones nuevas rechazan una segunda clave activa para ese auxiliar, aunque sea de otro grupo. Las reglas históricas existentes no se reescriben de forma retroactiva.

## Validación pendiente en Windows / Visual Studio

La implementación remota **no está compilada ni aceptada**. No se ejecutaron workflows GitHub Actions; por decisión de proyecto su lanzamiento es exclusivamente manual y requiere aprobación explícita.

1. Abrir la rama indicada y compilar con .NET 10 / WPF Windows, en Release. Corregir cualquier error de C# o XAML.
2. Ejecutar las pruebas locales y el verificador del repositorio: `pwsh -File .github/scripts/Verify-Windows.ps1`. No lanzar GitHub Actions.
3. Importación CSV y XLSX: descripción por código; códigos con ceros; descripciones ausentes y contradictorias; selección de registros y agrupación; ordenaciones; suma de importes positiva y negativa; revisión tras volver atrás.
4. Comprobar errores rojos con foco y sin foco; columnas obligatorias; clave sin nombre; clave de porcentajes distinta de 100 % guardada como borrador y excluida de reglas disponibles.
5. En ambas fases: crear clave, foco al nombre, elegir una clave mediante un solo clic; cambios pendientes, guardar/descartar/cancelar; pulsar Tab sobre controles editables y navegar tablas accesiblemente.
6. En reparto primario: editar columna Clave aplicada repetidamente sin parpadeos; guardar, cerrar, abrir y confirmar que el cierre impide modificar el reparto.
7. Verificar el resumen por centro/nodo, incluido coste negativo, igualdad con la suma de hechos primarios y conservación del signo.
8. Verificar que no se puede entrar en secundario con primario abierto; cerrar primario y volver a navegar; auxiliar sin clave, asignación desde tabla, cambio de clave, duplicados desde otro grupo, recalcular y conservar reglas tras reabrir archivo.
9. Comparar con cierres y análisis históricos; ES/EN, 125/150/200 % DPI; navegación solo teclado; capturas reales de ambas fases. Enviar incidencias y capturas antes de aceptar PR #37.

## Observaciones de metodología

La validación secundaria distingue cobertura completa de viabilidad matemática. Una clave publicada y asignada no implica solución del sistema recíproco; el cálculo es quien debe diagnosticar circuitos cerrados, autoasignaciones no autorizadas, singularidad y problemas de conservación. La exigencia de una sola clave activa por auxiliar para nuevas ediciones restringe el caso anterior de reglas diferentes según grupo. Revisar este cambio funcional específicamente antes de aceptar si existen estudios que usan varios grupos en un mismo auxiliar.
