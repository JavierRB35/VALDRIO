# ADR-0001 - Arquitectura base y límites técnicos

Estado: **VIGENTE COMO BASE TÉCNICA**  
Fecha: 2026-10-04

## Contexto

El repositorio estaba vacío salvo por un README. El prompt maestro v0.4 define la dirección técnica y exige no implementar fórmulas económicas en UI, SQL o infraestructura. También exige internacionalización desde los fundamentos y verificación Windows para WPF.

Este ADR registra decisiones técnicas explícitas del encargo. **No acepta ninguna política económica pendiente.**

## Decisión

1. Aplicación de escritorio local para Windows x64, en un único proceso.
2. Lenguaje/plataforma objetivo: C# sobre .NET 10 LTS; WPF con MVVM; SQLite y EF Core 10.
3. Las versiones exactas de SDK y paquetes **no se fijan en T00**. Se verificarán contra documentación oficial y parches vigentes en T01 antes de escribir `global.json` o referencias de paquetes.
4. Estructura lógica prevista:
   - `Valdrio.Domain`
   - `Valdrio.Calculation`
   - `Valdrio.Application`
   - `Valdrio.Infrastructure`
   - `Valdrio.Localization`
   - `Valdrio.Reporting`
   - `Valdrio.Desktop`
   - `Valdrio.Runner`
   - `Valdrio.Tests`
5. `Valdrio.Calculation` no depende de WPF, EF, SQL, red, rutas, cultura, hora ni usuario.
6. Las fórmulas económicas no se implementan en ViewModels, consultas SQL ni triggers.
7. Persistencia, importación, FX, exportación, backup, reloj y licencia se conectan mediante puertos acotados consumidos desde Application.
8. Idioma de interfaz, cultura de formato, perfil de interpretación de origen, moneda funcional y zona horaria se mantienen separados.
9. T01A debe preceder a pantallas funcionales posteriores.
10. No se introduce servidor, SaaS, microservicios, IA en el motor ni un sistema genérico de plugins por anticipación.

## Consecuencias

- El host Linux de esta sesión sirve para documentación y comprobaciones independientes, pero no valida WPF.
- T01 necesita Windows CI y un artefacto que realmente compile/abra en Windows.
- D02 puede obligar a revisar el modo de colaboración antes de T03 si se exige edición simultánea desde varios equipos.
- La dirección técnica queda registrada; las políticas D01-D09 se mantienen separadas en su registro y no se convierten en hechos aprobados por este ADR.
