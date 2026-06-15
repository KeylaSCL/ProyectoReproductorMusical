using System;

namespace ProyectoReproductorMusical.Audio
{
    /// <summary>
    /// Analiza datos de audio en tiempo real usando FFT manual.
    /// Extrae: espectro de frecuencias, volumen RMS, detección de pulsos (beat).
    /// </summary>
    public class AnalizadorAudio
    {
        // ── Constantes ──────────────────────────────────────────────────────────
        public const int FFT_SIZE = 1024;          // debe ser potencia de 2
        public const int SPECTRUM_BANDS = 64;      // barras visibles
        private const double BEAT_THRESHOLD = 1.4; // multiplicador sobre energía media
        private const int BEAT_HISTORY = 43;   // ~1 segundo a 43 fps

        // ── Estado interno ───────────────────────────────────────────────────
        private readonly float[] _spectrumSmooth = new float[SPECTRUM_BANDS];
        private readonly double[] _energyHistory = new double[BEAT_HISTORY];
        private int _energyIndex;
        private double _lastBeatTime;
        private float _volumeSmooth;

        // ── Resultados públicos ──────────────────────────────────────────────
        public float[] SpectrumData { get; private set; } = new float[SPECTRUM_BANDS];
        public float Volume { get; private set; }
        public bool IsBeat { get; private set; }
        public float BassEnergy { get; private set; }
        public float MidEnergy { get; private set; }
        public float HighEnergy { get; private set; }

        // ────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Procesa un bloque de muestras PCM float[-1..1].
        /// Llámalo cada vez que tu reproductor entrega un buffer de audio.
        /// </summary>
        public void Process(float[] samples, int count)
        {
            if (samples == null || count == 0) return;

            // 1. Calcular volumen RMS
            double sumSq = 0;
            int n = Math.Min(count, samples.Length);
            for (int i = 0; i < n; i++) sumSq += samples[i] * samples[i];
            float rms = (float)Math.Sqrt(sumSq / n);
            _volumeSmooth = _volumeSmooth * 0.85f + rms * 0.15f;
            Volume = Math.Min(1f, _volumeSmooth * 3f);

            // 2. Preparar ventana de Hann + FFT
            int fftLen = Math.Min(FFT_SIZE, n);
            double[] re = new double[FFT_SIZE];
            double[] im = new double[FFT_SIZE];
            for (int i = 0; i < fftLen; i++)
            {
                double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (fftLen - 1)));
                re[i] = samples[i] * window;
            }
            FFT(re, im, FFT_SIZE);

            // 3. Magnitudes → bandas logarítmicas
            float[] raw = new float[SPECTRUM_BANDS];
            double logMin = Math.Log(1);
            double logMax = Math.Log(FFT_SIZE / 2.0);
            for (int b = 0; b < SPECTRUM_BANDS; b++)
            {
                double t = (double)b / SPECTRUM_BANDS;
                int low = (int)Math.Exp(logMin + t * (logMax - logMin));
                int high = (int)Math.Exp(logMin + (t + 1.0 / SPECTRUM_BANDS) * (logMax - logMin));
                low = Math.Max(0, Math.Min(low, FFT_SIZE / 2 - 1));
                high = Math.Max(low + 1, Math.Min(high, FFT_SIZE / 2));

                double mag = 0;
                for (int k = low; k < high; k++)
                    mag = Math.Max(mag, Math.Sqrt(re[k] * re[k] + im[k] * im[k]));

                raw[b] = (float)(Math.Log10(1 + mag) / 3.0);
            }

            // 4. Suavizado temporal
            for (int b = 0; b < SPECTRUM_BANDS; b++)
            {
                float decay = raw[b] > _spectrumSmooth[b] ? 0.6f : 0.08f;
                _spectrumSmooth[b] = _spectrumSmooth[b] * (1 - decay) + raw[b] * decay;
                SpectrumData[b] = Math.Min(1f, _spectrumSmooth[b] * 2f);
            }

            // 5. Energía por rangos
            BassEnergy = AverageBands(0, 8);
            MidEnergy = AverageBands(8, 32);
            HighEnergy = AverageBands(32, SPECTRUM_BANDS);

            // 6. Detección de beat (energy-based)
            double energy = BassEnergy;
            _energyHistory[_energyIndex % BEAT_HISTORY] = energy;
            _energyIndex++;
            double avg = 0;
            for (int i = 0; i < BEAT_HISTORY; i++) avg += _energyHistory[i];
            avg /= BEAT_HISTORY;

            double now = Environment.TickCount / 1000.0;
            IsBeat = energy > avg * BEAT_THRESHOLD && energy > 0.1 && (now - _lastBeatTime) > 0.25;
            if (IsBeat) _lastBeatTime = now;
        }

        // ────────────────────────────────────────────────────────────────────
        // FFT in-place Cooley-Tukey (radix-2, iterativa)
        private static void FFT(double[] re, double[] im, int n)
        {
            // Bit-reversal permutation
            int j = 0;
            for (int i = 1; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { Swap(ref re[i], ref re[j]); Swap(ref im[i], ref im[j]); }
            }
            // Butterfly
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wRe = Math.Cos(ang), wIm = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double curRe = 1, curIm = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        double uRe = re[i + k], uIm = im[i + k];
                        double vRe = re[i + k + len / 2] * curRe - im[i + k + len / 2] * curIm;
                        double vIm = re[i + k + len / 2] * curIm + im[i + k + len / 2] * curRe;
                        re[i + k] = uRe + vRe; im[i + k] = uIm + vIm;
                        re[i + k + len / 2] = uRe - vRe; im[i + k + len / 2] = uIm - vIm;
                        double newRe = curRe * wRe - curIm * wIm;
                        curIm = curRe * wIm + curIm * wRe;
                        curRe = newRe;
                    }
                }
            }
        }

        private float AverageBands(int from, int to)
        {
            float sum = 0;
            for (int i = from; i < to; i++) sum += SpectrumData[i];
            return sum / (to - from);
        }

        private static void Swap(ref double a, ref double b) { double t = a; a = b; b = t; }
    }
}