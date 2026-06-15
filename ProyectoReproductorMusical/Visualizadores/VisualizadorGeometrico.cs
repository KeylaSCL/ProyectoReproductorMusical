using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ProyectoReproductorMusical.Audio;

namespace ProyectoReproductorMusical.Visualizadores
{
    public class VisualizadorGeometrico : VisualizadorBase
    {
        public override string Name => "Figuras Geométricas";
        float _rot, _beatPulse;
        readonly Pen _polyPen = new Pen(Color.White, 1.5f);
        readonly Pen _gridPen = new Pen(Color.FromArgb(8, 0, 80, 200), 1);
        readonly Pen _starPen = new Pen(Color.White, 2f);
        readonly SolidBrush _vtxBr = new SolidBrush(Color.White);
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

            for (int li = 0; li < sides.Length; li++)
            {
                int n = sides[li];
                int bi = li * 10 % AnalizadorAudio.SPECTRUM_BANDS;
                float bv = Analyzer.SpectrumData[bi];
                float r = (baseR * (li + 1) + bv * baseR * 0.85f) * pulse;
                float a0 = _rot * (li % 2 == 0 ? 1 : -1) + li * 0.22f;

                PointF[] pts = new PointF[n];
                for (int i = 0; i < n; i++)
                {
                    float ang = a0 + i * (float)(Math.PI * 2 / n);
                    pts[i] = new PointF(cx + (float)Math.Cos(ang) * r, cy + (float)Math.Sin(ang) * r);
                }
                float hue = (float)li / sides.Length * 0.4f + 0.55f + Time * 0.038f;
                Color c = Hsl(hue % 1f, 1f, 0.48f + bv * 0.24f);
                _polyPen.Color = Color.FromArgb(200, c);
                _polyPen.Width = 1.5f + bv * 3.5f + _beatPulse * 0.8f;
                g.DrawPolygon(_polyPen, pts);

                float vs = 2.5f + bv * 4.5f;
                _vtxBr.Color = Color.FromArgb(160, c);
                foreach (var pt in pts)
                    g.FillEllipse(_vtxBr, pt.X - vs / 2, pt.Y - vs / 2, vs, vs);
            }

            float cr = Analyzer.BassEnergy * 50 + 16, cp = 1f + _beatPulse * 0.4f;
            Color cs = Hsl(Time * 0.11f + 0.58f, 1f, 0.70f);
            PointF[] star = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Time + i * (float)(Math.PI / 5);
                float ri = i % 2 == 0 ? cr * cp : cr * 0.38f * cp;
                star[i] = new PointF(cx + (float)Math.Cos(a) * ri, cy + (float)Math.Sin(a) * ri);
            }
            _starBr.Color = Color.FromArgb(200, cs);
            _starPen.Color = cs;
            g.FillPolygon(_starBr, star);
            g.DrawPolygon(_starPen, star);
        }
    }
}