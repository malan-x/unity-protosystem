using UnityEngine;

namespace ProtoSystem.Effects
{
    /// <summary>
    /// Данные для спавна эффекта. Генерируются объектом-источником при публикации события.
    /// </summary>
    public struct EffectSpawnData
    {
        /// <summary>Мировая позиция для эффекта</summary>
        public Vector3 WorldPosition;
        
        /// <summary>Мировой поворот для эффекта</summary>
        public Quaternion WorldRotation;
        
        /// <summary>Масштаб эффекта</summary>
        public Vector3 Scale;
        
        /// <summary>Transform для привязки (если LocalSpace)</summary>
        public Transform AttachTarget;
        
        /// <summary>Имя кости для привязки (опционально)</summary>
        public string AttachBoneName;
        
        /// <summary>Локальное смещение относительно точки привязки</summary>
        public Vector3 LocalOffset;
        
        /// <summary>Цвет/оттенок эффекта (опционально)</summary>
        public Color? TintColor;
        
        /// <summary>Переопределение времени жизни (опционально)</summary>
        public float? LifetimeOverride;
        
        /// <summary>Переопределение масштаба (опционально)</summary>
        public float? ScaleMultiplier;
        
        /// <summary>Дополнительные данные (для специфических эффектов)</summary>
        public object CustomData;

        /// <summary>
        /// Создаёт данные для WorldSpace эффекта
        /// </summary>
        public static EffectSpawnData AtPosition(Vector3 position)
        {
            return new EffectSpawnData
            {
                WorldPosition = position,
                WorldRotation = Quaternion.identity,
                Scale = Vector3.one
            };
        }

        /// <summary>
        /// Создаёт данные для LocalSpace эффекта, привязанного к Transform
        /// </summary>
        public static EffectSpawnData AttachedTo(Transform target, Vector3 localOffset = default)
        {
            return new EffectSpawnData
            {
                WorldPosition = target.position,
                WorldRotation = target.rotation,
                Scale = Vector3.one,
                AttachTarget = target,
                LocalOffset = localOffset
            };
        }

        /// <summary>
        /// Создаёт данные для LocalSpace эффекта, привязанного к кости
        /// </summary>
        public static EffectSpawnData AttachedToBone(Transform root, string boneName, Vector3 localOffset = default)
        {
            return new EffectSpawnData
            {
                WorldPosition = root.position,
                WorldRotation = root.rotation,
                Scale = Vector3.one,
                AttachTarget = root,
                AttachBoneName = boneName,
                LocalOffset = localOffset
            };
        }
    }

    /// <summary>
    /// Интерфейс для объектов, которые могут быть источником/целью эффектов.
    /// Объект НЕ знает о системе эффектов — он только предоставляет данные о себе.
    /// 
    /// ИСПОЛЬЗОВАНИЕ:
    /// Просто добавьте интерфейс к MonoBehaviour — дефолтные реализации сделают всё автоматически:
    /// 
    ///   public class MyClass : MonoBehaviour, IEffectTarget { }
    /// 
    /// Если нужна кастомизация — переопределите методы.
    /// </summary>
    public interface IEffectTarget
    {
        /// <summary>
        /// Генерирует данные для спавна эффекта на этом объекте.
        /// Дефолтная реализация использует transform объекта.
        /// </summary>
        EffectSpawnData GetEffectSpawnData()
        {
            var comp = this as Component;
            if (comp == null)
                return EffectSpawnData.AtPosition(Vector3.zero);

            return new EffectSpawnData
            {
                WorldPosition = comp.transform.position,
                WorldRotation = comp.transform.rotation,
                Scale = Vector3.one,
                AttachTarget = comp.transform
            };
        }
        
        /// <summary>
        /// Генерирует данные для спавна эффекта на указанной точке привязки.
        /// Дефолтная реализация ищет кость по имени или возвращает базовые данные.
        /// </summary>
        EffectSpawnData GetEffectSpawnData(string attachPoint)
        {
            if (string.IsNullOrEmpty(attachPoint))
                return GetEffectSpawnData();

            var comp = this as Component;
            if (comp == null)
                return GetEffectSpawnData();

            // Пробуем найти кость по имени
            var bone = FindBoneInHierarchy(comp.transform, attachPoint);
            if (bone != null)
            {
                return new EffectSpawnData
                {
                    WorldPosition = bone.position,
                    WorldRotation = bone.rotation,
                    Scale = Vector3.one,
                    AttachTarget = bone,
                    AttachBoneName = attachPoint
                };
            }

            return GetEffectSpawnData();
        }

        /// <summary>
        /// Вспомогательный метод для поиска кости в иерархии
        /// </summary>
        private static Transform FindBoneInHierarchy(Transform root, string boneName)
        {
            if (root.name == boneName) return root;
            
            foreach (Transform child in root)
            {
                var found = FindBoneInHierarchy(child, boneName);
                if (found != null) return found;
            }
            
            return null;
        }
    }

    /// <summary>
    /// Расширение для объектов с несколькими точками привязки эффектов.
    /// Добавляется на объекты с аниматором/скелетом для указания конкретных костей в инспекторе.
    /// </summary>
    public interface IEffectTargetMultiPoint : IEffectTarget
    {
        /// <summary>
        /// Возвращает список доступных точек привязки (для отображения в редакторе)
        /// </summary>
        string[] GetAvailableAttachPoints() => new[] { "default" };
        
        /// <summary>
        /// Точка привязки по умолчанию
        /// </summary>
        string DefaultAttachPoint => "default";
    }

    /// <summary>
    /// Extension методы для упрощения работы с IEffectTarget.
    /// Позволяют добавить минимальную реализацию интерфейса без переопределения методов.
    /// </summary>
    public static class IEffectTargetExtensions
    {
        /// <summary>
        /// Получает Transform объекта-цели эффекта
        /// </summary>
        public static Transform GetTransform(this IEffectTarget target)
        {
            if (target == null) return null;
            
            // IEffectTarget обычно реализуется MonoBehaviour
            var comp = target as Component;
            if (comp != null)
                return comp.transform;
            
            return null;
        }

        /// <summary>
        /// Проверяет, является ли объект валидной целью для эффектов
        /// </summary>
        public static bool IsValidTarget(this IEffectTarget target)
        {
            if (target == null) return false;
            
            var transform = target.GetTransform();
            return transform != null;
        }

        /// <summary>
        /// Создаёт базовые данные для спавна на основе Transform объекта.
        /// Используется как fallback если объект не переопределил GetEffectSpawnData().
        /// </summary>
        public static EffectSpawnData CreateDefaultSpawnData(this IEffectTarget target)
        {
            var transform = target.GetTransform();
            if (transform == null)
                return EffectSpawnData.AtPosition(Vector3.zero);

            return new EffectSpawnData
            {
                WorldPosition = transform.position,
                WorldRotation = transform.rotation,
                Scale = Vector3.one,
                AttachTarget = transform
            };
        }

        /// <summary>
        /// Пытается найти кость по имени в иерархии объекта
        /// </summary>
        public static Transform FindBone(this IEffectTarget target, string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;
            
            var root = target.GetTransform();
            if (root == null) return null;

            return FindBoneRecursive(root, boneName);
        }

        private static Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name == boneName) return parent;
            
            foreach (Transform child in parent)
            {
                var found = FindBoneRecursive(child, boneName);
                if (found != null) return found;
            }
            
            return null;
        }

        /// <summary>
        /// Пытается получить Animator из объекта
        /// </summary>
        public static Animator GetAnimator(this IEffectTarget target)
        {
            var transform = target.GetTransform();
            if (transform == null) return null;
            
            return transform.GetComponentInChildren<Animator>();
        }
    }
}
