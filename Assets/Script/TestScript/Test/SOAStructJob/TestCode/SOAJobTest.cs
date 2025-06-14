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
/// AITestJob�̃p�t�H�[�}���X�e�X�g
/// </summary>
public class SOAJobTest
{
    // �e�X�g�p�̃f�[�^
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

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("�J�n: OneTimeSetUp");

        // �O��̃e�X�g�f�[�^���c���Ă���ꍇ�͉��
        DisposeTestData();

        // �e�X�g�f�[�^�̏������i���������ɕύX�j
        yield return InitializeTestDataCoroutine();

        if ( !_dataInitialized )
        {
            Assert.Fail("�e�X�g�f�[�^�̏������Ɏ��s���܂���");
        }

        // �L�����N�^�[�f�[�^�̏�����
        yield return InitializeCharacterDataCoroutine();

        if ( !_charactersInitialized )
        {
            Assert.Fail("�L�����N�^�[�f�[�^�̏������Ɏ��s���܂���");
        }

        // AI�C���X�^���X�̏�����
        InitializeAIInstances();

        if ( !_aiInstancesInitialized )
        {
            Assert.Fail("AI�C���X�^���X�̏������Ɏ��s���܂���");
        }

        Debug.Log("����: OneTimeSetUp");
    }

    [TearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("�J�n: OneTimeTearDown");
        DisposeTestData();
        Debug.Log("����: OneTimeTearDown");
    }

    /// <summary>
    /// �e�X�g�f�[�^�̏������i�R���[�`���Łj
    /// </summary>
    private IEnumerator InitializeTestDataCoroutine()
    {
        Debug.Log($"�J�n: InitializeTestData (CharacterCount={_characterCount})");


        // UnsafeList�̏�����
        _characterData = new UnsafeList<CharacterData>(_characterCount, Allocator.Persistent);
        _characterData.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _judgeResultJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
        _judgeResultJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _judgeResultSoAJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
        _judgeResultSoAJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _soaData = new SoACharaDataDic();

        // SoA�p�̃f�[�^��񓯊��Ń��[�h
        var handle = Addressables.LoadAssetAsync<CharacterStatusList>(
            "Assets/Script/TestScript/Test/SOAStructJob/TestData/SoA/SoAList.asset");
        yield return handle;

        try
        {
            if ( handle.Status != AsyncOperationStatus.Succeeded )
            {
                Debug.LogError("SoAList�A�Z�b�g�̃��[�h�Ɏ��s���܂���");
                _dataInitialized = false;
                yield break;
            }

            _soaStatusList = handle.Result;
            _soaStatusList.MakeBrainDataArray();

            // StandardAI�p�̌��ʃ��X�g��������
            _judgeResultStandard = new List<MovementInfo>(_characterCount);
            for ( int i = 0; i < _characterCount; i++ )
            {
                _judgeResultStandard.Add(new MovementInfo());
            }

            // �`�[�����Ƃ̃w�C�g�}�b�v��������
            _teamHate = new NativeHashMap<int2, int>(100, Allocator.Persistent); // ��葽�߂Ɋm��
            _personalHate = new NativeHashMap<int2, int>(5, Allocator.Persistent);


            // �w�c�֌W�}�b�v��������
            _relationMap = new NativeArray<int>(3, Allocator.Persistent);
            InitializeRelationMap();

            _dataInitialized = true;

            // �o�b�`�J�E���g�̍œK��
            OptimizeBatchCount();
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestData�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            _dataInitialized = false;
        }

        Debug.Log("����: InitializeTestData");
    }

    /// <summary>
    /// �w�c�֌W�}�b�v�̏�����
    /// </summary>
    private void InitializeRelationMap()
    {
        for ( int i = 0; i < _relationMap.Length; i++ )
        {
            switch ( (CharacterSide)i )
            {
                case CharacterSide.�v���C���[:
                    _relationMap[i] = 1 << (int)CharacterSide.����;
                    break;
                case CharacterSide.����:
                    _relationMap[i] = 1 << (int)CharacterSide.�v���C���[;
                    break;
                case CharacterSide.���̑�:
                default:
                    _relationMap[i] = 0;
                    break;
            }
        }
    }

    /// <summary>
    /// �o�b�`�J�E���g�̍œK��
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
    /// AI�C���X�^���X�̏�����
    /// </summary>
    private void InitializeAIInstances()
    {
        Debug.Log("�J�n: InitializeAIInstances");

        try
        {
            // �K�v�ȃR���e�i�̏�Ԋm�F
            if ( !ValidateContainerStates() )
            {
                _aiInstancesInitialized = false;
                return;
            }

            // AITestJob�̏�����
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
            // NonJobAI�̏�����
            this._soAJobAI = new SoAJob((characterBaseInfo, characterAtkStatus, characterDefStatus, solidData,
     characterStateInfo, moveStatus, coldLog), _personalHate, _teamHate, _judgeResultSoAJob, this._relationMap, _soaStatusList.brainArray, _nowTime);

            _aiInstancesInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeAIInstances�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            _aiInstancesInitialized = false;
        }

        Debug.Log($"����: InitializeAIInstances (����={_aiInstancesInitialized})");
    }

    /// <summary>
    /// �R���e�i�̏�Ԃ�����
    /// </summary>
    private bool ValidateContainerStates()
    {
        if ( !_teamHate.IsCreated )
        {
            Debug.LogError("teamHate������������Ă��܂���");
            return false;
        }

        if ( !_characterData.IsCreated )
        {
            Debug.LogError("characterData������������Ă��܂���");
            return false;
        }

        if ( !_relationMap.IsCreated )
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
        Debug.Log($"�J�n: InitializeCharacterData (CharacterCount={_characterCount})");

        // �v���n�u�̑��݊m�F
        yield return ValidatePrefabsCoroutine();

        // �I�u�W�F�N�g�̃C���X�^���X��
        yield return InstantiateObjectsCoroutine();

        // �L�����N�^�[�f�[�^�̐���
        yield return GenerateCharacterDataCoroutine();

        _charactersInitialized = true;
        Debug.Log($"����: InitializeCharacterData (����={_charactersInitialized})");
    }

    /// <summary>
    /// �v���n�u�̑��݊m�F
    /// </summary>
    private IEnumerator ValidatePrefabsCoroutine()
    {
        for ( int i = 0; i < _prefabTypes.Length; i++ )
        {
            var checkOp = Addressables.LoadResourceLocationsAsync(_prefabTypes[i]);
            yield return checkOp;

            if ( checkOp.Status != AsyncOperationStatus.Succeeded || checkOp.Result.Count == 0 )
            {
                Debug.LogError($"�v���n�u��������܂���: {_prefabTypes[i]}");
                _charactersInitialized = false;
                yield break;
            }
        }
    }

    /// <summary>
    /// �I�u�W�F�N�g�̃C���X�^���X��
    /// </summary>
    private IEnumerator InstantiateObjectsCoroutine()
    {
        var tasks = new List<AsyncOperationHandle<GameObject>>(_characterCount);

        // �C���X�^���X�����N�G�X�g���J�n
        for ( int i = 0; i < _characterCount; i++ )
        {
            var task = Addressables.InstantiateAsync(_prefabTypes[i % 3]);
            tasks.Add(task);

            // �p�t�H�[�}���X�΍�F100���ƂɃt���[���X�L�b�v
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        // ���ׂẴI�u�W�F�N�g�����������̂�҂�
        foreach ( var task in tasks )
        {
            yield return task;

            if ( task.Status == AsyncOperationStatus.Succeeded )
            {
                _instantiatedObjects.Add(task.Result);
            }
            else
            {
                Debug.LogError("�I�u�W�F�N�g�̃C���X�^���X���Ɏ��s���܂���");
                _charactersInitialized = false;
                yield break;
            }
        }

        Debug.Log($"�I�u�W�F�N�g�̃C���X�^���X������: {_instantiatedObjects.Count}��");
    }

    /// <summary>
    /// �L�����N�^�[�f�[�^�̐���
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

            // �p�t�H�[�}���X�΍�F100���ƂɃt���[���X�L�b�v
            if ( i % 100 == 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"�L�����N�^�[�f�[�^��������: ������={successCount}/{_characterCount}");
        _charactersInitialized = successCount > 0;
    }

    /// <summary>
    /// �P��̃L�����N�^�[����
    /// </summary>
    private bool ProcessSingleCharacter(GameObject obj, int index, ref int successCount)
    {
        try
        {
            var aiComponent = obj.GetComponent<BaseController>();
            if ( aiComponent == null )
            {
                Debug.LogError($"BaseController�R���|�[�l���g��������܂���: {obj.name}");
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

            // �w�C�g�}�b�v�̍X�V
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
                    Debug.LogError($"CharacterData[{i}]�̉�����ɃG���[: {ex.Message}");
                }
            }
            _characterData.Dispose();
        }

        // ���̑���UnsafeList�̉��
        if ( _judgeResultJob.IsCreated )
            _judgeResultJob.Dispose();
        if ( _judgeResultSoAJob.IsCreated )
            _judgeResultSoAJob.Dispose();

        // NativeContainer�̉��
        if ( _teamHate.IsCreated )
            _teamHate.Dispose();
        if ( _relationMap.IsCreated )
            _relationMap.Dispose();

        // SoA�f�[�^�̉��
        _soaData?.Dispose();

        // �������ꂽGameObject�̍폜
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

        // StandardAI�p�̌��ʃ��X�g
        _judgeResultStandard?.Clear();

        // �t���O�����Z�b�g
        _dataInitialized = false;
        _charactersInitialized = false;
        _aiInstancesInitialized = false;
    }

    /// <summary>
    /// SoAJobAI�̃p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void SoAJobAI_Performance_Test()
    {
        // ��������Ԃ̊m�F
        Assert.IsTrue(_aiInstancesInitialized, "AI�C���X�^���X������������Ă��܂���");

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
    /// JobSystemAI�̃p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        // ��������Ԃ̊m�F
        Assert.IsTrue(_aiInstancesInitialized, "AI�C���X�^���X������������Ă��܂���");

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
    /// ���ʂ̌��؃e�X�g
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        // ��������Ԃ̊m�F
        Assert.IsTrue(_aiInstancesInitialized, "AI�C���X�^���X������������Ă��܂���");

        // AI�C���X�^���X�̎��Ԃ��X�V
        _aiTestJob.nowTime = _nowTime;
        _soAJobAI.nowTime = _nowTime;

        // �eAI�̏��������s
        JobHandle soaHandle = _soAJobAI.Schedule(_characterCount, _jobBatchCount);
        soaHandle.Complete();

        JobHandle jobHandle = _aiTestJob.Schedule(_characterCount, _jobBatchCount);
        jobHandle.Complete();

        // ���ʂ̌���
        int mismatchCount = ValidateResults();

        // �e�X�g���ʂ̌���
        Assert.AreEqual(0, mismatchCount, $"{mismatchCount}�̕s��v�����o����܂���");
    }

    /// <summary>
    /// ���ʂ̌��؂����s
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

                Debug.LogWarning($"�v�f[{i}] �s��v: " +
                               $"(Job={jobResult.result},{jobResult.actNum},{jobResult.targetHash}) " +
                               $"(SoAJob={soaJobResult.result},{soaJobResult.actNum},{soaJobResult.targetHash})" +
                               $"Job�f�o�b�O���{jobResult.GetDebugData()} SoA�f�o�b�O���{soaJobResult.GetDebugData()}" +
                               $"�^�[�Q�b�g{index}�Ԗ� �^�[�Q�b�g��������{(int)_soaStatusList.statusList[1].baseData.initialBelong} �s���ԍ�{(int)_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.useAttackOrHateNum}" +
                               $"�^�[�Q�b�g{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.filter.GetTargetType()}" +
                $"�t�B���^�[���{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.filter.DebugIsPassFilter(_soaData._solidData[index], _soaData._characterStateInfo[index])}" +
                $"�t�B���^�[�ڍ�{_soaStatusList.brainArray[_soaData._coldLog[i].characterID - 1].brainSetting[(int)ActState.�U��].behaviorSetting[0].targetCondition.filter.DebugIsPassFilterDetailed(_soaData._solidData[index], _soaData._characterStateInfo[index])}" +
                                $"�L�����t�B���^�[���{_characterData[i].brainData[(int)ActState.�U��].actCondition[0].targetCondition.filter.DebugIsPassFilter(_characterData[index])}" +
                $"�L�����t�B���^�[�ڍ�{_characterData[i].brainData[(int)ActState.�U��].actCondition[0].targetCondition.filter.DebugIsPassFilterWithCharacterInfo(_characterData[index])}");
                mismatchCount++;

                break;

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

        Debug.Log($"���ʌ��؊���: �s��v={mismatchCount}/{_characterCount}");

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
                yield return RecreateTestDataCoroutine(count);

                // SoAJobSystem�e�X�g
                using ( Measure.Scope("SoAJobAI") )
                {
                    JobHandle handle = _soAJobAI.Schedule(_characterCount, _jobBatchCount);
                    handle.Complete();
                }

                yield return null;

                // JobSystem�e�X�g
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
    /// �e�X�g�f�[�^�̍č쐬�i�R���[�`���Łj
    /// </summary>
    private IEnumerator RecreateTestDataCoroutine(int newCharacterCount)
    {
        // ���݂̃f�[�^�����
        DisposeTestData();

        // �L�����N�^�[�����X�V
        _characterCount = newCharacterCount;

        // �V�����f�[�^��������
        yield return InitializeTestDataCoroutine();

        Assert.IsTrue(_dataInitialized, "�e�X�g�f�[�^�̍ď������Ɏ��s���܂���");

        yield return InitializeCharacterDataCoroutine();

        Assert.IsTrue(_charactersInitialized, "�L�����N�^�[�f�[�^�̍ď������Ɏ��s���܂���");

        InitializeAIInstances();

        Assert.IsTrue(_aiInstancesInitialized, "AI�C���X�^���X�̍ď������Ɏ��s���܂���");
    }
}