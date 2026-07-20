# DiscoInfierno

Prototipo Unity 3D con mecánica slingshot en el plano XZ.

## Requisitos

- Unity **6000.3.20f1** (o compatible con el proyecto)
- Git LFS recomendado si más adelante agregás assets pesados (audio/video/texturas grandes)

## Abrir el proyecto

1. Clonar el repo
2. Abrir la carpeta con Unity Hub
3. Abrir la escena `Assets/DiscoInfierno/Scenes/Level1.unity`

## Estructura

- `Assets/DiscoInfierno/` — gameplay, escenas, materiales
- `Assets/Plugins/Demigiant/DOTween/` — tweening
- `Assets/Epic Toon FX/` — VFX (explosiones)
- `Packages/` / `ProjectSettings/` — configuración Unity (versionada)

## Notas Git (Unity)

Se ignoran carpetas generadas (`Library/`, `Temp/`, `Logs/`, `Obj/`, `UserSettings/`).  
No subas builds locales ni archivos de IDE regenerables (`.csproj`, `.sln`).
