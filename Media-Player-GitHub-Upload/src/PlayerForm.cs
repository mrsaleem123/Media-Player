using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LumaPlayer
{
    internal sealed class PlayerForm : Form
    {
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private static readonly string[] VideoExtensions = new string[]
        {
            ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv",
            ".flv", ".ts", ".mts", ".m2ts", ".mpg", ".mpeg", ".vob",
            ".ogv", ".3gp"
        };

        private static readonly string[] AudioExtensions = new string[]
        {
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus",
            ".wma", ".aiff", ".aif", ".alac", ".ape", ".ac3", ".dts"
        };

        private readonly string initialFile;
        private readonly List<string> folderFiles = new List<string>();
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        private readonly ToolTip toolTip = new ToolTip();

        private Panel videoPanel;
        private Panel controlsPanel;
        private Label emptyLabel;
        private Label statusLabel;
        private Label timeLabel;
        private TimelineBar timeline;
        private PlayerIconButton openButton;
        private PlayerIconButton previousButton;
        private PlayerIconButton frameBackButton;
        private PlayerIconButton playButton;
        private PlayerIconButton frameForwardButton;
        private PlayerIconButton nextButton;
        private PlayerIconButton captureButton;
        private PlayerIconButton recordButton;
        private SpeedSlider speedSlider;
        private VolumeSlider volumeSlider;

        private IntPtr mpv;
        private string currentFile;
        private int currentIndex = -1;
        private bool recording;
        private bool endHandled;
        private bool fullscreen;
        private DateTime transientStatusUntil;
        private FormBorderStyle savedBorderStyle;
        private Rectangle savedBounds;
        private FormWindowState savedWindowState;
        private double currentVolume = 70.0;
        private int queueGeneration;
        private DateTime fullscreenControlsHideAt;
        private DateTime lastFullscreenRevealRequest;
        private bool cursorHidden;

        private IntPtr mouseHook;
        private IntPtr keyboardHook;
        private NativeMethods.LowLevelMouseProc mouseHookDelegate;
        private NativeMethods.LowLevelKeyboardProc keyboardHookDelegate;

        private DateTime pendingClickTime;
        private Point pendingClickPosition;

        internal PlayerForm(string file)
        {
            initialFile = file;
            BuildInterface();
            ConfigureWindow();
            ConfigureTimers();
        }

        private void ConfigureWindow()
        {
            Text = "Luma Player";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 500);
            ClientSize = new Size(1120, 700);
            BackColor = Color.White;
            KeyPreview = true;
            AllowDrop = true;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            LoadWindowIcon();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            KeyDown += OnKeyDown;
            FormClosing += OnFormClosing;
            Shown += OnShown;
            Activated += delegate { InstallInputHooks(); };
            Deactivate += delegate { RemoveInputHooks(); };
            Resize += delegate { ApplyRoundedCorners(); };
        }

        private void BuildInterface()
        {
            SuspendLayout();

            controlsPanel = new Panel();
            controlsPanel.Dock = DockStyle.Bottom;
            controlsPanel.Height = 94;
            controlsPanel.BackColor = Color.White;
            controlsPanel.Padding = new Padding(12, 4, 12, 8);
            controlsPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(235, 238, 243)))
                    e.Graphics.DrawLine(pen, 0, 0, controlsPanel.Width, 0);
            };

            timeline = new TimelineBar();
            timeline.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            timeline.Location = new Point(12, 4);
            timeline.Height = 22;
            timeline.Width = 1088;
            timeline.SeekRequested += delegate(double seconds) { SeekAbsolute(seconds); };
            timeline.NudgeRequested += PauseAndNudgeTimeline;
            toolTip.SetToolTip(timeline, "Mouse wheel: pause and move 0.1 second forward/backward");

            timeLabel = new Label();
            timeLabel.Text = "00:00.000  /  00:00.000";
            timeLabel.ForeColor = Color.FromArgb(94, 103, 120);
            timeLabel.Location = new Point(16, 27);
            timeLabel.Size = new Size(205, 18);

            statusLabel = new Label();
            statusLabel.AutoEllipsis = true;
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            statusLabel.Text = "Ready";
            statusLabel.ForeColor = Color.FromArgb(116, 124, 139);
            statusLabel.Location = new Point(360, 27);
            statusLabel.Size = new Size(400, 18);

            openButton = CreateIconButton(PlayerIcon.Open, false);
            previousButton = CreateIconButton(PlayerIcon.Previous, false);
            frameBackButton = CreateIconButton(PlayerIcon.FrameBack, false);
            playButton = CreateIconButton(PlayerIcon.Play, true);
            frameForwardButton = CreateIconButton(PlayerIcon.FrameForward, false);
            nextButton = CreateIconButton(PlayerIcon.Next, false);
            captureButton = CreateIconButton(PlayerIcon.Camera, false);
            recordButton = CreateIconButton(PlayerIcon.Record, false);

            openButton.Click += delegate { OpenVideoDialog(); };
            previousButton.Click += delegate { PlayAdjacent(-1); };
            frameBackButton.Click += delegate { FrameStep(false); };
            playButton.Click += delegate { TogglePause(); };
            frameForwardButton.Click += delegate { FrameStep(true); };
            nextButton.Click += delegate { PlayAdjacent(1); };
            captureButton.Click += delegate { TakeScreenshot(); };
            recordButton.Click += delegate { ToggleRecording(); };

            toolTip.SetToolTip(openButton, "Open a video or audio file");
            toolTip.SetToolTip(previousButton, "Previous media file in this folder");
            toolTip.SetToolTip(frameBackButton, "Previous frame");
            toolTip.SetToolTip(playButton, "Play / Pause");
            toolTip.SetToolTip(frameForwardButton, "Next frame");
            toolTip.SetToolTip(nextButton, "Next media file in this folder");
            toolTip.SetToolTip(captureButton, "Save screenshot");
            toolTip.SetToolTip(recordButton, "Start / stop video recording");

            speedSlider = new SpeedSlider();
            speedSlider.Size = new Size(146, 36);
            speedSlider.Value = 1.0;
            speedSlider.ValueChanged += OnSpeedChanged;
            toolTip.SetToolTip(speedSlider, "Drag to change playback speed; double-click resets to 1.00x");

            volumeSlider = new VolumeSlider();
            volumeSlider.Size = new Size(170, 36);
            volumeSlider.Value = 70.0;
            volumeSlider.ValueChanged += OnVolumeChanged;
            toolTip.SetToolTip(volumeSlider, "Volume 0–200%");

            controlsPanel.Controls.Add(timeline);
            controlsPanel.Controls.Add(timeLabel);
            controlsPanel.Controls.Add(statusLabel);
            controlsPanel.Controls.Add(openButton);
            controlsPanel.Controls.Add(previousButton);
            controlsPanel.Controls.Add(frameBackButton);
            controlsPanel.Controls.Add(playButton);
            controlsPanel.Controls.Add(frameForwardButton);
            controlsPanel.Controls.Add(nextButton);
            controlsPanel.Controls.Add(captureButton);
            controlsPanel.Controls.Add(recordButton);
            controlsPanel.Controls.Add(speedSlider);
            controlsPanel.Controls.Add(volumeSlider);

            controlsPanel.Resize += LayoutBottomControls;

            videoPanel = new Panel();
            videoPanel.Dock = DockStyle.Fill;
            videoPanel.BackColor = Color.FromArgb(9, 12, 18);
            videoPanel.AllowDrop = true;
            videoPanel.DragEnter += OnDragEnter;
            videoPanel.DragDrop += OnDragDrop;

            emptyLabel = new Label();
            emptyLabel.AutoSize = false;
            emptyLabel.Dock = DockStyle.Fill;
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyLabel.ForeColor = Color.FromArgb(190, 197, 210);
            emptyLabel.Font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Point);
            emptyLabel.Text = "Drop a video or audio file here";
            emptyLabel.Cursor = Cursors.Hand;
            emptyLabel.Click += delegate { OpenVideoDialog(); };
            videoPanel.Controls.Add(emptyLabel);

            Controls.Add(videoPanel);
            Controls.Add(controlsPanel);

            RegisterVolumeWheel(this);
            ResumeLayout(true);
        }

        private void ConfigureTimers()
        {
            uiTimer.Interval = 200;
            uiTimer.Tick += OnUiTimer;
        }

        private void LoadWindowIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LumaPlayer.ico");
            try
            {
                if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            }
            catch { }
        }

        private void OnShown(object sender, EventArgs e)
        {
            ApplyRoundedCorners();
            InstallInputHooks();
            uiTimer.Start();
            if (!String.IsNullOrWhiteSpace(initialFile) && File.Exists(initialFile))
                LoadVideo(Path.GetFullPath(initialFile));
        }

        private void LayoutBottomControls(object sender, EventArgs e)
        {
            timeline.Width = Math.Max(200, controlsPanel.ClientSize.Width - 24);
            statusLabel.Width = Math.Min(400, Math.Max(160, controlsPanel.ClientSize.Width - 450));
            statusLabel.Left = (controlsPanel.ClientSize.Width - statusLabel.Width) / 2;

            int gap = 8;
            int total = openButton.Width + previousButton.Width + frameBackButton.Width + playButton.Width
                + frameForwardButton.Width + nextButton.Width + captureButton.Width + recordButton.Width
                + speedSlider.Width + volumeSlider.Width + gap * 9;
            int x = Math.Max(10, (controlsPanel.ClientSize.Width - total) / 2);
            int y = 49;

            Control[] ordered = new Control[]
            {
                openButton, previousButton, frameBackButton, playButton, frameForwardButton, nextButton,
                captureButton, recordButton, speedSlider, volumeSlider
            };

            for (int i = 0; i < ordered.Length; i++)
            {
                ordered[i].Left = x;
                ordered[i].Top = y;
                x += ordered[i].Width + gap;
            }
        }

        private PlayerIconButton CreateIconButton(PlayerIcon icon, bool primary)
        {
            PlayerIconButton button = new PlayerIconButton(icon, primary);
            button.Size = primary ? new Size(44, 40) : new Size(38, 36);
            return button;
        }

        private bool EnsureEngine()
        {
            if (mpv != IntPtr.Zero) return true;

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv-2.dll");
            if (!File.Exists(dllPath))
            {
                MessageBox.Show(
                    "Playback engine is missing. Reinstall Luma Player using the offline Windows installer.",
                    "Luma Player",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            try
            {
                videoPanel.CreateControl();
                mpv = MpvNative.mpv_create();
                if (mpv == IntPtr.Zero) throw new InvalidOperationException("Could not create the playback engine.");

                MpvNative.mpv_set_option_string(mpv, "terminal", "no");
                MpvNative.mpv_set_option_string(mpv, "msg-level", "all=no");
                MpvNative.mpv_set_option_string(mpv, "osc", "no");
                MpvNative.mpv_set_option_string(mpv, "input-default-bindings", "no");
                MpvNative.mpv_set_option_string(mpv, "input-vo-keyboard", "no");
                MpvNative.mpv_set_option_string(mpv, "window-dragging", "no");
                MpvNative.mpv_set_option_string(mpv, "hwdec", "auto");
                MpvNative.mpv_set_option_string(mpv, "vo", "gpu");
                MpvNative.mpv_set_option_string(mpv, "gpu-api", "d3d11");
                MpvNative.mpv_set_option_string(mpv, "keep-open", "yes");
                MpvNative.mpv_set_option_string(mpv, "autoload-files", "no");
                MpvNative.mpv_set_option_string(mpv, "demuxer-max-bytes", "32MiB");
                MpvNative.mpv_set_option_string(mpv, "demuxer-max-back-bytes", "8MiB");
                MpvNative.mpv_set_option_string(mpv, "sub-ass-override", "no");
                MpvNative.mpv_set_option_string(mpv, "volume-max", "200");
                MpvNative.mpv_set_option_string(mpv, "volume", "70");
                MpvNative.mpv_set_option_string(mpv, "screenshot-format", "png");

                long windowId = videoPanel.Handle.ToInt64();
                MpvNative.mpv_set_option(mpv, "wid", MpvNative.MPV_FORMAT_INT64, ref windowId);

                int result = MpvNative.mpv_initialize(mpv);
                if (result < 0) throw new InvalidOperationException("Playback engine initialization failed.");

                emptyLabel.Visible = false;
                return true;
            }
            catch (Exception exception)
            {
                if (mpv != IntPtr.Zero)
                {
                    try { MpvNative.mpv_terminate_destroy(mpv); }
                    catch { }
                    mpv = IntPtr.Zero;
                }
                MessageBox.Show(exception.Message, "Luma Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void LoadVideo(string path)
        {
            if (!File.Exists(path) || !IsSupportedMedia(path)) return;
            if (!EnsureEngine()) return;

            StopRecording(false);
            currentFile = Path.GetFullPath(path);
            int generation = ++queueGeneration;
            folderFiles.Clear();
            currentIndex = -1;
            endHandled = false;
            timeline.Duration = 0.0;
            timeline.Position = 0.0;

            MpvNative.Command(mpv, "loadfile", currentFile, "replace");
            statusLabel.Text = "Loading with hardware acceleration…";
            Text = Path.GetFileName(currentFile);
            BuildFolderQueueAsync(currentFile, generation);
        }

        private void BuildFolderQueueAsync(string selectedFile, int generation)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> discovered = new List<string>();
                int selectedIndex = -1;
                try
                {
                    string folder = Path.GetDirectoryName(selectedFile);
                    if (!String.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        foreach (string file in Directory.EnumerateFiles(folder))
                        {
                            if (IsSupportedMedia(file)) discovered.Add(file);
                        }
                        discovered.Sort(delegate(string left, string right)
                        {
                            return NativeMethods.StrCmpLogicalW(Path.GetFileName(left), Path.GetFileName(right));
                        });
                    }

                    for (int i = 0; i < discovered.Count; i++)
                    {
                        if (String.Equals(Path.GetFullPath(discovered[i]), selectedFile, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }
                catch { }

                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke(new Action(delegate
                    {
                        if (generation != queueGeneration || !String.Equals(currentFile, selectedFile, StringComparison.OrdinalIgnoreCase))
                            return;
                        folderFiles.Clear();
                        folderFiles.AddRange(discovered);
                        currentIndex = selectedIndex;
                    }));
                }
                catch { }
            });
        }

        private static bool IsSupportedMedia(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < VideoExtensions.Length; i++)
            {
                if (String.Equals(extension, VideoExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            for (int i = 0; i < AudioExtensions.Length; i++)
            {
                if (String.Equals(extension, AudioExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsAudioFile(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < AudioExtensions.Length; i++)
            {
                if (String.Equals(extension, AudioExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void PlayAdjacent(int direction)
        {
            if (folderFiles.Count == 0 || currentIndex < 0) return;
            int target = currentIndex + direction;
            if (target < 0) target = folderFiles.Count - 1;
            if (target >= folderFiles.Count) target = 0;
            LoadVideo(folderFiles[target]);
        }

        private void TogglePause()
        {
            if (mpv == IntPtr.Zero || String.IsNullOrEmpty(currentFile)) return;
            MpvNative.Command(mpv, "cycle", "pause");
        }

        private void FrameStep(bool forward)
        {
            if (mpv == IntPtr.Zero || String.IsNullOrEmpty(currentFile)) return;
            StopRecording(true);
            MpvNative.Command(mpv, forward ? "frame-step" : "frame-back-step");
        }

        private void SeekAbsolute(double seconds)
        {
            if (mpv == IntPtr.Zero) return;
            StopRecording(true);
            MpvNative.Command(mpv, "seek", seconds.ToString("0.000", CultureInfo.InvariantCulture), "absolute+exact");
        }

        private void SeekRelative(double seconds)
        {
            if (mpv == IntPtr.Zero || Math.Abs(seconds) < 0.0001) return;
            StopRecording(true);
            MpvNative.Command(mpv, "seek", seconds.ToString("0.000", CultureInfo.InvariantCulture), "relative+exact");
        }

        private void PauseAndNudgeTimeline(double seconds)
        {
            if (mpv == IntPtr.Zero || Math.Abs(seconds) < 0.0001) return;
            MpvNative.Command(mpv, "set", "pause", "yes");
            SeekRelative(seconds);
        }

        private void AdjustVolume(double delta)
        {
            if (mpv == IntPtr.Zero) return;
            currentVolume = Math.Max(0.0, Math.Min(200.0, currentVolume + delta));
            volumeSlider.SetValueFromPlayer(currentVolume);
            MpvNative.Command(mpv, "set", "volume", currentVolume.ToString("0.0", CultureInfo.InvariantCulture));
        }

        private void OnSpeedChanged(object sender, EventArgs e)
        {
            if (mpv == IntPtr.Zero) return;
            MpvNative.Command(mpv, "set", "speed", speedSlider.Value.ToString("0.00", CultureInfo.InvariantCulture));
        }

        private void OnVolumeChanged(object sender, EventArgs e)
        {
            currentVolume = volumeSlider.Value;
            if (mpv == IntPtr.Zero) return;
            MpvNative.Command(mpv, "set", "volume", currentVolume.ToString("0.0", CultureInfo.InvariantCulture));
        }

        private void TakeScreenshot()
        {
            if (mpv == IntPtr.Zero || String.IsNullOrEmpty(currentFile)) return;
            if (IsAudioFile(currentFile))
            {
                ShowStatus("Screenshot is available while playing video files", 4.0);
                return;
            }
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Luma Player", "Screenshots");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "Luma_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            int result = MpvNative.Command(mpv, "screenshot-to-file", path, "window");
            ShowStatus(result >= 0 ? "Screenshot saved: " + path : "Screenshot could not be saved", 4.0);
        }

        private void ToggleRecording()
        {
            if (recording) StopRecording(true);
            else StartRecording();
        }

        private void StartRecording()
        {
            if (mpv == IntPtr.Zero || String.IsNullOrEmpty(currentFile)) return;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Luma Player", "Recordings");
            Directory.CreateDirectory(folder);
            string extension = IsAudioFile(currentFile) ? ".mka" : ".mkv";
            string path = Path.Combine(folder, "Luma_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + extension);
            int result = MpvNative.Command(mpv, "set", "stream-record", path);
            if (result >= 0)
            {
                recording = true;
                recordButton.IconKind = PlayerIcon.Stop;
                recordButton.Active = true;
                statusLabel.Text = "Recording… seeking will safely finish this recording";
            }
            else
            {
                ShowStatus("Recording could not be started", 4.0);
            }
        }

        private void StopRecording(bool notify)
        {
            if (!recording || mpv == IntPtr.Zero) return;
            MpvNative.Command(mpv, "set", "stream-record", "");
            recording = false;
            recordButton.IconKind = PlayerIcon.Record;
            recordButton.Active = false;
            if (notify) ShowStatus("Recording saved in Videos\\Luma Player\\Recordings", 4.0);
        }

        private void OnUiTimer(object sender, EventArgs e)
        {
            if (fullscreen && controlsPanel.Visible && DateTime.UtcNow >= fullscreenControlsHideAt)
                HideFullscreenControls();
            if (mpv == IntPtr.Zero) return;

            double duration;
            double position;
            double volume;
            int paused;
            int eof;

            if (MpvNative.mpv_get_property_double(mpv, "duration", MpvNative.MPV_FORMAT_DOUBLE, out duration) >= 0)
                timeline.Duration = duration;
            else duration = timeline.Duration;

            if (MpvNative.mpv_get_property_double(mpv, "time-pos", MpvNative.MPV_FORMAT_DOUBLE, out position) >= 0)
                timeline.Position = position;
            else position = timeline.Position;

            if (MpvNative.mpv_get_property_double(mpv, "volume", MpvNative.MPV_FORMAT_DOUBLE, out volume) >= 0)
            {
                currentVolume = Math.Max(0.0, Math.Min(200.0, volume));
                volumeSlider.SetValueFromPlayer(currentVolume);
            }

            if (MpvNative.mpv_get_property_flag(mpv, "pause", MpvNative.MPV_FORMAT_FLAG, out paused) >= 0)
                playButton.IconKind = paused != 0 ? PlayerIcon.Play : PlayerIcon.Pause;

            timeLabel.Text = FormatTime(position) + "  /  " + FormatTime(duration);
            if (!recording && !String.IsNullOrEmpty(currentFile) && DateTime.UtcNow >= transientStatusUntil)
                statusLabel.Text = folderFiles.Count > 0
                    ? "Media " + (currentIndex + 1).ToString(CultureInfo.InvariantCulture) + " of " + folderFiles.Count.ToString(CultureInfo.InvariantCulture) + " in folder"
                    : "Playing";

            if (MpvNative.mpv_get_property_flag(mpv, "eof-reached", MpvNative.MPV_FORMAT_FLAG, out eof) >= 0)
            {
                if (eof != 0 && !endHandled && folderFiles.Count > 1)
                {
                    endHandled = true;
                    if (currentIndex >= 0 && currentIndex < folderFiles.Count - 1)
                        LoadVideo(folderFiles[currentIndex + 1]);
                }
                else if (eof == 0)
                {
                    endHandled = false;
                }
            }

        }

        private static string FormatTime(double seconds)
        {
            if (Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds < 0.0) seconds = 0.0;
            TimeSpan value = TimeSpan.FromSeconds(seconds);
            if (value.TotalHours >= 1.0)
                return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}.{3:000}", (int)value.TotalHours, value.Minutes, value.Seconds, value.Milliseconds);
            return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:000}", (int)value.TotalMinutes, value.Seconds, value.Milliseconds);
        }

        private void ShowStatus(string message, double seconds)
        {
            statusLabel.Text = message;
            transientStatusUntil = DateTime.UtcNow.AddSeconds(seconds);
        }

        private void OpenVideoDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Open media";
                dialog.Filter = "Media files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.wmv;*.flv;*.ts;*.mts;*.m2ts;*.mpg;*.mpeg;*.vob;*.ogv;*.3gp;*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.alac;*.ape;*.ac3;*.dts|Video files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.wmv;*.flv;*.ts;*.mts;*.m2ts;*.mpg;*.mpeg;*.vob;*.ogv;*.3gp|Audio files|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.alac;*.ape;*.ac3;*.dts|All files|*.*";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadVideo(dialog.FileName);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data == null ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            for (int i = 0; i < files.Length; i++)
            {
                if (File.Exists(files[i]) && IsSupportedMedia(files[i]))
                {
                    LoadVideo(files[i]);
                    return;
                }
            }
        }

        private void RegisterVolumeWheel(Control root)
        {
            if (!(root is TimelineBar))
            {
                root.MouseWheel += delegate(object sender, MouseEventArgs e)
                {
                    if (e.Delta != 0) AdjustVolume((e.Delta / 120.0) * 2.0);
                };
            }
            foreach (Control child in root.Controls)
                RegisterVolumeWheel(child);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (HandleShortcutKey(e.KeyCode, e.Control))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private bool HandleShortcutKey(Keys key, bool control)
        {
            if (key == Keys.Space) TogglePause();
            else if (key == Keys.F || key == Keys.F11) ToggleFullscreen();
            else if (key == Keys.Escape && fullscreen) ToggleFullscreen();
            else if (key == Keys.Left && control) FrameStep(false);
            else if (key == Keys.Right && control) FrameStep(true);
            else if (key == Keys.Left) SeekRelative(-5.0);
            else if (key == Keys.Right) SeekRelative(5.0);
            else if (key == Keys.Up) AdjustVolume(2.0);
            else if (key == Keys.Down) AdjustVolume(-2.0);
            else if (key == Keys.S) TakeScreenshot();
            else if (key == Keys.R) ToggleRecording();
            else if (key == Keys.PageUp) PlayAdjacent(-1);
            else if (key == Keys.PageDown) PlayAdjacent(1);
            else return false;
            return true;
        }

        private void ToggleFullscreen()
        {
            if (!fullscreen)
            {
                savedBorderStyle = FormBorderStyle;
                savedBounds = Bounds;
                savedWindowState = WindowState;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
                TopMost = true;
                controlsPanel.Dock = DockStyle.None;
                controlsPanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                controlsPanel.Bounds = new Rectangle(0, ClientSize.Height - controlsPanel.Height, ClientSize.Width, controlsPanel.Height);
                controlsPanel.Visible = false;
                fullscreen = true;
                HideCursorOnce();
            }
            else
            {
                ShowCursorOnce();
                TopMost = false;
                FormBorderStyle = savedBorderStyle;
                WindowState = FormWindowState.Normal;
                Bounds = savedBounds;
                WindowState = savedWindowState;
                controlsPanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                controlsPanel.Dock = DockStyle.Bottom;
                controlsPanel.Visible = true;
                fullscreen = false;
            }
        }

        private void ShowFullscreenControls()
        {
            if (!fullscreen) return;
            controlsPanel.Bounds = new Rectangle(0, ClientSize.Height - controlsPanel.Height, ClientSize.Width, controlsPanel.Height);
            controlsPanel.Visible = true;
            controlsPanel.BringToFront();
            fullscreenControlsHideAt = DateTime.UtcNow.AddSeconds(2.5);
            ShowCursorOnce();
        }

        private void HideFullscreenControls()
        {
            if (!fullscreen) return;
            controlsPanel.Visible = false;
            HideCursorOnce();
        }

        private void HideCursorOnce()
        {
            if (cursorHidden) return;
            Cursor.Hide();
            cursorHidden = true;
        }

        private void ShowCursorOnce()
        {
            if (!cursorHidden) return;
            Cursor.Show();
            cursorHidden = false;
        }

        private void HandleVideoLeftClick()
        {
            DateTime now = DateTime.UtcNow;
            Point position = Cursor.Position;
            TimeSpan elapsed = now - pendingClickTime;
            Size threshold = SystemInformation.DoubleClickSize;
            bool near = Math.Abs(position.X - pendingClickPosition.X) <= threshold.Width
                && Math.Abs(position.Y - pendingClickPosition.Y) <= threshold.Height;

            if (pendingClickTime != DateTime.MinValue
                && elapsed.TotalMilliseconds <= SystemInformation.DoubleClickTime && near)
            {
                pendingClickTime = DateTime.MinValue;
                TogglePause();
                ToggleFullscreen();
            }
            else
            {
                pendingClickTime = now;
                pendingClickPosition = position;
                TogglePause();
            }
        }

        private void ShowVideoContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = Font;
            menu.RenderMode = ToolStripRenderMode.System;

            ToolStripItem open = menu.Items.Add("Open media…");
            open.Click += delegate { OpenVideoDialog(); };
            ToolStripItem pause = menu.Items.Add("Play / Pause");
            pause.Click += delegate { TogglePause(); };
            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem previous = menu.Items.Add("Previous media");
            previous.Click += delegate { PlayAdjacent(-1); };
            ToolStripItem next = menu.Items.Add("Next media");
            next.Click += delegate { PlayAdjacent(1); };
            ToolStripItem capture = menu.Items.Add("Take screenshot");
            capture.Click += delegate { TakeScreenshot(); };
            ToolStripItem record = menu.Items.Add(recording ? "Stop recording" : "Start recording");
            record.Click += delegate { ToggleRecording(); };

            ToolStripMenuItem speed = new ToolStripMenuItem("Playback speed");
            string[] labels = new string[] { "0.25×", "0.50×", "0.75×", "1.00×", "1.25×", "1.50×", "2.00×", "3.00×", "4.00×" };
            double[] values = new double[] { 0.25, 0.50, 0.75, 1.00, 1.25, 1.50, 2.00, 3.00, 4.00 };
            for (int i = 0; i < labels.Length; i++)
            {
                int captured = i;
                ToolStripItem item = speed.DropDownItems.Add(labels[i]);
                item.Click += delegate
                {
                    speedSlider.Value = values[captured];
                };
            }
            menu.Items.Add(speed);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem full = menu.Items.Add(fullscreen ? "Exit fullscreen" : "Fullscreen");
            full.Click += delegate { ToggleFullscreen(); };

            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(Cursor.Position);
        }

        private void InstallInputHooks()
        {
            if (mouseHook != IntPtr.Zero && keyboardHook != IntPtr.Zero) return;
            IntPtr module = NativeMethods.GetModuleHandle(null);
            if (mouseHookDelegate == null) mouseHookDelegate = new NativeMethods.LowLevelMouseProc(LowLevelMouseHook);
            if (keyboardHookDelegate == null) keyboardHookDelegate = new NativeMethods.LowLevelKeyboardProc(LowLevelKeyboardHook);
            if (mouseHook == IntPtr.Zero)
                mouseHook = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, mouseHookDelegate, module, 0);
            if (keyboardHook == IntPtr.Zero)
                keyboardHook = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, keyboardHookDelegate, module, 0);

            if (mouseHook == IntPtr.Zero || keyboardHook == IntPtr.Zero)
                ShowStatus("Some input shortcuts could not be initialized", 5.0);
        }

        private IntPtr LowLevelMouseHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && IsPlayerForeground() && videoPanel != null && videoPanel.IsHandleCreated)
            {
                NativeMethods.MouseHookData data = (NativeMethods.MouseHookData)Marshal.PtrToStructure(
                    lParam, typeof(NativeMethods.MouseHookData));
                Point pointer = new Point(data.point.x, data.point.y);
                int message = wParam.ToInt32();

                if (fullscreen && message == WM_MOUSEMOVE)
                {
                    DateTime now = DateTime.UtcNow;
                    if ((now - lastFullscreenRevealRequest).TotalMilliseconds >= 100.0)
                    {
                        lastFullscreenRevealRequest = now;
                        try { BeginInvoke(new Action(ShowFullscreenControls)); }
                        catch { }
                    }
                }

                if (fullscreen && controlsPanel.Visible)
                {
                    Rectangle controlsBounds = controlsPanel.RectangleToScreen(controlsPanel.ClientRectangle);
                    if (controlsBounds.Contains(pointer))
                        return NativeMethods.CallNextHookEx(mouseHook, code, wParam, lParam);
                }

                Rectangle videoBounds = videoPanel.RectangleToScreen(videoPanel.ClientRectangle);

                if (videoBounds.Contains(pointer))
                {
                    if (message == WM_LBUTTONDOWN)
                    {
                        BeginInvoke(new Action(HandleVideoLeftClick));
                        return new IntPtr(1);
                    }
                    if (message == WM_RBUTTONUP)
                    {
                        BeginInvoke(new Action(ShowVideoContextMenu));
                        return new IntPtr(1);
                    }
                    if (message == WM_MOUSEWHEEL)
                    {
                        int delta = (short)((data.mouseData >> 16) & 0xffff);
                        BeginInvoke(new Action(delegate { AdjustVolume((delta / 120.0) * 2.0); }));
                        return new IntPtr(1);
                    }
                }
            }
            return NativeMethods.CallNextHookEx(mouseHook, code, wParam, lParam);
        }

        private IntPtr LowLevelKeyboardHook(int code, IntPtr wParam, IntPtr lParam)
        {
            int message = wParam.ToInt32();
            if (code >= 0 && IsPlayerForeground() && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                NativeMethods.KeyboardHookData data = (NativeMethods.KeyboardHookData)Marshal.PtrToStructure(
                    lParam, typeof(NativeMethods.KeyboardHookData));
                bool control = (NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
                if (HandleShortcutKey((Keys)data.virtualKey, control))
                    return new IntPtr(1);
            }
            return NativeMethods.CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        private bool IsPlayerForeground()
        {
            return IsHandleCreated && NativeMethods.GetForegroundWindow() == Handle;
        }

        private void RemoveInputHooks()
        {
            if (mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
            if (keyboardHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            ShowCursorOnce();
            uiTimer.Stop();
            StopRecording(false);
            RemoveInputHooks();

            if (mpv != IntPtr.Zero)
            {
                try { MpvNative.mpv_terminate_destroy(mpv); }
                catch { }
                mpv = IntPtr.Zero;
            }
        }

        private void ApplyRoundedCorners()
        {
            if (!IsHandleCreated) return;
            int preference = 2;
            try { NativeMethods.DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int)); }
            catch { }
        }

        private static class NativeMethods
        {
            internal delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);
            internal delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

            [StructLayout(LayoutKind.Sequential)]
            internal struct NativePoint
            {
                internal int x;
                internal int y;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct MouseHookData
            {
                internal NativePoint point;
                internal uint mouseData;
                internal uint flags;
                internal uint time;
                internal UIntPtr extraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct KeyboardHookData
            {
                internal uint virtualKey;
                internal uint scanCode;
                internal uint flags;
                internal uint time;
                internal UIntPtr extraInfo;
            }

            [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowsHookExW")]
            internal static extern IntPtr SetWindowsHookEx(
                int hookType, LowLevelMouseProc callback, IntPtr module, uint threadId);

            [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowsHookExW")]
            internal static extern IntPtr SetWindowsHookEx(
                int hookType, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool UnhookWindowsHookEx(IntPtr hook);

            [DllImport("user32.dll")]
            internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            internal static extern short GetAsyncKeyState(int virtualKey);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            internal static extern IntPtr GetModuleHandle(string moduleName);

            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            internal static extern int StrCmpLogicalW(string left, string right);

            [DllImport("dwmapi.dll")]
            internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        }

        private enum PlayerIcon
        {
            Open,
            Previous,
            FrameBack,
            Play,
            Pause,
            FrameForward,
            Next,
            Camera,
            Record,
            Stop
        }

        private sealed class PlayerIconButton : Control
        {
            private PlayerIcon iconKind;
            private readonly bool primary;
            private bool hovered;
            private bool pressed;
            private bool active;

            internal PlayerIconButton(PlayerIcon icon, bool isPrimary)
            {
                iconKind = icon;
                primary = isPrimary;
                Cursor = Cursors.Hand;
                TabStop = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                    | ControlStyles.Selectable, true);
            }

            internal PlayerIcon IconKind
            {
                get { return iconKind; }
                set
                {
                    if (iconKind == value) return;
                    iconKind = value;
                    Invalidate();
                }
            }

            internal bool Active
            {
                get { return active; }
                set
                {
                    if (active == value) return;
                    active = value;
                    Invalidate();
                }
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                hovered = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hovered = false;
                pressed = false;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                pressed = true;
                Focus();
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left) return;
                pressed = false;
                Invalidate();
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    OnClick(EventArgs.Empty);
                    e.Handled = true;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color background;
                Color iconColor;

                if (active)
                {
                    background = pressed ? Color.FromArgb(252, 214, 218) : Color.FromArgb(255, 232, 235);
                    iconColor = Color.FromArgb(209, 45, 63);
                }
                else if (primary)
                {
                    background = pressed ? Color.FromArgb(24, 91, 213)
                        : hovered ? Color.FromArgb(53, 126, 248) : Color.FromArgb(40, 113, 245);
                    iconColor = Color.White;
                }
                else
                {
                    background = pressed ? Color.FromArgb(226, 233, 243)
                        : hovered ? Color.FromArgb(237, 242, 249) : Color.FromArgb(246, 248, 251);
                    iconColor = Color.FromArgb(48, 58, 73);
                }

                RectangleF body = new RectangleF(0.5f, 0.5f, Math.Max(1f, Width - 1f), Math.Max(1f, Height - 1f));
                using (GraphicsPath path = RoundedRectangle(body, 9f))
                using (SolidBrush brush = new SolidBrush(background))
                    e.Graphics.FillPath(brush, path);

                DrawIcon(e.Graphics, iconColor);

                if (Focused && ShowFocusCues)
                {
                    Rectangle focus = ClientRectangle;
                    focus.Inflate(-3, -3);
                    ControlPaint.DrawFocusRectangle(e.Graphics, focus);
                }
            }

            private void DrawIcon(Graphics graphics, Color color)
            {
                float cx = Width / 2f;
                float cy = Height / 2f;
                using (Pen pen = new Pen(color, 2f))
                using (SolidBrush brush = new SolidBrush(color))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    if (iconKind == PlayerIcon.Play)
                    {
                        graphics.FillPolygon(brush, new PointF[]
                        {
                            new PointF(cx - 5f, cy - 8f), new PointF(cx - 5f, cy + 8f), new PointF(cx + 8f, cy)
                        });
                    }
                    else if (iconKind == PlayerIcon.Pause)
                    {
                        graphics.FillRectangle(brush, cx - 7f, cy - 8f, 4f, 16f);
                        graphics.FillRectangle(brush, cx + 3f, cy - 8f, 4f, 16f);
                    }
                    else if (iconKind == PlayerIcon.Previous || iconKind == PlayerIcon.Next
                        || iconKind == PlayerIcon.FrameBack || iconKind == PlayerIcon.FrameForward)
                    {
                        bool pointsRight = iconKind == PlayerIcon.Next || iconKind == PlayerIcon.FrameForward;
                        bool barOnRight = iconKind == PlayerIcon.FrameBack || iconKind == PlayerIcon.Next;
                        float triangleCenter = barOnRight ? cx - 2f : cx + 2f;
                        PointF[] triangle = pointsRight
                            ? new PointF[] { new PointF(triangleCenter - 6f, cy - 7f), new PointF(triangleCenter - 6f, cy + 7f), new PointF(triangleCenter + 6f, cy) }
                            : new PointF[] { new PointF(triangleCenter + 6f, cy - 7f), new PointF(triangleCenter + 6f, cy + 7f), new PointF(triangleCenter - 6f, cy) };
                        graphics.FillPolygon(brush, triangle);
                        float barX = barOnRight ? cx + 8f : cx - 8f;
                        graphics.DrawLine(pen, barX, cy - 7f, barX, cy + 7f);
                    }
                    else if (iconKind == PlayerIcon.Camera)
                    {
                        graphics.DrawRectangle(pen, cx - 9f, cy - 6f, 18f, 13f);
                        graphics.DrawLine(pen, cx - 5f, cy - 6f, cx - 2f, cy - 9f);
                        graphics.DrawLine(pen, cx - 2f, cy - 9f, cx + 3f, cy - 9f);
                        graphics.DrawLine(pen, cx + 3f, cy - 9f, cx + 5f, cy - 6f);
                        graphics.DrawEllipse(pen, cx - 4f, cy - 4f, 8f, 8f);
                    }
                    else if (iconKind == PlayerIcon.Record)
                    {
                        using (SolidBrush red = new SolidBrush(Color.FromArgb(224, 55, 72)))
                            graphics.FillEllipse(red, cx - 7f, cy - 7f, 14f, 14f);
                    }
                    else if (iconKind == PlayerIcon.Stop)
                    {
                        using (SolidBrush red = new SolidBrush(Color.FromArgb(209, 45, 63)))
                            graphics.FillRectangle(red, cx - 6f, cy - 6f, 12f, 12f);
                    }
                    else if (iconKind == PlayerIcon.Open)
                    {
                        using (GraphicsPath folder = new GraphicsPath())
                        {
                            folder.AddLine(cx - 9f, cy - 6f, cx - 2f, cy - 6f);
                            folder.AddLine(cx - 2f, cy - 6f, cx + 1f, cy - 3f);
                            folder.AddLine(cx + 1f, cy - 3f, cx + 9f, cy - 3f);
                            folder.AddLine(cx + 9f, cy - 3f, cx + 8f, cy + 7f);
                            folder.AddLine(cx + 8f, cy + 7f, cx - 9f, cy + 7f);
                            folder.CloseFigure();
                            graphics.DrawPath(pen, folder);
                        }
                    }
                }
            }

            private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
            {
                GraphicsPath path = new GraphicsPath();
                float diameter = radius * 2f;
                path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
                path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
                path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        private sealed class VolumeSlider : Control
        {
            private double currentValue = 70.0;
            private bool dragging;

            internal event EventHandler ValueChanged;

            internal VolumeSlider()
            {
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                BackColor = Color.White;
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
                SetStyle(ControlStyles.Selectable, true);
            }

            internal double Value
            {
                get { return currentValue; }
                set { SetValue(value, true); }
            }

            internal void SetValueFromPlayer(double value)
            {
                SetValue(value, false);
            }

            private void SetValue(double value, bool notify)
            {
                double adjusted = Math.Max(0.0, Math.Min(200.0, Math.Round(value)));
                if (Math.Abs(adjusted - currentValue) < 0.001) return;
                currentValue = adjusted;
                Invalidate();
                if (notify && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.White);

                float cy = Height / 2f;
                Color iconColor = Color.FromArgb(70, 80, 96);
                using (Pen iconPen = new Pen(iconColor, 1.7f))
                using (SolidBrush iconBrush = new SolidBrush(iconColor))
                {
                    iconPen.StartCap = LineCap.Round;
                    iconPen.EndCap = LineCap.Round;
                    e.Graphics.FillPolygon(iconBrush, new PointF[]
                    {
                        new PointF(2f, cy - 3f), new PointF(7f, cy - 3f), new PointF(12f, cy - 8f),
                        new PointF(12f, cy + 8f), new PointF(7f, cy + 3f), new PointF(2f, cy + 3f)
                    });
                    e.Graphics.DrawArc(iconPen, 10f, cy - 7f, 10f, 14f, -55f, 110f);
                }

                float left = 27f;
                float right = Math.Max(left + 12f, Width - 48f);
                float ratio = (float)(currentValue / 200.0);
                float knobX = left + (right - left) * ratio;

                using (Pen rail = new Pen(Color.FromArgb(221, 226, 234), 4f))
                {
                    rail.StartCap = LineCap.Round;
                    rail.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(rail, left, cy, right, cy);
                }
                using (Pen fill = new Pen(Color.FromArgb(40, 113, 245), 4f))
                {
                    fill.StartCap = LineCap.Round;
                    fill.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(fill, left, cy, knobX, cy);
                }
                using (SolidBrush knob = new SolidBrush(Color.White))
                    e.Graphics.FillEllipse(knob, knobX - 5f, cy - 5f, 10f, 10f);
                using (Pen outline = new Pen(Color.FromArgb(40, 113, 245), 2f))
                    e.Graphics.DrawEllipse(outline, knobX - 5f, cy - 5f, 10f, 10f);

                string label = Math.Round(currentValue).ToString(CultureInfo.InvariantCulture) + "%";
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(58, 66, 81)))
                {
                    StringFormat format = new StringFormat();
                    format.Alignment = StringAlignment.Far;
                    format.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString(label, Font, textBrush, new RectangleF(Width - 45f, 0f, 43f, Height), format);
                    format.Dispose();
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                dragging = true;
                Capture = true;
                UpdateFromMouse(e.X);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (dragging) UpdateFromMouse(e.X);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left) return;
                dragging = false;
                Capture = false;
            }

            private void UpdateFromMouse(int x)
            {
                double ratio = (x - 27.0) / Math.Max(12.0, Width - 75.0);
                ratio = Math.Max(0.0, Math.Min(1.0, ratio));
                Value = ratio * 200.0;
            }
        }

        private sealed class SpeedSlider : Control
        {
            private const double Minimum = 0.25;
            private const double Maximum = 4.00;
            private double currentValue = 1.00;
            private bool dragging;

            internal event EventHandler ValueChanged;

            internal SpeedSlider()
            {
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                BackColor = Color.White;
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
                SetStyle(ControlStyles.Selectable | ControlStyles.StandardDoubleClick, true);
            }

            internal double Value
            {
                get { return currentValue; }
                set
                {
                    double adjusted = Math.Max(Minimum, Math.Min(Maximum, Math.Round(value * 20.0) / 20.0));
                    if (Math.Abs(adjusted - currentValue) < 0.001) return;
                    currentValue = adjusted;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.White);

                string label = currentValue.ToString("0.00", CultureInfo.InvariantCulture) + "×";
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(58, 66, 81)))
                    e.Graphics.DrawString(label, Font, textBrush, new RectangleF(0, 8, 48, 20));

                float left = 51f;
                float right = Math.Max(left + 12f, Width - 8f);
                float y = Height / 2f;
                float ratio = (float)((currentValue - Minimum) / (Maximum - Minimum));
                float knobX = left + (right - left) * ratio;

                using (Pen rail = new Pen(Color.FromArgb(221, 226, 234), 4f))
                {
                    rail.StartCap = LineCap.Round;
                    rail.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(rail, left, y, right, y);
                }
                using (Pen fill = new Pen(Color.FromArgb(40, 113, 245), 4f))
                {
                    fill.StartCap = LineCap.Round;
                    fill.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(fill, left, y, knobX, y);
                }
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(36, 0, 0, 0)))
                    e.Graphics.FillEllipse(shadow, knobX - 7, y - 7, 14, 14);
                using (SolidBrush knob = new SolidBrush(Color.White))
                    e.Graphics.FillEllipse(knob, knobX - 5, y - 5, 10, 10);
                using (Pen outline = new Pen(Color.FromArgb(40, 113, 245), 2f))
                    e.Graphics.DrawEllipse(outline, knobX - 5, y - 5, 10, 10);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                dragging = true;
                Capture = true;
                UpdateFromMouse(e.X);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (dragging) UpdateFromMouse(e.X);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left) return;
                dragging = false;
                Capture = false;
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                base.OnMouseDoubleClick(e);
                if (e.Button == MouseButtons.Left) Value = 1.00;
            }

            private void UpdateFromMouse(int x)
            {
                double ratio = (x - 51.0) / Math.Max(12.0, Width - 59.0);
                ratio = Math.Max(0.0, Math.Min(1.0, ratio));
                Value = Minimum + (Maximum - Minimum) * ratio;
            }
        }
    }
}
