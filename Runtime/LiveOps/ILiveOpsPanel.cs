// Packages/com.protosystem.core/Runtime/LiveOps/ILiveOpsPanel.cs
namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Панель Community/LiveOps, которой LiveOpsSystem управляет напрямую
    /// (видимость по доступности сервера). Реализации:
    /// - CommunityPanelWindow (uGUI, MonoBehaviour) — SetActive на GameObject;
    /// - CommunityPanelToolkit (UI Toolkit, контроллер VisualElement) — display на корне.
    /// Данные панель получает через EventBus (Evt.LiveOps.DataUpdated).
    /// </summary>
    public interface ILiveOpsPanel
    {
        /// <summary>Показать/скрыть панель целиком (сервер недоступен → false).</summary>
        void SetPanelVisible(bool visible);
    }
}
