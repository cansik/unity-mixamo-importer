using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.VersionControl;
using UnityEngine;
using Object = UnityEngine.Object;

public class MixamoAssetImporter : AssetPostprocessor
{
    private void OnPreprocessAnimation()
    {
        var modelImporter = assetImporter as ModelImporter;

        var importSetting = GetMixAmoImportSetting();
        var asset = AssetDatabase.LoadAssetAtPath<Object>(assetImporter.assetPath);
        if (asset != null) return;
        if (importSetting != null && IsAssetContainedInMixAmoDir(assetImporter.assetPath, importSetting))
        {
            var animations = modelImporter.defaultClipAnimations;
            if (animations != null && animations.Length > 0)
            {
                var animationsCount = animations.Length;
                if (animationsCount > 2)
                {
                    for (var i = 0; i < animationsCount; i++)
                    {
                        animations[i].name = Path.GetFileNameWithoutExtension(modelImporter.assetPath) + "_" + i;
                        if (importSetting.loopAnimation)
                        {
                            animations[i].loop = true;
                            animations[i].loopTime = true;
                        }
                    }
                }
                else
                {
                    animations[0].name = Path.GetFileNameWithoutExtension(modelImporter.assetPath);
                    if (importSetting.loopAnimation)
                    {
                        animations[0].loop = true;
                        animations[0].loopTime = true;
                    }
                }

                modelImporter.clipAnimations = animations;

                var avatar = importSetting.avatar;

                if (avatar != null) modelImporter.sourceAvatar = avatar;
            }

            modelImporter.animationType = ModelImporterAnimationType.Human;
        }
    }

    private void OnPreprocessModel()
    {
        var modelImporter = assetImporter as ModelImporter;
        modelImporter.globalScale = 100.0f;
        modelImporter.importNormals = ModelImporterNormals.Calculate;

        var asset = AssetDatabase.LoadAssetAtPath<Object>(assetImporter.assetPath);
        if (asset != null) return;
        var importSetting = GetMixAmoImportSetting();
        if (importSetting != null)
        {
            if (IsAssetContainedInMixAmoDir(assetImporter.assetPath, importSetting))
            {
                modelImporter.animationType = ModelImporterAnimationType.Human;
                modelImporter.materialSearch = ModelImporterMaterialSearch.Local;
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;

                var path = Path.GetDirectoryName(modelImporter.assetPath);
                var fileName = Path.GetFileNameWithoutExtension(modelImporter.assetPath);
                var textureDir = Path.Combine(path, importSetting.textureDirectory, fileName);
                CreateDirectoryTree(textureDir);

                Debug.Log($"Extracting textures for {assetImporter.assetPath}");
                modelImporter.ExtractTextures(textureDir);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        var importSetting = GetMixAmoImportSetting();
        if (importSetting == null)
            return;

        var files = importedAssets.Where(e => e.EndsWith(".fbx")).ToList();
        if (files.Count == 0) return;

        foreach (var file in files)
        {
            if (!IsAssetContainedInMixAmoDir(file, importSetting))
                continue;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            FindAndMapTextures(file, importSetting);

            if (importSetting.createAnimationController)
                CreateAnimationController(file, importSetting);
        }
    }

    private static void CreateAnimationController(string file, MixamoImportSetting settings)
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        var animationDir = Path.Combine(Path.GetDirectoryName(file), settings.animationDirectory);
        CreateDirectoryTree(animationDir);

        var animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(file);
        if (animationClip == null) return;
        animationClip.name = animationClip.name.Replace(".", "_");

        var controllerPath = Path.Combine(animationDir, $"{fileName}.controller");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddMotion(animationClip);

        var obj = AssetDatabase.LoadAssetAtPath<GameObject>(file);
        var animator = obj.GetComponent<Animator>();
        if (animator == null) return;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = settings.applyRootMotion;
    }

    private static void FindAndMapTextures(string file, MixamoImportSetting settings)
    {
        var path = Path.GetDirectoryName(file);
        var fileName = Path.GetFileNameWithoutExtension(file);
        var textureDir = Path.Combine(path, settings.textureDirectory, fileName);

        var gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(file);
        if (gameObject == null) return;

        var renderer = gameObject.GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        var material = new Material(Shader.Find("Standard"));

        var assets = GetAllAssetsInPath(textureDir);
        var texture = assets.OfType<Texture2D>().First();

        material.mainTexture = texture;
        renderer.material = material;
        AssetDatabase.SaveAssets();
    }

    private static MixamoImportSetting GetMixAmoImportSetting()
    {
        var importSettingPaths = AssetDatabase.FindAssets("t:MixAmoImportSetting");
        if (importSettingPaths.Length > 0)
        {
            var importSetting =
                AssetDatabase.LoadAssetAtPath<MixamoImportSetting>(
                    AssetDatabase.GUIDToAssetPath(importSettingPaths[0]));
            return importSetting;
        }

        return null;
    }

    private static bool IsAssetContainedInMixAmoDir(string assetPath, MixamoImportSetting importSetting)
    {
        var allDirectories = new List<string>();
        allDirectories.AddRange(Directory.GetDirectories(importSetting.MixamoDirectory, "*",
            SearchOption.AllDirectories));
        allDirectories.Add(importSetting.MixamoDirectory);
        for (var i = 0; i < allDirectories.Count; i++)
            allDirectories[i] = FileUtil.GetProjectRelativePath(allDirectories[i].Replace("\\", "/"));

        var processedAssetPath = Path.GetDirectoryName(assetPath).Replace("\\", "/");

        return allDirectories.Contains(processedAssetPath);
    }

    private static void CreateDirectoryTree(string path)
    {
        var parts = path.Split("/");
        var root = "";

        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(root) && !AssetDatabase.IsValidFolder($"{root}/{part}"))
            {
                AssetDatabase.CreateFolder(root, part);
            }

            if (string.IsNullOrEmpty(root))
                root = part;
            else
                root = $"{root}/{part}";
        }
    }

    private static IEnumerable<Object> GetAllAssetsInPath(string path)
    {
        var files = Directory.GetFiles(path)
            .Where(e => !e.EndsWith(".meta"))
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
            .ToList();
        return files;
    }
}