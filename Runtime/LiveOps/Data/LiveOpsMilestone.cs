// Packages/com.protosystem.core/Runtime/LiveOps/Data/LiveOpsMilestone.cs
using System;
using UnityEngine;

namespace ProtoSystem.LiveOps
{
    /// <summary>Milestone — прогресс-бар к цели (вишлисты, продажи и т.п.).</summary>
    [Serializable]
    public class LiveOpsMilestoneData
    {
        public LocalizedString title = new();
        public LocalizedString description = new();
        public int current;
        public int goal;
        public LocalizedString unit = new();
        public string updatedAt;

        public float Progress => goal > 0 ? Mathf.Clamp01((float)current / goal) : 0f;
    }
}
