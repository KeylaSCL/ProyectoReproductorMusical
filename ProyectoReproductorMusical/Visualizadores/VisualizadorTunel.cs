using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ProyectoReproductorMusical.Audio;

namespace ProyectoReproductorMusical.Visualizadores
{
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

            for (int ri = RINGS - 1; ri >= 0; ri--)
            {
                float t = ((float)ri / RINGS + _depth) % 1f;
                float persp = 1f - t;
                int alpha = Math.Max(0, Math.Min(255, (int)(persp * 220)));
                int bi = (int)(t * AnalizadorAudio.SPECTRUM_BANDS) % AnalizadorAudio.SPECTRUM_BANDS;
                float bv = Analyzer != null ? Analyzer.SpectrumData[bi] : 0;
                float sz = Math.Min(w, h) * 0.46f * persp * (1f + bv * 0.30f + _beatPulse * 0.07f);
                if (sz < 1f) continue;

                float hue = t * 0.3f + 0.55f + Time * 0.048f + _beatPulse * 0.18f;
                Color c = Hsl(hue % 1f, 1f, 0.42f + bv * 0.28f);

                _ringPen.Color = Color.FromArgb(alpha, c);
                _ringPen.Width = Math.Max(0.5f, 1f + bv * 5f + _beatPulse * 1.8f);

                int sides = 6;
                float rot = ri * 0.11f + Time * 0.38f + t * 0.5f;
                PointF[] pts = new PointF[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = rot + i * (float)(Math.PI * 2 / sides);
                    pts[i] = new PointF(cx + (float)Math.Cos(a) * sz, cy + (float)Math.Sin(a) * sz);
                }
                g.DrawPolygon(_ringPen, pts);

                if (ri % 3 == 0)
                {
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
}