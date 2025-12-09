using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JollyCoop
{
    public class OutlineColorManager
    {
        public static OutlineColorManager instance;

        private class OutlineColorDataFile
        {
            [System.Serializable]
            public class OutlineColorEntry
            {
                public string name = "";
                public float[] outline_color = new float[0];
            }

            public List<OutlineColorEntry> outline_colors = new List<OutlineColorEntry>();
        }

        public class OutlineColorItem
        {
            public string name;
            public Color color;
        }

        private List<string> outlineColorNameList = new List<string>();
        private Dictionary<string, OutlineColorItem> registeredOutlineColors = new Dictionary<string, OutlineColorItem>();

        public string folderPath;
        public static readonly Color defaultOutlineColor = Color.black;

        public List<string> OutlineColorNameList => outlineColorNameList;
        public Dictionary<string, OutlineColorItem> RegisteredOutlineColors => registeredOutlineColors;

        public OutlineColorManager()
        {
            folderPath = ETGMod.FolderPath(JollyCoopModule.instance);
            instance = this;
            LoadAllOutlineColorData();
        }

        private void LoadDefaultOutlineColorData()
        {
            AddOutlineColor("black", new Color(0f, 0f, 0f, 1f));
            AddOutlineColor("red", new Color(1f, 0f, 0f, 1f));
            AddOutlineColor("green", new Color(0f, 1f, 0f, 1f));
            AddOutlineColor("blue", new Color(0f, 0f, 1f, 1f));
            AddOutlineColor("yellow", new Color(1f, 1f, 0f, 1f));
            AddOutlineColor("cyan", new Color(0f, 1f, 1f, 1f));
            AddOutlineColor("magenta", new Color(1f, 0f, 1f, 1f));
            AddOutlineColor("white", new Color(1f, 1f, 1f, 1f));
            AddOutlineColor("orange", new Color(1f, 0.5f, 0f, 1f));
            AddOutlineColor("purple", new Color(0.5f, 0f, 0.5f, 1f));
            AddOutlineColor("pink", new Color(1f, 0.75f, 0.8f, 1f));
            AddOutlineColor("brown", new Color(0.65f, 0.16f, 0.16f, 1f));
            AddOutlineColor("gray", new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        public void LoadAllOutlineColorData()
        {
            outlineColorNameList.Clear();
            registeredOutlineColors.Clear();

            LoadDefaultOutlineColorData();

            var files = Directory.GetFiles(folderPath, "*.jcprofile", SearchOption.AllDirectories);
            Debug.Log($"找到 {files.Length} 个 .jcprofile 文件。Find {files.Length} jcprofile files.");

            foreach (string path in files)
            {
                try
                {
                    string json = File.ReadAllText(path);
                    OutlineColorDataFile data = JsonConvert.DeserializeObject<OutlineColorDataFile>(json);

                    if (data == null)
                    {
                        Debug.LogWarning($"无法解析 JSON 文件: {path}。Unable to parse JSON file: {path}.");
                        continue;
                    }

                    ProcessOutlineColorFile(data, path);
                }
                catch (Exception e)
                {
                    Debug.LogError($"加载文件失败 {path}: {e.Message}。Failed to load file {path}: {e.Message}.");
                }
            }

            Debug.Log($"加载轮廓颜色: {outlineColorNameList.Count} 个。Load outline colors: {outlineColorNameList.Count}.");
        }

        private void ProcessOutlineColorFile(OutlineColorDataFile data, string filePath)
        {
            if (data.outline_colors == null)
            {
                Debug.LogWarning($"文件没有 outline_colors 字段: {filePath}。The file does not have an outline_comors field: {filePath}.");
                return;
            }

            foreach (var colorEntry in data.outline_colors)
            {
                if (colorEntry == null)
                {
                    Debug.LogWarning($"轮廓颜色配置条目为 null，文件: {filePath}。Outline color configuration entry is null, file: {filePath}.");
                    continue;
                }

                if (string.IsNullOrEmpty(colorEntry.name))
                {
                    Debug.LogWarning($"轮廓颜色配置缺少名称，文件: {filePath}。The contour color configuration is missing a name, file: {filePath}.");
                    continue;
                }

                if (colorEntry.outline_color == null || colorEntry.outline_color.Length < 3)
                {
                    Debug.LogWarning($"轮廓颜色配置格式错误: {colorEntry.name}，需要3个RGB值，文件: {filePath}。Outline color configuration format error: {colorEntry.name}, requires 3 RGB values, file: {filePath}.");
                    continue;
                }

                Color color = new Color(
                    Mathf.Clamp01(colorEntry.outline_color[0]),
                    Mathf.Clamp01(colorEntry.outline_color[1]),
                    Mathf.Clamp01(colorEntry.outline_color[2]),
                    1f
                );

                AddOutlineColor(colorEntry.name, color);
            }
        }

        private string GetUniqueName<T>(Dictionary<string, T> dict, string baseName)
        {
            if (!dict.ContainsKey(baseName))
                return baseName;

            int maxNumber = 0;
            string prefix = baseName + "_";
            bool hasAnyNumberedKey = false;

            foreach (var key in dict.Keys)
            {
                if (key == baseName)
                    continue;

                if (key.StartsWith(prefix))
                {
                    string suffix = key.Substring(prefix.Length);
                    if (int.TryParse(suffix, out int number))
                    {
                        hasAnyNumberedKey = true;
                        if (number > maxNumber)
                            maxNumber = number;
                    }
                }
            }

            return hasAnyNumberedKey ? $"{baseName}_{maxNumber + 1}" : $"{baseName}_1";
        }

        public Color GetOutlineColorByName(string name)
        {
            if (name == null)
                return defaultOutlineColor;

            if (registeredOutlineColors.TryGetValue(name, out var item))
                return item.color;

            return defaultOutlineColor;
        }

        public bool AddOutlineColor(string name, Color color)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            string finalName = GetUniqueName(registeredOutlineColors, name);

            registeredOutlineColors[finalName] = new OutlineColorItem
            {
                name = finalName,
                color = color
            };

            if (!outlineColorNameList.Contains(finalName))
                outlineColorNameList.Add(finalName);

            return true;
        }
    }
}
