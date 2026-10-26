# MooView

A compact Windows image viewer with an icon-only interface, Korean/English UI, animated GIF playback, multi-image layouts, and a small layered editor.

Windows 10/11 x64. Download **MooView-Setup-1.2.0-x64.exe** from [Releases](https://github.com/vegitaprince/MooView/releases/latest). The installer includes .NET; no separate runtime installation is required. A portable ZIP is also available.

## Features

- Wheel / Space image navigation; middle-click or F11 fullscreen, Esc to exit.
- Fullscreen controls stay hidden during keyboard navigation and appear when the mouse moves near the top edge.
- One to four images, horizontal / grid / vertical layouts, fit / fill / width / custom zoom, and a zoom minimap.
- Light and dark palettes; transparency checkerboard or automatic theme background.
- GIF frame timing, repeat counts, transparency and disposal handling, pause/resume.
- EXIF view/copy, batch conversion, and rename preview with collision checks.
- Text objects with selection, dragging, resizing, rotation, content editing and deletion. Drawing with a point eraser and undo/redo.
- Crop, image rotation/flip, levels and RGB curves.
- Korean / English toggle in the lower left rail. The file sidebar is hidden by default; Tab toggles it.

## Default image app

The installer registers MooView with Windows. Use the settings icon in MooView, or right-click an image → Open with → Choose another app → MooView → Always. Windows controls the final default selection per extension.

## Editing notes

Text and strokes stay editable during the open editor session, including after saving. Exported image files contain flattened pixels; they do not store editable layers. GIF editing/conversion uses its first frame; TIFF uses its first page. Export formats: PNG, JPEG, BMP, TIFF and GIF. EXIF/ICC metadata is not copied to edited/exported images. JPEG alpha is composited over white. WebP requires a compatible Windows WIC codec. RAW and archives are not supported. Slideshow supports a target rate up to 30 fps; actual speed depends on decoding and hardware.

## Build

Install .NET 10 SDK and Inno Setup 6, then run:

```powershell
./build.ps1
```

Run tests with `MooView.exe --ui-test`. This also runs the image-processing tests. Test files are created beside the executable. Build outputs are under `artifacts/`.

## 한국어

[최신 릴리스](https://github.com/vegitaprince/MooView/releases/latest)의 설치 파일을 실행하세요. .NET 런타임을 포함하며 사용자 계정에 설치됩니다. 왼쪽 아래 언어 아이콘으로 한영 전환, Tab으로 파일 목록 표시/숨김, 휠 클릭으로 전체화면을 전환합니다.

텍스트를 추가한 뒤 선택 도구로 클릭하면 이동할 수 있습니다. 모서리 사각형은 크기, 위쪽 원은 회전을 조절합니다. 더블클릭으로 내용을 수정하고 Delete로 삭제할 수 있습니다. 지우개는 그린 선의 일부만 지우며 원본 이미지를 변경하지 않습니다.

The current binaries are not code-signed. A publisher signing certificate is not included in this project.

