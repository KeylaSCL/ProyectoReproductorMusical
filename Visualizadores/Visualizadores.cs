using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using MusicVisualizer.Audio;

namespace MusicVisualizer.Visualizadores
{
    // ═══════════════════════════════════════════════════════════════════════
    //  BASE
    // ═══════════════════════════════════════════════════════════════════════
    public abstract class VisualizadorBase
    {
        protected AnalizadorAudio Analyzer;
        protected float Time;
        public abstract string Name { get; }
        public void SetAnalyzer(AnalizadorAudio a) => Analyzer = a;
        public void Update(float dt) { Time += dt; OnUpdate(dt); }
        public void Render(Graphics g, int w, int h) { OnRender(g, w, h); }
        protected abstract void OnUpdate(float dt);
        protected abstract void OnRender(Graphics g, int w, int h);

        // HSL → RGB propio (sin librerías)
        protected static Color Hsl(float h, float s, float l, int alpha = 255)
        {
            h = ((h % 1f) + 1f) % 1f;
            float c  = (1 - Math.Abs(2 * l - 1)) * s;
            float x  = c * (1 - Math.Abs((h * 6) % 2 - 1));
            float m  = l - c / 2;
            float r, gr, b;
            switch ((int)(h * 6) % 6) {
                case 0: r=c; gr=x; b=0; break; case 1: r=x; gr=c; b=0; break;
                case 2: r=0; gr=c; b=x; break; case 3: r=0; gr=x; b=c; break;
                case 4: r=x; gr=0; b=c; break; default: r=c; gr=0; b=x; break;
            }
            return Color.FromArgb(alpha, Cl((r+m)*255), Cl((gr+m)*255), Cl((b+m)*255));
        }
        protected static int Cl(float v) => Math.Max(0, Math.Min(255, (int)v));

        // Degradado de fondo azul/índigo oscuro
        protected static void FillBg(Graphics g, int w, int h, Color top, Color bot)
        {
            using (var br = new LinearGradientBrush(new Point(0, 0), new Point(0, h), top, bot))
                g.FillRectangle(br, 0, 0, w, h);
        }

        // Colores de tema (azul índigo oscuro)
        protected static readonly Color BG_TOP  = Color.FromArgb(6,  8,  20);
        protected static readonly Color BG_BOT  = Color.FromArgb(10, 6,  26);
        protected static readonly Color BG_MID  = Color.FromArgb(8,  10, 24);

        protected static void SafeRadialGlow(Graphics g, float cx, float cy, float cr, Color c)
        {
            if (cr < 1f) return;
            try {
                using (var path = new GraphicsPath()) {
                    path.AddEllipse(cx - cr, cy - cr, cr * 2, cr * 2);
                    using (var br = new PathGradientBrush(path)) {
                        br.CenterColor    = c;
                        br.SurroundColors = new[] { Color.Transparent };
                        g.FillPath(br, path);
                    }
                }
            } catch { }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  1. BARRAS DE ESPECTRO — fondo azul oscuro
    // ═══════════════════════════════════════════════════════════════════════
    public class VisualizadorBarrasEspectro : VisualizadorBase
    {
        public override string Name => "Barras de Espectro";
        float[] _sm   = new float[AnalizadorAudio.SPECTRUM_BANDS];
        float[] _peak = new float[AnalizadorAudio.SPECTRUM_BANDS];
        float[] _pkV  = new float[AnalizadorAudio.SPECTRUM_BANDS];
        readonly Pen _basePen = new Pen(Color.FromArgb(35, 0, 120, 200), 1);

        protected override void OnUpdate(float dt)
        {
            if (Analyzer == null) return;
            for (int i = 0; i < AnalizadorAudio.SPECTRUM_BANDS; i++) {
                _sm[i] = _sm[i] * 0.72f + Analyzer.SpectrumData[i] * 0.28f;
                if (_sm[i] >= _peak[i]) { _peak[i] = _sm[i]; _pkV[i] = 0; }
                else { _pkV[i] += 2.2f * dt; _peak[i] = Math.Max(0, _peak[i] - _pkV[i] * dt); }
            }
        }

        protected override void OnRender(Graphics g, int w, int h)
        {
            FillBg(g, w, h, BG_TOP, BG_BOT);
            using (var gridPen = new Pen(Color.FromArgb(10, 0, 80, 160), 1))
                for (int i = 1; i < 4; i++) g.DrawLine(gridPen, 0, h * i / 4, w, h * i / 4);
            g.DrawLine(_basePen, 0, h - 1, w, h - 1);

            if (Analyzer == null) return;
            g.SmoothingMode = SmoothingMode.None;

            int   bands = AnalizadorAudio.SPECTRUM_BANDS;
            float bw    = (float)w / bands;
            float pad   = bw > 5 ? 1.5f : 0.5f;

            for (int i = 0; i < bands; i++) {
                float x   = i * bw + pad;
                float bh  = _sm[i] * (h - 4);
                float hue = (float)i / bands * 0.72f + Time * 0.04f;   // azul→violeta→cian
                Color c   = Hsl(hue + 0.55f, 1f, 0.42f + _sm[i] * 0.3f);

                using (var br = new SolidBrush(Color.FromArgb(220, c)))
                {
                    float y = h - bh;
                    g.FillRectangle(br, x, y, bw - pad * 2, bh);
                }

                // Marcador de pico
                float py = h - _peak[i] * (h - 4) - 2;
                using (var br = new SolidBrush(Color.FromArgb(200, c)))
                    g.FillRectangle(br, x, py, bw - pad * 2, 2);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  2. ONDA CIRCULAR — tema índigo
    // ═══════════════════════════════════════════════════════════════════════
    public class VisualizadorOndaCircular : VisualizadorBase
    {
        public override string Name => "Onda Circular";
        float   _beatScale = 1f;
        float[] _smooth    = new float[AnalizadorAudio.SPECTRUM_BANDS];
        readonly Pen _wavePen = new Pen(Color.White, 1.5f);

        protected override void OnUpdate(float dt)
        {
            if (Analyzer == null) return;
            if (Analyzer.IsBeat) _beatScale = 1.28f;
            _beatScale = _beatScale * 0.86f + 1f * 0.14f;
            for (int i = 0; i < AnalizadorAudio.SPECTRUM_BANDS; i++)
                _smooth[i] = _smooth[i] * 0.65f + Analyzer.SpectrumData[i] * 0.35f;
        }

        protected override void OnRender(Graphics g, int w, int h)
        {
            // Fondo: negro con tinte azul índigo
            FillBg(g, w, h, Color.FromArgb(6, 6, 18), Color.FromArgb(10, 4, 24));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Analyzer == null) return;

            float cx = w / 2f, cy = h / 2f;
            int   bands  = AnalizadorAudio.SPECTRUM_BANDS;
            float baseR  = Math.Min(w, h) * 0.24f * _beatScale;

            // Anillos guía (azul/violeta en lugar de verde)
            using (var ringPen = new Pen(Color.White, 1f)) {
                for (int r2 = 4; r2 >= 1; r2--) {
                    float rr = baseR * r2 * 0.55f;
                    // Alterna cian y violeta
                    int   alpha = 16 / r2;
                    ringPen.Color = r2 % 2 == 0
                        ? Color.FromArgb(alpha, 0, 160, 255)
                        : Color.FromArgb(alpha, 120, 60, 255);
                    g.DrawEllipse(ringPen, cx - rr, cy - rr, rr * 2, rr * 2);
                }
            }

            PointF[] outer = new PointF[bands + 1];
            PointF[] inner = new PointF[bands + 1];
            const double TWO_PI = Math.PI * 2;
            for (int i = 0; i <= bands; i++) {
                int   idx = i % bands;
                float val = _smooth[idx];
                float ang = (float)(i / (double)bands * TWO_PI - Math.PI / 2);
                float co  = (float)Math.Cos(ang), si = (float)Math.Sin(ang);
                outer[i] = new PointF(cx + co * (baseR + val * baseR * 1.75f), cy + si * (baseR + val * baseR * 1.75f));
                inner[i] = new PointF(cx + co * baseR * 0.55f, cy + si * baseR * 0.55f);
            }

            // Relleno cian translúcido
            PointF[] fill = new PointF[bands * 2 + 2];
            for (int i = 0; i <= bands; i++) fill[i] = outer[i];
            for (int i = 0; i <= bands; i++) fill[bands + 1 + i] = inner[Math.Max(0, bands - i)];
            using (var br = new SolidBrush(Color.FromArgb(18, 0, 150, 255)))
                g.FillPolygon(br, fill);

            // Contorno con hues azul→violeta→cian
            for (int i = 0; i < bands; i++) {
                float amp = _smooth[i];
                float hue = (float)i / bands * 0.3f + 0.55f + Time * 0.04f;  // 0.55→0.85 (azul→violeta)
                _wavePen.Color = Hsl(hue, 1f, 0.50f + amp * 0.24f);
                _wavePen.Width = 1.5f + amp * 4f;
                g.DrawLine(_wavePen, outer[i], outer[i + 1 < outer.Length ? i + 1 : 0]);
            }

            // Núcleo con brillo violeta/cian
            float cr = (Analyzer.BassEnergy * 32 + 8) * _beatScale;
            SafeRadialGlow(g, cx, cy, cr, Color.FromArgb(210, Hsl(Time * 0.10f + 0.60f, 1f, 0.65f)));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  3. PARTÍCULAS — fondo azul muy oscuro
    // ═══════════════════════════════════════════════════════════════════════
    public class VisualizadorParticulas : VisualizadorBase
    {
        public override string Name => "Partículas";
        readonly SistemaParticulas _ps  = new SistemaParticulas();
        readonly Random         _rng = new Random(42);
        float _beatFlash;
        int   _W = 800, _H = 500;

        protected override void OnUpdate(float dt)
        {
            _ps.Update(dt);
            if (Analyzer == null) return;
            if (Analyzer.Volume > 0.04f) {
                int cnt = (int)(Analyzer.Volume * 12) + 1;
                // Hue en rango azul/cian/violeta
                float hue = Time * 0.15f % 1f * 0.4f + 0.55f;
                _ps.Emit(_W / 2f + (float)(_rng.NextDouble() - 0.5) * _W * 0.28f, _H * 0.12f,
                    cnt, Analyzer.Volume * 200 + 55, Hsl(hue, 1f, 0.68f));
            }
            if (Analyzer.IsBeat) {
                _beatFlash = 1f;
                for (int i = 0; i < 8; i++) {
                    float hue = (float)i / 8 + Time * 0.1f + 0.55f;
                    _ps.EmitBurst(_W / 2f, _H / 2f, Analyzer.BassEnergy, Hsl(hue % 1f, 1f, 0.62f));
                }
            }
            _beatFlash *= 0.82f;
        }

        protected override void OnRender(Graphics g, int w, int h)
        {
            _W = w; _H = h;
            // Trail con tinte índigo
            using (var br = new SolidBrush(Color.FromArgb(30, 4, 6, 20)))
                g.FillRectangle(br, 0, 0, w, h);
            if (_beatFlash > 0.02f)
                using (var br = new SolidBrush(Color.FromArgb((int)(_beatFlash * 30), 0, 120, 255)))
                    g.FillRectangle(br, 0, 0, w, h);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            _ps.Draw(g);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  4. FIGURAS GEOMÉTRICAS — fondo azul/índigo
    // ═══════════════════════════════════════════════════════════════════════
    public class VisualizadorGeometrico : VisualizadorBase
    {
        public override string Name => "Figuras Geométricas";
        float _rot, _beatPulse;
        readonly Pen   _polyPen  = new Pen(Color.White, 1.5f);
        readonly Pen   _gridPen  = new Pen(Color.FromArgb(8, 0, 80, 200), 1);
        readonly Pen   _starPen  = new Pen(Color.White, 2f);
        readonly SolidBrush _vtxBr  = new SolidBrush(Color.White);
        readonly SolidBrush _starBr = new SolidBrush(Color.White);

        protected override void OnUpdate(float dt)
        {
            float spd = Analyzer != null ? Analyzer.Volume * 2.2f + 0.35f : 0.35f;
            _rot += spd * dt;
            if (Analyzer != null && Analyzer.IsBeat) _beatPulse = 1f;
            _beatPulse *= 0.80f;
        }

        protected override void OnRender(Graphics g, int w, int h)
        {
            FillBg(g, w, h, Color.FromArgb(6, 8, 18), Color.FromArgb(10, 6, 24));
            for (int xi = 0; xi < w; xi += 55)
                for (int yi = 0; yi < h; yi += 48)
                    g.DrawLine(_gridPen, xi, yi, xi + 27, yi + 24);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Analyzer == null) return;

            float cx = w / 2f, cy = h / 2f;
            float baseR = Math.Min(w, h) * 0.12f;
            float pulse = 1f + _beatPulse * 0.32f;
            int[] sides = { 3, 4, 5, 6, 8, 12 };

            for (int li = 0; li < sides.Length; li++) {
                int   n   = sides[li];
                int   bi  = li * 10 % AnalizadorAudio.SPECTRUM_BANDS;
                float bv  = Analyzer.SpectrumData[bi];
                float r   = (baseR * (li + 1) + bv * baseR * 0.85f) * pulse;
                float a0  = _rot * (li % 2 == 0 ? 1 : -1) + li * 0.22f;

                PointF[] pts = new PointF[n];
                for (int i = 0; i < n; i++) {
                    float ang = a0 + i * (float)(Math.PI * 2 / n);
                    pts[i] = new PointF(cx + (float)Math.Cos(ang) * r, cy + (float)Math.Sin(ang) * r);
                }
                // Hues en azul/violeta/cian
                float hue = (float)li / sides.Length * 0.4f + 0.55f + Time * 0.038f;
                Color c   = Hsl(hue % 1f, 1f, 0.48f + bv * 0.24f);
                _polyPen.Color = Color.FromArgb(200, c);
                _polyPen.Width = 1.5f + bv * 3.5f + _beatPulse * 0.8f;
                g.DrawPolygon(_polyPen, pts);

                float vs = 2.5f + bv * 4.5f;
                _vtxBr.Color = Color.FromArgb(160, c);
                foreach (var pt in pts)
                    g.FillEllipse(_vtxBr, pt.X - vs / 2, pt.Y - vs / 2, vs, vs);
            }

            // Estrella central cian
            float cr = Analyzer.BassEnergy * 50 + 16, cp = 1f + _beatPulse * 0.4f;
            Color cs = Hsl(Time * 0.11f + 0.58f, 1f, 0.70f);
            PointF[] star = new PointF[10];
            for (int i = 0; i < 10; i++) {
                float a  = Time + i * (float)(Math.PI / 5);
                float ri = i % 2 == 0 ? cr * cp : cr * 0.38f * cp;
                star[i]  = new PointF(cx + (float)Math.Cos(a) * ri, cy + (float)Math.Sin(a) * ri);
            }
            _starBr.Color = Color.FromArgb(200, cs);
            _starPen.Color = cs;
            g.FillPolygon(_starBr, star);
            g.DrawPolygon(_starPen, star);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  5. OSCILOSCOPIO — estilo monitor CRT azul
    // ═══════════════════════════════════════════════════════════════════════
    public class VisualizadorOsciloscopio : VisualizadorBase
    {
        public override string Name => "Osciloscopio";
        float _glow;
        readonly Pen       _gridPen   = new Pen(Color.FromArgb(18, 0, 100, 220), 1);
        readonly Pen       _wavePen   = new Pen(Color.White, 1.4f);
        readonly Pen       _centerPen = new Pen(Color.White, 1f);
        readonly SolidBrush _fillBr   = new SolidBrush(Color.FromArgb(15, 0, 150, 255));

        protected override void OnUpdate(float dt)
        {
            if (Analyzer != null && Analyzer.IsBeat) _glow = 1f;
            _glow *= 0.86f;
        }

        protected override void OnRender(Graphics g, int w, int h)
        {
            // Fondo negro profundo con tinte azul
            g.Clear(Color.FromArgb(4, 6, 16));
            using (var scanPen = new Pen(Color.FromArgb(5, 0, 20, 50), 2))
                for (int y = 0; y < h; y += 4) g.DrawLine(scanPen, 0, y, w, y);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Analyzer == null) return;

            float cy = h / 2f;
            int   bands = AnalizadorAudio.SPECTRUM_BANDS;

            for (int i = 1; i < 4; i++) g.DrawLine(_gridPen, 0, h * i / 4f, w, h * i / 4f);
            for (int i = 1; i < 8; i++) g.DrawLine(_gridPen, w * i / 8f, 0, w * i / 8f, h);

            PointF[] top  = new PointF[bands];
            PointF[] bot  = new PointF[bands];
            PointF[] fill = new PointF[bands * 2];
            for (int i = 0; i < bands; i++) {
                float x = (float)i / (bands - 1) * w, val = Analyzer.SpectrumData[i];
                top[i]         = new PointF(x, cy - val * cy * 0.85f);
                bot[i]         = new PointF(x, cy + val * cy * 0.85f);
                fill[i]        = top[i];
                fill[bands + i] = bot[bands - 1 - i];
            }
            g.FillPolygon(_fillBr, fill);

            for (int i = 0; i < bands - 1; i++) {
                float amp = Analyzer.SpectrumData[i];
                // Hue en rango cian→azul→violeta
                float hue = (float)i / bands * 0.3f + 0.55f + Time * 0.03f + _glow * 0.1f;
                Color c   = Hsl(hue % 1f, 1f, 0.44f + amp * 0.28f + _glow * 0.14f);
                _wavePen.Color = c;
                _wavePen.Width = 1.4f + amp * 3.8f + _glow * 1.8f;
                g.DrawLine(_wavePen, top[i], top[i + 1]);
                g.DrawLine(_wavePen, bot[i], bot[i + 1]);
            }
            _centerPen.Color = Color.FromArgb((int)(40 + _glow * 100), 0, 160, 255);
            _centerPen.Width = 1 + _glow * 2;
            g.DrawLine(_centerPen, 0, cy, w, cy);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  6. TÚNEL DE LUZ — azul/violeta
    // ═══════════════════════════════════════════════════════════════════════
    public class VisualizadorTunel : VisualizadorBase
    {
        public override string Name => "Túnel de Luz";
        float _depth, _beatPulse;
        const int RINGS = 20;
        readonly Pen _ringPen = new Pen(Color.White, 1f);
        readonly Pen _linePen = new Pen(Color.White, 0.5f);

        protected override void OnUpdate(float dt)
        {
            float spd = Analyzer != null ? Analyzer.Volume * 5.5f + 0.7f : 0.7f;
            _depth = (_depth + spd * dt) % 1f;
            if (Analyzer != null && Analyzer.IsBeat) _beatPulse = 1f;
            _beatPulse *= 0.78f;
        }

        protected override void OnRender(Graphics g, int w, int h)
        {
            g.Clear(Color.FromArgb(3, 4, 14));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = w / 2f, cy = h / 2f;

            for (int ri = RINGS - 1; ri >= 0; ri--) {
                float t     = ((float)ri / RINGS + _depth) % 1f;
                float persp = 1f - t;
                int   alpha = Math.Max(0, Math.Min(255, (int)(persp * 220)));
                int   bi    = (int)(t * AnalizadorAudio.SPECTRUM_BANDS) % AnalizadorAudio.SPECTRUM_BANDS;
                float bv    = Analyzer != null ? Analyzer.SpectrumData[bi] : 0;
                float sz    = Math.Min(w, h) * 0.46f * persp * (1f + bv * 0.30f + _beatPulse * 0.07f);
                if (sz < 1f) continue;

                // Hues azul/cian/violeta  (0.55 – 0.85)
                float hue = t * 0.3f + 0.55f + Time * 0.048f + _beatPulse * 0.18f;
                Color c   = Hsl(hue % 1f, 1f, 0.42f + bv * 0.28f);

                _ringPen.Color = Color.FromArgb(alpha, c);
                _ringPen.Width = Math.Max(0.5f, 1f + bv * 5f + _beatPulse * 1.8f);

                int   sides = 6;
                float rot   = ri * 0.11f + Time * 0.38f + t * 0.5f;
                PointF[] pts = new PointF[sides];
                for (int i = 0; i < sides; i++) {
                    float a = rot + i * (float)(Math.PI * 2 / sides);
                    pts[i] = new PointF(cx + (float)Math.Cos(a) * sz, cy + (float)Math.Sin(a) * sz);
                }
                g.DrawPolygon(_ringPen, pts);

                if (ri % 3 == 0) {
                    int lineAlpha = Math.Max(0, alpha / 4);
                    _linePen.Color = Color.FromArgb(lineAlpha, c);
                    foreach (var pt in pts)
                        g.DrawLine(_linePen, cx, cy, pt.X, pt.Y);
                }
            }

            float cr = (Analyzer?.BassEnergy ?? 0) * 38 + 8 + _beatPulse * 28;
            Color cc = Hsl(Time * 0.075f + 0.60f, 1f, 0.68f);
            SafeRadialGlow(g, cx, cy, cr, Color.FromArgb(190, cc));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  COMBINADOS
    // ═══════════════════════════════════════════════════════════════════════
    public class VisualizadorParticulasEspectro : VisualizadorBase
    {
        public override string Name => "Partículas + Espectro";
        readonly VisualizadorBarrasEspectro _spectrum  = new VisualizadorBarrasEspectro();
        readonly VisualizadorParticulas     _particles = new VisualizadorParticulas();

        protected override void OnUpdate(float dt)
        {
            if (Analyzer != null) { _spectrum.SetAnalyzer(Analyzer); _particles.SetAnalyzer(Analyzer); }
            _spectrum.Update(dt); _particles.Update(dt);
        }
        protected override void OnRender(Graphics g, int w, int h)
        {
            _spectrum.Render(g, w, h);
            _particles.Render(g, w, h);
        }
    }

    public class VisualizadorGeometriaTunel : VisualizadorBase
    {
        public override string Name => "Figuras + Túnel";
        readonly VisualizadorTunel     _tunnel = new VisualizadorTunel();
        readonly VisualizadorGeometrico  _geo    = new VisualizadorGeometrico();

        protected override void OnUpdate(float dt)
        {
            if (Analyzer != null) { _tunnel.SetAnalyzer(Analyzer); _geo.SetAnalyzer(Analyzer); }
            _tunnel.Update(dt); _geo.Update(dt);
        }
        protected override void OnRender(Graphics g, int w, int h)
        {
            _tunnel.Render(g, w, h);
            _geo.Render(g, w, h);
        }
    }
}
