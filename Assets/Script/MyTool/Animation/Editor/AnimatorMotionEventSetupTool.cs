using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// AnimatorControllerのステートにStateMachineBehaviourを自動設定するエディターツール
/// </summary>
public class AnimatorMotionEventSetupTool : EditorWindow
{
    private string targetFolderPath = "Assets/Animations/Characters";
    private string targetTag = "MotionEvent";
    private bool searchSubFolders = true;
    private bool showDetailedLog = true;
    private bool createBackup = true;

    private List<AnimatorControllerInfo> foundControllers = new List<AnimatorControllerInfo>();
    private Vector2 scrollPosition;

    [System.Serializable]
    private class AnimatorControllerInfo
    {
        public AnimatorController controller;
        public string path;
        public List<StateInfo> states = new List<StateInfo>();
        public bool isExpanded = true;
    }

    [System.Serializable]
    private class StateInfo
    {
        public AnimatorState state;
        public string statePath;
        public bool hasTargetTag;
        public bool hasBehaviour;
        public bool needsSetup;
    }

    [MenuItem("Tools/Animation/Setup Motion Event Behaviours")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimatorMotionEventSetupTool>("Motion Event Setup");
        window.minSize = new Vector2(500, 400);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        // ヘッダー
        EditorGUILayout.LabelField("Animator Motion Event Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "このツールは指定フォルダ内のAnimatorControllerをスキャンし、" +
            "特定のタグが付いたステートにStateMachineBehaviourを自動設定します。",
            MessageType.Info
        );

        EditorGUILayout.Space();

        // 設定セクション
        DrawSettingsSection();

        EditorGUILayout.Space();

        // アクションボタン
        DrawActionButtons();

        EditorGUILayout.Space();

        // 結果表示
        DrawResultsSection();
    }

    private void DrawSettingsSection()
    {
        EditorGUILayout.LabelField("設定", EditorStyles.boldLabel);

        using ( new EditorGUILayout.VerticalScope("box") )
        {
            // フォルダ選択
            EditorGUILayout.BeginHorizontal();
            targetFolderPath = EditorGUILayout.TextField("対象フォルダ", targetFolderPath);
            if ( GUILayout.Button("選択", GUILayout.Width(60)) )
            {
                string selectedPath = EditorUtility.OpenFolderPanel(
                    "対象フォルダを選択",
                    targetFolderPath,
                    ""
                );
                if ( !string.IsNullOrEmpty(selectedPath) )
                {
                    // Assets相対パスに変換
                    if ( selectedPath.StartsWith(Application.dataPath) )
                    {
                        targetFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            targetTag = EditorGUILayout.TextField("対象タグ", targetTag);
            searchSubFolders = EditorGUILayout.Toggle("サブフォルダも検索", searchSubFolders);
            createBackup = EditorGUILayout.Toggle("バックアップを作成", createBackup);
            showDetailedLog = EditorGUILayout.Toggle("詳細ログ表示", showDetailedLog);
        }
    }

    private void DrawActionButtons()
    {
        using ( new EditorGUILayout.HorizontalScope() )
        {
            if ( GUILayout.Button("スキャン", GUILayout.Height(30)) )
            {
                ScanAnimatorControllers();
            }

            GUI.enabled = foundControllers.Count > 0;

            if ( GUILayout.Button("選択項目に設定", GUILayout.Height(30)) )
            {
                ApplyStateMachineBehaviours(false);
            }

            if ( GUILayout.Button("すべてに設定", GUILayout.Height(30)) )
            {
                ApplyStateMachineBehaviours(true);
            }

            GUI.enabled = true;
        }
    }

    private void DrawResultsSection()
    {
        if ( foundControllers.Count == 0 )
        {
            EditorGUILayout.HelpBox("「スキャン」ボタンを押してAnimatorControllerを検索してください。", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"検索結果 ({foundControllers.Count} Controllers)", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach ( var controllerInfo in foundControllers )
        {
            DrawControllerInfo(controllerInfo);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawControllerInfo(AnimatorControllerInfo info)
    {
        using ( new EditorGUILayout.VerticalScope("box") )
        {
            // コントローラー情報
            EditorGUILayout.BeginHorizontal();
            info.isExpanded = EditorGUILayout.Foldout(info.isExpanded, info.controller.name, true);

            int needsSetupCount = info.states.Count(s => s.needsSetup);
            if ( needsSetupCount > 0 )
            {
                EditorGUILayout.LabelField(
                    $"要設定: {needsSetupCount}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(80)
                );
            }

            if ( GUILayout.Button("選択", GUILayout.Width(50)) )
            {
                Selection.activeObject = info.controller;
            }
            EditorGUILayout.EndHorizontal();

            if ( info.isExpanded )
            {
                EditorGUI.indentLevel++;

                foreach ( var stateInfo in info.states.Where(s => s.hasTargetTag) )
                {
                    DrawStateInfo(stateInfo);
                }

                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawStateInfo(StateInfo stateInfo)
    {
        using ( new EditorGUILayout.HorizontalScope() )
        {
            // 状態アイコン
            string icon = stateInfo.hasBehaviour ? "✓" : "✗";
            Color iconColor = stateInfo.hasBehaviour ? Color.green : Color.red;

            GUI.color = iconColor;
            EditorGUILayout.LabelField(icon, GUILayout.Width(20));
            GUI.color = Color.white;

            // ステート名
            EditorGUILayout.LabelField(stateInfo.state.name);

            // パス
            EditorGUILayout.LabelField(
                stateInfo.statePath,
                EditorStyles.miniLabel,
                GUILayout.Width(200)
            );
        }
    }

    private void ScanAnimatorControllers()
    {
        foundControllers.Clear();

        // AnimatorControllerを検索
        string searchPattern = searchSubFolders ? "t:AnimatorController" : "t:AnimatorController";
        string[] guids = AssetDatabase.FindAssets(searchPattern, new[] { targetFolderPath });

        foreach ( string guid in guids )
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // サブフォルダ検索の制御
            if ( !searchSubFolders )
            {
                string directory = Path.GetDirectoryName(path).Replace('\\', '/');
                if ( directory != targetFolderPath )
                    continue;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if ( controller != null )
            {
                var controllerInfo = AnalyzeController(controller, path);
                if ( controllerInfo.states.Any(s => s.hasTargetTag) )
                {
                    foundControllers.Add(controllerInfo);
                }
            }
        }

        Debug.Log($"スキャン完了: {foundControllers.Count}個のAnimatorControllerを検出");
    }

    private AnimatorControllerInfo AnalyzeController(AnimatorController controller, string path)
    {
        var info = new AnimatorControllerInfo
        {
            controller = controller,
            path = path
        };

        // すべてのレイヤーをチェック
        foreach ( var layer in controller.layers )
        {
            AnalyzeStateMachine(layer.stateMachine, "", info, layer.name);
        }

        return info;
    }

    private void AnalyzeStateMachine(
        AnimatorStateMachine stateMachine,
        string path,
        AnimatorControllerInfo controllerInfo,
        string layerName)
    {
        string currentPath = string.IsNullOrEmpty(path)
            ? layerName
            : $"{path}/{stateMachine.name}";

        // ステートをチェック
        foreach ( var state in stateMachine.states )
        {
            var stateInfo = new StateInfo
            {
                state = state.state,
                statePath = $"{currentPath}/{state.state.name}",
                hasTargetTag = !string.IsNullOrEmpty(state.state.tag) && state.state.tag == targetTag,
                hasBehaviour = HasMotionEventBehaviour(state.state),
                needsSetup = false
            };

            // タグがあってBehaviourがない場合は設定が必要
            stateInfo.needsSetup = stateInfo.hasTargetTag && !stateInfo.hasBehaviour;

            controllerInfo.states.Add(stateInfo);
        }

        // サブステートマシンを再帰的にチェック
        foreach ( var subStateMachine in stateMachine.stateMachines )
        {
            AnalyzeStateMachine(subStateMachine.stateMachine, currentPath, controllerInfo, layerName);
        }
    }

    private bool HasMotionEventBehaviour(AnimatorState state)
    {
        if ( state.behaviours == null || state.behaviours.Length == 0 )
            return false;

        // MotionEnddBehaviourが既に設定されているかチェック
        return state.behaviours.Any(b => b.GetType() == typeof(MotionEndBehavior));
    }

    private void ApplyStateMachineBehaviours(bool applyToAll)
    {
        int appliedCount = 0;
        int skippedCount = 0;

        // バックアップ作成
        if ( createBackup )
        {
            CreateBackups();
        }

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach ( var controllerInfo in foundControllers )
            {
                bool modified = false;

                foreach ( var stateInfo in controllerInfo.states )
                {
                    if ( !stateInfo.needsSetup )
                    {
                        skippedCount++;
                        continue;
                    }

                    if ( !applyToAll && !IsStateSelected(stateInfo) )
                    {
                        skippedCount++;
                        continue;
                    }

                    // StateMachineBehaviourを追加
                    AddMotionEventBehaviour(stateInfo.state);
                    appliedCount++;
                    modified = true;

                    if ( showDetailedLog )
                    {
                        Debug.Log($"Added behaviour to: {controllerInfo.controller.name}/{stateInfo.statePath}");
                    }
                }

                if ( modified )
                {
                    EditorUtility.SetDirty(controllerInfo.controller);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"設定完了: {appliedCount} 個のステートに適用, {skippedCount} 個をスキップ");

        // 再スキャン
        ScanAnimatorControllers();
    }

    private void AddMotionEventBehaviour(AnimatorState state)
    {
        // MotionEnddBehaviourを追加
        var behaviour = state.AddStateMachineBehaviour<MotionEndBehavior>();

        // デフォルト設定
        if ( behaviour != null )
        {
            SerializedObject serializedBehaviour = new SerializedObject(behaviour);

            // motionNameプロパティにステート名を設定
            var motionNameProp = serializedBehaviour.FindProperty("motionName");
            if ( motionNameProp != null )
            {
                motionNameProp.stringValue = state.name;
            }

            // デフォルト設定
            var triggerOnExitProp = serializedBehaviour.FindProperty("triggerOnExit");
            if ( triggerOnExitProp != null )
            {
                triggerOnExitProp.boolValue = true;
            }

            var triggerOnCompleteProp = serializedBehaviour.FindProperty("triggerOnComplete");
            if ( triggerOnCompleteProp != null )
            {
                triggerOnCompleteProp.boolValue = true;
            }

            var completeThresholdProp = serializedBehaviour.FindProperty("completeThreshold");
            if ( completeThresholdProp != null )
            {
                completeThresholdProp.floatValue = 0.95f;
            }

            serializedBehaviour.ApplyModifiedProperties();
        }
    }

    private bool IsStateSelected(StateInfo stateInfo)
    {
        // 選択中のオブジェクトに含まれるかチェック
        // この実装では常にtrueを返す（全選択と同じ）
        // 必要に応じてUI上でチェックボックスを追加して個別選択を実装可能
        return true;
    }

    private void CreateBackups()
    {
        string backupFolder = $"Assets/AnimatorBackups/{System.DateTime.Now:yyyyMMdd_HHmmss}";

        if ( !AssetDatabase.IsValidFolder("Assets/AnimatorBackups") )
        {
            AssetDatabase.CreateFolder("Assets", "AnimatorBackups");
        }

        string folderName = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        AssetDatabase.CreateFolder("Assets/AnimatorBackups", folderName);

        foreach ( var controllerInfo in foundControllers )
        {
            string fileName = Path.GetFileName(controllerInfo.path);
            string backupPath = $"{backupFolder}/{fileName}";

            if ( !AssetDatabase.CopyAsset(controllerInfo.path, backupPath) )
            {
                Debug.LogWarning($"バックアップ作成失敗: {controllerInfo.path}");
            }
        }

        Debug.Log($"バックアップを作成しました: {backupFolder}");
    }
}

/// <summary>
/// カスタムプロパティドロワー（オプション）
/// </summary>
[CustomEditor(typeof(MotionEndBehavior))]
public class MotionEnddBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "このBehaviourはモーションの終了を検知して通知します。\n" +
            "- OnStateEnter: モーション開始\n" +
            "- OnStateUpdate: 進行状況チェック\n" +
            "- OnStateExit: モーション終了",
            MessageType.Info
        );
    }
}