# Luma Player 0.6

Luma Player is a lightweight Windows 11 media player with a clean white interface and hardware-accelerated playback through mpv/libmpv.

## Offline Windows installer

1. Download the verified `LumaPlayer-Offline-Setup-v0.6.0.exe` artifact produced by the GitHub Actions Windows runner.
2. Run the installer. The compiled player and x64 mpv playback engine are already bundled; the target PC does not compile or download application components.
3. Windows 11 opens **Default apps > Luma Player**. Click **Set default** once to confirm the change.

The installation is per-user and does not request administrator permission. No internet connection is required during installation. Windows retains control of the final default-app confirmation.

## Controls

- Mouse wheel anywhere except the timeline: volume up/down by 2%.
- Mouse wheel on timeline: forward/backward by 0.1 second per wheel step.
- Single left-click on video: play/pause.
- Double left-click on video: enter/exit fullscreen.
- Right-click on video: options menu.
- Drag and drop: open a supported video or audio file.
- Previous/Next: move through all supported media files in the current folder. The list stays hidden.
- Frame buttons: one frame backward/forward.
- Capture: saves a PNG in `Pictures\Luma Player\Screenshots`.
- Record: saves video as MKV and audio as MKA in `Videos\Luma Player\Recordings`.
- Speed slider: continuously adjustable from 0.25x to 4.00x; double-click resets it to 1.00x.

The next folder media file starts automatically when the current file finishes. Filename order uses Windows natural sorting.

Supported audio extensions: MP3, WAV, FLAC, M4A, AAC, OGG, OPUS, WMA, AIFF, ALAC, APE, AC3, and DTS.

## Keyboard shortcuts

- `Space`: play/pause
- `F` or `F11`: fullscreen
- `Esc`: leave fullscreen
- `Left/Right`: seek 5 seconds
- `Ctrl+Left/Ctrl+Right`: previous/next frame
- `Up/Down`: volume
- `Page Up/Page Down`: previous/next folder media file
- `S`: screenshot
- `R`: start/stop recording

## Recording behavior

Recording copies the current media stream directly instead of re-encoding it. This keeps CPU/GPU use low even with high-resolution videos. Seeking, frame stepping, or changing videos safely finishes an active recording first.

## 8K playback

8K performance depends on the video's codec, bitrate, storage speed, GPU decoder support, display, and graphics driver. The player requests automatic hardware decoding and Direct3D 11 output.

## Performance changes in 0.6

- Media starts loading before the current folder is scanned.
- Folder media discovery and natural sorting run in the background.
- Duplicate mpv folder autoloading is disabled.
- Playback cache memory limits are reduced for a lighter footprint.
- Input hooks are active only while Luma Player is the foreground app.
- Single-click responds immediately; double-click restores playback state and toggles true fullscreen.
- Top and bottom bars are compact to maximize the video area.

Version 0.6 is compiled on GitHub's Windows runner and packaged using Inno Setup. The workflow verifies the compiled EXE version, bundled playback-engine size, installer size, and Windows PE signature before publishing the artifact. The top bar visibly shows `Luma Player 0.6`.
