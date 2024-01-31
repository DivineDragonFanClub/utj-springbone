using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UTJ.Jobs;
using UTJ.Support.GameObjectExtensions;

namespace UTJ
{
    public static class SpringBoneEditorActions
    {
        public static void ShowSpringBoneWindow()
        {
            SpringBoneWindow.ShowWindow();
        }

        public static void AssignSpringBonesRecursively()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("再生モードを止めてください。");
                return;
            }

            if (Selection.gameObjects.Length < 1)
            {
                Debug.LogError("一つ以上のオブジェクトを選択してください。");
                return;
            }

            var springManagers = new HashSet<SpringJobManager>();
            foreach (var gameObject in Selection.gameObjects)
            {
                SpringBoneSetupUTJ.AssignSpringBonesRecursively(gameObject.transform);
                var manager = gameObject.GetComponentInParent<SpringJobManager>();
                if (manager != null)
                {
                    springManagers.Add(manager);
                }
            }

            foreach (var manager in springManagers)
            {
                SpringBoneSetupUTJ.FindAndAssignSpringBones(manager, true);
            }

            AssetDatabase.Refresh();
        }

        public static void CreatePivotForSpringBones()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("再生モードを止めてください。");
                return;
            }

            if (Selection.gameObjects.Length < 1)
            {
                Debug.LogError("一つ以上のオブジェクトを選択してください。");
                return;
            }

            var selectedSpringBones = Selection.gameObjects
                .Select(gameObject => gameObject.GetComponent<SpringBone>())
                .Where(bone => bone != null);
            foreach (var springBone in selectedSpringBones)
            {
                SpringBoneSetupUTJ.CreateSpringPivotNode(springBone);
            }
        }

        public static void AddToOrUpdateSpringManagerInSelection()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("再生モードを止めてください。");
                return;
            }

            if (Selection.gameObjects.Length <= 0)
            {
                Debug.LogError("一つ以上のオブジェクトを選択してください。");
                return;
            }

            foreach (var gameObject in Selection.gameObjects)
            {
                var manager = gameObject.GetComponent<SpringJobManager>();
                if (manager == null) { manager = gameObject.AddComponent<SpringJobManager>(); }
                SpringBoneSetupUTJ.FindAndAssignSpringBones(manager, true);
            }
        }

        public static void SelectChildSpringBones()
        {
            var springBoneObjects = Selection.gameObjects
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<SpringBone>(true))
                .Select(bone => bone.gameObject)
                .Distinct()
                .ToArray();
            Selection.objects = springBoneObjects;
        }

        public static void DeleteSpringBonesAndManagers()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("再生モードを止めてください。");
                return;
            }

            if (Selection.gameObjects.Length != 1)
            {
                Debug.LogError("一つだけのルートオブジェクトを選択してください");
                return;
            }

            var rootObject = Selection.gameObjects.First();
            var queryMessage = "本当にこのオブジェクトとその子供に入っている全ての\n"
                + "スプリングボーンとスプリングマネージャーを削除しますか？\n\n"
                + rootObject.name;
            if (EditorUtility.DisplayDialog(
                "スプリングボーンとマネージャーを削除", queryMessage, "削除", "キャンセル"))
            {
                SpringBoneSetupUTJ.DestroySpringManagersAndBones(rootObject);
                AssetDatabase.Refresh();
            }
        }

        public static void DeleteSelectedBones()
        {
            var springBonesToDelete = Support.GameObjectExtensions.GameObjectUtil.FindComponentsOfType<SpringBone>()
                .Where(bone => Selection.gameObjects.Contains(bone.gameObject))
                .ToArray();
            var springManagersToUpdate =  Support.GameObjectExtensions.GameObjectUtil.FindComponentsOfType<SpringManager>()
                .Where(manager => manager.springBones.Any(bone => springBonesToDelete.Contains(bone)))
                .ToArray();
            Undo.RecordObjects(springManagersToUpdate, "Delete selected bones");
            foreach (var boneToDelete in springBonesToDelete)
            {
                Undo.DestroyObjectImmediate(boneToDelete);
            }
            foreach (var manager in springManagersToUpdate)
            {
                manager.FindSpringBones(true);
            }
        }

        public static void PromptToUpdateSpringBonesFromList()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("再生中に更新できません");
                return;
            }

            var selectedSpringManagers = Selection.gameObjects
                .Select(gameObject => gameObject.GetComponent<SpringJobManager>())
                .Where(manager => manager != null)
                .ToArray();
            if (!selectedSpringManagers.Any())
            {
                selectedSpringManagers = GameObjectUtil.FindComponentsOfType<SpringJobManager>().ToArray();
            }

            if (selectedSpringManagers.Count() != 1)
            {
                Debug.LogError("一つだけのSpringManagerを選択してください");
                return;
            }

            var springManager = selectedSpringManagers.First();
            var queryMessage = "ボーンリストから揺れものボーンを更新しますか？\n\n"
                + "リストにないSpringBone情報は削除され、\n"
                + "モデルにないSpringBone情報は追加されます。\n\n"
                + "SpringManager: " + springManager.name;
            if (EditorUtility.DisplayDialog("ボーンリストから更新", queryMessage, "更新", "キャンセル"))
            {
                AutoSpringBoneSetup.UpdateSpringManagerFromBoneList(springManager);
            }
        }
    }
}