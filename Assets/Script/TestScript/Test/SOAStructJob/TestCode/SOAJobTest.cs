using CharacterController;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using TestScript;
using TestScript.Collections;
using TestScript.SOATest;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TestTools;
using static CharacterController.BaseController;
using static CharacterController.BrainStatus;

/// <summary>
/// AITestJobのパフォーマンステスト
/// </summary>
public class SOAJobTest
{
    // テスト用のデータ
    private UnsafeList<CharacterData> _characterData;
    private SoACharaDataDic _soaData;

    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultSoAJob;
    private List<MovementInfo> _judgeResultStandard;
    private NativeHashMap<int2, int> _teamHate;
    private NativeHashMap<int2, int> _personalHate;
    private NativeArray<int> _relationMap;

    private CharacterStatusList _soaStatusList;
    private readonly List<GameObject> _instantiatedObjects = new();

    // 初期化状態を追跡するフラグ
    private bool _dataInitialized = false;
    private bool _charactersInitialized = false;
    private bool _aiInstancesInitialized = false;

    private int _jobBatchCount = 1;

    // 生成オブジェクトの配列
    private readonly string[] _prefabTypes = {
        "Assets/Script/TestScript/Test/SOAStructJob/TestObject/JobTestObjectA.prefab",
        "Assets/Script/TestScript/Test/SOAStructJob/TestObject/JobTestObjectB.prefab",
        "Assets/Script/TestScript/Test/SOAStructJob/TestObject/JobTestObjectC.prefab"
    };

    // テスト用のパラメータ
    private int _characterCount = 100;
    private float _nowTime = 100.0f;

    // AIテスト用のインスタンス
    private JobAI _aiTestJob;
    private SoAJob _soAJobAI;

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("開始: OneTimeSetUp");

        // 前回のテストデータが残っている場合は解放
        DisposeTestData();

        // テストデータの初期化（同期処理に変更）
        yield return InitializeTestDataCoroutine();

        if ( !_dataInitialized )
        {
            Assert.Fail("テストデータの初期化に失敗しました");
        }

        // キャラクターデータの初期化
        yield return InitializeCharacterDataCoroutine();

        if ( !_charactersInitialized )
        {
            Assert.Fail("キャラクターデータの初期化に失敗しました");
        }

        // AIインスタンスの初期化
        InitializeAIInstances();

        if ( !_aiInstancesInitialized )
        {
            Assert.Fail("AIインスタンスの初期化に失敗しました");
        }

        Debug.Log("完了: OneTimeSetUp");
    }

    [TearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("開始: OneTimeTearDown");
        DisposeTestData();
        Debug.Log("完了: OneTimeTearDown");
    }

    /// <summary>
    /// テストデータの初期化（コルーチン版）
    /// </summary>
    private IEnumerator InitializeTestDataCoroutine()
    {
        Debug.Log($"開始: InitializeTestData (CharacterCount={_characterCount})");


        // UnsafeListの初期化
        _characterData = new UnsafeList<CharacterData>(_characterCount, Allocator.Persistent);
        _characterData.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _judgeResultJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
        _judgeResultJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _judgeResultSoAJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
        _judgeResultSoAJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _soaData = new SoACharaDataDic();

        // SoA用のデータを非同期でロード
        var handle = Addressables.LoadAssetAsync<CharacterStatusList>(
            "Assets/Script/TestScript/Test/SOAStructJob/TestData/SoA/SoAList.asset");
        yield return handle;

        try
        {
            if ( handle.Status != AsyncOperationStatus.Succeeded )
            {
                Debug.LogError("SoAListアセットのロードに失敗しました");
                _dataInitialized = false;
                yield break;
            }

            _soaStatusList = handle.Result;
            _soaStatusList.MakeBrainDataArray();

            // StandardAI用の結果リストを初期化
            _judgeResultStandard = new List<MovementInfo>(_characterCount);
            for ( int i = 0; i < _characterCount; i++ )
            {
                _judgeResultStandard.Add(new MovementInfo());
            }

            // チームごとのヘイトマップを初期化
            _teamHate = new NativeHashMap<int2, int>(100, Allocator.Persistent); // より多めに確保
            _personalHate = new NativeHashMap<int2, int>(5, Allocator.Persistent);


            // 陣営関係マップを初期化
            _relationMap = new NativeArray<int>(3, Allocator.Persistent);
            InitializeRelationMap();

            _dataInitialized = true;

            // バッチカウントの最適化
            OptimizeBatchCount();
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestDataでのエラー: {ex.Message}\n{ex.StackTrace}");
            _dataInitialized = false;
        }

        Debug.Log("完了: InitializeTestData");
    }

    /// <summary>
    /// 陣営関係マップの初期化
    /// </summary>
    private void InitializeRelationMap()
    {
        for ( int i = 0; i < _relationMap.Length; i++ )
        {
            switch ( (CharacterSide)i )
            {
                case CharacterSide.プレイヤー:
                    _relationMap[i] = 1 << (int)CharacterSide.魔物;
                    break;
                case CharacterSide.魔物:
                    _relationMap[i] = 1 << (int)CharacterSide.プレイヤー;
                    break;
                case CharacterSide.その他:
                default:
                    _relationMap[i] = 0;
                    break;
            }
        }
    }

    /// <summary>
    /// バッチカウントの最適化
    /// </summary>
    private void OptimizeBatchCount()
    {
        _jobBatchCount = _characterCount switch
        {
            <= 32 => 1,
            <= 128 => 16,
            <= 512 => 64,
            _ => 128
        };
    }

    /// <summary>
    /// AIインスタンスの初期化
    /// </summary>
    private void InitializeAIInstances()
    {
        Debug.Log("開始: InitializeAIInstances");

        try
        {
            // 必要なコンテナの状態確認
            if ( !ValidateContainerStates() )
            {
                _aiInstancesInitialized = false;
                return;
            }

            // AITestJobの初期化
            _aiTestJob = new JobAI
            {
                teamHate = _teamHate,
                characterData = _characterData,
                nowTime = _nowTime,
                judgeResult = _judgeResultJob,
                relationMap = _relationMap,
            };

            var (characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog) = _soaData;
            // NonJobAIの初期化
            this._soAJobAI = new SoAJob((characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog), _personalHate, _teamHate, _judgeResultSoAJob, this._relationMap, _soaStatusList.brainArray, _nowTime);

            _aiInstancesInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeAIInstancesでのエラー: {ex.Message}\n{ex.StackTrace}");
            _aiInstancesInitialized = false;
        }

        Debug.Log($"完了: InitializeAIInstances (成功={_aiInstancesInitialized})");
    }

    /// <summary>
    /// コンテナの状態を検証
    /// </summary>
    private bool ValidateContainerStates()
    {
        if ( !_teamHate.IsCreated )
        {
            Debug.LogError("teamHateが初期化されていません");
            return false;
        }

        if ( !_characterData.IsCreated )
        {
            Debug.LogError("characterDataが初期化されていません");
            return false;
        }

        if ( !_relationMap.IsCreated )
        {
            Debug.LogError("relationMapが初期化されていません");
            return false;
        }

        return true;
    }

    /// <summary>
    /// キャラクターデータの初期化（コルーチン版）
    /// </summary>
    private IEnumerator InitializeCharacterDataCoroutine()
    {
        Debug.Log($"開始: InitializeCharacterData (CharacterCount={_characterCount})");

        // プレハブの存在確認
        yield return ValidatePrefabsCoroutine();

        // オブジェクトのインスタンス化
        yield return InstantiateObjectsCoroutine();

        // キャラクターデータの生成
        yield return GenerateCharacterDataCoroutine();

        _charactersInitialized = true;
        Debug.Log($"完了: InitializeCharacterData (成功={_charactersInitialized})");
    }

    /// <summary>
    /// プレハブの存在確認
    /// </summary>
    private IEnumerator ValidatePrefabsCoroutine()
    {
        for ( int i = 0; i < _prefabTypes.Length; i++ )
        {
            var checkOp = Addressables.LoadResourceLocationsAsync(_prefabTypes[i]);
            yield return checkOp;

            if ( checkOp.Status != AsyncOperationStatus.Succeeded || checkOp.Result.Count == 0 )
            {
                Debug.LogError($"プレハブが見つかりません: {_prefabTypes[i]}");
                _charactersInitialized = false;
                yield break;
            }
        }
    }

    /// <summary>
    /// オブジェクトのインスタンス化
    /// </summary>
    private IEnumerator InstantiateObjectsCoroutine()
    {
        var tasks = new List<AsyncOperationHandle<GameObject>>(_characterCount);

        // インスタンス化リクエストを開始
        for ( int i = 0; i < _characterCount; i++ )
        {
            var task = Addressables.InstantiateAsync(_prefabTypes[i % 3]);
            tasks.Add(task);

            // パフォーマンス対策：100個ごとにフレームスキップ
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        // すべてのオブジェクトが生成されるのを待つ
        foreach ( var task in tasks )
        {
            yield return task;

            if ( task.Status == AsyncOperationStatus.Succeeded )
            {
                _instantiatedObjects.Add(task.Result);
            }
            else
            {
                Debug.LogError("オブジェクトのインスタンス化に失敗しました");
                _charactersInitialized = false;
                yield break;
            }
        }

        Debug.Log($"オブジェクトのインスタンス化完了: {_instantiatedObjects.Count}個");
    }

    /// <summary>
    /// キャラクターデータの生成
    /// </summary>
    private IEnumerator GenerateCharacterDataCoroutine()
    {
        int successCount = 0;

        for ( int i = 0; i < _instantiatedObjects.Count && i < _characterCount; i++ )
        {
            GameObject obj = _instantiatedObjects[i];

            if ( !ProcessSingleCharacter(obj, i, ref successCount) )
            {
                continue;
            }

            // パフォーマンス対策：100個ごとにフレームスキップ
            if ( i % 100 == 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"キャラクターデータ生成完了: 成功数={successCount}/{_characterCount}");
        _charactersInitialized = successCount > 0;
    }

    /// <summary>
    /// 単一のキャラクター処理
    /// </summary>
    private bool ProcessSingleCharacter(GameObject obj, int index, ref int successCount)
    {
        try
        {
            var aiComponent = obj.GetComponent<BaseController>();
            if ( aiComponent == null )
            {
                Debug.LogError($"BaseControllerコンポーネントが見つかりません: {obj.name}");
                return false;
            }

            var (brainStatus, gameObject) = aiComponent.MakeTestData();
            var data = new CharacterData(brainStatus, gameObject);
            _characterData[index] = data;

            int statusNum;

            if ( brainStatus.name == "BrainStatusA" )
            {
                statusNum = 0;
            }
            else if ( brainStatus.name == "BrainStatusB" )
            {
                statusNum = 1;
            }
            else
            {
                statusNum = 2;
            }

            _soaData.Add(aiComponent.gameObject, _soaStatusList.statusList[statusNum], aiComponent);

            // ヘイトマップの更新
            int teamNum = (int)data.liveData.belong;
            int2 hateKey = new(teamNum, data.hashCode);

            if ( !_teamHate.ContainsKey(hateKey) )
            {
                _teamHate.Add(hateKey, 10);
            }

            successCount++;
            return true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"キャラクター処理時にエラー (index={index}): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// テストデータのメモリ解放
    /// </summary>
    private void DisposeTestData()
    {
        // キャラクターデータの解放
        if ( _characterData.IsCreated )
        {
            for ( int i = 0; i < _characterData.Length; i++ )
            {
                try
                {
                    CharacterData data = _characterData[i];
                    data.Dispose();
                }
                catch ( Exception ex )
                {
                    Debug.LogError($"CharacterData[{i}]の解放時にエラー: {ex.Message}");
                }
            }
            _characterData.Dispose();
        }

        // その他のUnsafeListの解放
        if ( _judgeResultJob.IsCreated )
            _judgeResultJob.Dispose();
        if ( _judgeResultSoAJob.IsCreated )
            _judgeResultSoAJob.Dispose();

        // NativeContainerの解放
        if ( _teamHate.IsCreated )
            _teamHate.Dispose();
        if ( _relationMap.IsCreated )
            _relationMap.Dispose();

        // SoAデータの解放
        _soaData?.Dispose();

        // 生成されたGameObjectの削除
        foreach ( var obj in _instantiatedObjects )
        {
            if ( obj != null )
            {
                if ( Application.isPlaying )
                {
                    UnityEngine.Object.Destroy(obj);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }
        _instantiatedObjects.Clear();

        // StandardAI用の結果リスト
        _judgeResultStandard?.Clear();

        // フラグをリセット
        _dataInitialized = false;
        _charactersInitialized = false;
        _aiInstancesInitialized = false;
    }

    /// <summary>
    /// SoAJobAIのパフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void SoAJobAI_Performance_Test()
    {
        // 初期化状態の確認
        Assert.IsTrue(_aiInstancesInitialized, "AIインスタンスが初期化されていません");

        Measure.Method(() =>
        {
            JobHandle handle = _soAJobAI.Schedule(_characterCount, _jobBatchCount);
            handle.Complete();
        })
        .WarmupCount(5)
        .MeasurementCount(20)
        .IterationsPerMeasurement(1)
        .Run();
    }

    /// <summary>
    /// JobSystemAIのパフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        // 初期化状態の確認
        Assert.IsTrue(_aiInstancesInitialized, "AIインスタンスが初期化されていません");

        Measure.Method(() =>
        {
            JobHandle handle = _aiTestJob.Schedule(_characterCount, _jobBatchCount);
            handle.Complete();
        })
        .WarmupCount(5)
        .MeasurementCount(20)
        .IterationsPerMeasurement(1)
        .Run();
    }

    /// <summary>
    /// 結果の検証テスト
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        // 初期化状態の確認
        Assert.IsTrue(_aiInstancesInitialized, "AIインスタンスが初期化されていません");

        // AIインスタンスの時間を更新
        _aiTestJob.nowTime = _nowTime;
        _soAJobAI.nowTime = _nowTime;

        // 各AIの処理を実行
        JobHandle soaHandle = _soAJobAI.Schedule(_characterCount, _jobBatchCount);
        soaHandle.Complete();

        JobHandle jobHandle = _aiTestJob.Schedule(_characterCount, _jobBatchCount);
        jobHandle.Complete();

        // 結果の検証
        int mismatchCount = ValidateResults();

        // テスト結果の検証
        Assert.AreEqual(0, mismatchCount, $"{mismatchCount}個の不一致が検出されました");
    }

    /// <summary>
    /// 結果の検証を実行
    /// </summary>
    private int ValidateResults()
    {
        int mismatchCount = 0;

        for ( int i = 0; i < _characterCount; i++ )
        {
            MovementInfo jobResult = _judgeResultJob[i];
            MovementInfo soaJobResult = _judgeResultSoAJob[i];

            bool allMatch =
                jobResult.result == soaJobResult.result &&
                jobResult.actNum == soaJobResult.actNum &&
                jobResult.targetHash == soaJobResult.targetHash;

            if ( !allMatch )
            {
                _soaData.TryGetIndexByHash(soaJobResult.targetHash, out int index);

                Debug.LogWarning($"要素[{i}] 不一致: " +
                               $"(Job={jobResult.result},{jobResult.actNum},{jobResult.targetHash}) " +
                               $"(SoAJob={soaJobResult.result},{soaJobResult.actNum},{soaJobResult.targetHash})" +
                               $"Jobデバッグ情報{jobResult.GetDebugData()} SoAデバッグ情報{soaJobResult.GetDebugData()}" +
                               $"ターゲット{index}番目 ターゲット初期所属{(int)_soaStatusList.statusList[1].baseData.initialBelong} 行動番号{(int)_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.useAttackOrHateNum}" +
                               $"ターゲット{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.filter.GetTargetType()}" +
                $"フィルター情報{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.filter.DebugIsPassFilter(_soaData._solidData[index], _soaData._characterStateInfo[index])}" +
                $"フィルター詳細{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.filter.DebugIsPassFilterDetailed(_soaData._solidData[index], _soaData._characterStateInfo[index])}" +
                                $"キャラフィルター情報{_characterData[i].brainData[(int)ActState.攻撃].actCondition[0].targetCondition.filter.DebugIsPassFilter(_characterData[index])}" +
                $"キャラフィルター詳細{_characterData[i].brainData[(int)ActState.攻撃].actCondition[0].targetCondition.filter.DebugIsPassFilterWithCharacterInfo(_characterData[index])}");
                mismatchCount++;

                break;

                //int targetType = (int)_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.filter.GetTargetType();

                //Debug.Log($"a{targetType} d{(int)_soaData._characterStateInfo[index].belong} ddd{targetType & (int)_soaData._characterStateInfo[index].belong}num{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.useAttackOrHateNum}");
                //Debug.Log($"a{targetType} d{(int)_soaData._characterStateInfo[index].belong} ddd{targetType & (int)_soaData._characterStateInfo[index].belong}num{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.useAttackOrHateNum}");
            }
            //else
            //{
            //    _soaData.TryGetIndexByHash(soaJobResult.targetHash, out int index);
            //    Debug.LogWarning($"要素[{i}] 一致: " +
            //                   $"(Job={jobResult.result},{jobResult.actNum},{jobResult.targetHash}) " +
            //                   $"(SoAJob={soaJobResult.result},{soaJobResult.actNum},{soaJobResult.targetHash})" +
            //                   $"{index}番目");
            //}
        }

        Debug.Log($"結果検証完了: 不一致={mismatchCount}/{_characterCount}");

        //Debug.Log($"ステータス確認");
        //Debug.Log($" 数：{_soaStatusList.statusList.Length},{_soaStatusList.statusList[0].brainData.Count},{_soaStatusList.statusList[0].brainData[SOAStatus.ActState.攻撃].judgeData.Length}");
        //for ( int i = 0; i < _soaStatusList.brainArray.Length; i++ )
        //{
        //    if ( !_soaStatusList.brainArray[i].brainSetting.ContainsKey((int)ActState.攻撃) )
        //    {
        //        continue;
        //    }
        //    SOAStatus.TargetJudgeData actData = _soaStatusList.brainArray[i].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition;
        //    Debug.Log($"belong:{(int)_soaStatusList.statusList[i].baseData.initialBelong == (int)_soaData._characterStateInfo[i].belong && (int)_soaData._characterStateInfo[i].belong == (int)_characterData[i].liveData.belong} judge:{actData.judgeCondition} inv:{actData.isInvert} act:{actData.useAttackOrHateNum} filt:{actData.filter.GetTargetType()} eq:{actData.filter.Equals(_soaStatusList.statusList[0].brainData[SOAStatus.ActState.攻撃].judgeData[0].targetCondition.filter)}");
        //}
        return mismatchCount;
    }

    /// <summary>
    /// 異なるキャラクター数でのパフォーマンス比較テスト
    /// </summary>
        // [UnityTest, Performance]
    public IEnumerator Compare_Different_Character_Counts()
    {
        int[] characterCounts = { 50, 100, 200 };

        foreach ( int count in characterCounts )
        {
            using ( Measure.Scope($"Character Count: {count}") )
            {
                // テストデータの再作成
                yield return RecreateTestDataCoroutine(count);

                // SoAJobSystemテスト
                using ( Measure.Scope("SoAJobAI") )
                {
                    JobHandle handle = _soAJobAI.Schedule(_characterCount, _jobBatchCount);
                    handle.Complete();
                }

                yield return null;

                // JobSystemテスト
                using ( Measure.Scope("JobSystemAI") )
                {
                    JobHandle handle = _aiTestJob.Schedule(_characterCount, _jobBatchCount);
                    handle.Complete();
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// テストデータの再作成（コルーチン版）
    /// </summary>
    private IEnumerator RecreateTestDataCoroutine(int newCharacterCount)
    {
        // 現在のデータを解放
        DisposeTestData();

        // キャラクター数を更新
        _characterCount = newCharacterCount;

        // 新しいデータを初期化
        yield return InitializeTestDataCoroutine();

        Assert.IsTrue(_dataInitialized, "テストデータの再初期化に失敗しました");

        yield return InitializeCharacterDataCoroutine();

        Assert.IsTrue(_charactersInitialized, "キャラクターデータの再初期化に失敗しました");

        InitializeAIInstances();

        Assert.IsTrue(_aiInstancesInitialized, "AIインスタンスの再初期化に失敗しました");
    }
}