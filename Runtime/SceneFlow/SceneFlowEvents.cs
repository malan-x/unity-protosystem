// Packages/com.protosystem.core/Runtime/SceneFlow/SceneFlowEvents.cs
namespace ProtoSystem
{
    /// <summary>
    /// События системы загрузки сцен
    /// </summary>
    public static partial class EventBus
    {
        public static partial class SceneFlow
        {
            /// <summary>Загрузка началась. Data: SceneLoadEventData</summary>
            public const int LoadStarted = 10300;
            /// <summary>Прогресс загрузки. Data: SceneLoadEventData</summary>
            public const int LoadProgress = 10301;
            /// <summary>Загрузка завершена. Data: SceneLoadEventData</summary>
            public const int LoadCompleted = 10302;
            /// <summary>Ошибка загрузки. Data: SceneLoadEventData</summary>
            public const int LoadFailed = 10303;
            /// <summary>Выгрузка сцены. Data: SceneUnloadEventData</summary>
            public const int UnloadCompleted = 10304;
            /// <summary>Переход начался. Data: TransitionEventData</summary>
            public const int TransitionStarted = 10310;
            /// <summary>Переход завершён. Data: TransitionEventData</summary>
            public const int TransitionCompleted = 10311;
        }
    }
}

namespace ProtoSystem.SceneFlow
{
    /// <summary>
    /// Данные события загрузки сцены
    /// </summary>
    public struct SceneLoadEventData
    {
        public string SceneName;
        public float Progress;
        public bool Success;
        public string ErrorMessage;
    }

    /// <summary>
    /// Данные события выгрузки сцены
    /// </summary>
    public struct SceneUnloadEventData
    {
        public string SceneName;
    }

    /// <summary>
    /// Данные события перехода
    /// </summary>
    public struct TransitionEventData
    {
        public TransitionType Type;
        public float Duration;
    }
}
