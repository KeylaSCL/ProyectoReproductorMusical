using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MusicVisualizer.Audio
{
    /// <summary>
    /// Reproductor WAV/PCM via WinMM — versión v4, corrección de corrupción de memoria.
    ///
    /// BUG RAÍZ del crash a los 5 segundos:
    ///   El struct WAVEHDR debe estar fijo en memoria (GCHandle Pinned) ANTES de pasarlo
    ///   a waveOutPrepareHeader, y ese mismo GCHandle debe usarse para obtener el puntero
    ///   que se pasa al driver. Si se guarda el struct en un array y luego se pinnea
    ///   una copia, el driver escribe en memoria ya liberada → corrupción → crash.
    ///
    /// SOLUCIÓN: Allocar los WAVEHDR en memoria no administrada (Marshal.AllocHGlobal)
    ///   y escribirlos con Marshal.StructureToPtr. Así el puntero es estable para siempre.
    /// </summary>
    public class ReproductorAudio : IDisposable
    {
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX f, WaveDelegate cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int sz);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int sz);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int sz);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutPause(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutRestart(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutSetVolume(IntPtr h, uint v);

        delegate void WaveDelegate(IntPtr h, int msg, IntPtr inst, IntPtr hdr, IntPtr reserved);

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX {
            public short wFormatTag, nChannels;
            public int   nSamplesPerSec, nAvgBytesPerSec;
            public short nBlockAlign, wBitsPerSample, cbSize;
        }

        // WAVEHDR en layout secuencial — debe coincidir exactamente con winmm
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR {
            public IntPtr lpData;
            public int    dwBufferLength;
            public int    dwBytesRecorded;
            public IntPtr dwUser;
            public int    dwFlags;
            public int    dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        const int WOM_DONE          = 0x3BD;
        const int CALLBACK_FUNCTION = 0x30000;
        const int BUFFER_COUNT      = 3;

        // ── PCM ────────────────────────────────────────────────────────────────
        byte[]  _pcmData;
        int     _channels, _sampleRate, _bitsPerSample, _blockAlign;
        int     _pcmPos;
        long    _playedBytes;   // long para evitar overflow en canciones largas

        // ── Dispositivo ────────────────────────────────────────────────────────
        IntPtr       _hwo;
        bool         _playing, _paused, _disposed;
        int          _bufferBytes;
        WaveDelegate _callback;     // referencia viva para que el GC no la recoja
        float        _volume = 0.8f;

        // Memoria no administrada para los WAVEHDR (estable para siempre)
        IntPtr[]  _hdrPtrs  = new IntPtr[BUFFER_COUNT];
        // Memoria no administrada para los buffers de audio
        IntPtr[]  _bufPtrs  = new IntPtr[BUFFER_COUNT];
        int[]     _bufSizes = new int[BUFFER_COUNT];

        public AnalizadorAudio Analyzer { get; } = new AnalizadorAudio();
        public bool   HasFile   => _pcmData != null;
        public bool   IsPlaying => _playing && !_paused;
        public bool   IsPaused  => _paused;
        public bool   HasEnded  => _pcmData != null && !_playing && !_paused && _playedBytes >= _pcmData.Length;

        public double Duration => _pcmData == null ? 0
            : (double)_pcmData.Length / (_sampleRate * _blockAlign);

        public double Position => _pcmData == null ? 0
            : Math.Min(Duration, (double)_playedBytes / (_sampleRate * _blockAlign));

        public ReproductorAudio()
        {
            // Reservar los WAVEHDR en memoria no administrada una sola vez
            int hdrSz = Marshal.SizeOf(typeof(WAVEHDR));
            for (int i = 0; i < BUFFER_COUNT; i++)
                _hdrPtrs[i] = Marshal.AllocHGlobal(hdrSz);
        }

        // ── Carga ───────────────────────────────────────────────────────────────
        public void Load(string path)
        {
            Stop();
            ParseWav(File.ReadAllBytes(path));
        }

        void ParseWav(byte[] wav)
        {
            if (wav.Length < 44 || wav[0] != 'R')
                throw new Exception("No es WAV RIFF válido.");
            int p = 12;
            _pcmData = null;
            while (p + 8 <= wav.Length)
            {
                string id   = System.Text.Encoding.ASCII.GetString(wav, p, 4);
                int    size = BitConverter.ToInt32(wav, p + 4);
                p += 8;
                if (id == "fmt ")
                {
                    short fmt = BitConverter.ToInt16(wav, p);
                    if (fmt != 1)
                        throw new Exception("Solo WAV PCM. Convierte con: ffmpeg -i cancion.mp3 cancion.wav");
                    _channels      = BitConverter.ToInt16(wav, p + 2);
                    _sampleRate    = BitConverter.ToInt32(wav, p + 4);
                    _bitsPerSample = BitConverter.ToInt16(wav, p + 14);
                    _blockAlign    = _channels * (_bitsPerSample / 8);
                }
                else if (id == "data")
                {
                    int len = Math.Min(size, wav.Length - p);
                    _pcmData = new byte[len];
                    Array.Copy(wav, p, _pcmData, 0, len);
                    break;
                }
                if (size > 0) p += size; else break;
            }
            if (_pcmData == null)
                throw new Exception("No se encontró chunk 'data'.");
        }

        // ── Play / Pause / Stop ─────────────────────────────────────────────────
        public void Play()
        {
            if (_pcmData == null) return;
            if (_paused) { _paused = false; waveOutRestart(_hwo); return; }
            InternalStop();
            _pcmPos      = 0;
            _playedBytes = 0;
            StartPlayback();
        }

        public void Pause()
        {
            if (!_playing || _paused) return;
            _paused = true;
            waveOutPause(_hwo);
        }

        public void Stop()
        {
            InternalStop();
            _pcmPos      = 0;
            _playedBytes = 0;
        }

        public void SeekTo(double seconds)
        {
            if (_pcmData == null) return;
            bool wasPlaying = _playing && !_paused;
            bool wasPaused  = _paused;
            InternalStop();

            int target = (int)(seconds * _sampleRate * _blockAlign);
            target = (target / _blockAlign) * _blockAlign;          // alinear al bloque
            target = Math.Max(0, Math.Min(_pcmData.Length - _blockAlign, target));
            _pcmPos      = target;
            _playedBytes = target;

            if (wasPlaying || wasPaused)
            {
                StartPlayback();
                if (wasPaused) { _paused = true; waveOutPause(_hwo); }
            }
        }

        public void SetVolume(float v)
        {
            _volume = Math.Max(0, Math.Min(1, v));
            if (_hwo != IntPtr.Zero) {
                uint vol = (uint)(_volume * 0xFFFF);
                waveOutSetVolume(_hwo, vol | (vol << 16));
            }
        }

        // ── Internals ───────────────────────────────────────────────────────────
        void StartPlayback()
        {
            var wfx = new WAVEFORMATEX {
                wFormatTag      = 1,
                nChannels       = (short)_channels,
                nSamplesPerSec  = _sampleRate,
                wBitsPerSample  = (short)_bitsPerSample,
                nBlockAlign     = (short)_blockAlign,
                nAvgBytesPerSec = _sampleRate * _blockAlign
            };
            _callback    = WaveCallback;
            int ret = waveOutOpen(out _hwo, -1, ref wfx, _callback, IntPtr.Zero, CALLBACK_FUNCTION);
            if (ret != 0) throw new Exception($"waveOutOpen falló: {ret}");

            // Buffer de 250 ms
            _bufferBytes = _sampleRate * _blockAlign / 4;
            _playing = true;
            _paused  = false;

            SetVolume(_volume);
            for (int i = 0; i < BUFFER_COUNT; i++) QueueBuffer(i);
        }

        void InternalStop()
        {
            _playing = false;
            _paused  = false;
            if (_hwo == IntPtr.Zero) return;

            waveOutReset(_hwo);
            Thread.Sleep(80);   // esperar que el driver deje de tocar buffers

            // Desregistrar todos los headers
            int hdrSz = Marshal.SizeOf(typeof(WAVEHDR));
            for (int i = 0; i < BUFFER_COUNT; i++)
            {
                try { waveOutUnprepareHeader(_hwo, _hdrPtrs[i], hdrSz); } catch { }
                // Liberar buffer de audio no administrado
                if (_bufPtrs[i] != IntPtr.Zero) {
                    Marshal.FreeHGlobal(_bufPtrs[i]);
                    _bufPtrs[i]  = IntPtr.Zero;
                    _bufSizes[i] = 0;
                }
            }
            waveOutClose(_hwo);
            _hwo = IntPtr.Zero;
        }

        void QueueBuffer(int idx)
        {
            if (!_playing || _pcmData == null || _pcmPos >= _pcmData.Length) return;

            int bytes = Math.Min(_bufferBytes, _pcmData.Length - _pcmPos);
            if (bytes <= 0) return;

            // 1. Copiar PCM a buffer no administrado
            if (_bufPtrs[idx] != IntPtr.Zero && _bufSizes[idx] < bytes) {
                Marshal.FreeHGlobal(_bufPtrs[idx]);
                _bufPtrs[idx] = IntPtr.Zero;
            }
            if (_bufPtrs[idx] == IntPtr.Zero) {
                _bufPtrs[idx]  = Marshal.AllocHGlobal(bytes);
                _bufSizes[idx] = bytes;
            }
            Marshal.Copy(_pcmData, _pcmPos, _bufPtrs[idx], bytes);
            _pcmPos      += bytes;
            _playedBytes += bytes;

            // 2. Alimentar el analizador (copia a array administrado temporal)
            byte[] tmp = new byte[bytes];
            Marshal.Copy(_bufPtrs[idx], tmp, 0, bytes);
            FeedAnalyzer(tmp);

            // 3. Construir WAVEHDR en la memoria no administrada fija
            int hdrSz = Marshal.SizeOf(typeof(WAVEHDR));
            // Desregistrar el header anterior de este slot (si lo había)
            try { waveOutUnprepareHeader(_hwo, _hdrPtrs[idx], hdrSz); } catch { }

            // Limpiar el struct a cero y escribir los campos necesarios
            for (int b = 0; b < hdrSz; b++)
                Marshal.WriteByte(_hdrPtrs[idx], b, 0);
            Marshal.WriteIntPtr(_hdrPtrs[idx], 0,               _bufPtrs[idx]);   // lpData
            Marshal.WriteInt32 (_hdrPtrs[idx], IntPtr.Size,     bytes);            // dwBufferLength

            // 4. Registrar y enviar al driver
            waveOutPrepareHeader(_hwo, _hdrPtrs[idx], hdrSz);
            waveOutWrite        (_hwo, _hdrPtrs[idx], hdrSz);
        }

        // Callback de WinMM — ejecutado en hilo de kernel.
        // SOLO puede llamar a QueueBuffer (no toca el device handle ni llama Stop).
        void WaveCallback(IntPtr hwo, int msg, IntPtr inst, IntPtr hdrPtr, IntPtr reserved)
        {
            if (msg != WOM_DONE || !_playing || _paused) return;
            // Identificar qué slot terminó comparando el puntero del header
            for (int i = 0; i < BUFFER_COUNT; i++)
                if (_hdrPtrs[i] == hdrPtr) { QueueBuffer(i); return; }
        }

        void FeedAnalyzer(byte[] buf)
        {
            int bps    = _bitsPerSample / 8;
            int count  = buf.Length / (bps * _channels);
            var mono   = new float[count];
            for (int i = 0; i < count; i++) {
                float sum = 0;
                for (int ch = 0; ch < _channels; ch++) {
                    int off = (i * _channels + ch) * bps;
                    if      (_bitsPerSample == 16) sum += BitConverter.ToInt16(buf, off) / 32768f;
                    else if (_bitsPerSample == 8 ) sum += (buf[off] - 128) / 128f;
                    else if (_bitsPerSample == 32) sum += BitConverter.ToSingle(buf, off);
                }
                mono[i] = sum / _channels;
            }
            Analyzer.Process(mono, count);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            InternalStop();
            // Liberar memoria no administrada permanente
            int hdrSz = Marshal.SizeOf(typeof(WAVEHDR));
            for (int i = 0; i < BUFFER_COUNT; i++) {
                if (_hdrPtrs[i] != IntPtr.Zero) Marshal.FreeHGlobal(_hdrPtrs[i]);
                if (_bufPtrs[i] != IntPtr.Zero) Marshal.FreeHGlobal(_bufPtrs[i]);
            }
        }
    }
}
