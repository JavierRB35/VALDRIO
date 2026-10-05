# Dependencias fijadas para T01

Fecha de verificación: 2026-10-04.

Estas versiones se fijan para la base técnica de T01. No implican que toda la funcionalidad asociada esté implementada.

| Componente | Versión fijada | Uso en T01 | Licencia / procedencia |
|---|---:|---|---|
| .NET SDK | 10.0.401 | Compilación | Microsoft; .NET 10 LTS |
| .NET runtime/desktop | 10.0.12 asociado al canal vigente | WPF en Windows | Microsoft |
| CommunityToolkit.Mvvm | 8.4.2 | ViewModel de shell | MIT |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 | Archivo de empresa T03/T04, mappings TEXT, contextos congelados v1/v2 y migraciones | MIT |
| Microsoft.NET.Test.Sdk | 18.10.1 | Ejecución de pruebas | MIT |
| MSTest.TestFramework | 4.4.1 | Pruebas | MIT |
| MSTest.TestAdapter | 4.4.1 | Descubrimiento/ejecución de pruebas | MIT |

## Fuentes verificadas

- Microsoft .NET 10 downloads: https://dotnet.microsoft.com/download/dotnet/10.0
- NuGet CommunityToolkit.Mvvm: https://www.nuget.org/packages/CommunityToolkit.Mvvm
- NuGet Microsoft.EntityFrameworkCore.Sqlite: https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite
- NuGet Microsoft.NET.Test.Sdk: https://www.nuget.org/packages/Microsoft.NET.Test.Sdk
- NuGet MSTest.TestFramework: https://www.nuget.org/packages/MSTest.TestFramework
- NuGet MSTest.TestAdapter: https://www.nuget.org/packages/MSTest.TestAdapter
- GitHub Actions checkout: https://github.com/actions/checkout
- GitHub Actions setup-dotnet: https://github.com/actions/setup-dotnet
- GitHub Actions upload-artifact: https://github.com/actions/upload-artifact

## Límites

- EF Core/SQLite está referenciado para validar la base técnica, pero T01 no crea DbContext, migraciones ni esquema de datos.
- El fixture `samples/t01-smoke.valdrio-dev.json` es exclusivamente de desarrollo. No es el formato de archivo VALDRIO ni congela un contrato de persistencia.
- La localización completa y los recursos .resx pertenecen a T01A.

T05 mantiene las versiones de paquetes: nuevo StudyDbContext/mapping v3 y migración propia, sin reescribir snapshots históricos ni añadir bibliotecas de importación. La elección de lector XLSX se realiza en T06 con documentación/versiones/licencia verificadas.
