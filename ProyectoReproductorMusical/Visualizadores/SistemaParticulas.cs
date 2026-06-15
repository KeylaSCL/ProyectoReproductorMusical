using System;
using System.Collections.Generic;
using System.Drawing;

namespace MusicVisualizer.Visualizadores
{
    /// <summary>
    /// Administra el ciclo de vida, emisión y renderizado del conjunto de partículas.
    /// </summary>
    public class SistemaParticulas
    {
        readonly List<Particula> _particles = new List<Particula>(2000);
        readonly Random _rng = new Random();

        public void Update(float dt)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                _particles[i].Update(dt);
                if (_particles[i].Dead) _particles.RemoveAt(i);
            }
        }

        public void Emit(float x, float y, int count, float speed, Color baseColor,
            float sizeMin = 2.5f, float sizeMax = 8f, float lifeMin = 0.7f, float lifeMax = 2.5f)
        {
            for (int i = 0; i < count; i++)
            {
                double ang = _rng.NextDouble() * Math.PI * 2;
                float spd = (float)(_rng.NextDouble() * speed + speed * 0.25);
                float lf = (float)(_rng.NextDouble() * (lifeMax - lifeMin) + lifeMin);
                float sz = (float)(_rng.NextDouble() * (sizeMax - sizeMin) + sizeMin);
                int dr = _rng.Next(-28, 28), dg = _rng.Next(-28, 28), db = _rng.Next(-28, 28);

                Color c = Color.FromArgb(
                    Math.Max(0, Math.Min(255, baseColor.R + dr)),
                    Math.Max(0, Math.Min(255, baseColor.G + dg)),
                    Math.Max(0, Math.Min(255, baseColor.B + db)));

                _particles.Add(new Particula
                {
                    X = x,
                    Y = y,
                    VX = (float)(Math.Cos(ang) * spd),
                    VY = (float)(Math.Sin(ang) * spd) - speed * 0.45f,
                    Life = lf,
                    MaxLife = lf,
                    Size = sz,
                    Color = c
                });
            }
        }

        public void EmitBurst(float x, float y, float energy, Color baseColor)
        {
            int cnt = (int)(energy * 38) + 5;
            Emit(x, y, cnt, energy * 280 + 70, baseColor, 2, 10, 0.4f, 1.8f);
        }

        public void Draw(Graphics g)
        {
            foreach (var p in _particles)
            {
                float a = Math.Max(0, p.Life / p.MaxLife);
                Color c = Color.FromArgb((int)(a * 215), p.Color);
                float half = p.Size / 2;
                using (var br = new SolidBrush(c))
                    g.FillEllipse(br, p.X - half, p.Y - half, p.Size, p.Size);
            }
        }
    }
}