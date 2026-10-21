using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Совместимость запроса с семейством модели. Родилось из реального фейла: выбрали
    /// flux1-schnell в выпадающем списке, а стиль-пресет продолжил слать SDXL-параметры —
    /// 30 шагов и cfg 7 превратили 15-секундную генерацию в двухминутную без выигрыша в качестве.
    ///
    /// Почему именно эти правки:
    /// — Flux-schnell — дистиллят под 4 шага; после ~8 качество не растёт, время — линейно.
    /// — cfg &gt; 1 заставляет сэмплер гонять модель дважды на шаг (positive + negative);
    ///   schnell рассчитан на cfg 1 без негатива.
    /// — SDXL-LoRA на архитектуру Flux (DiT) не ложится вовсе: на картинку не влияет,
    ///   время на загрузку/патчинг съедает.
    /// </summary>
    public static class ModelCompat
    {
        private const int MaxFluxSteps = 8;
        private const float MaxFluxCfg = 1.5f;

        public static bool IsFlux(string checkpointFile)
            => !string.IsNullOrEmpty(checkpointFile) &&
               checkpointFile.ToLowerInvariant().Contains("flux");

        /// <summary>Понадобится ли Adjust: SDXL-параметры/LoRA/IPAdapter при Flux-чекпоинте.</summary>
        public static bool NeedsAdjustment(ComfyUIClient.IconRequest r)
            => IsFlux(r.EffCheckpoint) &&
               (r.Steps > MaxFluxSteps || r.Cfg > MaxFluxCfg ||
                !string.IsNullOrEmpty(r.EffLora) ||
                r.HasStyleRef ||
                r.Sampler != "euler" || r.Scheduler != "simple");

        /// <summary>
        /// Подогнать запрос под Flux: шаги/cfg/сэмплер по канону schnell, LoRA отключить.
        /// Правится сам запрос ДО отправки — рецепт варианта запишет реальные параметры.
        /// Не-Flux запросы возвращаются нетронутыми.
        /// </summary>
        public static ComfyUIClient.IconRequest Adjust(ComfyUIClient.IconRequest r)
        {
            if (!IsFlux(r.EffCheckpoint)) return r;

            string checkpoint = r.EffCheckpoint;   // до включения оверрайда, пока EffCheckpoint ещё глобальный
            if (r.Steps > MaxFluxSteps) r.Steps = 4;
            if (r.Cfg > MaxFluxCfg) r.Cfg = 1f;
            r.Sampler = "euler";
            r.Scheduler = "simple";

            // LoRA глушится через оверрайд модели: чекпоинт остаётся тем же, LoRA — явно пустая
            r.HasModelOverride = true;
            r.CheckpointOverride = checkpoint;
            r.LoraOverride = "";
            r.LoraStrengthOverride = 0f;

            // IPAdapter (SDXL) на Flux не ложится — референсы стиля отключаются
            r.StyleRefFiles = null;
            return r;
        }
    }
}
