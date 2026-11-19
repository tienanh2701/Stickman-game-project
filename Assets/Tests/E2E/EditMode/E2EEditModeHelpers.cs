using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StickmanBeatEmUp.Tests.E2E.EditMode
{
    public static class E2EEditModeHelpers
    {
        public static Type FindRuntimeType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            Type t = Type.GetType(fullName, throwOnError: false);
            if (t != null)
            {
                return t;
            }

            t = Type.GetType(fullName + ", Assembly-CSharp", throwOnError: false);
            if (t != null)
            {
                return t;
            }

            t = Type.GetType(fullName + ", Assembly-CSharp-Editor", throwOnError: false);
            if (t != null)
            {
                return t;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                string asmName = assemblies[i].GetName().Name;
                if (string.Equals(asmName, "Assembly-CSharp", StringComparison.Ordinal) ||
                    string.Equals(asmName, "Assembly-CSharp-Editor", StringComparison.Ordinal))
                {
                    t = assemblies[i].GetType(fullName, throwOnError: false);
                    if (t != null)
                    {
                        return t;
                    }
                }
            }

            for (int i = 0; i < assemblies.Length; i++)
            {
                t = assemblies[i].GetType(fullName, throwOnError: false);
                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        public static Type RequireRuntimeType(string fullName)
        {
            Type t = FindRuntimeType(fullName);
            Assert.IsNotNull(t, $"Required runtime type '{fullName}' could not be resolved via reflection.");
            return t;
        }

        public static Type FindEditorType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            Type t = Type.GetType(fullName + ", Assembly-CSharp-Editor", throwOnError: false);
            if (t != null)
            {
                return t;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(assemblies[i].GetName().Name, "Assembly-CSharp-Editor", StringComparison.Ordinal))
                {
                    t = assemblies[i].GetType(fullName, throwOnError: false);
                    if (t != null)
                    {
                        return t;
                    }
                }
            }

            return FindRuntimeType(fullName);
        }

        public static Type RequireEditorType(string fullName)
        {
            Type t = FindEditorType(fullName);
            Assert.IsNotNull(t, $"Required editor type '{fullName}' could not be resolved via reflection.");
            return t;
        }

        public static int FindMissingScriptCount(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            int totalMissing = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    totalMissing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[j].gameObject);
                }
            }

            return totalMissing;
        }

        public static GameObject FindUIElement(Scene scene, string nameOrPath)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(nameOrPath))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            if (nameOrPath.Contains("/"))
            {
                string trimmed = nameOrPath.TrimStart('/');
                string[] parts = trimmed.Split('/');
                if (parts.Length == 0)
                {
                    return null;
                }

                for (int i = 0; i < roots.Length; i++)
                {
                    if (string.Equals(roots[i].name, parts[0], StringComparison.Ordinal))
                    {
                        if (parts.Length == 1)
                        {
                            return roots[i];
                        }

                        string subPath = string.Join("/", parts, 1, parts.Length - 1);
                        Transform exact = roots[i].transform.Find(subPath);
                        if (exact != null)
                        {
                            return exact.gameObject;
                        }
                    }
                }

                string leafName = parts[parts.Length - 1];
                for (int i = 0; i < roots.Length; i++)
                {
                    Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                    for (int j = 0; j < all.Length; j++)
                    {
                        if (!string.Equals(all[j].name, leafName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (MatchesSuffixChain(all[j], parts))
                        {
                            return all[j].gameObject;
                        }
                    }
                }

                return null;
            }

            GameObject firstMatch = null;
            GameObject uniqueActiveMatch = null;
            int activeCount = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    if (!string.Equals(all[j].name, nameOrPath, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (firstMatch == null)
                    {
                        firstMatch = all[j].gameObject;
                    }

                    if (all[j].gameObject.activeInHierarchy)
                    {
                        activeCount++;
                        if (activeCount == 1)
                        {
                            uniqueActiveMatch = all[j].gameObject;
                        }
                        else
                        {
                            uniqueActiveMatch = null;
                        }
                    }
                }
            }

            return uniqueActiveMatch != null ? uniqueActiveMatch : firstMatch;
        }

        public static Rect GetRectBoundsIn1920x1080(RectTransform rt, Canvas rootCanvas)
        {
            Assert.IsNotNull(rt, "RectTransform must not be null.");
            Assert.IsNotNull(rootCanvas, "Root Canvas must not be null.");

            Canvas.ForceUpdateCanvases();
            RectTransform canvasRt = rootCanvas.transform as RectTransform;
            Assert.IsNotNull(canvasRt, "Root Canvas transform is not a RectTransform.");

            Rect canvasRect = canvasRt.rect;
            if (Mathf.Abs(canvasRect.width - 1920f) < 0.5f && Mathf.Abs(canvasRect.height - 1080f) < 0.5f)
            {
                Vector3[] worldCorners = new Vector3[4];
                rt.GetWorldCorners(worldCorners);

                float minX = float.MaxValue;
                float minY = float.MaxValue;
                float maxX = float.MinValue;
                float maxY = float.MinValue;

                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = canvasRt.InverseTransformPoint(worldCorners[i]);
                    float x = local.x - canvasRect.xMin;
                    float y = local.y - canvasRect.yMin;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }

                return Rect.MinMaxRect(minX, minY, maxX, maxY);
            }

            List<RectTransform> chain = new List<RectTransform>();
            Transform cursor = rt;
            while (cursor != null && cursor != rootCanvas.transform)
            {
                RectTransform step = cursor as RectTransform;
                if (step == null)
                {
                    Assert.Fail($"Non-RectTransform ancestor '{cursor.name}' found between '{rt.name}' and '{rootCanvas.name}'.");
                }
                chain.Add(step);
                cursor = cursor.parent;
            }

            Assert.AreEqual(rootCanvas.transform, cursor, $"'{rt.name}' is not a descendant of Canvas '{rootCanvas.name}'.");

            Vector2 rootPivot = canvasRt.pivot;
            Rect currentLocalRect = new Rect(-1920f * rootPivot.x, -1080f * rootPivot.y, 1920f, 1080f);
            Matrix4x4 currentTo1920 = Matrix4x4.Translate(new Vector3(1920f * rootPivot.x, 1080f * rootPivot.y, 0f));

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                RectTransform c = chain[i];
                Vector2 anchorMinPos = new Vector2(
                    currentLocalRect.xMin + c.anchorMin.x * currentLocalRect.width,
                    currentLocalRect.yMin + c.anchorMin.y * currentLocalRect.height);
                Vector2 anchorMaxPos = new Vector2(
                    currentLocalRect.xMin + c.anchorMax.x * currentLocalRect.width,
                    currentLocalRect.yMin + c.anchorMax.y * currentLocalRect.height);

                Vector2 minInParent = anchorMinPos + c.offsetMin;
                Vector2 maxInParent = anchorMaxPos + c.offsetMax;
                Vector2 size = maxInParent - minInParent;
                Vector2 pivotInParent = minInParent + Vector2.Scale(size, c.pivot);

                currentLocalRect = new Rect(-size.x * c.pivot.x, -size.y * c.pivot.y, size.x, size.y);
                Matrix4x4 localToParent = Matrix4x4.TRS(
                    new Vector3(pivotInParent.x, pivotInParent.y, 0f),
                    c.localRotation,
                    c.localScale);
                currentTo1920 = currentTo1920 * localToParent;
            }

            Vector3[] localCorners =
            {
                new Vector3(currentLocalRect.xMin, currentLocalRect.yMin, 0f),
                new Vector3(currentLocalRect.xMin, currentLocalRect.yMax, 0f),
                new Vector3(currentLocalRect.xMax, currentLocalRect.yMax, 0f),
                new Vector3(currentLocalRect.xMax, currentLocalRect.yMin, 0f),
            };

            float rMinX = float.MaxValue;
            float rMinY = float.MaxValue;
            float rMaxX = float.MinValue;
            float rMaxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector3 pt = currentTo1920.MultiplyPoint3x4(localCorners[i]);
                if (pt.x < rMinX) rMinX = pt.x;
                if (pt.y < rMinY) rMinY = pt.y;
                if (pt.x > rMaxX) rMaxX = pt.x;
                if (pt.y > rMaxY) rMaxY = pt.y;
            }

            return Rect.MinMaxRect(rMinX, rMinY, rMaxX, rMaxY);
        }

        private static bool MatchesSuffixChain(Transform leaf, string[] parts)
        {
            Transform current = leaf;
            for (int k = parts.Length - 1; k >= 0; k--)
            {
                if (current == null || !string.Equals(current.name, parts[k], StringComparison.Ordinal))
                {
                    return false;
                }
                current = current.parent;
            }
            return true;
        }
    }
}
