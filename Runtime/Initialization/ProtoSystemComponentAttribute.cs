// Packages/com.protosystem.core/Runtime/Initialization/ProtoSystemComponentAttribute.cs
using System;

namespace ProtoSystem
{
    /// <summary>
    /// Помечает систему как компонент пакета ProtoSystem.
    /// Используется редактором для автоматического обнаружения и создания систем.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ProtoSystemComponentAttribute : Attribute
    {
        /// <summary>Отображаемое имя системы</summary>
        public string DisplayName { get; }
        
        /// <summary>Описание системы</summary>
        public string Description { get; }
        
        /// <summary>Категория (Core, UI, Network, Tools)</summary>
        public string Category { get; }
        
        /// <summary>Иконка (emoji)</summary>
        public string Icon { get; }
        
        /// <summary>Порядок в списке</summary>
        public int Order { get; }

        public ProtoSystemComponentAttribute(
            string displayName, 
            string description = "", 
            string category = "Core",
            string icon = "⚙️",
            int order = 100)
        {
            DisplayName = displayName;
            Description = description;
            Category = category;
            Icon = icon;
            Order = order;
        }
    }
}
