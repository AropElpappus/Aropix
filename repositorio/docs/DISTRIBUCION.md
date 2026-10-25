# Distribuir Aropix

## Código fuente en GitHub

Incluye LICENSE, README, CONTRIBUTING, LEEME, .gitignore, .editorconfig, Aropix.csproj, src/, tests/, scripts/, los recursos requeridos de assets/ y la documentación pública de docs/.

No incluyas bin/, obj/, artifacts/, archive/, archivos personales .pixelart, sesiones, preferencias, recuperaciones, claves ni capturas de trabajo. El .gitignore no reemplaza revisar el contenido de una subida manual.

## Ejecutables en Releases

1. Compila Release y ejecuta las pruebas.
2. Prepara un ZIP con PixelArt.exe, LICENSE e instrucciones de ejecución. Los recursos gráficos ya están integrados; no copies XML personales.
3. Distribuye el código fuente correspondiente exacto de ese ejecutable, incluidos recursos y scripts de compilación, cumpliendo GPLv3. Una forma práctica para una descarga en línea es ofrecer junto al binario un archivo del código correspondiente de la misma versión, sin coste adicional.
4. Identifica la versión o revisión fuente usada. No ofrezcas un binario junto a fuentes de una versión distinta.
5. Conserva los avisos legales e indica cambios y fechas al distribuir versiones modificadas. Consulta LICENSE para todas las obligaciones.

GPLv3 permite distribución comercial y modificaciones. La versión oficial puede seguir ofreciéndose gratuitamente. No se añaden restricciones de uso de IA ni de venta a la licencia.

## Comprobaciones manuales antes de distribuir

El programa requiere Windows con .NET Framework y permisos para escribir en LocalAppData y en los destinos que el usuario elija para sus proyectos. No necesita escribir junto al ejecutable para guardar nuevas preferencias.

No se ha certificado una instalación limpia de Windows ni una versión mínima del sistema. Comprueba en otro equipo: inicio desde carpeta sin permisos de escritura, crear/guardar/reabrir proyectos, importar/exportar, recuperar sesión, escalado DPI, teclado y ventanas flotantes. No hay instalador ni firma de código incluidos.