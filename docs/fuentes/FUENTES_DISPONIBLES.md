# Fuentes disponibles y huellas

Fecha de reconciliación: 2026-10-04

## Fuentes documentales individuales

| Archivo | SHA-256 |
|---|---|
| `VALDRIO_Prompt_Desarrollo_v0_4.md` | `6a44af88c8688e11a32b875661c646c8afe5642c9ac9aa60d168494c07a187f2` |
| `VALDRIO_Especificacion_Internacional_v0_4.pdf` | `ffa4e3b94822543a4a04b2414757814287e35ce203b451faef265268421a3767` |
| `VALDRIO_Estrategia_y_Posicionamiento.pdf` | `38f621e67859dbe57add1577affed112f17bc2e16a43ed48ebc56fc3906ffe01` |
| `VALDRIO_Manual_de_Marca_v1.pdf` | `fb795aa0db6beddd0acabb0b1e5747c7a8046f5698282243d602837203e4e253` |
| `VALDRIO_Marketing_y_Mensajes_ES_EN.pdf` | `48780f136775d653c407c0d3b2d37d2c32c88188b4b1fd3da582ab08057fc310` |
| `VALDRIO_Naming_y_Comprobacion_Publica.pdf` | `b0014b661c0e081186e9268ece6fa97a80450b5a3406e5b2f9f5e913e2ce8aed` |

## Paquete de desarrollo v0.4

Archivo: `VALDRIO_Paquete_Desarrollo_v0_4.zip`  
SHA-256 del ZIP: `1d8f8c21e3386117426f0eede257508a0bae30ed8c48cec7d2fb0e8ac68d7ced`

Contenido comprobado: 59 entradas, incluyendo:

- `LEEME.md`, `MANIFIESTO.json`, `SHA256SUMS.txt`;
- prompt maestro y PDF internacional;
- especificaciones 01-05;
- 16 contratos metodológicos;
- 38 fichas de tareas;
- 39 casos numéricos;
- 15 criterios internacionales;
- catálogo monetario de ejemplo;
- verificador de referencias;
- plantillas de decisión, estado, evidencia y revisión.

## Comprobaciones ejecutadas

### Integridad

```text
sha256sum -c SHA256SUMS.txt
```

Resultado: todas las entradas enumeradas = **OK**.

### Referencias numéricas

```text
python3 casos/verificar_referencias.py
```

Resultado:

- `status = passed`
- `verified_count = 39`
- C01-C39 presentes.

Alcance declarado por el propio verificador: referencias sintéticas con `Fraction`; **no** prueba del motor C#, SQLite ni WPF.

## Corrección de registro

Una versión anterior de este archivo afirmó erróneamente que el ZIP no estaba disponible. Esa afirmación queda revocada por esta reconciliación y por la evidencia `docs/evidencias/T00_PAQUETE_2026-10-04.md`.
