using Amplitude.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Amplitude.Helpers
{
    public class QuickCaptureManager
    {
        private readonly SoundClipManager soundClipManager;
        private readonly ConfigManager configManager;
        private readonly QuickCaptureRecordingService recorder;

        private readonly Dictionary<string, DateTime> pressedAt = [];
        private readonly Dictionary<string, bool> recording = [];
        private readonly Dictionary<string, CancellationTokenSource> startRecordingCancellation = [];
        private readonly HashSet<string> suppressPlaybackOnRelease = [];

        public QuickCaptureManager(SoundClipManager soundClipManager, ConfigManager configManager, QuickCaptureRecordingService recorder)
        {
            this.soundClipManager = soundClipManager;
            this.configManager = configManager;
            this.recorder = recorder;
        }

        public async Task HandleKeyDown(string hotkey)
        {
            var clips = soundClipManager.SoundClips;
            foreach (var clip in clips.Values)
            {
                if (!clip.IsQuickCaptureEnabled || clip.Hotkey != hotkey) continue;
                pressedAt[clip.Id] = DateTime.UtcNow;
                recording[clip.Id] = false;
                suppressPlaybackOnRelease.Add(clip.Id);

                if (startRecordingCancellation.TryGetValue(clip.Id, out var existingCts))
                {
                    existingCts.Cancel();
                    existingCts.Dispose();
                }

                var cts = new CancellationTokenSource();
                startRecordingCancellation[clip.Id] = cts;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(configManager.Config.QuickCaptureHoldThresholdMs, cts.Token);
                        if (!cts.IsCancellationRequested && pressedAt.ContainsKey(clip.Id) && !recording[clip.Id])
                        {
                            recording[clip.Id] = true;
                            await recorder.StartRecordingAsync(clip);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                });
            }
        }

        public async Task HandleKeyUp(string hotkey)
        {
            var clips = soundClipManager.SoundClips;
            foreach (var clip in clips.Values)
            {
                if (!clip.IsQuickCaptureEnabled || clip.Hotkey != hotkey) continue;
                if (startRecordingCancellation.TryGetValue(clip.Id, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    startRecordingCancellation.Remove(clip.Id);
                }

                if (recording.TryGetValue(clip.Id, out var isRec) && isRec)
                {
                    await recorder.StopRecordingAsync();
                    clip.SavedCapturePath = clip.AudioFilePath = System.IO.Path.Combine(AmplitudeSoundboard.App.APP_STORAGE, "QuickCaptures", $"{clip.Id}.wav");
                    soundClipManager.SaveClip(clip.ShallowCopy());
                    suppressPlaybackOnRelease.Remove(clip.Id);
                }
                else if (!suppressPlaybackOnRelease.Contains(clip.Id) && !string.IsNullOrEmpty(clip.AudioFilePath))
                {
                    clip.PlayAudio();
                }

                suppressPlaybackOnRelease.Remove(clip.Id);
                pressedAt.Remove(clip.Id);
                recording.Remove(clip.Id);
            }
        }
    }
}
