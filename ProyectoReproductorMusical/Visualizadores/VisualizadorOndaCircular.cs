using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ProyectoReproductorMusical.Audio;

namespace ProyectoReproductorMusical.Visualizadores
{
    public class VisualizadorOndaCircular : VisualizadorBase
    {
        public override string Name => "Onda Circular";
        float _beatScale = 1f;
        float[] _smooth = new float[AnalizadorAudio.SPECTRUM_BANDS];
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
            FillBg(g, w, h, Color.FromArgb(6, 6, 18), Color.FromArgb(10, 4, 24));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Analyzer == null) return;

            float cx = w / 2f, cy = h / 2f;
            int bands = AnalizadorAudio.SPECTRUM_BANDS;
            float baseR = Math.Min(w, h) * 0.24f * _beatScale;

            using (var ringPen = new Pen(Color.White, 1f))
            {
                for (int r2 = 4; r2 >= 1; r2--)
                {
                    float rr = baseR * r2 * 0.55f;
                    int alpha = 16 / r2;
                    ringPen.Color = r2 % 2 == 0
                        ? Color.FromArgb(alpha, 0, 160, 255)
                        : Color.FromArgb(alpha, 120, 60, 255);
                    g.DrawEllipse(ringPen, cx - rr, cy - rr, rr * 2, rr * 2);
                }
            }

            PointF[] outer = new PointF[bands + 1];
            PointF[] inner = new PointF[bands + 1];
            const double TWO_PI = Math.PI * 2;
            for (int i = 0; i <= bands; i++)
            {
                int idx = i % bands;
                float val = _smooth[idx];
                float ang = (float)(i / (double)bands * TWO_PI - Math.PI / 2);
                float co = (float)Math.Cos(ang), si = (float)Math.Sin(ang);
                outer[i] = new PointF(cx + co * (baseR + val * baseR * 1.75f), cy + si * (baseR + val * baseR * 1.75f));
                inner[i] = new PointF(cx + co * baseR * 0.55f, cy + si * baseR * 0.55f);
            }

            PointF[] fill = new PointF[bands * 2 + 2];
            for (int i = 0; i <= bands; i++) fill[i] = outer[i];
            for (int i = 0; i <= bands; i++) fill[bands + 1 + i] = inner[Math.Max(0, bands - i)];
            using (var br = new SolidBrush(Color.FromArgb(18, 0, 150, 255)))
                g.FillPolygon(br, fill);

            for (int i = 0; i < bands; i++)
            {
                float amp = _smooth[i];
                float hue = (float)i / bands * 0.3f + 0.55f + Time * 0.04f;
                _wavePen.Color = Hsl(hue, 1f, 0.50f + amp * 0.24f);
                _wavePen.Width = 1.5f + amp * 4f;
                g.DrawLine(_wavePen, outer[i], outer[i + 1 < outer.Length ? i + 1 : 0]);
            }

            float cr = (Analyzer.BassEnergy * 32 + 8) * _beatScale;
            SafeRadialGlow(g, cx, cy, cr, Color.FromArgb(210, Hsl(Time * 0.10f + 0.60f, 1f, 0.65f)));
        }
    }
}