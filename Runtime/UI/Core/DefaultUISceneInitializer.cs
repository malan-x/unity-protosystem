// Packages/com.protosystem.core/Runtime/UI/Core/DefaultUISceneInitializer.cs
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Стандартный инициализатор UI сцены.
    /// Открывает указанные окна при старте.
    /// </summary>
    [AddComponentMenu("ProtoSystem/UI/Default Scene Initializer")]
    public class DefaultUISceneInitializer : UISceneInitializerBase
    {
        [Header("Debug")]
        [SerializeField] private bool logInitialization = true;

        public override void Initialize(UISystem uiSystem)
        {
            if (logInitialization)
                Debug.Log($"[DefaultUISceneInitializer] Initializing with {startupWindows.Count} startup windows");

            base.Initialize(uiSystem);
        }

        public override IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
        {
            // Базовая реализация возвращает переходы из Inspector
            return base.GetAdditionalTransitions();
        }
    }
}
