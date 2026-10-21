using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace ProtoSystem.SpriteGen.Editor
{
    public static class AnimatorControllerBuilder
    {
        public static AnimatorController Build(SpriteGenConfig config, string savePath)
        {
            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir))
            {
                string absDir = Path.GetFullPath(dir);
                if (!Directory.Exists(absDir))
                    Directory.CreateDirectory(absDir);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(savePath);
            var rootStateMachine = controller.layers[0].stateMachine;

            AnimatorState defaultState = null;

            for (int i = 0; i < config.animations.Count; i++)
            {
                var entry = config.animations[i];
                if (entry == null || entry.clip == null) continue;

                var state = rootStateMachine.AddState(entry.name);
                state.motion = entry.clip;

                // Настройка loop через AnimationClipSettings
                var clipSettings = AnimationUtility.GetAnimationClipSettings(entry.clip);
                clipSettings.loopTime = entry.loop;
                AnimationUtility.SetAnimationClipSettings(entry.clip, clipSettings);

                // Первая анимация — дефолтная (или idle если найдём)
                if (defaultState == null)
                    defaultState = state;

                string lowerName = entry.name.ToLower();
                if (lowerName == "idle")
                    defaultState = state;
            }

            if (defaultState != null)
                rootStateMachine.defaultState = defaultState;

            // Добавить параметр для переключения анимаций
            controller.AddParameter("AnimationIndex", AnimatorControllerParameterType.Int);

            // Переходы из Any State по индексу
            for (int i = 0; i < rootStateMachine.states.Length; i++)
            {
                var state = rootStateMachine.states[i].state;
                var transition = rootStateMachine.AddAnyStateTransition(state);
                transition.AddCondition(AnimatorConditionMode.Equals, i, "AnimationIndex");
                transition.hasExitTime = false;
                transition.duration = 0.15f;
                transition.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }

        public static RuntimeAnimatorController BuildForPreview(SpriteGenConfig config)
        {
            string folder = "Assets/SpriteGen/Generated";
            string modelName = config.modelPrefab != null ? config.modelPrefab.name : "Model";
            string path = Path.Combine(folder, $"{modelName}_Preview.controller");

            return Build(config, path);
        }
    }
}
