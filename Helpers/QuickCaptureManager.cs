using Amplitude.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                _ = Task.Run(async () =>
                {
                    await Task.Delay(configManager.Config.QuickCaptureHoldThresholdMs);
                    if (pressedAt.ContainsKey(clip.Id) && !recording[clip.Id])
                    {
                        recording[clip.Id] = true;
                        await recorder.StartRecordingAsync(clip);
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
                if (recording.TryGetValue(clip.Id, out var isRec) && isRec)
                {
                    await recorder.StopRecordingAsync();
                    clip.SavedCapturePath = clip.AudioFilePath = System.IO.Path.Combine(AmplitudeSoundboard.App.APP_STORAGE, "QuickCaptures", $"{clip.Id}.wav");
                    soundClipManager.SaveClip(clip.ShallowCopy());
                }
                else if (!string.IsNullOrEmpty(clip.AudioFilePath))
                {
                    clip.PlayAudio();
                }
                pressedAt.Remove(clip.Id);
                recording.Remove(clip.Id);
            }
        }
    }
}
