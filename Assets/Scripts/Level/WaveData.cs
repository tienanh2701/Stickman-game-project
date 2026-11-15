using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickmanBeatEmUp.Level
{
    [Serializable]
    public class EnemySpawnEntry
    {
        public GameObject enemyPrefab;
        public Vector2 spawnOffset;
        public float spawnDelay = 0f;
        [Tooltip("Nếu > 0 sẽ ghi đè MaxHP mặc định của Enemy (ví dụ Elite 35 HP trong Stage 4).")]
        public float maxHealthOverride = 0f;
    }

    [Serializable]
    public class WaveData
    {
        public string waveName = "Wave 1";
        public List<EnemySpawnEntry> enemies = new List<EnemySpawnEntry>();
        public float delayBeforeWave = 1.0f;
    }
}
