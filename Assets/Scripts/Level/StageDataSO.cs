using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Level
{
    /// <summary>
    /// ScriptableObject định nghĩa cấu hình từng màn chơi (Stage):
    /// - World 1: Thành phố nát (Attack on Titan)
    /// - World 2: Hoang mạc (Dragon Ball)
    /// - World 3: Neo City (Diend World)
    /// Mỗi World có 4 màn, màn 4 là Màn Boss
    /// </summary>
    [CreateAssetMenu(fileName = "NewStageData", menuName = "Stickman BeatEmUp/Stage Data")]
    public class StageDataSO : ScriptableObject
    {
        [Header("Stage Identity")]
        public WorldType worldType = WorldType.World1_RuinedCity;
        [Range(1, 4)] public int stageNumber = 1;
        public string stageName = "Ruined City - Stage 1";

        [Header("Beat 'em up Arena Lock Coordinates")]
        [Tooltip("Tọa độ X khi Player bước qua sẽ kích hoạt Khóa Camera và Spawn quái")]
        public float lockTriggerX = 10f;
        [Tooltip("Giới hạn Camera bên trái khi bị khóa")]
        public float lockedCameraMinX = 5f;
        [Tooltip("Giới hạn Camera bên phải khi bị khóa")]
        public float lockedCameraMaxX = 15f;

        [Header("Wave Progression")]
        public List<WaveData> waves = new List<WaveData>();

        [Header("Boss Fight (Chỉ dùng cho Màn 4)")]
        public bool isBossStage = false;
        public GameObject bossPrefab;
        public Vector2 bossSpawnPosition = new Vector2(14f, 0f);
    }
}
