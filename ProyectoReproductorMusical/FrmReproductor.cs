using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using ProyectoReproductorMusical.Audio;
using ProyectoReproductorMusical.Visualizadores;

namespace ProyectoReproductorMusical
{
    public partial class FrmReproductor : Form
    {
        // ── Controles ──────────────────────────────────────────────────────────
        private Panel    _vizPanel, _controlBar, _topBar, _queuePanel;
        private Button   _btnLoad, _btnPlay, _btnStop, _btnPrev, _btnNext;
        private Label    _lblTitle, _lblTime, _lblVolPct, _lblQueueTitle;
        private ComboBox _cmbMode;
        private TrackBar _tbVolume;
        private System.Windows.Forms.Timer _timer;
        private Bitmap   _backBuffer;
        private Graphics _bufGfx;
        private BarraProgreso  _seekBar;
        private PlaylistPanel _lstPlaylist;

        // ── Audio ──────────────────────────────────────────────────────────────
        private readonly ReproductorAudio _player = new ReproductorAudio();
        private readonly List<VisualizadorBase> _visualizers = new List<VisualizadorBase>();
        private int _vizIndex;

        // ── Playlist ───────────────────────────────────────────────────────────
        private readonly List<string> _playlist = new List<string>();
        private int _songIndex = -1;
        private VisualizadorBase CurrentViz => _visualizers[_vizIndex];

        // ── Timing ─────────────────────────────────────────────────────────────
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private long  _lastTick;
        private float _beatFlash;

        // ── Paleta: azul índigo oscuro (profesional, NO verde) ─────────────────
        static readonly Color BG_DARK    = Color.FromArgb(8,  10, 20);
        static readonly Color BG_PANEL   = Color.FromArgb(12, 14, 28);
        static readonly Color BG_CTRL    = Color.FromArgb(10, 12, 24);
        static readonly Color BG_QUEUE   = Color.FromArgb(14, 16, 32);
        static readonly Color CLR_CYAN   = Color.FromArgb(0,  200, 255);
        static readonly Color CLR_VIOLET = Color.FromArgb(160, 80, 255);
        static readonly Color CLR_PINK   = Color.FromArgb(255, 60, 160);
        static readonly Color CLR_AMBER  = Color.FromArgb(255, 190, 30);
        static readonly Color CLR_GREEN  = Color.FromArgb(60,  230, 140);
        static readonly Color CLR_RED    = Color.FromArgb(255, 70,  70);
        static readonly Color TEXT_PRI   = Color.FromArgb(220, 225, 255);
        static readonly Color TEXT_SEC   = Color.FromArgb(110, 120, 180);
        static readonly Color BORDER     = Color.FromArgb(30,  40,  80);

        public FrmReproductor()
        {
            InitVisualizers();
            InitUI();
            _lastTick = _sw.ElapsedMilliseconds;
        }

        void InitVisualizers()
        {
            _visualizers.Clear();
            _visualizers.Add(new VisualizadorOndaCircular());
            _visualizers.Add(new VisualizadorParticulasEspectro());
            _visualizers.Add(new VisualizadorGeometriaTunel());
            foreach (var v in _visualizers) v.SetAnalyzer(_player.Analyzer);
        }

        void InitUI()
        {
            Text           = "MusicVisualizer — Reproductor Gráfico";
            Size           = new Size(1120, 780);
            MinimumSize    = new Size(840, 600);
            BackColor      = BG_DARK;
            ForeColor      = TEXT_PRI;
            DoubleBuffered = true;
            StartPosition  = FormStartPosition.CenterScreen;
            KeyPreview     = true;
            KeyDown       += MainForm_KeyDown;

            // ── TOP BAR ────────────────────────────────────────────────────────
            _topBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = BG_PANEL };
            _topBar.Paint += (s, e) => {
                var g = e.Graphics;
                using (var br = new LinearGradientBrush(
                    new Point(0, 0), new Point(_topBar.Width, 0),
                    Color.FromArgb(18, 20, 42), Color.FromArgb(10, 12, 28)))
                    g.FillRectangle(br, 0, 0, _topBar.Width, _topBar.Height);
                // Línea inferior degradada
                using (var br2 = new LinearGradientBrush(
                    new Point(0, 0), new Point(_topBar.Width, 0),
                    CLR_CYAN, CLR_VIOLET))
                using (var pen = new Pen(br2, 2))
                    g.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
                // Logo "MV"
                using (var f  = new Font("Segoe UI", 14f, FontStyle.Bold))
                using (var br3 = new LinearGradientBrush(new Rectangle(8,8,36,36), CLR_CYAN, CLR_VIOLET, 45f))
                    g.DrawString("MV", f, br3, 10, 10);
            };

            _lblTitle = new Label {
                Text = "Arrastra un .WAV aquí   o   pulsa  ＋ Agregar",
                ForeColor = TEXT_PRI, Left = 54, Top = 0,
                AutoSize = false, Width = 560, Height = 56,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold) };

            var lblMode = new Label {
                Text = "VISUALIZACIÓN", ForeColor = TEXT_SEC,
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                AutoSize = true, Left = 622, Top = 20 };

            _cmbMode = new ComboBox {
                Left = 730, Top = 14, Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(16, 18, 40),
                ForeColor = TEXT_PRI, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f) };
            foreach (var v in _visualizers) _cmbMode.Items.Add(v.Name);
            _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += (s, e) => {
                _vizIndex = _cmbMode.SelectedIndex;
                CurrentViz.SetAnalyzer(_player.Analyzer);
            };

            var lblKeys = new Label {
                Text = "⌨  Espacio=Play/Pausa  ←/→=±5s",
                ForeColor = Color.FromArgb(55, 65, 110),
                Font = new Font("Segoe UI", 7.5f),
                AutoSize = true, Left = 1000, Top = 20 };

            _topBar.Controls.AddRange(new Control[] { _lblTitle, lblMode, _cmbMode, lblKeys });

            // ── VIZ PANEL ──────────────────────────────────────────────────────
            _vizPanel = new Panel { Dock = DockStyle.Fill, BackColor = BG_DARK };
            _vizPanel.Paint  += VizPanel_Paint;
            _vizPanel.Resize += (s, e) => AllocBuffer();

            // ── CONTROL BAR ────────────────────────────────────────────────────
            _controlBar = new Panel { Dock = DockStyle.Bottom, Height = 170, BackColor = BG_CTRL };
            _controlBar.Paint += (s, e) => {
                var g = e.Graphics;
                using (var br = new LinearGradientBrush(
                    new Point(0, 0), new Point(0, _controlBar.Height),
                    Color.FromArgb(18, 20, 42), Color.FromArgb(8, 10, 20)))
                    g.FillRectangle(br, 0, 0, _controlBar.Width, _controlBar.Height);
                using (var pen = new Pen(Color.FromArgb(40, 50, 100), 1))
                    g.DrawLine(pen, 0, 0, _controlBar.Width, 0);
            };

            BuildControlBar();

            Controls.Add(_vizPanel);
            Controls.Add(_topBar);
            Controls.Add(_controlBar);

            // Drag & Drop
            AllowDrop  = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop  += (s, e) => {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(files);
            };

            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            AllocBuffer();
        }

        void BuildControlBar()
        {
            // ── Seek bar ───────────────────────────────────────────────────────
            _seekBar = new BarraProgreso {
                Left = 20, Top = 10,
                Width = _controlBar.Width - 40, Height = 26 };
            _seekBar.SeekRequested += (pos) => {
                if (_player.HasFile) _player.SeekTo(pos);
            };
            _controlBar.Resize += (s, e) => RepositionControls();

            // ── Tiempo ────────────────────────────────────────────────────────
            _lblTime = new Label {
                Text = "0:00 / 0:00", Left = 22, Top = 40,
                ForeColor = CLR_CYAN, BackColor = Color.Transparent,
                AutoSize = true,
                Font = new Font("Consolas", 10f, FontStyle.Bold) };

            // ── Botones de transporte ─────────────────────────────────────────
            int by = 65;
            _btnLoad = MakeBtn("＋", "Agregar", 68, 0, by, CLR_CYAN);
            _btnPrev = MakeBtn("⏮", "Anterior", 64, 0, by, CLR_VIOLET);
            _btnPlay = MakeBtn("▶", "Reproducir", 72, 0, by, CLR_GREEN);
            _btnStop = MakeBtn("⏹", "Detener", 64, 0, by, CLR_RED);
            _btnNext = MakeBtn("⏭", "Siguiente", 64, 0, by, CLR_VIOLET);

            // ── Volumen ───────────────────────────────────────────────────────
            var volIcon = new Label {
                Text = "🔊", AutoSize = true,
                Font = new Font("Segoe UI Emoji", 12f), BackColor = Color.Transparent };
            _tbVolume = new TrackBar {
                Minimum = 0, Maximum = 100, Value = 80,
                TickStyle = TickStyle.None, Width = 150, Height = 22,
                BackColor = BG_CTRL };
            _tbVolume.ValueChanged += (s, e) => {
                _player.SetVolume(_tbVolume.Value / 100f);
                _lblVolPct.Text = $"{_tbVolume.Value}%";
            };
            _lblVolPct = new Label {
                Text = "80%", ForeColor = CLR_CYAN,
                BackColor = Color.Transparent, AutoSize = true,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold) };

            // ── Cola de reproducción ───────────────────────────────────────────
            _queuePanel = new Panel {
                BackColor = BG_QUEUE,
                Top = 40, Width = 330, Height = 120 };
            _queuePanel.Paint += PaintQueuePanel;

            _lblQueueTitle = new Label {
                Text = "COLA DE REPRODUCCIÓN",
                ForeColor = CLR_CYAN, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                AutoSize = true, Left = 8, Top = 6 };

            _lstPlaylist = new PlaylistPanel {
                Left = 0, Top = 22, Width = 330, Height = 98,
                Accent = CLR_CYAN };
            _lstPlaylist.SongDoubleClicked += (idx) => LoadSong(idx, true);

            _queuePanel.Controls.Add(_lblQueueTitle);
            _queuePanel.Controls.Add(_lstPlaylist);

            // ── Hint ──────────────────────────────────────────────────────────
            var hint = new Label {
                Text = "⚡ Solo WAV PCM  •  Convertir: ffmpeg -i cancion.mp3 cancion.wav",
                Left = 14, Top = 152, AutoSize = true,
                ForeColor = Color.FromArgb(35, 45, 80),
                Font = new Font("Segoe UI", 7.5f) };

            _controlBar.Controls.AddRange(new Control[] {
                _seekBar, _lblTime,
                _btnLoad, _btnPrev, _btnPlay, _btnStop, _btnNext,
                volIcon, _tbVolume, _lblVolPct,
                _queuePanel, hint });

            // ── Eventos botones ───────────────────────────────────────────────
            _btnLoad.Click += (s, e) => {
                using (var ofd = new OpenFileDialog()) {
                    ofd.Filter = "Audio WAV PCM|*.wav";
                    ofd.Title  = "Selecciona una o varias canciones";
                    ofd.Multiselect = true;
                    if (ofd.ShowDialog() == DialogResult.OK)
                        AddFiles(ofd.FileNames);
                }
            };
            _btnPlay.Click += (s, e) => {
                if (!_player.HasFile) return;
                if (_player.IsPlaying) _player.Pause();
                else _player.Play();
                RefreshBtns();
            };
            _btnStop.Click += (s, e) => {
                _player.Stop();
                _seekBar.SetPosition(0, _player.Duration);
                _lblTime.Text = "0:00 / " + Fmt(_player.Duration);
                RefreshBtns();
            };
            _btnPrev.Click += (s, e) => PlayPreviousSong();
            _btnNext.Click += (s, e) => PlayNextSong();

            _player.SetVolume(0.8f);
            RefreshBtns();
            RepositionControls();
        }

        Button MakeBtn(string icon, string label, int w, int x, int y, Color accent)
        {
            var btn = new Button {
                Left = x, Top = y, Width = w, Height = 58,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(20, accent.R, accent.G, accent.B),
                ForeColor = Color.White,
                Tag = new object[] { icon, label, accent } };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, accent.R, accent.G, accent.B);
            btn.FlatAppearance.BorderSize  = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, accent.R, accent.G, accent.B);
            btn.Paint += PaintBtn;
            return btn;
        }

        void PaintBtn(object sender, PaintEventArgs e)
        {
            var btn = (Button)sender;
            var arr = (object[])btn.Tag;
            string ico = (string)arr[0], lbl = (string)arr[1];
            Color  ac  = (Color)arr[2];
            bool   en  = btn.Enabled;
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var rc = btn.ClientRectangle;

            // Fondo degradado
            using (var br = new LinearGradientBrush(rc,
                Color.FromArgb(en ? 70 : 20, ac),
                Color.FromArgb(en ? 22 : 7, ac.R/4, ac.G/4, ac.B/4),
                LinearGradientMode.Vertical))
                g.FillRectangle(br, rc);

            // Borde redondeado con color del acento
            using (var path = RRectPath(1, 1, rc.Width - 2, rc.Height - 2, 8))
            using (var pen  = new Pen(Color.FromArgb(en ? 150 : 40, ac), 1.5f))
                g.DrawPath(pen, path);

            // Icono grande
            using (var f  = new Font("Segoe UI Emoji", 19f))
            using (var br = new SolidBrush(en ? Color.White : Color.FromArgb(50, 60, 80)))
            {
                var sz = g.MeasureString(ico, f);
                g.DrawString(ico, f, br, (btn.Width - sz.Width) / 2f, 4f);
            }
            // Etiqueta pequeña
            using (var f  = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (var br = new SolidBrush(Color.FromArgb(en ? 180 : 50, TEXT_SEC)))
            {
                var sz = g.MeasureString(lbl, f);
                g.DrawString(lbl, f, br, (btn.Width - sz.Width) / 2f, btn.Height - 14f);
            }
        }

        void PaintQueuePanel(object sender, PaintEventArgs e)
        {
            var g  = e.Graphics;
            var rc = _queuePanel.ClientRectangle;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = RRectPath(0, 0, rc.Width - 1, rc.Height - 1, 8))
            {
                using (var br = new SolidBrush(BG_QUEUE))
                    g.FillPath(br, path);
                using (var pen = new Pen(Color.FromArgb(40, CLR_CYAN), 1.2f))
                    g.DrawPath(pen, path);
            }
        }

        void RefreshBtns()
        {
            bool has = _player.HasFile;
            _btnPlay.Enabled = has;
            _btnStop.Enabled = has;
            _btnPrev.Enabled = _playlist.Count > 0;
            _btnNext.Enabled = _playlist.Count > 0;

            if (has)
            {
                var arr = (object[])_btnPlay.Tag;
                if (_player.IsPlaying) { arr[0] = "⏸"; arr[1] = "Pausar"; }
                else { arr[0] = "▶"; arr[1] = _player.IsPaused ? "Reanudar" : "Reproducir"; }
            }
            _btnPlay.Invalidate();
            _btnStop.Invalidate();
        }

        void AddFiles(string[] files)
        {
            bool wasEmpty = _playlist.Count == 0;
            foreach (string f in files)
                if (!_playlist.Contains(f)) _playlist.Add(f);
            if (wasEmpty && _playlist.Count > 0) _songIndex = 0;
            RefreshPlaylistBox();
            if (wasEmpty && _playlist.Count > 0) LoadSong(_songIndex, true);
        }

        void LoadFile(string path)
        {
            try {
                _player.Load(path);
                _lblTitle.Text = "♫  " + Path.GetFileName(path);
                _seekBar.SetPosition(0, _player.Duration);
                _lblTime.Text  = "0:00 / " + Fmt(_player.Duration);
                _player.Play();
                if (_tbVolume != null) _player.SetVolume(_tbVolume.Value / 100f);
                RefreshBtns();
            }
            catch (Exception ex) {
                MessageBox.Show("No se pudo cargar:\n\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void Timer_Tick(object s, EventArgs e)
        {
            if (_player.HasEnded)
            {
                if (_playlist.Count > 1) PlayNextSong();
                else ResetToInitialStateAfterSongEnd();
                return;
            }

            long now = _sw.ElapsedMilliseconds;
            float dt = Math.Min((now - _lastTick) / 1000f, 0.05f);
            _lastTick = now;

            CurrentViz.Update(dt);

            if (_player.Analyzer != null && _player.Analyzer.IsBeat) _beatFlash = 1f;
            _beatFlash *= 0.75f;

            double pos = _player.Position;
            double dur = _player.Duration;
            if (!_seekBar.IsDragging)
            {
                _seekBar.SetPosition(pos, dur);
                _lblTime.Text = Fmt(pos) + " / " + Fmt(dur);
            }

            if (_player.HasFile && dur > 0 && !_player.IsPlaying && !_player.IsPaused && pos >= dur - 0.5)
                RefreshBtns();

            RenderFrame();
        }

        void AllocBuffer()
        {
            _bufGfx?.Dispose(); _backBuffer?.Dispose();
            if (_vizPanel.Width > 0 && _vizPanel.Height > 0) {
                _backBuffer = new Bitmap(_vizPanel.Width, _vizPanel.Height);
                _bufGfx     = Graphics.FromImage(_backBuffer);
                _bufGfx.TextRenderingHint = TextRenderingHint.AntiAlias;
            }
        }

        void RenderFrame()
        {
            if (_backBuffer == null || _bufGfx == null) return;
            CurrentViz.Render(_bufGfx, _vizPanel.Width, _vizPanel.Height);
            DrawHUD(_bufGfx, _vizPanel.Width, _vizPanel.Height);
            using (var g = _vizPanel.CreateGraphics())
                g.DrawImageUnscaled(_backBuffer, 0, 0);
        }

        void VizPanel_Paint(object s, PaintEventArgs e)
        {
            if (_backBuffer != null) e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
            else e.Graphics.Clear(BG_DARK);
        }

        void DrawHUD(Graphics g, int W, int H)
        {
            var a = _player.Analyzer;
            if (a == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Beat flash sutil
            if (_beatFlash > 0.05f)
                using (var br = new SolidBrush(Color.FromArgb((int)(_beatFlash * 12), 0, 150, 255)))
                    g.FillRectangle(br, 0, 0, W, H);

            // ── VU meters ──────────────────────────────────────────────────────
            int mH = 80, mW = 11, gap = 5;
            int x0 = W - (mW + gap) * 4 - 24, y0 = 16;
            int pw = (mW + gap) * 4 + 16;

            using (var path = RRectPath(x0 - 8, y0 - 6, pw, mH + 28, 8)) {
                using (var br = new SolidBrush(Color.FromArgb(150, 8, 12, 28)))
                    g.FillPath(br, path);
                using (var pen = new Pen(Color.FromArgb(50, 0, 150, 255), 1))
                    g.DrawPath(pen, path);
            }

            string[] lbls = { "B", "M", "H", "V" };
            float[]  vals = { a.BassEnergy, a.MidEnergy, a.HighEnergy, a.Volume };
            Color[]  cols = { CLR_RED, CLR_CYAN, CLR_VIOLET, CLR_AMBER };

            for (int i = 0; i < 4; i++) {
                int xi = x0 + i * (mW + gap);
                using (var br = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
                    g.FillRectangle(br, xi, y0, mW, mH);
                int filled = (int)(Math.Min(vals[i], 1f) * mH);
                if (filled > 0) {
                    var top = vals[i] > 0.85f ? CLR_RED : cols[i];
                    using (var br = new LinearGradientBrush(
                        new Point(xi, y0 + mH - filled), new Point(xi, y0 + mH), top, DimC(top, 0.20f)))
                        g.FillRectangle(br, xi, y0 + mH - filled, mW, filled);
                }
                using (var f  = new Font("Segoe UI", 6.5f, FontStyle.Bold))
                using (var br = new SolidBrush(Color.FromArgb(130, TEXT_SEC)))
                    g.DrawString(lbls[i], f, br, xi - 1, y0 + mH + 6);
            }

            // Beat dot
            bool beat = a.IsBeat;
            using (var br = new SolidBrush(beat ? CLR_CYAN : Color.FromArgb(25, 0, 80, 120)))
                g.FillEllipse(br, W - 24, H - 24, 14, 14);
            if (beat)
                using (var pen = new Pen(Color.FromArgb(120, CLR_CYAN), 1.5f))
                    g.DrawEllipse(pen, W - 28, H - 28, 22, 22);

            // Nombre del visualizador (esquina inferior izquierda)
            using (var f  = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var br = new SolidBrush(Color.FromArgb(55, 0, 150, 255)))
                g.DrawString(CurrentViz.Name.ToUpper(), f, br, 12, H - 22);

            // Mensaje sin archivo
            if (!_player.HasFile) {
                string msg = "Arrastra un archivo .WAV aquí\no pulsa  ＋ Agregar";
                using (var f  = new Font("Segoe UI", 16))
                using (var br = new SolidBrush(Color.FromArgb(45, 0, 150, 255))) {
                    var sz = g.MeasureString(msg, f);
                    g.DrawString(msg, f, br, (W - sz.Width) / 2, (H - sz.Height) / 2);
                }
            }
        }

        static Color DimC(Color c, float f) =>
            Color.FromArgb((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));

        static GraphicsPath RRectPath(int x, int y, int w, int h, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(x,         y,         r * 2, r * 2, 180, 90);
            p.AddArc(x + w - r * 2, y,         r * 2, r * 2, 270, 90);
            p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2,   0, 90);
            p.AddArc(x,         y + h - r * 2, r * 2, r * 2,  90, 90);
            p.CloseFigure();
            return p;
        }

        void RepositionControls()
        {
            if (_controlBar == null) return;
            int W = _controlBar.Width;

            if (_seekBar != null) {
                _seekBar.Left  = 20;
                _seekBar.Top   = 10;
                _seekBar.Width = W - 40;
            }
            if (_lblTime != null)
                _lblTime.Location = new Point(22, 40);

            // Cola: siempre a la izquierda
            if (_queuePanel != null) {
                _queuePanel.Left = 14;
                _queuePanel.Top  = 64;
            }

            // Botones centrados en el espacio restante (después de la cola)
            int qEnd   = 14 + 330 + 12;      // fin de la cola + margen
            int volW   = 200;                 // ancho zona volumen
            int availW = W - qEnd - volW - 20;

            int bW   = _btnPlay.Width;
            int totalBtns = _btnPrev.Width + _btnPlay.Width + _btnStop.Width +
                            _btnNext.Width + 12 * 3;
            int bx = qEnd + (availW - totalBtns) / 2;
            int by = 70;

            _btnPrev.Location = new Point(bx, by); bx += _btnPrev.Width + 12;
            _btnPlay.Location = new Point(bx, by); bx += _btnPlay.Width + 12;
            _btnStop.Location = new Point(bx, by); bx += _btnStop.Width + 12;
            _btnNext.Location = new Point(bx, by);

            // Botón "Agregar" encima de la cola
            _btnLoad.Location = new Point(14 + 330 + 12, 70);

            // Volumen a la derecha
            if (_tbVolume != null && _lblVolPct != null)
            {
                // Buscar el volIcon entre los controles
                foreach (Control c in _controlBar.Controls)
                    if (c is Label lbl && lbl.Text == "🔊")
                        lbl.Location = new Point(W - volW + 4, 82);

                _tbVolume.Left  = W - volW + 28;
                _tbVolume.Top   = 82;
                _tbVolume.Width = 140;

                _lblVolPct.Left = _tbVolume.Right + 6;
                _lblVolPct.Top  = 86;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop(); _player.Dispose();
            _bufGfx?.Dispose(); _backBuffer?.Dispose();
            base.OnFormClosed(e);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Right) {
                if (_player.HasFile) _player.SeekTo(Math.Min(_player.Duration, _player.Position + 5.0));
                e.Handled = true;
            } else if (e.KeyCode == Keys.Left) {
                if (_player.HasFile) _player.SeekTo(Math.Max(0.0, _player.Position - 5.0));
                e.Handled = true;
            } else if (e.KeyCode == Keys.Space) {
                if (_player.IsPlaying) _player.Pause(); else _player.Play();
                RefreshBtns();
                e.Handled = true;
            }
        }

        string Fmt(double s)
        {
            if (double.IsNaN(s) || double.IsInfinity(s) || s < 0) s = 0;
            int t = (int)s;
            return (t / 60) + ":" + (t % 60).ToString("D2");
        }

        void RefreshPlaylistBox()
        {
            if (_lstPlaylist == null) return;
            var items = new List<string>();
            for (int i = 0; i < _playlist.Count; i++) {
                string name = Path.GetFileNameWithoutExtension(_playlist[i]);
                if (name.Length > 36) name = name.Substring(0, 36) + "…";
                items.Add(name);
            }
            _lstPlaylist.SetItems(items, _songIndex);
        }

        void LoadSong(int index, bool autoPlay)
        {
            if (index < 0 || index >= _playlist.Count) return;
            _songIndex = index;
            string file = _playlist[_songIndex];
            try {
                _player.Load(file);
                _lblTitle.Text = "♫  " + Path.GetFileName(file);
                _lstPlaylist?.SetItems(null, _songIndex);
                _lstPlaylist?.Invalidate();
                _seekBar.SetPosition(0, Math.Max(1, _player.Duration));
                _lblTime.Text = "0:00 / " + Fmt(_player.Duration);
                if (autoPlay) _player.Play();
                RefreshBtns();
                _vizPanel.Invalidate();
            } catch (Exception ex) {
                MessageBox.Show("Error cargando:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void PlayPreviousSong()
        {
            if (_playlist.Count == 0) return;
            _songIndex = (_songIndex - 1 + _playlist.Count) % _playlist.Count;
            LoadSong(_songIndex, true);
        }

        void PlayNextSong()
        {
            if (_playlist.Count == 0) return;
            _songIndex = (_songIndex + 1) % _playlist.Count;
            LoadSong(_songIndex, true);
        }

        void ResetToInitialStateAfterSongEnd()
        {
            _player.Stop();
            _seekBar.SetPosition(0, Math.Max(1, _player.Duration));
            _lblTime.Text = "0:00 / " + Fmt(_player.Duration);
            RefreshBtns();
            _vizPanel.Invalidate();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PlaylistPanel — cola de reproducción dibujada a mano con scroll suave
    // ══════════════════════════════════════════════════════════════════════════
    public class PlaylistPanel : Control
    {
        public event Action<int> SongDoubleClicked;
        public Color Accent { get; set; } = Color.FromArgb(0, 200, 255);

        List<string> _items = new List<string>();
        int _selected = -1;
        int _scrollOffset = 0;
        const int ROW_H = 22;

        static readonly Color C_BG     = Color.FromArgb(14, 16, 32);
        static readonly Color C_ROW    = Color.FromArgb(18, 22, 42);
        static readonly Color C_ROWSEL = Color.FromArgb(22, 30, 58);
        static readonly Color C_TXT    = Color.FromArgb(190, 200, 240);
        static readonly Color C_IDX    = Color.FromArgb(80, 90, 140);

        public PlaylistPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
            BackColor = C_BG;
        }

        public void SetItems(List<string> items, int selected)
        {
            if (items != null) _items = items;
            _selected = selected;
            // Scroll para mostrar el seleccionado
            if (_selected >= 0) {
                int maxVisible = Height / ROW_H;
                if (_selected < _scrollOffset) _scrollOffset = _selected;
                if (_selected >= _scrollOffset + maxVisible)
                    _scrollOffset = _selected - maxVisible + 1;
            }
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int row = e.Y / ROW_H + _scrollOffset;
            if (row >= 0 && row < _items.Count) {
                _selected = row;
                Invalidate();
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            if (_selected >= 0 && _selected < _items.Count)
                SongDoubleClicked?.Invoke(_selected);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int maxScroll = Math.Max(0, _items.Count - Height / ROW_H);
            _scrollOffset = Math.Max(0, Math.Min(maxScroll,
                _scrollOffset + (e.Delta < 0 ? 1 : -1)));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(C_BG);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int maxVisible = Height / ROW_H + 1;
            for (int i = _scrollOffset; i < Math.Min(_scrollOffset + maxVisible, _items.Count); i++)
            {
                int y   = (i - _scrollOffset) * ROW_H;
                bool sel = i == _selected;

                // Fondo fila
                using (var br = new SolidBrush(sel ? C_ROWSEL : (i % 2 == 0 ? C_BG : C_ROW)))
                    g.FillRectangle(br, 0, y, Width, ROW_H);

                // Borde izquierdo si es seleccionado
                if (sel) {
                    using (var br = new SolidBrush(Accent))
                        g.FillRectangle(br, 0, y, 3, ROW_H);
                }

                // Número de índice
                using (var f  = new Font("Consolas", 8f))
                using (var br = new SolidBrush(sel ? Accent : C_IDX))
                    g.DrawString((i + 1).ToString("D2"), f, br, 5, y + (ROW_H - 13) / 2);

                // Nombre canción
                using (var f  = new Font("Segoe UI", 8.5f, sel ? FontStyle.Bold : FontStyle.Regular))
                using (var br = new SolidBrush(sel ? Color.White : C_TXT))
                    g.DrawString(_items[i], f, br, 32, y + (ROW_H - 14) / 2);
            }

            // Scrollbar visual
            if (_items.Count > Height / ROW_H)
            {
                float ratio   = (float)(Height / ROW_H) / _items.Count;
                float thumbH  = Math.Max(20, Height * ratio);
                float thumbY  = (Height - thumbH) * ((float)_scrollOffset / Math.Max(1, _items.Count - Height / ROW_H));
                using (var br = new SolidBrush(Color.FromArgb(60, Accent)))
                    g.FillRectangle(br, Width - 5, (int)thumbY, 4, (int)thumbH);
            }

            // Mensaje si vacío
            if (_items.Count == 0)
            {
                using (var f  = new Font("Segoe UI", 8.5f))
                using (var br = new SolidBrush(Color.FromArgb(60, 80, 120)))
                    g.DrawString("La cola está vacía.\nAgrega canciones WAV…", f, br, 10, 10);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BarraProgreso — barra de progreso interactiva dibujada a mano
    // ══════════════════════════════════════════════════════════════════════════
    public class BarraProgreso : Control
    {
        public event Action<double> SeekRequested;
        public bool IsDragging => _drag;

        double _pos, _dur = 1;
        bool   _drag, _hover;
        float  _hoverX;

        static readonly Color C_BG    = Color.FromArgb(16, 20, 45);
        static readonly Color C_FILL  = Color.FromArgb(0,  180, 255);
        static readonly Color C_FILL2 = Color.FromArgb(160, 60, 255);
        static readonly Color C_THUMB = Color.FromArgb(200, 220, 255);

        public BarraProgreso()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }

        public void SetPosition(double pos, double dur)
        {
            if (!_drag) { _pos = pos; _dur = dur; }
            else { _dur = dur; }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }
        protected override void OnMouseMove(MouseEventArgs e) {
            _hoverX = e.X;
            if (_drag) _pos = Math.Max(0, Math.Min(_dur, _dur * e.X / Width));
            Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            if (e.Button != MouseButtons.Left) return;
            _drag = true;
            _pos  = Math.Max(0, Math.Min(_dur, _dur * e.X / Width));
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e) {
            if (!_drag) return;
            _drag = false;
            _pos  = Math.Max(0, Math.Min(_dur, _dur * e.X / Width));
            Invalidate();
            SeekRequested?.Invoke(_pos);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int th = _hover || _drag ? 9 : 5;
            int ty = Height / 2 - th / 2;

            // Pista
            using (var path = Pill(0, ty, Width, th))
            using (var br   = new SolidBrush(C_BG))
                g.FillPath(br, path);

            // Progreso
            double ratio = _dur > 0 ? Math.Min(1, _pos / _dur) : 0;
            int fw = Math.Max(0, (int)(Width * ratio));
            if (fw > 2) {
                using (var path = Pill(0, ty, fw, th))
                using (var br   = new LinearGradientBrush(
                    new Point(0, ty), new Point(fw + 1, ty), C_FILL, C_FILL2))
                    g.FillPath(br, path);
            }

            // Thumb
            float tx = (float)(Width * ratio);
            float tr = _hover || _drag ? 8f : 5.5f;
            using (var br = new SolidBrush(C_THUMB))
                g.FillEllipse(br, tx - tr, Height / 2f - tr, tr * 2, tr * 2);
            using (var pen = new Pen(Color.FromArgb(80, 0, 150, 220), 1.2f))
                g.DrawEllipse(pen, tx - tr, Height / 2f - tr, tr * 2, tr * 2);

            // Tiempo en hover/drag
            if ((_hover || _drag) && _dur > 0)
            {
                double hp = Math.Max(0, Math.Min(_dur, _dur * _hoverX / Width));
                string ts = Fmt(hp);
                using (var f  = new Font("Consolas", 8.5f))
                using (var br = new SolidBrush(Color.FromArgb(160, 0, 180, 255))) {
                    var sz = g.MeasureString(ts, f);
                    float lx = Math.Max(sz.Width / 2, Math.Min(Width - sz.Width / 2, _hoverX));
                    g.DrawString(ts, f, br, lx - sz.Width / 2, 0);
                }
            }
        }

        static string Fmt(double s) { int t = (int)s; return $"{t / 60}:{t % 60:D2}"; }

        static GraphicsPath Pill(int x, int y, int w, int h)
        {
            var p = new GraphicsPath();
            if (w < h) { p.AddEllipse(x, y, w, h); return p; }
            p.AddArc(x,         y, h, h,  90, 180);
            p.AddArc(x + w - h, y, h, h, 270, 180);
            p.CloseFigure();
            return p;
        }
    }
}
