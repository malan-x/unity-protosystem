// Packages/com.protosystem.core/Runtime/Initialization/InlineConfigAttribute.cs
using System;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Помечает поле конфига для inline-редактирования в инспекторе системы.
    /// Конфиг будет отображаться развёрнутым прямо в инспекторе системы.
    /// </summary>
    /// <example>
    /// <code>
    /// [SerializeField, InlineConfig]
    /// private MySystemConfig config;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class InlineConfigAttribute : PropertyAttribute
    {
        /// <summary>
        /// Если true — конфиг можно свернуть/развернуть.
        /// Если false — всегда развёрнут.
        /// </summary>
        public bool Foldout { get; }
        
        /// <summary>
        /// Создаёт атрибут для inline-редактирования конфига.
        /// </summary>
        /// <param name="foldout">Можно ли сворачивать (по умолчанию true)</param>
        public InlineConfigAttribute(bool foldout = true)
        {
            Foldout = foldout;
        }
    }
}
