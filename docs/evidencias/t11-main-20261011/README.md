# Integración T11 en main — 2026-10-11

Autorización explícita del propietario: «integra en main».
Base main: `6f54557`; rama integrada: `feature/t11-reparto-secundario-cierre@b38c65d`.
Merge local `263bb47818ce8950229483c2ef504ba2bd2ba39e`, sin conflictos y árbol idéntico a la rama validada.

La suite completa detectó un recurso ausente: AllocationUI.SecondaryBehaviorAssumedFixed.
Se añade «Fijo por defecto» / «Fixed by default» y una prueba que exige etiquetas
para todos los comportamientos del dominio. La prueba de cierre primario antes
fallida y el resto de la batería pasan tras la corrección.

Comandos finales:

```powershell
dotnet build Valdrio.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --blame-hang-timeout 90s --blame-hang-dump-type none --logger "trx;LogFileName=main-final.trx" --logger "console;verbosity=normal" --results-directory artifacts/main-final
```

Build: 0 errores y 0 advertencias. Suite completa: **627/627**, 3,306 minutos.
Los primeros intentos de build se solaparon con testhost y fallaron al copiar DLL
bloqueadas (MSB3027/MSB3021). Se detuvieron únicamente los procesos de la prueba
fallida y se repitieron build y test secuencialmente, con el resultado anterior.
Validación visual reutilizada: 91 comprobaciones WPF documentadas en
../t11-claves-viabilidad-20261011/README.md; no hay cambio de layout en esta integración.

No se ejecutan workflows. Coste Actions: 0 USD. T12 comienza después de esta integración.
