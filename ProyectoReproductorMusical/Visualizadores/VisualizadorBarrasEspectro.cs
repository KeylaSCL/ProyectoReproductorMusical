using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ProyectoReproductorMusical.Audio;

namespace ProyectoReproductorMusical.Visualizadores
{
    public class VisualizadorBarrasEspectro : VisualizadorBase
    {
        public override string Name => "Barras de Espectro";
        float[] _sm = new float[AnalizadorAudio.SPECTRUM_BANDS];
        float[] _peak = new float[AnalizadorAudio.SPECTRUM_BANDS];
        float[] _pkV = new float[AnalizadorAudio.SPECTRUM_BANDS];
        readonly Pen _basePen = new Pen(Color.FromArgb(35, 0, 120, 200), 1);

        protected override void OnUpdate(float dt)
        {
            if (Analyzer == null) return;
            for (int i = 0; i < AnalizadorAudio.SPECTRUM_BANDS; i++)
            {
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

            int bands = AnalizadorAudio.SPECTRUM_BANDS;
            float bw = (float)w / bands;
            float pad = bw > 5 ? 1.5f : 0.5f;

            for (int i = 0; i < bands; i++)
            {
                float x = i * bw + pad;
                float bh = _sm[i] * (h - 4);
                float hue = (float)i / bands * 0.72f + Time * 0.04f;
                Color c = Hsl(hue + 0.55f, 1f, 0.42f + _sm[i] * 0.3f);

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
}