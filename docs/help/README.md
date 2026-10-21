# Ayudas contextuales de VALDRIO

**Idioma inicial:** español. **Corte de código:** `main@211af0d`. **Estado:** documentación inicial, pendiente de integración y pruebas WPF.

El catálogo de vistas está en [help-map.json](help-map.json). Cada archivo numerado contiene objetivo de gestión, campos y unidades, interpretación de resultados y errores/validaciones. La numeración ordena la documentación; **no** representa las etapas de cálculo ni implica que todas las ventanas aparezcan en el menú lateral. Un panel físico puede servir a más de un ID lógico (primario/secundario).

Para validar localmente:

```bash
python scripts/verify_help.py
python scripts/verify_help.py --base main
```

El segundo comando compara cambios con la base Git indicada y requiere que la ayuda cambie cuando se toca XAML/ViewModel mapeado. Las modificaciones de cálculo requieren además revisión semántica manual de los consumidores. No ejecutar GitHub Actions para validar documentación. Consultar [arquitectura completa](../ARCHITECTURE_HELP.md) y [plantilla](template-help.md).
