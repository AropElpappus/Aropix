# Aropix

Aropix es un editor de Pixel Art gratuito y de código abierto para Windows, desarrollado en C# y Windows Forms. Funciona localmente, sin navegador ni conexión a Internet.

## Funciones

- Lápiz cuadrado/circular, borrador, cubeta, cuentagotas y figuras geométricas.
- Varita mágica con conectividad de cuatro lados; Control añade regiones y Alt las resta.
- Copiar, pegar, mover, girar, voltear y redimensionar selecciones irregulares.
- Capas con opacidad, visibilidad, bloqueo, orden por arrastre y sombras con vista previa.
- Importación PNG/JPG y exportación PNG del documento o de sus capas.
- Simetría con ejes desplazables, teclado de precisión, zoom y cuadrícula adaptativa.
- Lienzos de hasta 2048 × 2048, múltiples proyectos, historial y paletas.
- Autoguardado, recuperación de sesiones, paneles personalizables y Centro de Ayuda local.

## Requisitos

Windows con escritorio gráfico y .NET Framework 4.x. Entorno comprobado: .NET Framework Full 4.8.09037 (Release 533325), compilador C# 4.8.9232.0. No se ha certificado una versión mínima mediante pruebas en otros equipos.

La compilación utiliza MSBuild y C# del Framework instalado en Windows. Comprueba que exista `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe` o su equivalente en `Framework`. No basta con instalar el SDK de .NET moderno. No se utiliza NuGet ni bibliotecas externas. Evita rutas de descarga excesivamente largas: el Framework actual puede fallar cuando un archivo temporal supera 260 caracteres; una carpeta corta como C:\dev\Aropix es adecuada.

## Compilar y ejecutar

Desde la carpeta del proyecto, en PowerShell:

```powershell
.\scripts\compilar.cmd
.\bin\Release\PixelArt.exe
```

Para reconstruir o compilar Debug:

```powershell
.\scripts\compilar.cmd /t:Rebuild
.\scripts\compilar.cmd /p:Configuration=Debug
```

La salida oficial de uso es `bin/Release/PixelArt.exe`, AnyCPU. El icono y el logo están integrados en el ejecutable. Los scripts se pueden invocar por ruta absoluta desde cualquier carpeta. El proyecto usa MSBuild clásico, no `dotnet build` ni un proyecto SDK.

## Pruebas

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\probar.ps1
```

El script compila y ejecuta `--test`, muestra resultados y devuelve un código distinto de cero si falla. Las pruebas usan datos sintéticos aislados en `artifacts/tests/`. Requieren una sesión gráfica de Windows. La ejecución directa de `PixelArt.exe --test` utiliza una carpeta única dentro de `%TEMP%/Aropix-tests`.

Consulta [tests/README.md](tests/README.md) para los diagnósticos de historial y autoguardado y los límites de las comprobaciones automatizadas.

## Estructura

```text
Aropix.csproj        Lista explícita de fuentes, recursos y configuración
src/Core/           Entrada, documento y lienzo
src/Editing/        Capas, selección, historial y operaciones de edición
src/Tools/          Varita, puntas y navegación de herramientas
src/UI/             Controles, estilos, paneles, cursores y bienvenida
src/Persistence/    Sesiones, recuperación y autoguardado
src/Help/           Centro de Ayuda y contenido
assets/             Logo e icono integrados
tests/              Pruebas y diagnósticos
scripts/            Compilación, pruebas y mediciones
docs/               Arquitectura y distribución
```

Solo se compilan los 44 archivos enumerados en `Aropix.csproj`. Las clases parciales conservan el namespace `PixelArt`. Consulta [arquitectura](docs/ARQUITECTURA.md).

## Archivos y privacidad

Los archivos `.pixelart` contienen el proyecto: imágenes PNG en Base64, capas, paleta y ajustes. Se mantiene la lectura de las versiones 1 y 2 admitidas por el editor.

Las preferencias y recuperaciones se guardan en `%LOCALAPPDATA%/Aropix`. No son parte del repositorio. No publiques sesiones, recuperaciones ni dibujos personales. La copia pública no incluye archivos personales, ejecutables, capturas, resultados de pruebas ni históricos locales.

## Licencia

Aropix se distribuye bajo **GNU General Public License, versión 3 únicamente** (`GPL-3.0-only`). El texto completo está en [LICENSE](LICENSE).

Puedes usar, estudiar, modificar y redistribuir el programa, también con ayuda de IA. Se permite el uso comercial y vender copias. Al distribuir versiones modificadas debes cumplir GPLv3, mantener los avisos y proporcionar el código fuente correspondiente bajo esa licencia. El programa se ofrece sin garantía, en los términos de LICENSE.

La licencia cubre el código, la documentación y los recursos propios incluidos en este repositorio. Usar Aropix no obliga a licenciar bajo GPL los dibujos que crees: la licencia del programa no se aplica automáticamente a tu contenido.

## Contribuciones y distribución

Se aceptan sugerencias, reportes y contribuciones manuales o asistidas por IA. Consulta [CONTRIBUTING.md](CONTRIBUTING.md).

Para distribuir ejecutables, consulta [docs/DISTRIBUCION.md](docs/DISTRIBUCION.md). Publicar este repositorio o una Release es una acción aparte; preparar los archivos no los publica automáticamente.