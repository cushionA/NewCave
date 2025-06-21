/*
// アセンブリレベル設定
[assembly: BurstCompile(OptimizeFor = OptimizeFor.Performance)]
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
    private UnsafeList<MovementInfo> _judgeResultSplitJob;

    private List<MovementInfo> _judgeResultStandard;
    private NativeHashMap<int2, int> _teamHate;
    private NativeHashMap<int2, int> _personalHate;
    private NativeArray<int> _relationMap;

    private CharacterStatusList _soaStatusList;
    private readonly List<GameObject> _instantiatedObjects = new();

    public UnsafeList<int> selectMoveList;

    public UnsafeList<int> stateList;

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

    private FirstJob _firstJob;
    private SecondJob _secondJob;
    private ThirdJob _thirdJob;

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("開始: OneTimeSetUp");

        // ランタイム設定
        JobsUtility.JobWorkerCount = Mathf.Max(1, SystemInfo.processorCount - 1);

        // 前回のテストデータが残っている場合は解放
        this.DisposeTestData();

        // テストデータの初期化（同期処理に変更）
        yield return this.InitializeTestDataCoroutine();

        if ( !this._dataInitialized )
        {
            Assert.Fail("テストデータの初期化に失敗しました");
        }

        // キャラクターデータの初期化
        yield return this.InitializeCharacterDataCoroutine();

        if ( !this._charactersInitialized )
        {
            Assert.Fail("キャラクターデータの初期化に失敗しました");
        }

        // AIインスタンスの初期化
        this.InitializeAIInstances();

        if ( !this._aiInstancesInitialized )
        {
            Assert.Fail("AIインスタンスの初期化に失敗しました");
        }

        Debug.Log("完了: OneTimeSetUp");
    }

    [TearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("開始: OneTimeTearDown");
        this.DisposeTestData();
        Debug.Log("完了: OneTimeTearDown");
    }

    /// <summary>
    /// テストデータの初期化（コルーチン版）
    /// </summary>
    private IEnumerator InitializeTestDataCoroutine()
    {
        Debug.Log($"開始: InitializeTestData (CharacterCount={this._characterCount})");

        // UnsafeListの初期化
        this._characterData = new UnsafeList<CharacterData>(this._characterCount, Allocator.Persistent);
        this._characterData.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

        this._judgeResultJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
        this._judgeResultJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

        this._judgeResultSoAJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
        this._judgeResultSoAJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

        this._judgeResultSplitJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
        this._judgeResultSplitJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

        this._soaData = new SoACharaDataDic();

        this.selectMoveList = new UnsafeList<int>(this._characterCount, Allocator.Persistent);
        this.selectMoveList.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

        this.stateList = new UnsafeList<int>(this._characterCount, Allocator.Persistent);
        this.stateList.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

        // SoA用のデータを非同期でロード
        AsyncOperationHandle<CharacterStatusList> handle = Addressables.LoadAssetAsync<CharacterStatusList>(
            "Assets/Script/TestScript/Test/SOAStructJob/TestData/SoA/SoAList.asset");
        yield return handle;

        try
        {
            if ( handle.Status != AsyncOperationStatus.Succeeded )
            {
                Debug.LogError("SoAListアセットのロードに失敗しました");
                this._dataInitialized = false;
                yield break;
            }

            this._soaStatusList = handle.Result;
            this._soaStatusList.MakeBrainDataArray();

            // StandardAI用の結果リストを初期化
            this._judgeResultStandard = new List<MovementInfo>(this._characterCount);
            for ( int i = 0; i < this._characterCount; i++ )
            {
                this._judgeResultStandard.Add(new MovementInfo());
            }

            // チームごとのヘイトマップを初期化
            this._teamHate = new NativeHashMap<int2, int>(100, Allocator.Persistent); // より多めに確保
            this._personalHate = new NativeHashMap<int2, int>(5, Allocator.Persistent);

            // 陣営関係マップを初期化
            this._relationMap = new NativeArray<int>(3, Allocator.Persistent);
            this.InitializeRelationMap();

            this._dataInitialized = true;

            // バッチカウントの最適化
            this.OptimizeBatchCount();
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestDataでのエラー: {ex.Message}\n{ex.StackTrace}");
            this._dataInitialized = false;
        }

        Debug.Log("完了: InitializeTestData");
    }

    /// <summary>
    /// 陣営関係マップの初期化
    /// </summary>
    private void InitializeRelationMap()
    {
        for ( int i = 0; i < this._relationMap.Length; i++ )
        {
            this._relationMap[i] = (CharacterSide)i switch
            {
                CharacterSide.プレイヤー => 1 << (int)CharacterSide.魔物,
                CharacterSide.魔物 => 1 << (int)CharacterSide.プレイヤー,
                _ => 0,
            };
        }
    }

    /// <summary>
    /// バッチカウントの最適化
    /// </summary>
    private void OptimizeBatchCount()
    {
        this._jobBatchCount = 1;
        return;
        this._jobBatchCount = this._characterCount switch
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
            if ( !this.ValidateContainerStates() )
            {
                this._aiInstancesInitialized = false;
                return;
            }

            // AITestJobの初期化
            this._aiTestJob = new JobAI
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap,
            };

            (UnsafeList<SOAStatus.CharacterBaseInfo> characterBaseInfo, UnsafeList<SOAStatus.CharacterAtkStatus> characterAtkStatus, UnsafeList<SOAStatus.CharacterDefStatus> characterDefStatus, UnsafeList<SOAStatus.SolidData> solidData,
     UnsafeList<SOAStatus.CharacterStateInfo> characterStateInfo, UnsafeList<SOAStatus.MoveStatus> moveStatus, UnsafeList<SOAStatus.CharaColdLog> coldLog) = this._soaData;
            // NonJobAIの初期化
            this._soAJobAI = new SoAJob((characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog), this._personalHate, this._teamHate, this._judgeResultSoAJob, this._relationMap, this._soaStatusList.brainArray, this._nowTime);

            this._firstJob = new FirstJob(this.stateList, coldLog, this._soaStatusList.brainArray, this._nowTime);
            // SplitJobAIの初期化
            this._secondJob = new SecondJob((characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog), this._personalHate, this._teamHate, this._judgeResultSplitJob, this._relationMap, this._soaStatusList.brainArray, this.selectMoveList, this.stateList);

            this._thirdJob = new ThirdJob((characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog), this._personalHate, this._teamHate, this._judgeResultSplitJob, this._relationMap, this._soaStatusList.brainArray, this.selectMoveList);

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
    /// コンテナの状態を検証
    /// </summary>
    private bool ValidateContainerStates()
    {
        if ( !this._teamHate.IsCreated )
        {
            Debug.LogError("teamHateが初期化されていません");
            return false;
        }

        if ( !this._characterData.IsCreated )
        {
            Debug.LogError("characterDataが初期化されていません");
            return false;
        }

        if ( !this._relationMap.IsCreated )
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
        Debug.Log($"開始: InitializeCharacterData (CharacterCount={this._characterCount})");

        // プレハブの存在確認
        yield return this.ValidatePrefabsCoroutine();

        // オブジェクトのインスタンス化
        yield return this.InstantiateObjectsCoroutine();

        // キャラクターデータの生成
        yield return this.GenerateCharacterDataCoroutine();

        this._charactersInitialized = true;
        Debug.Log($"完了: InitializeCharacterData (成功={this._charactersInitialized})");
    }

    /// <summary>
    /// プレハブの存在確認
    /// </summary>
    private IEnumerator ValidatePrefabsCoroutine()
    {
        for ( int i = 0; i < this._prefabTypes.Length; i++ )
        {
            AsyncOperationHandle<IList<IResourceLocation>> checkOp = Addressables.LoadResourceLocationsAsync(this._prefabTypes[i]);
            yield return checkOp;

            if ( checkOp.Status != AsyncOperationStatus.Succeeded || checkOp.Result.Count == 0 )
            {
                Debug.LogError($"プレハブが見つかりません: {this._prefabTypes[i]}");
                this._charactersInitialized = false;
                yield break;
            }
        }
    }

    /// <summary>
    /// オブジェクトのインスタンス化
    /// </summary>
    private IEnumerator InstantiateObjectsCoroutine()
    {
        List<AsyncOperationHandle<GameObject>> tasks = new(this._characterCount);

        // インスタンス化リクエストを開始
        for ( int i = 0; i < this._characterCount; i++ )
        {
            AsyncOperationHandle<GameObject> task = Addressables.InstantiateAsync(this._prefabTypes[i % 3]);
            tasks.Add(task);

            // パフォーマンス対策：100個ごとにフレームスキップ
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        // すべてのオブジェクトが生成されるのを待つ
        foreach ( AsyncOperationHandle<GameObject> task in tasks )
        {
            yield return task;

            if ( task.Status == AsyncOperationStatus.Succeeded )
            {
                this._instantiatedObjects.Add(task.Result);
            }
            else
            {
                Debug.LogError("オブジェクトのインスタンス化に失敗しました");
                this._charactersInitialized = false;
                yield break;
            }
        }

        Debug.Log($"オブジェクトのインスタンス化完了: {this._instantiatedObjects.Count}個");
    }

    /// <summary>
    /// キャラクターデータの生成
    /// </summary>
    private IEnumerator GenerateCharacterDataCoroutine()
    {
        int successCount = 0;

        for ( int i = 0; i < this._instantiatedObjects.Count && i < this._characterCount; i++ )
        {
            GameObject obj = this._instantiatedObjects[i];

            if ( !this.ProcessSingleCharacter(obj, i, ref successCount) )
            {
                continue;
            }

            // パフォーマンス対策：100個ごとにフレームスキップ
            if ( i % 100 == 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"キャラクターデータ生成完了: 成功数={successCount}/{this._characterCount}");
        this._charactersInitialized = successCount > 0;
    }

    /// <summary>
    /// 単一のキャラクター処理
    /// </summary>
    private bool ProcessSingleCharacter(GameObject obj, int index, ref int successCount)
    {
        try
        {
            BaseController aiComponent = obj.GetComponent<BaseController>();
            if ( aiComponent == null )
            {
                Debug.LogError($"BaseControllerコンポーネントが見つかりません: {obj.name}");
                return false;
            }

            (BrainStatus brainStatus, GameObject gameObject) = aiComponent.MakeTestData();
            CharacterData data = new(brainStatus, gameObject);
            this._characterData[index] = data;

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

            _ = this._soaData.Add(aiComponent.gameObject, this._soaStatusList.statusList[statusNum], aiComponent);

            // ヘイトマップの更新
            int teamNum = (int)data.liveData.belong;
            int2 hateKey = new(teamNum, data.hashCode);

            if ( !this._teamHate.ContainsKey(hateKey) )
            {
                this._teamHate.Add(hateKey, 10);
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
        if ( this._characterData.IsCreated )
        {
            for ( int i = 0; i < this._characterData.Length; i++ )
            {
                try
                {
                    CharacterData data = this._characterData[i];
                    data.Dispose();
                }
                catch ( Exception ex )
                {
                    Debug.LogError($"CharacterData[{i}]の解放時にエラー: {ex.Message}");
                }
            }

            this._characterData.Dispose();
        }

        // その他のUnsafeListの解放
        if ( this._judgeResultJob.IsCreated )
        {
            this._judgeResultJob.Dispose();
        }

        if ( this._judgeResultSoAJob.IsCreated )
        {
            this._judgeResultSoAJob.Dispose();
        }

        // NativeContainerの解放
        if ( this._teamHate.IsCreated )
        {
            this._teamHate.Dispose();
        }

        if ( this._relationMap.IsCreated )
        {
            this._relationMap.Dispose();
        }

        // SoAデータの解放
        this._soaData?.Dispose();

        // 生成されたGameObjectの削除
        foreach ( GameObject obj in this._instantiatedObjects )
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

        this._instantiatedObjects.Clear();

        // StandardAI用の結果リスト
        this._judgeResultStandard?.Clear();

        // フラグをリセット
        this._dataInitialized = false;
        this._charactersInitialized = false;
        this._aiInstancesInitialized = false;
    }

    /// <summary>
    /// SoAJobAIのパフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void SoAJobAI_Performance_Test()
    {
        // 初期化状態の確認
        Assert.IsTrue(this._aiInstancesInitialized, "AIインスタンスが初期化されていません");

        Measure.Method(() =>
        {
            JobHandle handle = this._soAJobAI.Schedule(this._characterCount, this._jobBatchCount);
            handle.Complete();
        })
        .WarmupCount(10)
        .MeasurementCount(100)
        .Run();
    }

    /// <summary>
    /// SplitJobAIのパフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void SplitJobAI_Performance_Test()
    {
        // 初期化状態の確認
        Assert.IsTrue(this._aiInstancesInitialized, "AIインスタンスが初期化されていません");

        Measure.Method(() =>
        {
            JobHandle h1 = this._firstJob.Schedule(this._characterCount, this._jobBatchCount);
            JobHandle h2 = this._secondJob.Schedule(this._characterCount, this._jobBatchCount, h1);
            JobHandle h3 = this._thirdJob.Schedule(this._characterCount, this._jobBatchCount, h2);
            h3.Complete();
        })
        .WarmupCount(10)
        .MeasurementCount(100)
        .Run();
    }

    /// <summary>
    /// JobSystemAIのパフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        // 初期化状態の確認
        Assert.IsTrue(this._aiInstancesInitialized, "AIインスタンスが初期化されていません");

        Measure.Method(() =>
        {
            JobHandle handle = this._aiTestJob.Schedule(this._characterCount, this._jobBatchCount);
            handle.Complete();
        })
        .WarmupCount(10)
        .MeasurementCount(100)
        .Run();
    }

    /// <summary>
    /// 各ジョブのメモリ使用量を比較するテスト
    /// </summary>
    //[Test, Performance]
    public void Compare_Memory_Usage_Between_Jobs()
    {
        // 初期化状態の確認
        Assert.IsTrue(this._aiInstancesInitialized, "AIインスタンスが初期化されていません");

        // テスト設定
        int characterCount = 10000; // メモリ使用量を測定しやすい大きめのサイズ
        int jobBatchCount = 64;

        // AIインスタンスの時間を更新
        this._aiTestJob.nowTime = this._nowTime;
        this._soAJobAI.nowTime = this._nowTime;
        this._firstJob.nowTime = this._nowTime;

        // メモリ測定用のサンプルグループ
        SampleGroup memoryResults = new("Memory Usage (MB)", SampleUnit.Megabyte);

        // 1. SoAJobAIのメモリ使用量測定
        long soAJobMemory = this.MeasureJobMemory(() =>
        {
            JobHandle handle = this._soAJobAI.Schedule(characterCount, jobBatchCount);
            handle.Complete();
        }, "SoAJobAI");

        // 2. AITestJobのメモリ使用量測定
        long aiTestJobMemory = this.MeasureJobMemory(() =>
        {
            JobHandle jobHandle = this._aiTestJob.Schedule(characterCount, jobBatchCount);
            jobHandle.Complete();
        }, "AITestJob");

        // 3. 連鎖ジョブのメモリ使用量測定
        long chainedJobsMemory = this.MeasureJobMemory(() =>
        {
            JobHandle h1 = this._firstJob.Schedule(characterCount, jobBatchCount);
            JobHandle h2 = this._secondJob.Schedule(characterCount, jobBatchCount, h1);
            JobHandle h3 = this._thirdJob.Schedule(characterCount, jobBatchCount, h2);
            h3.Complete();
        }, "ChainedJobs (First+Second+Third)");

        // パフォーマンステストフレームワークへの記録
        Measure.Custom(memoryResults, soAJobMemory / (1024f * 1024f));
    }

    /// <summary>
    /// ジョブのメモリ使用量を測定
    /// </summary>
    private long MeasureJobMemory(System.Action jobExecution, string jobName)
    {
        // GCを実行してベースラインを安定させる
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();

        // 測定前のメモリ使用量
        long beforeMemory = Profiler.GetTotalAllocatedMemoryLong();

        // ジョブ実行
        jobExecution();

        // 測定後のメモリ使用量
        long afterMemory = Profiler.GetTotalAllocatedMemoryLong();
        long usedMemory = afterMemory - beforeMemory;

        Debug.Log($"{jobName} Memory Usage: {usedMemory / (1024f * 1024f):F2} MB");

        return usedMemory;
    }

    /// <summary>
    /// 結果の検証テスト
    /// </summary>
 //   [Test]
    public void Verify_Results_Are_Same()
    {
        // 初期化状態の確認
        Assert.IsTrue(this._aiInstancesInitialized, "AIインスタンスが初期化されていません");

        // AIインスタンスの時間を更新
        this._aiTestJob.nowTime = this._nowTime;
        this._soAJobAI.nowTime = this._nowTime;
        this._firstJob.nowTime = this._nowTime;

        // 各AIの処理を実行
        JobHandle handle = this._soAJobAI.Schedule(this._characterCount, this._jobBatchCount);
        handle.Complete();

        JobHandle jobHandle = this._aiTestJob.Schedule(this._characterCount, this._jobBatchCount);
        jobHandle.Complete();

        JobHandle h1 = this._firstJob.Schedule(this._characterCount, this._jobBatchCount);
        JobHandle h2 = this._secondJob.Schedule(this._characterCount, this._jobBatchCount, h1);
        JobHandle h3 = this._thirdJob.Schedule(this._characterCount, this._jobBatchCount, h2);
        h3.Complete();

        // 結果の検証
        int mismatchCount = this.ValidateResults();

        // テスト結果の検証
        Assert.AreEqual(0, mismatchCount, $"{mismatchCount}個の不一致が検出されました");
    }

    /// <summary>
    /// 結果の検証を実行
    /// </summary>
    private int ValidateResults()
    {
        int mismatchCount = 0;

        for ( int i = 0; i < this._characterCount; i++ )
        {
            MovementInfo jobResult = this._judgeResultJob[i];
            MovementInfo soaJobResult = this._judgeResultSoAJob[i];
            MovementInfo splitJobResult = this._judgeResultSplitJob[i];

            bool allMatch =
                jobResult.result == splitJobResult.result &&
                jobResult.actNum == splitJobResult.actNum &&
                jobResult.targetHash == splitJobResult.targetHash;

            if ( !allMatch )
            {
                _ = this._soaData.TryGetIndexByHash(soaJobResult.targetHash, out int index);

                Debug.LogWarning($"要素[{i}] 不一致: " +
                               $"(Job={jobResult.result},{jobResult.actNum},{jobResult.targetHash}) " +
                               $"(SoAJob={soaJobResult.result},{soaJobResult.actNum},{soaJobResult.targetHash})" +
                               $"Jobデバッグ情報{jobResult.GetDebugData()} SoAデバッグ情報{soaJobResult.GetDebugData()}" +
                               $"ターゲット{index}番目 ターゲット初期所属{(int)this._soaStatusList.statusList[1].baseData.initialBelong} 行動番号{(int)this._soaStatusList.brainArray[this._soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.useAttackOrHateNum}" +
                               $"ターゲット{this._soaStatusList.brainArray[this._soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.filter.GetTargetType()}" +
                $"フィルター情報{this._soaStatusList.brainArray[this._soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.filter.DebugIsPassFilter(this._soaData._solidData[index], this._soaData._characterStateInfo[index])}" +
                $"フィルター詳細{this._soaStatusList.brainArray[this._soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.攻撃].behaviorSetting[0].targetCondition.filter.DebugIsPassFilterDetailed(this._soaData._solidData[index], this._soaData._characterStateInfo[index])}" +
                                $"キャラフィルター情報{this._characterData[i].brainData[(int)ActState.攻撃].actCondition[0].targetCondition.filter.DebugIsPassFilter(this._characterData[index])}" +
                $"キャラフィルター詳細{this._characterData[i].brainData[(int)ActState.攻撃].actCondition[0].targetCondition.filter.DebugIsPassFilterWithCharacterInfo(this._characterData[index])}");
                mismatchCount++;

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

        Debug.Log($"結果検証完了: 不一致={mismatchCount}/{this._characterCount}");

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
                yield return this.RecreateTestDataCoroutine(count);

                // SoAJobSystemテスト
                using ( Measure.Scope("SoAJobAI") )
                {
                    JobHandle handle = this._soAJobAI.Schedule(this._characterCount, this._jobBatchCount);
                    handle.Complete();
                }

                yield return null;

                // JobSystemテスト
                using ( Measure.Scope("JobSystemAI") )
                {
                    JobHandle handle = this._aiTestJob.Schedule(this._characterCount, this._jobBatchCount);
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
        this.DisposeTestData();

        // キャラクター数を更新
        this._characterCount = newCharacterCount;

        // 新しいデータを初期化
        yield return this.InitializeTestDataCoroutine();

        Assert.IsTrue(this._dataInitialized, "テストデータの再初期化に失敗しました");

        yield return this.InitializeCharacterDataCoroutine();

        Assert.IsTrue(this._charactersInitialized, "キャラクターデータの再初期化に失敗しました");

        this.InitializeAIInstances();

        Assert.IsTrue(this._aiInstancesInitialized, "AIインスタンスの再初期化に失敗しました");
    }
}

*/