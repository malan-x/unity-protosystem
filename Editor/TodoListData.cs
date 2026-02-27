using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// TODO List data stored as ScriptableObject in project root
    /// </summary>
    public class TodoListData : ScriptableObject
    {
        private const string AssetPath = "Assets/TodoList.asset";

        public List<TodoTask> tasks = new List<TodoTask>();

        public int GetActiveCount()
        {
            int count = 0;
            foreach (var t in tasks)
                if (!t.done) count++;
            return count;
        }

        public static TodoListData GetOrCreate()
        {
            var data = AssetDatabase.LoadAssetAtPath<TodoListData>(AssetPath);

            if (data == null)
            {
                // Safety check: if file exists on disk but failed to load,
                // try reimporting before overwriting
                if (System.IO.File.Exists(AssetPath))
                {
                    AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
                    data = AssetDatabase.LoadAssetAtPath<TodoListData>(AssetPath);
                }

                // Only create new asset if file doesn't exist at all
                if (data == null && !System.IO.File.Exists(AssetPath))
                {
                    data = CreateInstance<TodoListData>();
                    AssetDatabase.CreateAsset(data, AssetPath);
                    AssetDatabase.SaveAssets();
                }
            }

            return data;
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }
    }

    [Serializable]
    public class TodoTask
    {
        public string text;
        public int category;
        public int priority;
        public bool done;
        public string created;
        public List<TodoTask> subtasks = new List<TodoTask>();

        public int SubtasksDone()
        {
            int c = 0;
            foreach (var s in subtasks)
                if (s.done) c++;
            return c;
        }
    }
}
