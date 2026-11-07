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
