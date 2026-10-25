# Pruebas y diagnósticos

Desde la raíz del proyecto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\probar.ps1
```

El script compila Release y ejecuta --test con datos sintéticos. Requiere Windows con escritorio. Cada ejecución crea una carpeta única en artifacts/tests/. resultados.txt registra grupos, duración y resultado; error.txt contiene la excepción si falla. El proceso devuelve 0 al aprobar; se detiene al primer fallo.

Hay 33 grupos y una entrada de suite integrada (34 entradas, no 34 casos independientes). Cubren herramientas, capas, selecciones, transformaciones, archivos, sesiones, recuperación, sombras, ayuda, recursos, interfaz, paneles, animaciones, bienvenida y cursor. Las pruebas generan sus propios documentos: no necesitas aportar dibujos personales.

La ejecución directa de PixelArt.exe --test utiliza %TEMP%/Aropix-tests. AROPIX_TEST_OUTPUT_ROOT permite elegir el directorio padre; el controlador añade un identificador único. El parámetro opcional -Proyecto comprueba la copia cargada de un archivo sin sobrescribir el original.

## Diagnósticos opcionales

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\medir-historial.ps1 -Repeticiones 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\medir-autoguardado.ps1
```

El historial prueba 17 escenarios: cinco dimensiones y tres cantidades de capas, varios proyectos y presupuesto; admite entre una y tres repeticiones. El autoguardado prueba seguridad y ocho escenarios de rendimiento. Los scripts usan procesos secuenciales y límites de memoria y tiempo. No ejecutes cargas grandes en paralelo. Los informes quedan en artifacts/history y artifacts/autosave.

## Límites

Las animaciones se desactivan en --test; sus pruebas avanzan un reloj manual. Se comprueban geometrías, imágenes, recursos, estados y fallos simulados, pero esto no sustituye verificar interacción física, distintos DPI, selectores nativos, arrastre y foco. Algunas capturas no incluyen el cursor nativo; su regresión lo dibuja directamente desde el handle de Windows.

No hay CI sin escritorio configurado. No se incluyen capturas ni resultados de desarrollo en el repositorio público. No publiques automáticamente los archivos generados por las pruebas.