using System.IO;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Editor.Sound
{
    /// <summary>
    /// Процедурный генератор базовых UI звуков
    /// </summary>
    public static class ProceduralSoundGenerator
    {
        private const int SAMPLE_RATE = 44100;
        
        /// <summary>
        /// Генерировать все базовые UI звуки
        /// </summary>
        /// <summary>
        /// Генерировать все базовые UI звуки
        /// </summary>
        public static void GenerateAllUISounds(string outputFolder)
        {
            EnsureFolder(outputFolder);

            // Window sounds
            GenerateWhoosh($"{outputFolder}/ui_whoosh.wav");
            GenerateClose($"{outputFolder}/ui_close.wav");
            GenerateModalOpen($"{outputFolder}/ui_modal_open.wav");
            GenerateModalClose($"{outputFolder}/ui_modal_close.wav");

            // Button sounds
            GenerateClick($"{outputFolder}/ui_click.wav");
            GenerateHover($"{outputFolder}/ui_hover.wav");
            GenerateDisabled($"{outputFolder}/ui_disabled.wav");

            // Navigation
            GenerateNavigate($"{outputFolder}/ui_navigate.wav");
            GenerateBack($"{outputFolder}/ui_back.wav");
            GenerateTab($"{outputFolder}/ui_tab.wav");

            // Feedback
            GenerateSuccess($"{outputFolder}/ui_success.wav");
            GenerateError($"{outputFolder}/ui_error.wav");
            GenerateWarning($"{outputFolder}/ui_warning.wav");
            GenerateNotification($"{outputFolder}/ui_notification.wav");

            // Controls
            GenerateSlider($"{outputFolder}/ui_slider.wav");
            GenerateToggleOn($"{outputFolder}/ui_toggle_on.wav");
            GenerateToggleOff($"{outputFolder}/ui_toggle_off.wav");
            GenerateDropdown($"{outputFolder}/ui_dropdown.wav");
            GenerateSelect($"{outputFolder}/ui_select.wav");

            AssetDatabase.Refresh();
            Debug.Log($"✅ Generated 19 UI sounds in '{outputFolder}'");
        }
        
        // ===== WINDOW SOUNDS =====
        
        private static void GenerateWhoosh(string path)
        {
            // Soft whoosh: filtered noise sweep, 80ms
            int length = (int)(SAMPLE_RATE * 0.08f);
            var samples = new float[length];
            
            System.Random rng = new System.Random(42);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float env = Mathf.Sin(t * Mathf.PI); // smooth in/out
                float noise = (float)(rng.NextDouble() * 2 - 1);
                // Low-pass effect by averaging
                float freq = Mathf.Lerp(200f, 800f, t);
                float tone = Mathf.Sin(2 * Mathf.PI * freq * (float)i / SAMPLE_RATE);
                samples[i] = (noise * 0.3f + tone * 0.4f) * env * 0.5f;
            }
            
            SaveWav(path, samples);
        }
        
        private static void GenerateClose(string path)
        {
            // Soft close: descending 600->200Hz, 60ms
            var samples = GenerateSweep(600f, 200f, 0.06f, volume: 0.4f, envelope: EnvelopeType.Soft);
            SaveWav(path, samples);
        }
        
        private static void GenerateModalOpen(string path)
        {
            // Modal open: ascending with reverb-like tail, 120ms
            int length = (int)(SAMPLE_RATE * 0.12f);
            var samples = new float[length];
            
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = (float)i / length;
                float freq = Mathf.Lerp(400f, 900f, progress * 0.5f); // quick rise then sustain
                float env = Mathf.Exp(-progress * 3f) * Mathf.Clamp01(progress * 10f);
                float fundamental = Mathf.Sin(2 * Mathf.PI * freq * t);
                float harmonic = Mathf.Sin(2 * Mathf.PI * freq * 2f * t) * 0.2f;
                samples[i] = (fundamental + harmonic) * 0.5f * env;
            }
            
            SaveWav(path, samples);
        }
        
        private static void GenerateModalClose(string path)
        {
            // Modal close: descending with quick fade, 100ms
            int length = (int)(SAMPLE_RATE * 0.1f);
            var samples = new float[length];
            
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = (float)i / length;
                float freq = Mathf.Lerp(800f, 300f, progress);
                float env = (1f - progress) * Mathf.Clamp01(progress * 15f);
                float fundamental = Mathf.Sin(2 * Mathf.PI * freq * t);
                samples[i] = fundamental * 0.45f * env;
            }
            
            SaveWav(path, samples);
        }
        
        // ===== BUTTON SOUNDS =====
        
        private static void GenerateClick(string path)
        {
            // Sharp click: 800Hz, 40ms
            var samples = GenerateTone(800f, 0.04f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        private static void GenerateHover(string path)
        {
            // Soft hover: 600Hz, 25ms, gentle
            var samples = GenerateTone(600f, 0.025f, volume: 0.4f, envelope: EnvelopeType.Soft);
            SaveWav(path, samples);
        }
        
        private static void GenerateDisabled(string path)
        {
            // Muted thud: 200Hz, 30ms
            var samples = GenerateTone(200f, 0.03f, volume: 0.3f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        // ===== NAVIGATION =====
        
        private static void GenerateNavigate(string path)
        {
            // Quick blip: 700Hz, 30ms
            var samples = GenerateTone(700f, 0.03f, volume: 0.5f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        private static void GenerateBack(string path)
        {
            // Descending: 800->400Hz, 60ms
            var samples = GenerateSweep(800f, 400f, 0.06f, envelope: EnvelopeType.Soft);
            SaveWav(path, samples);
        }
        
        private static void GenerateTab(string path)
        {
            // Tab switch: 900Hz, 20ms
            var samples = GenerateTone(900f, 0.02f, volume: 0.4f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        // ===== FEEDBACK =====
        
        private static void GenerateSuccess(string path)
        {
            // Ascending ding: 600->1200Hz, 100ms
            var samples = GenerateSweep(600f, 1200f, 0.1f, envelope: EnvelopeType.Bell);
            SaveWav(path, samples);
        }
        
        private static void GenerateError(string path)
        {
            // Harsh buzz: two dissonant tones
            int length = (int)(SAMPLE_RATE * 0.15f);
            var samples = new float[length];
            
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = GetEnvelope(i, length, EnvelopeType.Soft);
                float tone1 = Mathf.Sin(2 * Mathf.PI * 280f * t);
                float tone2 = Mathf.Sin(2 * Mathf.PI * 340f * t);
                samples[i] = (tone1 + tone2) * 0.35f * env;
            }
            
            SaveWav(path, samples);
        }
        
        private static void GenerateWarning(string path)
        {
            // Double beep: 500Hz, two pulses
            int pulseLength = (int)(SAMPLE_RATE * 0.05f);
            int gapLength = (int)(SAMPLE_RATE * 0.03f);
            var samples = new float[pulseLength * 2 + gapLength];
            
            for (int i = 0; i < pulseLength; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = GetEnvelope(i, pulseLength, EnvelopeType.Click);
                samples[i] = Mathf.Sin(2 * Mathf.PI * 500f * t) * 0.5f * env;
                samples[i + pulseLength + gapLength] = Mathf.Sin(2 * Mathf.PI * 500f * t) * 0.5f * env;
            }
            
            SaveWav(path, samples);
        }
        
        private static void GenerateNotification(string path)
        {
            // Pleasant ding: 1000Hz with harmonics, 200ms decay
            int length = (int)(SAMPLE_RATE * 0.2f);
            var samples = new float[length];
            
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = GetEnvelope(i, length, EnvelopeType.Bell);
                float fundamental = Mathf.Sin(2 * Mathf.PI * 1000f * t);
                float harmonic = Mathf.Sin(2 * Mathf.PI * 2000f * t) * 0.3f;
                samples[i] = (fundamental + harmonic) * 0.4f * env;
            }
            
            SaveWav(path, samples);
        }
        
        // ===== CONTROLS =====
        
        private static void GenerateSlider(string path)
        {
            // Tiny tick: 1200Hz, 10ms
            var samples = GenerateTone(1200f, 0.01f, volume: 0.25f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        private static void GenerateToggleOn(string path)
        {
            // Ascending: 600->900Hz, 40ms
            var samples = GenerateSweep(600f, 900f, 0.04f, volume: 0.5f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        private static void GenerateToggleOff(string path)
        {
            // Descending: 900->600Hz, 40ms
            var samples = GenerateSweep(900f, 600f, 0.04f, volume: 0.5f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        private static void GenerateDropdown(string path)
        {
            // Pop: 500->700Hz, 30ms
            var samples = GenerateSweep(500f, 700f, 0.03f, volume: 0.5f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        private static void GenerateSelect(string path)
        {
            // Confirm blip: 800Hz, 25ms
            var samples = GenerateTone(800f, 0.025f, volume: 0.45f, envelope: EnvelopeType.Click);
            SaveWav(path, samples);
        }
        
        // ===== CORE GENERATION =====
        
        private enum EnvelopeType { Click, Soft, Bell }
        
        private static float[] GenerateTone(float frequency, float duration, float volume = 0.5f, EnvelopeType envelope = EnvelopeType.Click)
        {
            int length = (int)(SAMPLE_RATE * duration);
            var samples = new float[length];
            
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = GetEnvelope(i, length, envelope);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * volume * env;
            }
            
            return samples;
        }
        
        private static float[] GenerateSweep(float startFreq, float endFreq, float duration, float volume = 0.5f, EnvelopeType envelope = EnvelopeType.Click)
        {
            int length = (int)(SAMPLE_RATE * duration);
            var samples = new float[length];
            
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float progress = (float)i / length;
                float freq = Mathf.Lerp(startFreq, endFreq, progress);
                float env = GetEnvelope(i, length, envelope);
                samples[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * volume * env;
            }
            
            return samples;
        }
        
        private static float GetEnvelope(int sample, int length, EnvelopeType type)
        {
            float t = (float)sample / length;
            
            return type switch
            {
                EnvelopeType.Click => (1f - t) * Mathf.Clamp01((float)sample / (length * 0.1f)),
                EnvelopeType.Soft => Mathf.Sin(t * Mathf.PI),
                EnvelopeType.Bell => Mathf.Exp(-t * 5f),
                _ => 1f
            };
        }
        
        // ===== WAV EXPORT =====
        
        private static void SaveWav(string path, float[] samples)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            
            int byteRate = SAMPLE_RATE * 2; // 16-bit mono
            int dataSize = samples.Length * 2;
            
            // RIFF header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            
            // fmt chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // chunk size
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(SAMPLE_RATE);
            writer.Write(byteRate);
            writer.Write((short)2); // block align
            writer.Write((short)16); // bits per sample
            
            // data chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            
            foreach (float sample in samples)
            {
                short s = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767);
                writer.Write(s);
            }
        }
        
        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
