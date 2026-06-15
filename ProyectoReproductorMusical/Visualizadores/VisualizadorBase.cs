using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ProyectoReproductorMusical.Audio;

namespace ProyectoReproductorMusical.Visualizadores
{
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
            float c = (1 - Math.Abs(2 * l - 1)) * s;
            float x = c * (1 - Math.Abs((h * 6) % 2 - 1));
            float m = l - c / 2;
            float r, gr, b;
            switch ((int)(h * 6) % 6)
            {
                case 0: r = c; gr = x; b = 0; break;
                case 1: r = x; gr = c; b = 0; break;
                case 2: r = 0; gr = c; b = x; break;
                case 3: r = 0; gr = x; b = c; break;
                case 4: r = x; gr = 0; b = c; break;
                default: r = c; gr = 0; b = x; break;
            }
            return Color.FromArgb(alpha, Cl((r + m) * 255), Cl((gr + m) * 255), Cl((b + m) * 255));
        }
        protected static int Cl(float v) => Math.Max(0, Math.Min(255, (int)v));

        // Degradado de fondo azul/índigo oscuro
        protected static void FillBg(Graphics g, int w, int h, Color top, Color bot)
        {
            using (var br = new LinearGradientBrush(new Point(0, 0), new Point(0, h), top, bot))
                g.FillRectangle(br, 0, 0, w, h);
        }

        // Colores de tema (azul índigo oscuro)
        protected static readonly Color BG_TOP = Color.FromArgb(6, 8, 20);
        protected static readonly Color BG_BOT = Color.FromArgb(10, 6, 26);
        protected static readonly Color BG_MID = Color.FromArgb(8, 10, 24);

        protected static void SafeRadialGlow(Graphics g, float cx, float cy, float cr, Color c)
        {
            if (cr < 1f) return;
            try
            {
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(cx - cr, cy - cr, cr * 2, cr * 2);
                    using (var br = new PathGradientBrush(path))
                    {
                        br.CenterColor = c;
                        br.SurroundColors = new[] { Color.Transparent };
                        g.FillPath(br, path);
                    }
                }
            }
            catch { }
        }
    }
}