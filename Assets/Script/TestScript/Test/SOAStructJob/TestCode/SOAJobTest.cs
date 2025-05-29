using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;

using UnityEngine.TestTools;
using static CharacterController.BaseController;
using static CharacterController.BrainStatus;
using CharacterController;

/// <summary>
/// AITestJobのパフォーマンステスト
/// </summary>
public class SOAJobTest
{
    // テスト用のデータ
    private UnsafeList<CharacterData> _characterData;
    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultNonJob;
    private List<MovementInfo> _judgeResultStandard; // StandardAI用の結果リスト
    private NativeHashMap<int2, int> _teamHate;
    private NativeArray<int> _relationMap;

    // 初期化状態を追跡するフラグ
    private bool _dataInitialized = false;
    private bool _charactersInitialized = false;
    private bool _aiInstancesInitialized = false;

    private int jobBatchCount = 1;

    /// <summary>
    /// 
    /// </summary>
    private BaseController[] characters;

    /// <summary>
    /// 生成オブジェクトの配列。
    /// </summary>
    private string[] types = new string[] { "Assets/Prefab/JobAI/TypeA.prefab", "Assets/Prefab/JobAI/TypeB.prefab", "Assets/Prefab/JobAI/TypeC.prefab" };

    // テスト用のパラメータ
    private int _characterCount = 300;
    private float _nowTime = 100.0f;

    // AIテスト用のインスタンス
    private JobAI _aiTestJob;

    // SOA用のJobも

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("開始: OneTimeSetUp");

        // テストデータの初期化
        try
        {
            this.InitializeTestData();

        }
        catch ( Exception ex )
        {
            Debug.LogError($"テストデータ初期化中のエラー: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        // キャラクターデータの初期化 - IEnumeratorなので yield returnする
        yield return this.InitializeCharacterData();

        if ( !this._charactersInitialized )
        {
            Debug.LogError("キャラクターデータの初期化に失敗しました");
            yield break;
        }

        Debug.Log($"キャラクターデータの初期化完了: characterData.Length={this._characterData.Length}");

        // teamHateの中身を確認
        //foreach ( var item in _teamHate )
        //{
        //    Debug.Log($" teamHate.キー={item.Key}");
        //}

        // AIインスタンスの初期化
        try
        {
            this.InitializeAIInstances();
            Debug.Log("AIインスタンスの初期化完了");
        }
        catch ( Exception ex )
        {
            Debug.LogError($"AIインスタンス初期化中のエラー: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        Debug.Log("完了: OneTimeSetUp");
    }

    [TearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("開始: OneTimeTearDown");
        // メモリリソースの解放
        this.DisposeTestData();
        Debug.Log("完了: OneTimeTearDown");
    }

    /// <summary>
    /// テストデータの初期化
    /// </summary>
    private void InitializeTestData()
    {
        Debug.Log($"開始: InitializeTestData (CharacterCount={this._characterCount})");
        try
        {
            // UnsafeListの初期化
            this._characterData = new UnsafeList<CharacterData>(this._characterCount, Allocator.Persistent);
            this._characterData.Resize(this._characterCount, NativeArrayOptions.ClearMemory);
            Debug.Log($"_characterData初期化完了: Length={this._characterData.Length}, IsCreated={this._characterData.IsCreated}");

            this._judgeResultJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
            this._judgeResultJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

            this._judgeResultNonJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
            this._judgeResultNonJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

            // StandardAI用の結果リストを初期化
            this._judgeResultStandard = new List<MovementInfo>(this._characterCount);
            for ( int i = 0; i < this._characterCount; i++ )
            {
                this._judgeResultStandard.Add(new MovementInfo());
            }

            // チームごとのヘイトマップを初期化
            this._teamHate = new NativeHashMap<int2, int>(3, Allocator.Persistent);
            Debug.Log($"_teamHate配列初期化完了: Length={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");

            // 陣営関係マップを初期化
            this._relationMap = new NativeArray<int>(3, Allocator.Persistent);

            for ( int i = 0; i < this._relationMap.Length; i++ )
            {
                // プレイヤーは敵に敵対、敵はプレイヤーに敵対、他は中立など
                switch ( (CharacterSide)i )
                {
                    case CharacterSide.プレイヤー:
                        this._relationMap[i] = 1 << (int)CharacterSide.魔物;  // プレイヤーは敵に敵対
                        break;
                    case CharacterSide.魔物:
                        this._relationMap[i] = 1 << (int)CharacterSide.プレイヤー;  // 敵はプレイヤーに敵対
                        break;
                    case CharacterSide.その他:
                    default:
                        this._relationMap[i] = 0;  // 中立は誰にも敵対しない
                        break;
                }
            }

            this._dataInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestDataでのエラー: {ex.Message}\n{ex.StackTrace}");
            this._dataInitialized = false;
        }

        Debug.Log("完了: InitializeTestData");

        // バッチカウントの最適化
        if ( this._characterCount <= 32 )
        {
            this.jobBatchCount = 1;
        }
        else if ( this._characterCount <= 128 )
        {
            this.jobBatchCount = 16;
        }
        else if ( this._characterCount <= 512 )
        {
            this.jobBatchCount = 64;
        }
        else // 513～1000
        {
            this.jobBatchCount = 128;
        }

    }

    /// <summary>
    /// AIインスタンスの初期化
    /// </summary>
    private void InitializeAIInstances()
    {
        Debug.Log("開始: InitializeAIInstances");
        try
        {
            // 各コンテナの状態確認
            if ( !this._teamHate.IsCreated )
            {
                Debug.LogError("teamHateが初期化されていません");
                return;
            }

            if ( !this._characterData.IsCreated )
            {
                Debug.LogError("characterDataが初期化されていません");
                return;
            }

            if ( !this._relationMap.IsCreated )
            {
                Debug.LogError("relationMapが初期化されていません");
                return;
            }

            // チームヘイトの各要素を確認
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                if ( !this._teamHate.IsCreated )
                {
                    Debug.LogError($"teamHate[{i}]が初期化されていません");
                    return;
                }
                //Debug.Log($"teamHate[{i}].Count={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");
            }

            // AITestJobの初期化
            this._aiTestJob = new AITestJob
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap,
            };

            // NonJobAIの初期化
            this._nonJobAI = new NonJobAI
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultNonJob,
                relationMap = this._relationMap
            };

            // StandardAIの初期化（NativeContainerからデータをコピー）
            this._standardAI = new StandardAI(this._teamHate, this._characterData, this._nowTime, this._relationMap);
            this._standardAI.judgeResult = this._judgeResultStandard;

            this._aiInstancesInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeAIInstancesでのエラー: {ex.Message}\n{ex.StackTrace}");
            this._aiInstancesInitialized = false;
        }

        Debug.Log($"完了: InitializeAIInstances (成功={this._aiInstancesInitialized})");
    }

    /// <summary>
    /// キャラクターデータの初期化
    /// </summary>
    private IEnumerator InitializeCharacterData()
    {
        Debug.Log($"開始: InitializeCharacterData (CharacterCount={this._characterCount})");

        // _characterDataが初期化されているか確認
        if ( !this._characterData.IsCreated )
        {
            Debug.LogError("_characterDataが初期化されていません");
            this._charactersInitialized = false;
            yield break;
        }

        // _teamHateが初期化されているか確認
        if ( !this._teamHate.IsCreated )
        {
            Debug.LogError("_teamHateが初期化されていません");
            this._charactersInitialized = false;
            yield break;
        }

        // プレハブの存在確認
        bool allPrefabsValid = true;
        for ( int i = 0; i < this.types.Length; i++ )
        {
            AsyncOperationHandle<IList<IResourceLocation>> checkOp = Addressables.LoadResourceLocationsAsync(this.types[i]);
            yield return checkOp;

            if ( checkOp.Result.Count == 0 )
            {
                Debug.LogError($"プレハブが見つかりません: {this.types[i]}");
                allPrefabsValid = false;
            }
            else
            {
                Debug.Log($"プレハブを確認: {this.types[i]}");
            }
        }

        if ( !allPrefabsValid )
        {
            Debug.LogError("一部のプレハブが見つかりませんでした");
            this._charactersInitialized = false;
            yield break;
        }

        // 複数のオブジェクトを並列でインスタンス化
        var tasks = new List<AsyncOperationHandle<GameObject>>(this._characterCount);

        for ( int i = 0; i < this._characterCount; i++ )
        {
            // Addressablesを使用してインスタンス化
            var task = Addressables.InstantiateAsync(this.types[i % 3]);
            tasks.Add(task);

            // 100個ごとにフレームスキップ（パフォーマンス対策）
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"インスタンス化リクエスト完了: {tasks.Count}個 {this._characterCount}");

        // すべてのオブジェクトが生成されるのを待つ
        bool allTasksCompleted = true;
        foreach ( var task in tasks )
        {
            yield return task;
            if ( task.Status != AsyncOperationStatus.Succeeded )
            {
                allTasksCompleted = false;
                Debug.LogError("オブジェクトのインスタンス化に失敗しました");
            }
        }

        if ( !allTasksCompleted )
        {
            Debug.LogError("一部のオブジェクトのインスタンス化に失敗しました");
            this._charactersInitialized = false;
            yield break;
        }

        Debug.Log("全オブジェクトのインスタンス化完了");

        // チェックポイント：_teamHateの状態を確認
        for ( int i = 0; i < this._teamHate.Count; i++ )
        {
            Debug.Log($"キャラ生成後の_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
        }

        // 生成されたオブジェクトと必要なコンポーネントを取得
        int successCount = 0;

        for ( int i = 0; i < tasks.Count; i++ )
        {
            GameObject obj;
            try
            {
                obj = tasks[i].Result;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"オブジェクト取得時にエラー: {ex.Message}");
                continue;
            }

            if ( obj == null )
            {
                Debug.LogError($"インデックス{i}のオブジェクトがnullです");
                continue;
            }

            var aiComponent = obj.GetComponent<BaseController>();
            if ( aiComponent == null )
            {
                Debug.LogError($"BaseControllerコンポーネントが見つかりません: {obj.name}");
                continue;
            }

            CharacterData data;
            try
            {
                (JobAITestStatus, GameObject) mat = aiComponent.MakeTestData();
                data = new CharacterData(mat.Item1, mat.Item2);
                this._characterData[i] = data;

                if ( data.brainData.Count == 0 )
                {
                    // 行動判断データ
                    Debug.Log($"  ■ 行動判断データなし！！");
                }
            }
            catch ( Exception ex )
            {
                Debug.LogError($"データ生成時にエラー: {ex.Message}");
                continue;
            }

            successCount++;

            // ヘイトマップも初期化
            int teamNum = (int)data.liveData.belong;

            if ( !this._teamHate.IsCreated )
            {
                Debug.LogError($"_teamHate[{teamNum}]が無効です");
                continue;
            }

            int2 hateKey = new(teamNum, data.hashCode);

            try
            {
                if ( this._teamHate.ContainsKey(hateKey) )
                {
                    Debug.LogWarning($"重複するhashCode: {data.hashCode} (チーム: {teamNum})");
                    continue;
                }

                this._teamHate.Add(hateKey, 10);
            }
            catch ( Exception ex )
            {
                Debug.LogError($"ヘイトマップ更新時にエラー: {ex.Message}");
                continue;
            }

            // 100個ごとにログ
            if ( i % 100 == 0 )
            {
                Debug.Log($"キャラクター初期化進捗: {i}/{tasks.Count}, teamHate[{teamNum}].Count={this._teamHate.Count}");
            }
        }

        Debug.Log($"キャラクターデータ初期化完了: 成功数={successCount}/{tasks.Count}");

        //// 各teamHateの最終状態を確認
        //for ( int i = 0; i < _teamHate.Count; i++ )
        //{
        //    Debug.Log($"最終_teamHate[{i}]: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
        //}

        // キャラクターデータのステータスをランダム化
        Debug.Log("キャラクターデータのランダム化を開始");

        for ( int i = 0; i < this._characterData.Length; i++ )
        {
            CharacterData data = this._characterData[i];

            try
            {
                CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
                this._characterData[i] = data;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"データランダム化時にエラー (index={i}): {ex.Message}");
            }

            // 100個ごとにフレームスキップ
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log("キャラクターデータのランダム化完了");

        this._charactersInitialized = successCount > 0;
        Debug.Log($"完了: InitializeCharacterData (成功={this._charactersInitialized})");
    }

    public static void DebugPrintCharacterData(CharacterData data)
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("===== CharacterData詳細情報 =====");
        _ = sb.AppendLine($"ハッシュコード: {data.hashCode}");

        // 基本情報
        _ = sb.AppendLine($"最終判断時間: {data.lastJudgeTime}");
        _ = sb.AppendLine($"最終移動判断時間: {data.lastMoveJudgeTime}");
        _ = sb.AppendLine($"移動判断間隔: {data.moveJudgeInterval}");
        _ = sb.AppendLine($"ターゲット数: {data.targetingCount}");

        // ライブデータ
        _ = sb.AppendLine("【LiveData情報】");
        _ = sb.AppendLine($"  現在位置: {data.liveData.nowPosition}");
        _ = sb.AppendLine($"  現在HP: {data.liveData.currentHp}/{data.liveData.maxHp}");
        _ = sb.AppendLine($"  所属: {data.liveData.belong}");
        _ = sb.AppendLine($"  状態: {data.liveData.actState}");
        // 他のliveDataフィールドも必要に応じて追加

        // BrainData情報
        _ = sb.AppendLine("【BrainData情報】");
        if ( data.brainData.IsCreated )
        {
            _ = sb.AppendLine($"  登録数: {data.brainData.Count}");
            var keys = data.brainData.GetKeyArray(Allocator.Temp);
            try
            {
                foreach ( var key in keys )
                {
                    if ( data.brainData.TryGetValue(key, out var brainStatus) )
                    {
                        _ = sb.AppendLine($"  モード[{key}]:");
                        _ = sb.AppendLine($"    判断間隔: {brainStatus.judgeInterval}");
                        // 他のbrainStatusフィールドも必要に応じて追加
                    }
                }

                keys.Dispose();
            }
            catch ( Exception ex )
            {
                _ = sb.AppendLine($"  BrainDataアクセス中にエラー: {ex.Message}");
                if ( keys.IsCreated )
                {
                    keys.Dispose();
                }
            }
        }
        else
        {
            _ = sb.AppendLine("  BrainDataは作成されていません");
        }

        // 個人ヘイト情報
        _ = sb.AppendLine("【PersonalHate情報】");
        if ( data.personalHate.IsCreated )
        {
            _ = sb.AppendLine($"  登録数: {data.personalHate.Count}");
            var hateKeys = data.personalHate.GetKeyArray(Allocator.Temp);
            try
            {
                foreach ( var target in hateKeys )
                {
                    if ( data.personalHate.TryGetValue(target, out var hateValue) )
                    {
                        _ = sb.AppendLine($"  対象[{target}]: ヘイト値={hateValue}");
                    }
                }

                hateKeys.Dispose();
            }
            catch ( Exception ex )
            {
                _ = sb.AppendLine($"  PersonalHateアクセス中にエラー: {ex.Message}");
                if ( hateKeys.IsCreated )
                {
                    hateKeys.Dispose();
                }
            }
        }
        else
        {
            _ = sb.AppendLine("  PersonalHateは作成されていません");
        }

        // 近距離キャラクター情報
        _ = sb.AppendLine("【ShortRangeCharacter情報】");
        if ( data.shortRangeCharacter.IsCreated )
        {
            _ = sb.AppendLine($"  登録数: {data.shortRangeCharacter.Length}");
            try
            {
                for ( int i = 0; i < data.shortRangeCharacter.Length; i++ )
                {
                    _ = sb.AppendLine($"  近距離キャラ[{i}]: Hash={data.shortRangeCharacter[i]}");
                }
            }
            catch ( Exception ex )
            {
                _ = sb.AppendLine($"  ShortRangeCharacterアクセス中にエラー: {ex.Message}");
            }
        }
        else
        {
            _ = sb.AppendLine("  ShortRangeCharacterは作成されていません");
        }

        _ = sb.AppendLine("============================");

        // 長いログを分割して出力（Unity consoleの文字数制限回避）
        const int maxLogLength = 1000;
        for ( int i = 0; i < sb.Length; i += maxLogLength )
        {
            int length = Math.Min(maxLogLength, sb.Length - i);
            Debug.Log(sb.ToString(i, length));
        }
    }

    /// <summary>
    /// テストデータのメモリ解放
    /// </summary>
    private void DisposeTestData()
    {
        // UnsafeListの解放
        if ( this._characterData.IsCreated )
        {
            // 各キャラクターデータ内のネイティブコンテナを解放
            for ( int i = 0; i < this._characterData.Length; i++ )
            {
                CharacterData data = this._characterData[i];
                data.Dispose();
            }

            this._characterData.Dispose();
        }

        if ( this._judgeResultJob.IsCreated )
        {
            this._judgeResultJob.Dispose();
        }

        if ( this._judgeResultNonJob.IsCreated )
        {
            this._judgeResultNonJob.Dispose();
        }

        // StandardAI用の結果リストは管理されたオブジェクトなのでGCが処理する
        this._judgeResultStandard = null;

        // チームヘイトマップの解放
        if ( this._teamHate.IsCreated )
        {
            this._teamHate.Dispose();
        }

        // その他のNativeArrayの解放
        if ( this._relationMap.IsCreated )
        {
            this._relationMap.Dispose();
        }
    }

    /// <summary>
    /// 非JobSystemのAI処理パフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void NonJobAI_Performance_Test()
    {
        Debug.Log($"テストデータの初期化完了: teamHate.IsCreated={this._teamHate.IsCreated}, Length={this._teamHate.Count}");

        Measure.Method(() =>
        {
            // 非JobSystemのAI処理実行
            this._nonJobAI.ExecuteAIDecision();
        })
        .WarmupCount(3)       // ウォームアップ回数
        .MeasurementCount(10) // 計測回数
        .IterationsPerMeasurement(1) // 1回の計測あたりの実行回数
                                     // .GC()                 // GCの計測も行う
        .Run();
    }

    /// <summary>
    /// StandardAIのパフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void StandardAI_Performance_Test()
    {
        Debug.Log($"StandardAIテスト開始: characterData.Count={this._standardAI.characterData.Count}");

        Measure.Method(() =>
        {
            // StandardAIの処理実行
            this._standardAI.ExecuteAIDecision();
        })
        .WarmupCount(3)       // ウォームアップ回数
        .MeasurementCount(10) // 計測回数
        .IterationsPerMeasurement(1) // 1回の計測あたりの実行回数
                                     // .GC()                 // GCの計測も行う
        .Run();
    }

    /// <summary>
    /// JobSystemのAI処理パフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        Debug.Log($"テストデータの初期化完了: teamHate.IsCreated={this._teamHate.IsCreated}, Length={this._teamHate.Count}");

        Measure.Method(() =>
        {
            // JobSystemのAI処理実行
            JobHandle handle = this._aiTestJob.Schedule(this._characterCount, this.jobBatchCount);
            handle.Complete();
        })
        .WarmupCount(3)       // ウォームアップ回数
        .MeasurementCount(10) // 計測回数
        .IterationsPerMeasurement(1) // 1回の計測あたりの実行回数
        //.GC()                 // GCの計測も行う
        .Run();
    }

    /// <summary>
    /// テストデータの再作成（キャラクター数変更時）
    /// </summary>
    private async UniTask RecreateTestData(int newCharacterCount)
    {
        // 現在のデータを解放
        this.DisposeTestData();

        // キャラクター数を更新
        this._characterCount = newCharacterCount;

        // 新しいデータを初期化して完了を待機
        this.InitializeTestData();

        Debug.Log($"テストデータの初期化完了: teamHate.IsCreated={this._teamHate.IsCreated}, Length={this._teamHate.Count}");

        // キャラクターデータの初期化
        await this.InitializeCharacterData();

        // AIインスタンスの初期化
        this.InitializeAIInstances();
    }

    /// <summary>
    /// 結果の検証テスト（全実装が同じ結果を出すか確認）
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        Debug.Log("ランダム化前のデータ ===================");
        this.PrintAllCharacterData("初期状態");

        //// データをランダム化
        //for ( int i = 0; i < _characterData.Length; i++ )
        //{
        //    CharacterData data = _characterData[i];
        //    CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
        //    _characterData[i] = data;
        //}

        //    Debug.Log("ランダム化後のデータ ===================");
        //   PrintAllCharacterData("ランダム化後");

        // StandardAIの初期化（NativeContainerからデータをコピー）
        this._standardAI = new StandardAI(this._teamHate, this._characterData, this._nowTime, this._relationMap);
        this._standardAI.judgeResult = this._judgeResultStandard;

        // AIインスタンスの時間を更新
        this._aiTestJob.nowTime = this._nowTime;
        this._nonJobAI.nowTime = this._nowTime;
        this._standardAI.nowTime = this._nowTime;

        // 各AIの処理を実行
        this._nonJobAI.ExecuteAIDecision();
        this._standardAI.ExecuteAIDecision();

        // JobSystemのAI処理実行
        JobHandle handle = this._aiTestJob.Schedule(this._characterCount, this.jobBatchCount);
        handle.Complete();

        // 全要素を検証して結果を出力
        Debug.Log("結果検証開始 ===================");
        int mismatchCount = 0;

        for ( int i = 0; i < this._characterCount; i++ )
        {
            MovementInfo jobResult = this._judgeResultJob[i];
            MovementInfo nonJobResult = this._judgeResultNonJob[i];
            MovementInfo standardResult = this._judgeResultStandard[i];

            // 3つの結果が全て一致するか確認
            bool allMatch =
                jobResult.result == nonJobResult.result &&
                jobResult.result == standardResult.result &&
                jobResult.actNum == nonJobResult.actNum &&
                jobResult.actNum == standardResult.actNum &&
                jobResult.targetHash == nonJobResult.targetHash &&
                jobResult.targetHash == standardResult.targetHash;

            if ( allMatch )
            {
                Debug.Log($"要素[{i}] 一致: (結果={jobResult.result}, 行動={jobResult.actNum}, ターゲット={jobResult.targetHash})");
            }
            else
            {
                Debug.LogWarning($"要素[{i}] 不一致: (Job={jobResult.result},{jobResult.actNum},{jobResult.targetHash}) " +
                               $"(NonJob={nonJobResult.result},{nonJobResult.actNum},{nonJobResult.targetHash}) " +
                               $"(Standard={standardResult.result},{standardResult.actNum},{standardResult.targetHash})");
                mismatchCount++;
            }
        }

        // 全体の結果を出力
        if ( mismatchCount == 0 )
        {
            Debug.Log("全要素検証完了: すべて一致しています");
        }
        else
        {
            Debug.LogError($"全要素検証完了: {mismatchCount}個の要素で不一致が見つかりました");
        }

        Debug.Log("結果検証終了 ===================");

        // テスト結果の検証（必要に応じてコメントアウト可能）
        Assert.AreEqual(0, mismatchCount, $"{mismatchCount}個の不一致が検出されました");
    }

    /// <summary>
    /// すべてのCharacterDataの内容を詳細に出力する
    /// </summary>
    /// <param name="label">出力時のラベル</param>
    private void PrintAllCharacterData(string label)
    {
        Debug.Log($"===== {label} - CharacterData一覧（全{this._characterCount}件）=====");

        for ( int i = 0; i < this._characterCount; i++ )
        {
            CharacterData data = this._characterData[i];
            Debug.Log($"CharacterData[{i}] hashCode: {data.hashCode}");

            // 基本情報
            Debug.Log($"  ■ 基本情報:");
            Debug.Log($"    - 所属: {data.liveData.belong}");
            Debug.Log($"    - 行動状態: {data.liveData.actState}");
            Debug.Log($"    - 最終判断時間: {data.lastJudgeTime}");

            // 位置情報
            Debug.Log($"  ■ 位置情報:");
            Debug.Log($"    - 現在位置: ({data.liveData.nowPosition.x}, {data.liveData.nowPosition.y})");

            // HP/MP情報
            Debug.Log($"  ■ ステータス情報:");
            Debug.Log($"    - HP: {data.liveData.currentHp}/{data.liveData.maxHp} ({data.liveData.hpRatio}%)");
            Debug.Log($"    - MP: {data.liveData.currentMp}/{data.liveData.maxMp} ({data.liveData.mpRatio}%)");

            // 攻撃/防御情報
            Debug.Log($"  ■ 攻撃能力:");
            Debug.Log($"    - 表示攻撃力: {data.liveData.dispAtk}");
            Debug.Log($"    - 斬/刺/打: {data.liveData.atk.slash}/{data.liveData.atk.pierce}/{data.liveData.atk.strike}");
            Debug.Log($"    - 炎/雷/光/闇: {data.liveData.atk.fire}/{data.liveData.atk.lightning}/{data.liveData.atk.light}/{data.liveData.atk.dark}");

            Debug.Log($"  ■ 防御能力:");
            Debug.Log($"    - 表示防御力: {data.liveData.dispDef}");
            Debug.Log($"    - 斬/刺/打: {data.liveData.def.slash}/{data.liveData.def.pierce}/{data.liveData.def.strike}");
            Debug.Log($"    - 炎/雷/光/闇: {data.liveData.def.fire}/{data.liveData.def.lightning}/{data.liveData.def.light}/{data.liveData.def.dark}");

            // 攻撃属性
            Debug.Log($"  ■ 攻撃属性: {data.solidData.attackElement}");

            // ターゲット情報
            Debug.Log($"  ■ ターゲット情報:");
            Debug.Log($"    - ターゲット数: {data.targetingCount}");

            // 行動判断データ
            Debug.Log($"  ■ 行動判断データ:");

            if ( data.brainData.Count == 0 )
            {
                // 行動判断データ
                Debug.Log($"  ■ 行動判断データなし！！");
            }

            for ( int j = 0; j < 8; j++ )
            {

                int key = 1 << j;

                if ( !data.brainData.ContainsKey(key) )
                {
                    continue;
                }

                var brain = data.brainData[key];
                Debug.Log($"    - 行動モード[{(ActState)key}]:");
                Debug.Log($"      判断間隔: {brain.judgeInterval}");

                // 行動条件の表示
                Debug.Log($"      行動条件数: {brain.actCondition.Length}");
                for ( int k = 0; k < brain.actCondition.Length; k++ )
                {
                    var condition = brain.actCondition[k];
                    Debug.Log($"      条件[{k}]: {condition.actCondition.judgeCondition}, 値: {condition.actCondition.judgeValue}, 反転: {condition.actCondition.isInvert}");
                    Debug.Log($"        ターゲット条件: {condition.targetCondition.judgeCondition}, 反転: {condition.targetCondition.isInvert}");
                }
            }

            Debug.Log("-------------------------------------");
        }

        Debug.Log($"===== {label} - CharacterData一覧 終了 =====");
    }

    /// <summary>
    /// 3種類のAI実装を比較検証するテスト
    /// </summary>
//    [Test, Performance]
    public void Compare_Three_AI_Implementations()
    {
        // データをランダム化
        for ( int i = 0; i < this._characterData.Length; i++ )
        {
            CharacterData data = this._characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
            this._characterData[i] = data;
        }

        // 時間を更新（全AIに同じ時間を設定）
        float testTime = 200.0f; // テスト用の時間
        this._aiTestJob.nowTime = testTime;
        this._nonJobAI.nowTime = testTime;
        this._standardAI.nowTime = testTime;

        // 各AIの処理を実行し、パフォーマンスを測定
        using ( Measure.Scope("JobSystemAI実行時間") )
        {
            JobHandle handle = this._aiTestJob.Schedule(this._characterCount, this.jobBatchCount);
            handle.Complete();
        }

        using ( Measure.Scope("NonJobAI実行時間") )
        {
            this._nonJobAI.ExecuteAIDecision();
        }

        using ( Measure.Scope("StandardAI実行時間") )
        {
            this._standardAI.ExecuteAIDecision();
        }

        // 結果検証用のログを出力
        int matchCount = 0;
        int mismatchCount = 0;

        for ( int i = 0; i < Math.Min(5, this._characterCount); i++ )
        {
            // 各AIの結果を取得
            MovementInfo jobResult = this._judgeResultJob[i];
            MovementInfo nonJobResult = this._judgeResultNonJob[i];
            MovementInfo standardResult = this._judgeResultStandard[i];

            // 結果が一致するか確認
            bool allMatch = jobResult.result == nonJobResult.result &&
                           jobResult.result == standardResult.result &&
                           jobResult.actNum == nonJobResult.actNum &&
                           jobResult.actNum == standardResult.actNum &&
                           jobResult.targetHash == nonJobResult.targetHash &&
                           jobResult.targetHash == standardResult.targetHash;

            if ( allMatch )
            {
                matchCount++;
            }
            else
            {
                mismatchCount++;
                Debug.LogWarning($"結果不一致(index={i}):\n" +
                                $"Job: {jobResult.result}, {jobResult.actNum}, {jobResult.targetHash}\n" +
                                $"NonJob: {nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}\n" +
                                $"Standard: {standardResult.result}, {standardResult.actNum}, {standardResult.targetHash}");
            }
        }

        Debug.Log($"サンプル結果比較: 一致={matchCount}, 不一致={mismatchCount}");

        // 検証（すべての実装で結果が一致することを確認）
        Assert.AreEqual(0, mismatchCount, "異なる実装間で結果が一致しません");
    }

    /// <summary>
    /// 異なるキャラクター数でのパフォーマンス比較テスト
    /// </summary>
 //   [UnityTest, Performance]
    public IEnumerator Compare_Different_Character_Counts()
    {
        // テスト用のキャラクター数の配列
        int[] characterCounts = { 10, 50, 100 };

        foreach ( int count in characterCounts )
        {
            // テストケース名を設定
            using ( Measure.Scope($"Character Count: {count}") )
            {
                // キャラクター数の更新と再初期化
                UniTask recreateTask = this.RecreateTestData(count);

                // UniTaskの完了を待機
                while ( !recreateTask.Status.IsCompleted() )
                {
                    yield return null;
                }

                // 非JobSystemテスト
                using ( Measure.Scope("NonJobAI") )
                {
                    this._nonJobAI.ExecuteAIDecision();
                }

                // StandardAIテスト
                using ( Measure.Scope("StandardAI") )
                {
                    this._standardAI.ExecuteAIDecision();
                }

                // フレームスキップ
                yield return null;

                // JobSystemテスト
                using ( Measure.Scope("JobSystemAI") )
                {
                    JobHandle handle = this._aiTestJob.Schedule(count, 64);
                    handle.Complete();
                }
            }

            // 次のテストの前にフレームをスキップ
            yield return null;
        }
    }
}