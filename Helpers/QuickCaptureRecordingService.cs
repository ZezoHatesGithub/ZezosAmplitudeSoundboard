using AmplitudeSoundboard;
using System;
using System.IO;
using System.Threading.Tasks;
using Amplitude.Models;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace Amplitude.Helpers
{
    public class QuickCaptureRecordingService
    {
        private WaveInEvent? micIn;
        private WasapiLoopbackCapture? loopback;
        private WaveFileWriter? writer;
        private string? activePath;

        private void EnsureWriter(string capturePath, WaveFormat waveFormat)
        {
            writer?.Dispose();
            writer = new WaveFileWriter(capturePath, waveFormat);
        }

        public bool IsRecording => writer != null;

        public Task<string?> StartRecordingAsync(SoundClip clip)
        {
#if Windows
            Directory.CreateDirectory(Path.Combine(App.APP_STORAGE, "QuickCaptures"));
            activePath = Path.Combine(App.APP_STORAGE, "QuickCaptures", $"{clip.Id}.wav");

            if (clip.QuickCaptureRecordingSource == "Microphone")
            {
                micIn = new WaveInEvent { WaveFormat = new WaveFormat(48000, 16, 2) };
                EnsureWriter(activePath, micIn.WaveFormat);
                micIn.DataAvailable += (_, e) => writer?.Write(e.Buffer, 0, e.BytesRecorded);
                micIn.StartRecording();
            }
            else if (clip.QuickCaptureRecordingSource == "Desktop")
            {
                loopback = new WasapiLoopbackCapture();
                EnsureWriter(activePath, loopback.WaveFormat);
                loopback.DataAvailable += (_, e) => writer?.Write(e.Buffer, 0, e.BytesRecorded);
                loopback.StartRecording();
            }
#endif
            return Task.FromResult(activePath);
        }

        public Task StopRecordingAsync()
        {
            micIn?.StopRecording(); micIn?.Dispose(); micIn = null;
            loopback?.StopRecording(); loopback?.Dispose(); loopback = null;
            writer?.Dispose(); writer = null;
            return Task.CompletedTask;
        }
    }
}
