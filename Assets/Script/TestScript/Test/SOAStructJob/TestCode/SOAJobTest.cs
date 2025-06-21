/*
// �A�Z���u�����x���ݒ�
[assembly: BurstCompile(OptimizeFor = OptimizeFor.Performance)]
/// <summary>
/// AITestJob�̃p�t�H�[�}���X�e�X�g
/// </summary>
public class SOAJobTest
{
    // �e�X�g�p�̃f�[�^
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

    // ��������Ԃ�ǐՂ���t���O
    private bool _dataInitialized = false;
    private bool _charactersInitialized = false;
    private bool _aiInstancesInitialized = false;

    private int _jobBatchCount = 1;

    // �����I�u�W�F�N�g�̔z��
    private readonly string[] _prefabTypes = {
        "Assets/Script/TestScript/Test/SOAStructJob/TestObject/JobTestObjectA.prefab",
        "Assets/Script/TestScript/Test/SOAStructJob/TestObject/JobTestObjectB.prefab",
        "Assets/Script/TestScript/Test/SOAStructJob/TestObject/JobTestObjectC.prefab"
    };

    // �e�X�g�p�̃p�����[�^
    private int _characterCount = 100;
    private float _nowTime = 100.0f;

    // AI�e�X�g�p�̃C���X�^���X
    private JobAI _aiTestJob;
    private SoAJob _soAJobAI;

    private FirstJob _firstJob;
    private SecondJob _secondJob;
    private ThirdJob _thirdJob;

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("�J�n: OneTimeSetUp");

        // �����^�C���ݒ�
        JobsUtility.JobWorkerCount = Mathf.Max(1, SystemInfo.processorCount - 1);

        // �O��̃e�X�g�f�[�^���c���Ă���ꍇ�͉��
        this.DisposeTestData();

        // �e�X�g�f�[�^�̏������i���������ɕύX�j
        yield return this.InitializeTestDataCoroutine();

        if ( !this._dataInitialized )
        {
            Assert.Fail("�e�X�g�f�[�^�̏������Ɏ��s���܂���");
        }

        // �L�����N�^�[�f�[�^�̏�����
        yield return this.InitializeCharacterDataCoroutine();

        if ( !this._charactersInitialized )
        {
            Assert.Fail("�L�����N�^�[�f�[�^�̏������Ɏ��s���܂���");
        }

        // AI�C���X�^���X�̏�����
        this.InitializeAIInstances();

        if ( !this._aiInstancesInitialized )
        {
            Assert.Fail("AI�C���X�^���X�̏������Ɏ��s���܂���");
        }

        Debug.Log("����: OneTimeSetUp");
    }

    [TearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("�J�n: OneTimeTearDown");
        this.DisposeTestData();
        Debug.Log("����: OneTimeTearDown");
    }

    /// <summary>
    /// �e�X�g�f�[�^�̏������i�R���[�`���Łj
    /// </summary>
    private IEnumerator InitializeTestDataCoroutine()
    {
        Debug.Log($"�J�n: InitializeTestData (CharacterCount={this._characterCount})");

        // UnsafeList�̏�����
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

        // SoA�p�̃f�[�^��񓯊��Ń��[�h
        AsyncOperationHandle<CharacterStatusList> handle = Addressables.LoadAssetAsync<CharacterStatusList>(
            "Assets/Script/TestScript/Test/SOAStructJob/TestData/SoA/SoAList.asset");
        yield return handle;

        try
        {
            if ( handle.Status != AsyncOperationStatus.Succeeded )
            {
                Debug.LogError("SoAList�A�Z�b�g�̃��[�h�Ɏ��s���܂���");
                this._dataInitialized = false;
                yield break;
            }

            this._soaStatusList = handle.Result;
            this._soaStatusList.MakeBrainDataArray();

            // StandardAI�p�̌��ʃ��X�g��������
            this._judgeResultStandard = new List<MovementInfo>(this._characterCount);
            for ( int i = 0; i < this._characterCount; i++ )
            {
                this._judgeResultStandard.Add(new MovementInfo());
            }

            // �`�[�����Ƃ̃w�C�g�}�b�v��������
            this._teamHate = new NativeHashMap<int2, int>(100, Allocator.Persistent); // ��葽�߂Ɋm��
            this._personalHate = new NativeHashMap<int2, int>(5, Allocator.Persistent);

            // �w�c�֌W�}�b�v��������
            this._relationMap = new NativeArray<int>(3, Allocator.Persistent);
            this.InitializeRelationMap();

            this._dataInitialized = true;

            // �o�b�`�J�E���g�̍œK��
            this.OptimizeBatchCount();
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestData�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            this._dataInitialized = false;
        }

        Debug.Log("����: InitializeTestData");
    }

    /// <summary>
    /// �w�c�֌W�}�b�v�̏�����
    /// </summary>
    private void InitializeRelationMap()
    {
        for ( int i = 0; i < this._relationMap.Length; i++ )
        {
            this._relationMap[i] = (CharacterSide)i switch
            {
                CharacterSide.�v���C���[ => 1 << (int)CharacterSide.����,
                CharacterSide.���� => 1 << (int)CharacterSide.�v���C���[,
                _ => 0,
            };
        }
    }

    /// <summary>
    /// �o�b�`�J�E���g�̍œK��
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
    /// AI�C���X�^���X�̏�����
    /// </summary>
    private void InitializeAIInstances()
    {
        Debug.Log("�J�n: InitializeAIInstances");

        try
        {
            // �K�v�ȃR���e�i�̏�Ԋm�F
            if ( !this.ValidateContainerStates() )
            {
                this._aiInstancesInitialized = false;
                return;
            }

            // AITestJob�̏�����
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
            // NonJobAI�̏�����
            this._soAJobAI = new SoAJob((characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog), this._personalHate, this._teamHate, this._judgeResultSoAJob, this._relationMap, this._soaStatusList.brainArray, this._nowTime);

            this._firstJob = new FirstJob(this.stateList, coldLog, this._soaStatusList.brainArray, this._nowTime);
            // SplitJobAI�̏�����
            this._secondJob = new SecondJob((characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog), this._personalHate, this._teamHate, this._judgeResultSplitJob, this._relationMap, this._soaStatusList.brainArray, this.selectMoveList, this.stateList);

            this._thirdJob = new ThirdJob((characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog), this._personalHate, this._teamHate, this._judgeResultSplitJob, this._relationMap, this._soaStatusList.brainArray, this.selectMoveList);

            this._aiInstancesInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeAIInstances�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            this._aiInstancesInitialized = false;
        }

        Debug.Log($"����: InitializeAIInstances (����={this._aiInstancesInitialized})");
    }

    /// <summary>
    /// �R���e�i�̏�Ԃ�����
    /// </summary>
    private bool ValidateContainerStates()
    {
        if ( !this._teamHate.IsCreated )
        {
            Debug.LogError("teamHate������������Ă��܂���");
            return false;
        }

        if ( !this._characterData.IsCreated )
        {
            Debug.LogError("characterData������������Ă��܂���");
            return false;
        }

        if ( !this._relationMap.IsCreated )
        {
            Debug.LogError("relationMap������������Ă��܂���");
            return false;
        }

        return true;
    }

    /// <summary>
    /// �L�����N�^�[�f�[�^�̏������i�R���[�`���Łj
    /// </summary>
    private IEnumerator InitializeCharacterDataCoroutine()
    {
        Debug.Log($"�J�n: InitializeCharacterData (CharacterCount={this._characterCount})");

        // �v���n�u�̑��݊m�F
        yield return this.ValidatePrefabsCoroutine();

        // �I�u�W�F�N�g�̃C���X�^���X��
        yield return this.InstantiateObjectsCoroutine();

        // �L�����N�^�[�f�[�^�̐���
        yield return this.GenerateCharacterDataCoroutine();

        this._charactersInitialized = true;
        Debug.Log($"����: InitializeCharacterData (����={this._charactersInitialized})");
    }

    /// <summary>
    /// �v���n�u�̑��݊m�F
    /// </summary>
    private IEnumerator ValidatePrefabsCoroutine()
    {
        for ( int i = 0; i < this._prefabTypes.Length; i++ )
        {
            AsyncOperationHandle<IList<IResourceLocation>> checkOp = Addressables.LoadResourceLocationsAsync(this._prefabTypes[i]);
            yield return checkOp;

            if ( checkOp.Status != AsyncOperationStatus.Succeeded || checkOp.Result.Count == 0 )
            {
                Debug.LogError($"�v���n�u��������܂���: {this._prefabTypes[i]}");
                this._charactersInitialized = false;
                yield break;
            }
        }
    }

    /// <summary>
    /// �I�u�W�F�N�g�̃C���X�^���X��
    /// </summary>
    private IEnumerator InstantiateObjectsCoroutine()
    {
        List<AsyncOperationHandle<GameObject>> tasks = new(this._characterCount);

        // �C���X�^���X�����N�G�X�g���J�n
        for ( int i = 0; i < this._characterCount; i++ )
        {
            AsyncOperationHandle<GameObject> task = Addressables.InstantiateAsync(this._prefabTypes[i % 3]);
            tasks.Add(task);

            // �p�t�H�[�}���X�΍�F100���ƂɃt���[���X�L�b�v
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        // ���ׂẴI�u�W�F�N�g�����������̂�҂�
        foreach ( AsyncOperationHandle<GameObject> task in tasks )
        {
            yield return task;

            if ( task.Status == AsyncOperationStatus.Succeeded )
            {
                this._instantiatedObjects.Add(task.Result);
            }
            else
            {
                Debug.LogError("�I�u�W�F�N�g�̃C���X�^���X���Ɏ��s���܂���");
                this._charactersInitialized = false;
                yield break;
            }
        }

        Debug.Log($"�I�u�W�F�N�g�̃C���X�^���X������: {this._instantiatedObjects.Count}��");
    }

    /// <summary>
    /// �L�����N�^�[�f�[�^�̐���
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

            // �p�t�H�[�}���X�΍�F100���ƂɃt���[���X�L�b�v
            if ( i % 100 == 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"�L�����N�^�[�f�[�^��������: ������={successCount}/{this._characterCount}");
        this._charactersInitialized = successCount > 0;
    }

    /// <summary>
    /// �P��̃L�����N�^�[����
    /// </summary>
    private bool ProcessSingleCharacter(GameObject obj, int index, ref int successCount)
    {
        try
        {
            BaseController aiComponent = obj.GetComponent<BaseController>();
            if ( aiComponent == null )
            {
                Debug.LogError($"BaseController�R���|�[�l���g��������܂���: {obj.name}");
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

            // �w�C�g�}�b�v�̍X�V
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
            Debug.LogError($"�L�����N�^�[�������ɃG���[ (index={index}): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// �e�X�g�f�[�^�̃��������
    /// </summary>
    private void DisposeTestData()
    {
        // �L�����N�^�[�f�[�^�̉��
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
                    Debug.LogError($"CharacterData[{i}]�̉�����ɃG���[: {ex.Message}");
                }
            }

            this._characterData.Dispose();
        }

        // ���̑���UnsafeList�̉��
        if ( this._judgeResultJob.IsCreated )
        {
            this._judgeResultJob.Dispose();
        }

        if ( this._judgeResultSoAJob.IsCreated )
        {
            this._judgeResultSoAJob.Dispose();
        }

        // NativeContainer�̉��
        if ( this._teamHate.IsCreated )
        {
            this._teamHate.Dispose();
        }

        if ( this._relationMap.IsCreated )
        {
            this._relationMap.Dispose();
        }

        // SoA�f�[�^�̉��
        this._soaData?.Dispose();

        // �������ꂽGameObject�̍폜
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

        // StandardAI�p�̌��ʃ��X�g
        this._judgeResultStandard?.Clear();

        // �t���O�����Z�b�g
        this._dataInitialized = false;
        this._charactersInitialized = false;
        this._aiInstancesInitialized = false;
    }

    /// <summary>
    /// SoAJobAI�̃p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void SoAJobAI_Performance_Test()
    {
        // ��������Ԃ̊m�F
        Assert.IsTrue(this._aiInstancesInitialized, "AI�C���X�^���X������������Ă��܂���");

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
    /// SplitJobAI�̃p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void SplitJobAI_Performance_Test()
    {
        // ��������Ԃ̊m�F
        Assert.IsTrue(this._aiInstancesInitialized, "AI�C���X�^���X������������Ă��܂���");

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
    /// JobSystemAI�̃p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        // ��������Ԃ̊m�F
        Assert.IsTrue(this._aiInstancesInitialized, "AI�C���X�^���X������������Ă��܂���");

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
    /// �e�W���u�̃������g�p�ʂ��r����e�X�g
    /// </summary>
    //[Test, Performance]
    public void Compare_Memory_Usage_Between_Jobs()
    {
        // ��������Ԃ̊m�F
        Assert.IsTrue(this._aiInstancesInitialized, "AI�C���X�^���X������������Ă��܂���");

        // �e�X�g�ݒ�
        int characterCount = 10000; // �������g�p�ʂ𑪒肵�₷���傫�߂̃T�C�Y
        int jobBatchCount = 64;

        // AI�C���X�^���X�̎��Ԃ��X�V
        this._aiTestJob.nowTime = this._nowTime;
        this._soAJobAI.nowTime = this._nowTime;
        this._firstJob.nowTime = this._nowTime;

        // ����������p�̃T���v���O���[�v
        SampleGroup memoryResults = new("Memory Usage (MB)", SampleUnit.Megabyte);

        // 1. SoAJobAI�̃������g�p�ʑ���
        long soAJobMemory = this.MeasureJobMemory(() =>
        {
            JobHandle handle = this._soAJobAI.Schedule(characterCount, jobBatchCount);
            handle.Complete();
        }, "SoAJobAI");

        // 2. AITestJob�̃������g�p�ʑ���
        long aiTestJobMemory = this.MeasureJobMemory(() =>
        {
            JobHandle jobHandle = this._aiTestJob.Schedule(characterCount, jobBatchCount);
            jobHandle.Complete();
        }, "AITestJob");

        // 3. �A���W���u�̃������g�p�ʑ���
        long chainedJobsMemory = this.MeasureJobMemory(() =>
        {
            JobHandle h1 = this._firstJob.Schedule(characterCount, jobBatchCount);
            JobHandle h2 = this._secondJob.Schedule(characterCount, jobBatchCount, h1);
            JobHandle h3 = this._thirdJob.Schedule(characterCount, jobBatchCount, h2);
            h3.Complete();
        }, "ChainedJobs (First+Second+Third)");

        // �p�t�H�[�}���X�e�X�g�t���[�����[�N�ւ̋L�^
        Measure.Custom(memoryResults, soAJobMemory / (1024f * 1024f));
    }

    /// <summary>
    /// �W���u�̃������g�p�ʂ𑪒�
    /// </summary>
    private long MeasureJobMemory(System.Action jobExecution, string jobName)
    {
        // GC�����s���ăx�[�X���C�������肳����
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();

        // ����O�̃������g�p��
        long beforeMemory = Profiler.GetTotalAllocatedMemoryLong();

        // �W���u���s
        jobExecution();

        // �����̃������g�p��
        long afterMemory = Profiler.GetTotalAllocatedMemoryLong();
        long usedMemory = afterMemory - beforeMemory;

        Debug.Log($"{jobName} Memory Usage: {usedMemory / (1024f * 1024f):F2} MB");

        return usedMemory;
    }

    /// <summary>
    /// ���ʂ̌��؃e�X�g
    /// </summary>
 //   [Test]
    public void Verify_Results_Are_Same()
    {
        // ��������Ԃ̊m�F
        Assert.IsTrue(this._aiInstancesInitialized, "AI�C���X�^���X������������Ă��܂���");

        // AI�C���X�^���X�̎��Ԃ��X�V
        this._aiTestJob.nowTime = this._nowTime;
        this._soAJobAI.nowTime = this._nowTime;
        this._firstJob.nowTime = this._nowTime;

        // �eAI�̏��������s
        JobHandle handle = this._soAJobAI.Schedule(this._characterCount, this._jobBatchCount);
        handle.Complete();

        JobHandle jobHandle = this._aiTestJob.Schedule(this._characterCount, this._jobBatchCount);
        jobHandle.Complete();

        JobHandle h1 = this._firstJob.Schedule(this._characterCount, this._jobBatchCount);
        JobHandle h2 = this._secondJob.Schedule(this._characterCount, this._jobBatchCount, h1);
        JobHandle h3 = this._thirdJob.Schedule(this._characterCount, this._jobBatchCount, h2);
        h3.Complete();

        // ���ʂ̌���
        int mismatchCount = this.ValidateResults();

        // �e�X�g���ʂ̌���
        Assert.AreEqual(0, mismatchCount, $"{mismatchCount}�̕s��v�����o����܂���");
    }

    /// <summary>
    /// ���ʂ̌��؂����s
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

                Debug.LogWarning($"�v�f[{i}] �s��v: " +
                               $"(Job={jobResult.result},{jobResult.actNum},{jobResult.targetHash}) " +
                               $"(SoAJob={soaJobResult.result},{soaJobResult.actNum},{soaJobResult.targetHash})" +
                               $"Job�f�o�b�O���{jobResult.GetDebugData()} SoA�f�o�b�O���{soaJobResult.GetDebugData()}" +
                               $"�^�[�Q�b�g{index}�Ԗ� �^�[�Q�b�g��������{(int)this._soaStatusList.statusList[1].baseData.initialBelong} �s���ԍ�{(int)this._soaStatusList.brainArray[this._soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.useAttackOrHateNum}" +
                               $"�^�[�Q�b�g{this._soaStatusList.brainArray[this._soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.filter.GetTargetType()}" +
                $"�t�B���^�[���{this._soaStatusList.brainArray[this._soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.filter.DebugIsPassFilter(this._soaData._solidData[index], this._soaData._characterStateInfo[index])}" +
                $"�t�B���^�[�ڍ�{this._soaStatusList.brainArray[this._soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.filter.DebugIsPassFilterDetailed(this._soaData._solidData[index], this._soaData._characterStateInfo[index])}" +
                                $"�L�����t�B���^�[���{this._characterData[i].brainData[(int)ActState.�U��].actCondition[0].targetCondition.filter.DebugIsPassFilter(this._characterData[index])}" +
                $"�L�����t�B���^�[�ڍ�{this._characterData[i].brainData[(int)ActState.�U��].actCondition[0].targetCondition.filter.DebugIsPassFilterWithCharacterInfo(this._characterData[index])}");
                mismatchCount++;

                //int targetType = (int)_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.filter.GetTargetType();

                //Debug.Log($"a{targetType} d{(int)_soaData._characterStateInfo[index].belong} ddd{targetType & (int)_soaData._characterStateInfo[index].belong}num{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.useAttackOrHateNum}");
                //Debug.Log($"a{targetType} d{(int)_soaData._characterStateInfo[index].belong} ddd{targetType & (int)_soaData._characterStateInfo[index].belong}num{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.useAttackOrHateNum}");
            }
            //else
            //{
            //    _soaData.TryGetIndexByHash(soaJobResult.targetHash, out int index);
            //    Debug.LogWarning($"�v�f[{i}] ��v: " +
            //                   $"(Job={jobResult.result},{jobResult.actNum},{jobResult.targetHash}) " +
            //                   $"(SoAJob={soaJobResult.result},{soaJobResult.actNum},{soaJobResult.targetHash})" +
            //                   $"{index}�Ԗ�");
            //}
        }

        Debug.Log($"���ʌ��؊���: �s��v={mismatchCount}/{this._characterCount}");

        //Debug.Log($"�X�e�[�^�X�m�F");
        //Debug.Log($" ���F{_soaStatusList.statusList.Length},{_soaStatusList.statusList[0].brainData.Count},{_soaStatusList.statusList[0].brainData[SOAStatus.ActState.�U��].judgeData.Length}");
        //for ( int i = 0; i < _soaStatusList.brainArray.Length; i++ )
        //{
        //    if ( !_soaStatusList.brainArray[i].brainSetting.ContainsKey((int)ActState.�U��) )
        //    {
        //        continue;
        //    }
        //    SOAStatus.TargetJudgeData actData = _soaStatusList.brainArray[i].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition;
        //    Debug.Log($"belong:{(int)_soaStatusList.statusList[i].baseData.initialBelong == (int)_soaData._characterStateInfo[i].belong && (int)_soaData._characterStateInfo[i].belong == (int)_characterData[i].liveData.belong} judge:{actData.judgeCondition} inv:{actData.isInvert} act:{actData.useAttackOrHateNum} filt:{actData.filter.GetTargetType()} eq:{actData.filter.Equals(_soaStatusList.statusList[0].brainData[SOAStatus.ActState.�U��].judgeData[0].targetCondition.filter)}");
        //}
        return mismatchCount;
    }

    /// <summary>
    /// �قȂ�L�����N�^�[���ł̃p�t�H�[�}���X��r�e�X�g
    /// </summary>
        // [UnityTest, Performance]
    public IEnumerator Compare_Different_Character_Counts()
    {
        int[] characterCounts = { 50, 100, 200 };

        foreach ( int count in characterCounts )
        {
            using ( Measure.Scope($"Character Count: {count}") )
            {
                // �e�X�g�f�[�^�̍č쐬
                yield return this.RecreateTestDataCoroutine(count);

                // SoAJobSystem�e�X�g
                using ( Measure.Scope("SoAJobAI") )
                {
                    JobHandle handle = this._soAJobAI.Schedule(this._characterCount, this._jobBatchCount);
                    handle.Complete();
                }

                yield return null;

                // JobSystem�e�X�g
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
    /// �e�X�g�f�[�^�̍č쐬�i�R���[�`���Łj
    /// </summary>
    private IEnumerator RecreateTestDataCoroutine(int newCharacterCount)
    {
        // ���݂̃f�[�^�����
        this.DisposeTestData();

        // �L�����N�^�[�����X�V
        this._characterCount = newCharacterCount;

        // �V�����f�[�^��������
        yield return this.InitializeTestDataCoroutine();

        Assert.IsTrue(this._dataInitialized, "�e�X�g�f�[�^�̍ď������Ɏ��s���܂���");

        yield return this.InitializeCharacterDataCoroutine();

        Assert.IsTrue(this._charactersInitialized, "�L�����N�^�[�f�[�^�̍ď������Ɏ��s���܂���");

        this.InitializeAIInstances();

        Assert.IsTrue(this._aiInstancesInitialized, "AI�C���X�^���X�̍ď������Ɏ��s���܂���");
    }
}

*/