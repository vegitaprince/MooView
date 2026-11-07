# MooView

A compact Windows image viewer with an icon-only interface, Korean/English UI, animated GIF playback, multi-image layouts, and a small layered editor.

Windows 10/11 x64. Download **MooView-Setup-1.9.0-x64.exe** from [Releases](https://github.com/vegitaprince/MooView/releases/latest). The installer includes .NET; no separate runtime installation is required. A portable ZIP is also available.

## Updates

Use the update icon near the bottom of the left rail. The update window identifies the current copy as **Installed** or **Portable** and selects the matching stable release asset. **Check now** is always available. Automatic checks are enabled by default, run on startup when due, and are revisited while the app remains open (at most one network check per 12 hours).

**Automatically download updates and apply on app exit** is off by default. When enabled, MooView downloads the matching installer or portable ZIP, verifies its GitHub SHA-256 digest, and prepares an independent update helper while you keep using the app. It applies only after a normal app exit and after all other MooView processes in the same folder have exited. It does not force-close windows or restart the app. Windows shutdown/restart defers application to a later normal exit. Turning the option off also prevents an already staged update from applying automatically, including when a helper is waiting for another window. Failed/cancelled/incomplete downloads never become pending updates.

Manual **Update and restart** remains available. Save edits and close other MooView windows first. A verified staged update is reused and the current image is reopened after a manual update. Automatic download progress does not interrupt fullscreen or editing with a dialog.

Portable updates stay portable: they use a manifest-bearing ZIP, replace only known program/runtime files, preserve images/settings/unrelated files in the same folder, and create no installer/uninstaller or file associations. Manifest paths, ZIP links/duplicates/expanded size, file lengths and individual SHA-256 digests are checked. Replacements use a backup transaction; ordinary file replacement errors restore the prior program files. Unchanged obsolete files tracked by the prior manifest can be removed; unrelated files are retained. The target folder must remain present and writable.

**Migration:** portable versions 1.8.0 and earlier need the 1.9.0 portable ZIP once; their old updater only understands installers. Installed copies can use the normal installer update. The installer excludes/removes the portable marker so it remains an installed copy.

Preferences, available-release metadata and verified pending handoffs are scoped to the application folder under `%LOCALAPPDATA%\MooView`. This keeps copies in separate folders from inheriting each other's consent or packages. Existing viewer settings keep their existing location. Packages/helpers/logs are cached under `Updates` for retries and troubleshooting; interrupted Windows shutdown leaves the update pending. No GitHub login or token is required.

Distribution builds use `--package-portable` to create `MooView.files.json` plus a portable marker and an archive containing exactly the declared payload. Both `MooView-Setup-X.Y.Z-x64.exe` and `MooView-Portable-X.Y.Z-x64.zip` must be attached to a stable `vX.Y.Z` release with GitHub asset digests. The build checks tag/project-version consistency. SHA-256 detects damaged downloads; it does not replace publisher code signing.

## Appearance

The interface uses neutral gray light/dark palettes. On first launch, including migration from older preferences, it follows Windows **app** light/dark mode and updates when Windows changes. Click the theme icon (or press D) to choose the opposite theme; that manual choice is remembered across restarts. Right-click the theme icon to resume following Windows. This does not change Windows settings. Drawing swatches remain colored so you can edit in color.

Rail and toolbar buttons reserve space for their complete vector glyphs. Dropdowns keep content and the arrow in separate columns, including the editable FPS field, so localized labels and arrows do not overlap.

## About and slideshow

Click the eye logo at the top left for a brief introduction. Click anywhere inside the app to dismiss it; that click is consumed so it does not trigger a menu or image gesture behind the card. Escape or activating another application also dismisses it. Korean/English and the current theme apply to the card.

Slideshow pacing uses absolute elapsed-time deadlines, requests a paint when due, and commits at most one image group per distinct WPF render. The rate can be changed during playback (0.1–30 fps). The status bar shows measured image commits / requested fps; changing the rate resets that measurement window. Hardware, image decoding and display refresh may limit presentation speed. Slow frames never cause bursts that skip image groups.

A bounded 192 MiB/256-entry decoded image cache (plus currently displayed images) shares in-flight reads, preloads upcoming images across loop boundaries, and reuses GIF data. File-list/metadata updates are decoupled from image presentation. Manual seeking pauses playback. A local benchmark with 543 small 160×120 images improved observed 30-fps presentation from about 21.4 to 29.5 image changes per second; these are measurements on this PC, not a guarantee for all images/devices.

## Features

- Window controls share the image toolbar row; no separate caption row.
- Bottom-bar filenames and a draggable image timeline. Click/drag the track to browse directly; manual seeking pauses slideshow playback.
- Cached vector icons, DPI-aware strokes, grayscale text, and consistent vector buttons/checkboxes/sliders.


- Crisp UI: Per-Monitor V2 DPI awareness, pixel-aligned layout and display text formatting; no shadow effects rasterizing whole controls.
- Drag the divider beside the file sidebar to resize it. Its width is saved between sessions.
- Integrated minimize/maximize/close buttons. Drag the blank header to move; double-click it to maximize/restore. Native edge resizing remains available.
- Multi-image navigation advances the complete visible group with no overlapping images. The last group shows only remaining images. This applies to wheel, keyboard, toolbar and slideshow.


- Drag the displayed image or selected sidebar files to Explorer and other programs. When zoomed in, Ctrl+drag exports the file; ordinary drag pans the image. The destination decides whether to copy or move; Ctrl/Shift follow its drop behavior. MooView refreshes after files move out.
- Drag the sidebar size slider from compact filenames to small or large thumbnails. Shift selects a range, Ctrl toggles individual files, Alt clears a file, and clicking a selected file again clears it after mouse release. Sweep the checkboxes to select or clear a range.
- The editor has six muted color swatches and a shared pen/eraser diameter slider. Select text directly and use its handles to resize/rotate. Drawing a crop rectangle asks for confirmation immediately.
- Click the repeat icon to turn slideshow looping on or off.

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
