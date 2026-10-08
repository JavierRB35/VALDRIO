# Recorrido guiado de empresa a importación

Petición: cambiar radicalmente la usabilidad sin eliminar funcionalidades ni iniciar
T07. La directriz permanente está en `docs/directrices/USABILIDAD.md` y AGENTS.md.

## Comportamiento

- Navegación estable Empresa → Estructura → Estudio → Importación. Solo se presenta
  la sección elegida. Se muestran el requisito pendiente y el archivo de destino.
- Estructura en cuatro pasos: elegir/crear, centros/unidades, nodos, guardar/publicar.
  Crear unidades se hace junto a los centros. Guardar/validar/publicar tiene una
  acción conjunta; también se conservan las operaciones individuales.
- Estudio: guardar y continuar. El acceso normal a importación redirige al estudio
  cuando falta guardarlo. La inspección sin estudio se mantiene como opción explícita.
- Importación: archivo, columnas/números, registros, significado/moneda, revisión y
  guardado. Los botones de avance, destino y ayuda permanecen visibles. Se exige
  reconocer el significado de los importes, sin asumirlo por el nombre del archivo.
- Guardar importación escribe el lote. El resultado guardado se diferencia de la
  preparación. Los errores conservan el formulario y permiten volver a corregirlo.
- Ayuda específica en campos de negocio e importación, con tooltip, texto persistente
  al enfocar y AutomationProperties.HelpText. Textos ES/EN y pseudolocalización.
- Opciones de conversión, acumulados, sustitución, instantáneas, mantenimiento,
  recuperación y herramientas siguen disponibles. No cambian esquema ni reglas económicas.

## Comprobación

La suite portátil conserva las 455 pruebas anteriores y añade 3 comprobaciones del
contrato de revisión guiada (458 en total). Los recorridos nativos existentes verifican
también las opciones avanzadas tras la reorganización; navegan a cada sección antes
de interactuar con sus controles.

Se añade `MainWindow.UsabilitySmoke.cs`: 19 comprobaciones desde una empresa vacía,
con nodo incompleto, guardado/publicación, retroceso sin perder el estudio, bloqueo
temprano del destino, selección de todos menos uno, 6005 = 358,35 independientemente
de la descripción, ayuda con teclado, revisión sin escritura y reapertura del lote.
El verificador Windows exige los 19 resultados y tres capturas ES/pseudo-RTL.

La aceptación final se acredita mediante el recibo de Verify-Windows vinculado al
commit y a los binarios, no por esta declaración. Los archivos de prueba son sintéticos.
La valoración de claridad por el propietario sigue siendo necesaria para iterar;
las pruebas no sustituyen esa valoración ni declaran resuelta una incidencia de
guardado de centros/modelos que no se haya reproducido.
