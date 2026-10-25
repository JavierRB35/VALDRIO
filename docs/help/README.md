# Ayudas contextuales de VALDRIO

**Corte documental:** `main@6885eca675cce04127b709a7d98cc35aea9ad697` · 25/10/2026. La aplicación incluye 16 guías en español y 16 en inglés, empaquetadas como contenido local junto al escritorio WPF. El catálogo está en [`help-map.json`](help-map.json); los archivos de código asociados describen el origen de cada ficha.

La guía completa se abre con **Ayuda (F1)** y explica el propósito de la pantalla, sus entradas, la interpretación de resultados, los errores frecuentes y cómo resolverlos. Cada ayuda se vincula a una vista lógica, no solo a un control WPF. Por ello el panel de reparto compartido puede mostrar una guía distinta para primario y secundario; los asistentes y ventanas modales conservan la ayuda de su propia operación. La numeración ordena las fichas, pero no representa por sí sola el orden del menú ni una secuencia obligatoria de cálculo.

Para validar localmente:

```bash
python scripts/verify_help.py
python scripts/verify_help.py --base main
```

El segundo comando compara cambios con la base Git indicada y exige actualizar español e inglés cuando se modifica XAML/ViewModel mapeado. Los cambios de cálculo requieren además revisar manualmente sus pantallas consumidoras, porque una comprobación estructural no puede deducir el significado económico. No ejecutar GitHub Actions para validar documentación. Consulta [arquitectura y contrato de mantenimiento](../ARCHITECTURE_HELP.md) y [plantilla](template-help.md).

## Integración en escritorio

La cabecera de VALDRIO presenta `Ayuda (F1)` / `Help (F1)` y abre una ventana de lectura no modal. Su ficha se resuelve a partir de la vista lógica activa, también en subpantallas integradas: primario/secundario tienen IDs distintos. La ventana se actualiza cuando cambia la vista o el idioma mientras está abierta, no almacena datos económicos y no sustituye la ayuda breve de campos. El foco y el puntero muestran la microayuda de campo; Tab mantiene la navegación normal.

Los recursos Markdown y `help-map.json` se copian junto al ejecutable (`help/`); los idiomas se buscan en `help/en/` para inglés y en `help/` para español. Si falta traducción, el lector avisa expresamente de que muestra el español; el catálogo validado requiere las 16 traducciones completas. No realiza peticiones web.

Para probar en Windows: `dotnet restore Valdrio.slnx`, `dotnet build Valdrio.slnx -c Release --no-restore`, `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore`, `dotnet run --project src/Valdrio.Desktop -c Release`. En las vistas internas y en los asistentes, verificar F1, cierre Escape, regreso al foco, navegación, tablas anchas, ES/EN y escalado 125–200 %. Registrar cada resultado manual con el SHA exacto; una compilación o el verificador de catálogo por sí solos no certifican ese recorrido.
