# Directrices permanentes de VALDRIO

Antes de modificar la interfaz, leer `docs/directrices/USABILIDAD.md`.
La complejidad metodológica nunca justifica complejidad de uso. Priorizar recorridos
guiados, requisitos comprensibles, ayuda contextual y guardado inequívoco, conservando
todas las funciones. El propietario autorizó completar T07 el 2026-10-07 en la rama existente. Mantener
estos requisitos de usabilidad en T07 y las fases siguientes.

## Validación local y costes de CI — todo el proyecto y todas las fases

Decisión permanente del propietario, 2026-10-07. Se aplica a todas las fases
presentes y futuras, tareas, ramas y entregas de VALDRIO; no está limitada a T07.

El agente ejecuta localmente la compilación y las comprobaciones pertinentes como
parte del trabajo autorizado, resuelve los fallos y registra los resultados reales,
sin esperar a que el propietario lance comandos o dé una confirmación adicional.

GitHub Actions se ejecuta únicamente por petición expresa del propietario. Los
workflows de compilación y validación no deben activarse automáticamente por push,
pull request, calendario, otros workflows ni eventos equivalentes. No reactivar
estos disparadores ni añadir otros que generen compilaciones remotas automáticas
sin una nueva instrucción expresa del propietario.
