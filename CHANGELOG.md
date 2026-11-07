# MooView 1.9.0

- Portable updates now select verified manifest-bearing ZIPs and retain portable mode. Only declared program/runtime files are replaced; user images/settings/unrelated files remain intact. No installer, shortcuts or file-association registration is invoked by the portable updater.
- Added opt-in automatic download and apply on normal app exit, off by default. Packages, portable payloads and the independent helper are prepared while the app runs. Other windows are awaited without force closing. Automatic application does not restart the app; manual update-and-restart remains available.
- Revalidate the latest consent after all windows exit, so disabling the option also stops an already waiting helper. Windows shutdown/restart defers application. Folder-scoped preferences/pending state prevent cross-updates between separate copies; verified staging survives app restarts.
- Added ZIP traversal/link/duplicate/size/file-hash checks, same-volume backup transactions and rollback for replacement failures. Only unchanged obsolete files tracked by the previous manifest are removed.
- Packaging generates the payload manifest and portable marker automatically. The installer excludes/removes that marker and accepts the project version from the build script; release tag/version mismatches fail the build.

## 한국어

- 포터블은 ZIP으로 프로그램 파일만 교체하며 포터블로 유지합니다. 사진·설정·기타 사용자 파일은 보존합니다.
- 업데이트 창에서 자동 다운로드·종료 시 적용을 켤 수 있습니다. 기본은 꺼짐이며, 종료 후 적용해도 앱을 자동으로 다시 열지 않습니다.
- 다른 창이 남아 있으면 기다리고, 자동 업데이트를 끄거나 Windows가 종료 중이면 적용을 보류합니다. 수동 업데이트 및 다시 시작도 유지합니다.
- 1.8.0 이하 포터블은 이번 1.9.0 ZIP으로 한 번 교체해야 이후 포터블 업데이트가 가능합니다.
# MooView 1.8.0

- Replaced the top-left image/import lookalike with a vector eye logo. Clicking it opens a brief localized introduction. Any mouse button inside dismisses and consumes the click/release; Escape or clicking another application also closes it.
- Replaced drifting slideshow intervals with absolute deadlines and paint-driven image presentation, with a wake-up pulse for an idle visual tree. One group is committed per distinct render; stalls do not burst through or skip groups.
- Added immediate live FPS changes and measured/requested playback FPS in the status bar. File-list and metadata work is decoupled from per-frame image updates.
- Added shared in-flight bitmap/GIF decodes, lookahead across looping folders, and a bounded decoded-image cache. Repeated GIF navigation reuses decoded data.
- Small-image benchmark: 30-fps observed presentation improved from about 21.4 to 29.5 image changes/s on this PC with 543 160×120 images. Actual rates depend on hardware and files.

## 한국어

- 좌상단 눈 아이콘을 클릭하면 MooView 소개가 표시됩니다. 아무 곳이나 클릭하거나 Esc를 누르면 닫히며 뒤쪽 메뉴가 함께 실행되지 않습니다.
- 자동 재생의 타이머 지연과 파일 목록 갱신 부담을 줄이고, 이미지가 실제로 그려지는 시점에 맞춰 넘기도록 수정했습니다.
- 재생 중 FPS 변경을 즉시 반영하고, 하단에 실제 측정 FPS / 설정 FPS를 표시합니다. 큰 이미지에서 느려져도 이미지를 건너뛰지 않습니다.
# MooView 1.7.0

- Fixed clipped rail, toolbar and window-control icons by giving glyphs enough content space and including stroke widths inside their viewport.
- Separated dropdown labels/icons and arrows into dedicated columns, including the editable FPS field. Korean and English labels have room at narrow window widths.
- Replaced the green tint with neutral grayscale light/dark palettes across controls, checkerboards, minimaps, crop/selection overlays and the tone graph. Drawing colors remain available.
- Default to Windows app appearance and respond to system theme changes. Click the theme icon or press D for a remembered manual override; right-click the icon to follow Windows again. Legacy settings start in system mode.

## 한국어

- 왼쪽 메뉴·툴바·창 버튼의 아이콘 잘림을 수정하고 드롭다운 글자와 화살표 영역을 분리했습니다.
- UI의 녹색 기운을 제거하고 무채색 라이트·다크 테마로 바꿨습니다.
- 기본값은 Windows 앱 모드를 따릅니다. 테마 아이콘 클릭/D로 수동 전환하고, 우클릭으로 Windows 연동을 복원합니다. 직접 선택한 테마는 저장됩니다.
# MooView 1.6.0

- Added an icon-only update entry, startup update checks with a 12-hour interval, an opt-out preference, and manual checks. Fullscreen, inactive windows, and modal editing defer automatic notices.
- Added Korean/English release notes, download progress and cancellation. Only newer stable MooView GitHub installers with valid SHA-256 asset digests are accepted; failed or cancelled downloads cannot install.
- Update and restart uses a separate bundled helper, waits for the viewer to exit, installs into the existing folder without force-closing applications, and reopens the current image. Other MooView windows must be closed first.

## 한국어

- 왼쪽 하단 다운로드 화살표 아이콘에서 업데이트를 수동 확인하거나 시작 시 자동 확인을 켜고 끌 수 있습니다.
- 자동 확인은 12시간 간격이며 전체화면·편집 중에는 알림을 미룹니다. 업데이트를 누르면 다운로드와 SHA-256 검증 후 설치하고 현재 이미지로 다시 시작합니다.
- 1.5.0 이하 버전은 이번 설치 파일로 한 번 업데이트해야 합니다. 이후 버전부터 앱 안에서 업데이트할 수 있습니다.
# MooView 1.5.0

- Moved minimize/maximize/close onto the image toolbar row and removed the separate empty caption row. Blank toolbar space still drags the window.
- Added filenames to the bottom bar, including all visible filenames in multi-image views and a full-name tooltip.
- Added a draggable image timeline. Click or drag anywhere on its track to browse folder images. Seeking pauses slideshow playback, coalesces rapid requests, and keeps names/counters synchronized. Normal next/previous navigation still advances full image groups.
- Reworked icons as cached vector geometries with DPI-aware strokes and axis guidelines. Button borders, dropdown chevrons, checkboxes and slider thumbs use a consistent vector style. Grayscale text avoids RGB color fringing.

## 한국어

- 창 버튼을 메뉴와 같은 줄 우측으로 이동하고 별도의 빈 상단 줄을 제거했습니다.
- 하단 바에 파일명을 표시하며 여러 장 보기에서는 표시 중인 파일 이름을 함께 보여줍니다.
- 하단 탐색 슬라이더를 클릭하거나 드래그하면 폴더의 이미지를 바로 탐색합니다. 수동 탐색을 시작하면 자동 재생은 정지합니다.
- 아이콘의 벡터 도형, 선 굵기와 픽셀 정렬을 다듬고 버튼·체크박스·슬라이더 스타일을 통일했습니다.

# MooView 1.4.0

- Sharper UI: Per-Monitor V2 DPI awareness, layout pixel rounding, display text formatting, and removal of whole-container shadow effects.
- Resize the file sidebar by dragging its divider. The chosen width is remembered when hiding the panel, leaving fullscreen, and restarting.
- Integrated minimize, maximize/restore, and close controls replace the native main-window title bar. Drag the blank header to move; double-click it to maximize/restore. Native border resizing is retained.
- Multi-image navigation replaces an entire group at a time across wheel, keyboard, toolbar and slideshow. Partial first/last groups remain separate and do not mix images from the previous view.

## Included changes since the public 1.2.0 release

- Drag images and selected files to Explorer and other programs; refresh the viewer after source files move.
- A slider changes the file list into resizable thumbnails.
- Shift/Ctrl/Alt selection, click-to-deselect, and checkbox sweep selection.
- Six muted editor color swatches and a shared pen/eraser diameter slider.
- Direct text selection with move/resize/rotation handles; simplified editor controls.
- Crop confirmation immediately after selecting a rectangle.
- A repeat icon toggle replaces the slideshow loop checkbox.

## 한국어

- UI 전체를 흐리게 만들 수 있는 그림자 효과를 제거하고 화면 배율별 픽셀 정렬을 적용했습니다.
- 파일 탐색 패널과 이미지 영역 사이의 구분선을 끌어 패널 너비를 조절합니다. 너비는 다음 실행에도 유지됩니다.
- 메인 창 타이틀 바를 없애고 우상단에 최소화·최대화/복원·종료 버튼을 통합했습니다. 빈 상단을 드래그하면 창 이동, 더블클릭하면 최대화/복원합니다.
- 3장 보기의 1·2·3 다음은 4·5·6입니다. 마지막에 남은 장수만 표시하며 휠·키보드·버튼·자동 재생에 동일하게 적용됩니다.

The installer includes the .NET runtime. Binaries are not code-signed. GIF editing exports its first frame; saved images contain flattened layers. See README.md for the supported formats and other editing notes.
