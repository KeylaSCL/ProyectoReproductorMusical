using ProyectoReproductorMusical.Audio;
using MusicVisualizer.Visualizadores;
using ProyectoReproductorMusical;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ProyectoReproductorMusical.Visualizadores
{
    public class VisualizadorParticulas : VisualizadorBase
    {
        public override string Name => "Partículas";
        readonly SistemaParticulas _ps = new SistemaParticulas();
        readonly Random _rng = new Random(42);
        float _beatFlash;
        int _W = 800, _H = 500;

        protected override void OnUpdate(float dt)
        {
            _ps.Update(dt);
            if (Analyzer == null) return;
            if (Analyzer.Volume > 0.04f)
            {
                int cnt = (int)(Analyzer.Volume * 12) + 1;
                float hue = Time * 0.15f % 1f * 0.4f + 0.55f;
                _ps.Emit(_W / 2f + (float)(_rng.NextDouble() - 0.5) * _W * 0.28f, _H * 0.12f,
                    cnt, Analyzer.Volume * 200 + 55, Hsl(hue, 1f, 0.68f));
            }
            if (Analyzer.IsBeat)
            {
                _beatFlash = 1f;
                for (int i = 0; i < 8; i++)
                {
                    float hue = (float)i / 8 + Time * 0.1f + 0.55f;
                    _ps.EmitBurst(_W / 2f, _H / 2f, Analyzer.BassEnergy, Hsl(hue % 1f, 1f, 0.62f));
                }
            }
            _beatFlash *= 0.82f;
        }

        protected override void OnRender(Graphics g, int w, int h)
        {
            _W = w; _H = h;
            using (var br = new SolidBrush(Color.FromArgb(30, 4, 6, 20)))
                g.FillRectangle(br, 0, 0, w, h);
            if (_beatFlash > 0.02f)
                using (var br = new SolidBrush(Color.FromArgb((int)(_beatFlash * 30), 0, 120, 255)))
                    g.FillRectangle(br, 0, 0, w, h);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            _ps.Draw(g);
        }
    }
}