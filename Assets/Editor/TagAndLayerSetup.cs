using UnityEditor;
using UnityEngine;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Tự động thêm Tag 'Player' và các Layer 'Ground', 'Player', 'Enemy' vào TagManager.asset
    /// Giúp người dùng không cần mở Project Settings để cấu hình thủ công
    /// </summary>
    public static class TagAndLayerSetup
    {
        public static void EnsureTagsAndLayers()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // 1. Đảm bảo Tag 'Player' tồn tại
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            bool hasPlayerTag = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == "Player")
                {
                    hasPlayerTag = true;
                    break;
                }
            }
            if (!hasPlayerTag)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = "Player";
            }

            // 2. Đảm bảo các Layer 'Ground', 'Player', 'Enemy' tồn tại (từ index 6 trở lên)
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            EnsureLayer(layersProp, 6, "Ground");
            EnsureLayer(layersProp, 7, "Player");
            EnsureLayer(layersProp, 8, "Enemy");

            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureLayer(SerializedProperty layersProp, int targetIndex, string layerName)
        {
            // Kiểm tra xem layerName đã có ở đâu chưa
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return; // Đã tồn tại
                }
            }

            // Nếu ô targetIndex đang trống thì ghi vào
            if (targetIndex < layersProp.arraySize)
            {
                SerializedProperty p = layersProp.GetArrayElementAtIndex(targetIndex);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = layerName;
                    return;
                }
            }

            // Nếu không, tìm ô trống đầu tiên từ index 8 đến 31
            for (int i = 8; i < 32 && i < layersProp.arraySize; i++)
            {
                SerializedProperty p = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = layerName;
                    return;
                }
            }
        }
    }
}
