# Arquitectura de Aropix

Windows Forms en C#, namespace PixelArt, .NET Framework instalado en Windows. Aropix.csproj utiliza la tarea Csc de MSBuild clásico con 44 inclusiones explícitas: 31 fuentes de src/ y 13 de tests/. No usa NuGet ni inclusión recursiva automática.

| Carpeta | Responsabilidad |
| --- | --- |
| src/Core | PixelArt.cs contiene entrada, documento, lienzo, editor y pruebas originales. |
| src/Editing | Capas, historial, selecciones, proyectos, importación, dimensiones, sombras y exportación. |
| src/Tools | Varita mágica, puntas, grupos de herramientas y navegación precisa. |
| src/UI | Tema, diálogos, controles, distribución, animaciones, cursores y bienvenida. |
| src/Persistence | Sesiones, recuperación y autoguardado; SymmetrySave conserva también su lógica de simetría. |
| src/Help | Ventana no modal y catálogo local de ayuda. |
| tests | Pruebas separadas, resultados y diagnósticos de rendimiento. |

Las clases parciales comparten estado dentro del mismo ensamblado. Algunas pruebas y formularios siguen junto a la implementación correspondiente; la organización física no los separa en bibliotecas. No compiles indiscriminadamente todos los archivos que encuentres fuera de la lista del proyecto.

Los comandos compartidos de UICommands conectan menús y accesos rápidos. Las herramientas conservan sus identificadores existentes. El historial combina capturas completas con entradas ligeras para propiedades. Las selecciones irregulares usan máscaras que acompañan sus transformaciones. El formato .pixelart y las preferencias son independientes de estas carpetas.

El icono assets/aropix.ico se integra como recurso nativo y assets/aropix-logo.png como recurso administrado Aropix.Logo. No requieren archivos externos durante la ejecución.

Las preferencias y recuperación viven en LocalAppData/Aropix. El mecanismo existente también contempla archivos antiguos junto al ejecutable; no deben incluirse en una distribución. Las pruebas emplean carpetas aisladas y datos sintéticos.

La compilación conserva el compilador del Framework, AnyCPU y los ajustes existentes. No promete binarios idénticos entre versiones diferentes de compilador ni certifica una versión mínima de Windows. Para compilación y pruebas, sigue el README de la raíz.