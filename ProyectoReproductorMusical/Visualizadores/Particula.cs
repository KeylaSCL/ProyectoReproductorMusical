using System;
using System.Drawing;

namespace MusicVisualizer.Visualizadores
{
    /// <summary>
    /// Representa una partícula individual con física newtoniana propia.
    /// </summary>
    public class Particula
    {
        public float X, Y, VX, VY, Life, MaxLife, Size;
        public Color Color;
        public bool Dead => Life <= 0;

        public void Update(float dt)
        {
            X += VX * dt;
            Y += VY * dt;
            VY += 110f * dt;   // Gravedad
            VX *= 0.985f;      // Fricción lateral
            Life -= dt;
            Size *= 0.993f;
        }
    }
}