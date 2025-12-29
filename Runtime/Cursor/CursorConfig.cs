// Packages/com.protosystem.core/Runtime/Cursor/CursorConfig.cs
using System;
using UnityEngine;

namespace ProtoSystem.Cursor
{
    /// <summary>
    /// Конфигурация системы курсора
    /// </summary>
    [CreateAssetMenu(fileName = "CursorConfig", menuName = "ProtoSystem/Cursor/Config")]
    public class CursorConfig : ScriptableObject
    {
        [Header("Default State")]
        [Tooltip("Режим по умолчанию")]
        public CursorMode defaultMode = CursorMode.Free;
        
        [Tooltip("Виден ли курсор по умолчанию")]
        public bool defaultVisible = true;

        [Header("Auto Management")]
        [Tooltip("Автоматически управлять курсором при открытии UI")]
        public bool autoManageForUI = true;

        [Header("Custom Cursors")]
        [Tooltip("Кастомные курсоры")]
        public CursorData[] customCursors;

        public static CursorConfig CreateDefault()
        {
            return CreateInstance<CursorConfig>();
        }
    }

    /// <summary>
    /// Данные кастомного курсора
    /// </summary>
    [Serializable]
    public class CursorData
    {
        [Tooltip("Уникальный ID курсора")]
        public string id;
        
        [Tooltip("Текстура курсора")]
        public Texture2D texture;
        
        [Tooltip("Точка клика")]
        public Vector2 hotspot;
        
        [Tooltip("Описание")]
        public string description;
    }
}
