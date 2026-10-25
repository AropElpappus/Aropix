# Contribuir a Aropix

Se aceptan reportes de errores, sugerencias y cambios manuales o asistidos por IA.

1. Describe el problema, pasos para reproducirlo, resultado esperado y versión de Windows/.NET.
2. Lee [la arquitectura](docs/ARQUITECTURA.md) y compila con `scripts/compilar.cmd`.
3. Mantén cambios pequeños y reutiliza los sistemas existentes. Incluye cada fuente nueva explícitamente en `Aropix.csproj`.
4. Respeta `.editorconfig`. No cambies incidentalmente el framework, formato de proyecto ni identificadores de herramientas.
5. Ejecuta `scripts/probar.ps1` y distingue pruebas ejecutadas de comprobaciones manuales pendientes.
6. No adjuntes sesiones, preferencias, claves, dibujos personales ni capturas con información privada.

## Licencia de las contribuciones

Las contribuciones destinadas a incorporarse a Aropix deben poder distribuirse bajo GNU GPL versión 3 únicamente (`GPL-3.0-only`), como el proyecto; consulta [LICENSE](LICENSE). Conserva los avisos existentes e identifica cambios y fechas al distribuir versiones modificadas conforme a la licencia. No se exige ceder la titularidad de tu contribución.

Si utilizas IA, revisa el resultado y ejecuta las pruebas. Usar IA no elimina tu responsabilidad de comprobar que puedes aportar ese código y que no contiene material incompatible ni datos privados. No incluyas dependencias o recursos de terceros sin identificar su procedencia y comprobar la compatibilidad de sus licencias.