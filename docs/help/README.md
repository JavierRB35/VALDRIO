# Ayudas contextuales de VALDRIO

**Idiomas:** español e inglés (16 fichas en cada idioma). **Corte original de código:** `main@211af0d`. **Estado:** documentación inicial y lector WPF propuesto en PR; pendiente de compilación y aceptación visual en Windows.

El catálogo de vistas está en [help-map.json](help-map.json). Cada archivo numerado contiene objetivo de gestión, campos y unidades, interpretación de resultados y errores/validaciones. La numeración ordena la documentación; **no** representa las etapas de cálculo ni implica que todas las ventanas aparezcan en el menú lateral. Un panel físico puede servir a más de un ID lógico (primario/secundario).

Para validar localmente:

```bash
python scripts/verify_help.py
python scripts/verify_help.py --base main
```

El segundo comando compara cambios con la base Git indicada y requiere que la ayuda cambie cuando se toca XAML/ViewModel mapeado. Las modificaciones de cálculo requieren además revisión semántica manual de los consumidores. No ejecutar GitHub Actions para validar documentación. Consultar [arquitectura completa](../ARCHITECTURE_HELP.md) y [plantilla](template-help.md).

## Integración en escritorio

La cabecera de VALDRIO presenta `Ayuda (F1)` / `Help (F1)` y abre una ventana de lectura no modal. Su ficha se resuelve a partir de la vista lógica activa, también en subpantallas integradas: primario/secundario tienen IDs distintos. La ventana se actualiza cuando cambia la vista o el idioma mientras está abierta, no almacena datos económicos y no sustituye la ayuda breve de campos.

Los recursos Markdown y `help-map.json` se copian junto al ejecutable (`help/`); los idiomas se buscan en `help/en/` para inglés y en `help/` para español. Si falta traducción, el lector avisa expresamente de que muestra el español; la validación de catálogo exige actualmente las 16 traducciones completas. No realiza peticiones web.

Para probar en Windows: `dotnet restore Valdrio.slnx`, `dotnet build Valdrio.slnx -c Release --no-restore`, `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore`, `dotnet run --project src/Valdrio.Desktop -c Release`. En las vistas internas y en los asistentes, verificar F1, cierre Escape, regreso al foco, navegación, tablas anchas, ES/EN y escalado 125–200 %. No fusionar la integración hasta registrar el resultado real.
