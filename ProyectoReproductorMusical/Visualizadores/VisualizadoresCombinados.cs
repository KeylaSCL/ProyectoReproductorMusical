using System.Drawing;

namespace ProyectoReproductorMusical.Visualizadores
{
    public class VisualizadorParticulasEspectro : VisualizadorBase
    {
        public override string Name => "Partículas + Espectro";
        readonly VisualizadorBarrasEspectro _spectrum = new VisualizadorBarrasEspectro();
        readonly VisualizadorParticulas _particles = new VisualizadorParticulas();

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
        readonly VisualizadorTunel _tunnel = new VisualizadorTunel();
        readonly VisualizadorGeometrico _geo = new VisualizadorGeometrico();

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