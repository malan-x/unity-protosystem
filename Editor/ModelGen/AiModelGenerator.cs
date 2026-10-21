using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>
    /// Очередь генерации 3D-моделей. Последовательная (VRAM), с Progress API.
    /// Пока очередь не пуста — LockReloadAssemblies: domain reload молча убивает
    /// статическую очередь (грабля аудио-студии).
    /// </summary>
    public static class AiModelGenerator
    {
        public class Job
        {
            public ModelGenRequest Request;
            public Action<ModelGenResult> OnDone;   // вызывается на главном потоке
            public int ProgressId = -1;
        }

        private static readonly Queue<Job> Queue = new();
        private static Job _current;
        private static Task<ModelGenResult> _task;
        private static CancellationTokenSource _cts;
        private static bool _locked;

        public static int Pending => Queue.Count + (_current != null ? 1 : 0);
        public static string CurrentLabel { get; private set; } = "";
        public static float CurrentProgress { get; private set; }
        public static event Action Changed;

        static AiModelGenerator() => EditorApplication.update += Tick;

        public static void Enqueue(ModelGenRequest req, Action<ModelGenResult> onDone)
        {
            Queue.Enqueue(new Job { Request = req, OnDone = onDone });
            if (!_locked) { EditorApplication.LockReloadAssemblies(); _locked = true; }
            Changed?.Invoke();
        }

        public static void CancelAll()
        {
            Queue.Clear();
            _cts?.Cancel();
            Changed?.Invoke();
        }

        private static void Tick()
        {
            if (_current == null)
            {
                if (Queue.Count == 0)
                {
                    if (_locked) { EditorApplication.UnlockReloadAssemblies(); _locked = false; }
                    return;
                }
                _current = Queue.Dequeue();
                var req = _current.Request;
                _current.ProgressId = Progress.Start("3D: " + req.EntityId, req.Engine.ToString(),
                    Progress.Options.Managed);
                _cts = new CancellationTokenSource();

                void Prog(float p, string label)
                {
                    CurrentProgress = p; CurrentLabel = label;
                }

                _task = req.Engine switch
                {
                    ModelEngine.Meshy => MeshyClient.GenerateAsync(req, ModelAiSettings.MeshyApiKey, Prog, _cts.Token),
                    _ => Hy3DComfyClient.GenerateAsync(req, ModelAiSettings.ComfyServer,
                            ModelAiSettings.ComfyOutputDir, Prog, _cts.Token),
                };
                Changed?.Invoke();
                return;
            }

            if (_current.ProgressId >= 0)
                Progress.Report(_current.ProgressId, CurrentProgress, CurrentLabel);

            if (_task == null || !_task.IsCompleted) return;

            var job = _current;
            var result = _task.Status == TaskStatus.RanToCompletion
                ? _task.Result
                : new ModelGenResult { Request = job.Request, Error = _task.Exception?.GetBaseException().Message ?? "Задача прервана." };
            _current = null; _task = null;
            _cts?.Dispose(); _cts = null;

            if (job.ProgressId >= 0)
            {
                if (result.Ok) Progress.Finish(job.ProgressId);
                else Progress.Finish(job.ProgressId, Progress.Status.Failed);
            }

            if (!result.Ok)
                Debug.LogError($"[ModelStudio] {job.Request.EntityId}: {result.Error}");

            try { job.OnDone?.Invoke(result); }
            catch (Exception e) { Debug.LogException(e); }
            Changed?.Invoke();
        }
    }
}
