using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ProyectoReproductorMusical.Audio;

namespace ProyectoReproductorMusical.Visualizadores
{
    public class VisualizadorOsciloscopio : VisualizadorBase
    {
        public override string Name => "Osciloscopio";
        float _glow;
        readonly Pen _gridPen = new Pen(Color.FromArgb(18, 0, 100, 220), 1);
        readonly Pen _wavePen = new Pen(Color.White, 1.4f);
        readonly Pen _centerPen = new Pen(Color.White, 1f);
        readonly SolidBrush _fillBr = new SolidBrush(Color.FromArgb(15, 0, 150, 255));

        protected override void OnUpdate(float dt)
        {
            if (Analyzer != null && Analyzer.IsBeat) _glow = 1f;
            _glow *= 0.86f;
        }

        protected override void OnRender(Graphics g, int w, int h)
        {
            g.Clear(Color.FromArgb(4, 6, 16));
            using (var scanPen = new Pen(Color.FromArgb(5, 0, 20, 50), 2))
                for (int y = 0; y < h; y += 4) g.DrawLine(scanPen, 0, y, w, y);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Analyzer == null) return;

            float cy = h / 2f;
            int bands = AnalizadorAudio.SPECTRUM_BANDS;

            for (int i = 1; i < 4; i++) g.DrawLine(_gridPen, 0, h * i / 4f, w, h * i / 4f);
            for (int i = 1; i < 8; i++) g.DrawLine(_gridPen, w * i / 8f, 0, w * i / 8f, h);

            PointF[] top = new PointF[bands];
            PointF[] bot = new PointF[bands];
            PointF[] fill = new PointF[bands * 2];
            for (int i = 0; i < bands; i++)
            {
                float x = (float)i / (bands - 1) * w, val = Analyzer.SpectrumData[i];
                top[i] = new PointF(x, cy - val * cy * 0.85f);
                bot[i] = new PointF(x, cy + val * cy * 0.85f);
                fill[i] = top[i];
                fill[bands + i] = bot[bands - 1 - i];
            }
            g.FillPolygon(_fillBr, fill);

            for (int i = 0; i < bands - 1; i++)
            {
                float amp = Analyzer.SpectrumData[i];
                float hue = (float)i / bands * 0.3f + 0.55f + Time * 0.03f + _glow * 0.1f;
                Color c = Hsl(hue % 1f, 1f, 0.44f + amp * 0.28f + _glow * 0.14f);
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
}