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
/// AITestJob�̃p�t�H�[�}���X�e�X�g
/// </summary>
public class SOAJobTest
{
    // �e�X�g�p�̃f�[�^
    private UnsafeList<CharacterData> _characterData;
    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultNonJob;
    private List<MovementInfo> _judgeResultStandard; // StandardAI�p�̌��ʃ��X�g
    private NativeHashMap<int2, int> _teamHate;
    private NativeArray<int> _relationMap;

    // ��������Ԃ�ǐՂ���t���O
    private bool _dataInitialized = false;
    private bool _charactersInitialized = false;
    private bool _aiInstancesInitialized = false;

    private int jobBatchCount = 1;

    /// <summary>
    /// 
    /// </summary>
    private BaseController[] characters;

    /// <summary>
    /// �����I�u�W�F�N�g�̔z��B
    /// </summary>
    private string[] types = new string[] { "Assets/Prefab/JobAI/TypeA.prefab", "Assets/Prefab/JobAI/TypeB.prefab", "Assets/Prefab/JobAI/TypeC.prefab" };

    // �e�X�g�p�̃p�����[�^
    private int _characterCount = 300;
    private float _nowTime = 100.0f;

    // AI�e�X�g�p�̃C���X�^���X
    private JobAI _aiTestJob;

    // SOA�p��Job��

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("�J�n: OneTimeSetUp");

        // �e�X�g�f�[�^�̏�����
        try
        {
            this.InitializeTestData();

        }
        catch ( Exception ex )
        {
            Debug.LogError($"�e�X�g�f�[�^���������̃G���[: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        // �L�����N�^�[�f�[�^�̏����� - IEnumerator�Ȃ̂� yield return����
        yield return this.InitializeCharacterData();

        if ( !this._charactersInitialized )
        {
            Debug.LogError("�L�����N�^�[�f�[�^�̏������Ɏ��s���܂���");
            yield break;
        }

        Debug.Log($"�L�����N�^�[�f�[�^�̏���������: characterData.Length={this._characterData.Length}");

        // teamHate�̒��g���m�F
        //foreach ( var item in _teamHate )
        //{
        //    Debug.Log($" teamHate.�L�[={item.Key}");
        //}

        // AI�C���X�^���X�̏�����
        try
        {
            this.InitializeAIInstances();
            Debug.Log("AI�C���X�^���X�̏���������");
        }
        catch ( Exception ex )
        {
            Debug.LogError($"AI�C���X�^���X���������̃G���[: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        Debug.Log("����: OneTimeSetUp");
    }

    [TearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("�J�n: OneTimeTearDown");
        // ���������\�[�X�̉��
        this.DisposeTestData();
        Debug.Log("����: OneTimeTearDown");
    }

    /// <summary>
    /// �e�X�g�f�[�^�̏�����
    /// </summary>
    private void InitializeTestData()
    {
        Debug.Log($"�J�n: InitializeTestData (CharacterCount={this._characterCount})");
        try
        {
            // UnsafeList�̏�����
            this._characterData = new UnsafeList<CharacterData>(this._characterCount, Allocator.Persistent);
            this._characterData.Resize(this._characterCount, NativeArrayOptions.ClearMemory);
            Debug.Log($"_characterData����������: Length={this._characterData.Length}, IsCreated={this._characterData.IsCreated}");

            this._judgeResultJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
            this._judgeResultJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

            this._judgeResultNonJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
            this._judgeResultNonJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

            // StandardAI�p�̌��ʃ��X�g��������
            this._judgeResultStandard = new List<MovementInfo>(this._characterCount);
            for ( int i = 0; i < this._characterCount; i++ )
            {
                this._judgeResultStandard.Add(new MovementInfo());
            }

            // �`�[�����Ƃ̃w�C�g�}�b�v��������
            this._teamHate = new NativeHashMap<int2, int>(3, Allocator.Persistent);
            Debug.Log($"_teamHate�z�񏉊�������: Length={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");

            // �w�c�֌W�}�b�v��������
            this._relationMap = new NativeArray<int>(3, Allocator.Persistent);

            for ( int i = 0; i < this._relationMap.Length; i++ )
            {
                // �v���C���[�͓G�ɓG�΁A�G�̓v���C���[�ɓG�΁A���͒����Ȃ�
                switch ( (CharacterSide)i )
                {
                    case CharacterSide.�v���C���[:
                        this._relationMap[i] = 1 << (int)CharacterSide.����;  // �v���C���[�͓G�ɓG��
                        break;
                    case CharacterSide.����:
                        this._relationMap[i] = 1 << (int)CharacterSide.�v���C���[;  // �G�̓v���C���[�ɓG��
                        break;
                    case CharacterSide.���̑�:
                    default:
                        this._relationMap[i] = 0;  // �����͒N�ɂ��G�΂��Ȃ�
                        break;
                }
            }

            this._dataInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestData�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            this._dataInitialized = false;
        }

        Debug.Log("����: InitializeTestData");

        // �o�b�`�J�E���g�̍œK��
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
        else // 513�`1000
        {
            this.jobBatchCount = 128;
        }

    }

    /// <summary>
    /// AI�C���X�^���X�̏�����
    /// </summary>
    private void InitializeAIInstances()
    {
        Debug.Log("�J�n: InitializeAIInstances");
        try
        {
            // �e�R���e�i�̏�Ԋm�F
            if ( !this._teamHate.IsCreated )
            {
                Debug.LogError("teamHate������������Ă��܂���");
                return;
            }

            if ( !this._characterData.IsCreated )
            {
                Debug.LogError("characterData������������Ă��܂���");
                return;
            }

            if ( !this._relationMap.IsCreated )
            {
                Debug.LogError("relationMap������������Ă��܂���");
                return;
            }

            // �`�[���w�C�g�̊e�v�f���m�F
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                if ( !this._teamHate.IsCreated )
                {
                    Debug.LogError($"teamHate[{i}]������������Ă��܂���");
                    return;
                }
                //Debug.Log($"teamHate[{i}].Count={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");
            }

            // AITestJob�̏�����
            this._aiTestJob = new AITestJob
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap,
            };

            // NonJobAI�̏�����
            this._nonJobAI = new NonJobAI
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultNonJob,
                relationMap = this._relationMap
            };

            // StandardAI�̏������iNativeContainer����f�[�^���R�s�[�j
            this._standardAI = new StandardAI(this._teamHate, this._characterData, this._nowTime, this._relationMap);
            this._standardAI.judgeResult = this._judgeResultStandard;

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
    /// �L�����N�^�[�f�[�^�̏�����
    /// </summary>
    private IEnumerator InitializeCharacterData()
    {
        Debug.Log($"�J�n: InitializeCharacterData (CharacterCount={this._characterCount})");

        // _characterData������������Ă��邩�m�F
        if ( !this._characterData.IsCreated )
        {
            Debug.LogError("_characterData������������Ă��܂���");
            this._charactersInitialized = false;
            yield break;
        }

        // _teamHate������������Ă��邩�m�F
        if ( !this._teamHate.IsCreated )
        {
            Debug.LogError("_teamHate������������Ă��܂���");
            this._charactersInitialized = false;
            yield break;
        }

        // �v���n�u�̑��݊m�F
        bool allPrefabsValid = true;
        for ( int i = 0; i < this.types.Length; i++ )
        {
            AsyncOperationHandle<IList<IResourceLocation>> checkOp = Addressables.LoadResourceLocationsAsync(this.types[i]);
            yield return checkOp;

            if ( checkOp.Result.Count == 0 )
            {
                Debug.LogError($"�v���n�u��������܂���: {this.types[i]}");
                allPrefabsValid = false;
            }
            else
            {
                Debug.Log($"�v���n�u���m�F: {this.types[i]}");
            }
        }

        if ( !allPrefabsValid )
        {
            Debug.LogError("�ꕔ�̃v���n�u��������܂���ł���");
            this._charactersInitialized = false;
            yield break;
        }

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(this._characterCount);

        for ( int i = 0; i < this._characterCount; i++ )
        {
            // Addressables���g�p���ăC���X�^���X��
            var task = Addressables.InstantiateAsync(this.types[i % 3]);
            tasks.Add(task);

            // 100���ƂɃt���[���X�L�b�v�i�p�t�H�[�}���X�΍�j
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"�C���X�^���X�����N�G�X�g����: {tasks.Count}�� {this._characterCount}");

        // ���ׂẴI�u�W�F�N�g�����������̂�҂�
        bool allTasksCompleted = true;
        foreach ( var task in tasks )
        {
            yield return task;
            if ( task.Status != AsyncOperationStatus.Succeeded )
            {
                allTasksCompleted = false;
                Debug.LogError("�I�u�W�F�N�g�̃C���X�^���X���Ɏ��s���܂���");
            }
        }

        if ( !allTasksCompleted )
        {
            Debug.LogError("�ꕔ�̃I�u�W�F�N�g�̃C���X�^���X���Ɏ��s���܂���");
            this._charactersInitialized = false;
            yield break;
        }

        Debug.Log("�S�I�u�W�F�N�g�̃C���X�^���X������");

        // �`�F�b�N�|�C���g�F_teamHate�̏�Ԃ��m�F
        for ( int i = 0; i < this._teamHate.Count; i++ )
        {
            Debug.Log($"�L�����������_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
        }

        // �������ꂽ�I�u�W�F�N�g�ƕK�v�ȃR���|�[�l���g���擾
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
                Debug.LogError($"�I�u�W�F�N�g�擾���ɃG���[: {ex.Message}");
                continue;
            }

            if ( obj == null )
            {
                Debug.LogError($"�C���f�b�N�X{i}�̃I�u�W�F�N�g��null�ł�");
                continue;
            }

            var aiComponent = obj.GetComponent<BaseController>();
            if ( aiComponent == null )
            {
                Debug.LogError($"BaseController�R���|�[�l���g��������܂���: {obj.name}");
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
                    // �s�����f�f�[�^
                    Debug.Log($"  �� �s�����f�f�[�^�Ȃ��I�I");
                }
            }
            catch ( Exception ex )
            {
                Debug.LogError($"�f�[�^�������ɃG���[: {ex.Message}");
                continue;
            }

            successCount++;

            // �w�C�g�}�b�v��������
            int teamNum = (int)data.liveData.belong;

            if ( !this._teamHate.IsCreated )
            {
                Debug.LogError($"_teamHate[{teamNum}]�������ł�");
                continue;
            }

            int2 hateKey = new(teamNum, data.hashCode);

            try
            {
                if ( this._teamHate.ContainsKey(hateKey) )
                {
                    Debug.LogWarning($"�d������hashCode: {data.hashCode} (�`�[��: {teamNum})");
                    continue;
                }

                this._teamHate.Add(hateKey, 10);
            }
            catch ( Exception ex )
            {
                Debug.LogError($"�w�C�g�}�b�v�X�V���ɃG���[: {ex.Message}");
                continue;
            }

            // 100���ƂɃ��O
            if ( i % 100 == 0 )
            {
                Debug.Log($"�L�����N�^�[�������i��: {i}/{tasks.Count}, teamHate[{teamNum}].Count={this._teamHate.Count}");
            }
        }

        Debug.Log($"�L�����N�^�[�f�[�^����������: ������={successCount}/{tasks.Count}");

        //// �eteamHate�̍ŏI��Ԃ��m�F
        //for ( int i = 0; i < _teamHate.Count; i++ )
        //{
        //    Debug.Log($"�ŏI_teamHate[{i}]: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
        //}

        // �L�����N�^�[�f�[�^�̃X�e�[�^�X�������_����
        Debug.Log("�L�����N�^�[�f�[�^�̃����_�������J�n");

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
                Debug.LogError($"�f�[�^�����_�������ɃG���[ (index={i}): {ex.Message}");
            }

            // 100���ƂɃt���[���X�L�b�v
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log("�L�����N�^�[�f�[�^�̃����_��������");

        this._charactersInitialized = successCount > 0;
        Debug.Log($"����: InitializeCharacterData (����={this._charactersInitialized})");
    }

    public static void DebugPrintCharacterData(CharacterData data)
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("===== CharacterData�ڍ׏�� =====");
        _ = sb.AppendLine($"�n�b�V���R�[�h: {data.hashCode}");

        // ��{���
        _ = sb.AppendLine($"�ŏI���f����: {data.lastJudgeTime}");
        _ = sb.AppendLine($"�ŏI�ړ����f����: {data.lastMoveJudgeTime}");
        _ = sb.AppendLine($"�ړ����f�Ԋu: {data.moveJudgeInterval}");
        _ = sb.AppendLine($"�^�[�Q�b�g��: {data.targetingCount}");

        // ���C�u�f�[�^
        _ = sb.AppendLine("�yLiveData���z");
        _ = sb.AppendLine($"  ���݈ʒu: {data.liveData.nowPosition}");
        _ = sb.AppendLine($"  ����HP: {data.liveData.currentHp}/{data.liveData.maxHp}");
        _ = sb.AppendLine($"  ����: {data.liveData.belong}");
        _ = sb.AppendLine($"  ���: {data.liveData.actState}");
        // ����liveData�t�B�[���h���K�v�ɉ����Ēǉ�

        // BrainData���
        _ = sb.AppendLine("�yBrainData���z");
        if ( data.brainData.IsCreated )
        {
            _ = sb.AppendLine($"  �o�^��: {data.brainData.Count}");
            var keys = data.brainData.GetKeyArray(Allocator.Temp);
            try
            {
                foreach ( var key in keys )
                {
                    if ( data.brainData.TryGetValue(key, out var brainStatus) )
                    {
                        _ = sb.AppendLine($"  ���[�h[{key}]:");
                        _ = sb.AppendLine($"    ���f�Ԋu: {brainStatus.judgeInterval}");
                        // ����brainStatus�t�B�[���h���K�v�ɉ����Ēǉ�
                    }
                }

                keys.Dispose();
            }
            catch ( Exception ex )
            {
                _ = sb.AppendLine($"  BrainData�A�N�Z�X���ɃG���[: {ex.Message}");
                if ( keys.IsCreated )
                {
                    keys.Dispose();
                }
            }
        }
        else
        {
            _ = sb.AppendLine("  BrainData�͍쐬����Ă��܂���");
        }

        // �l�w�C�g���
        _ = sb.AppendLine("�yPersonalHate���z");
        if ( data.personalHate.IsCreated )
        {
            _ = sb.AppendLine($"  �o�^��: {data.personalHate.Count}");
            var hateKeys = data.personalHate.GetKeyArray(Allocator.Temp);
            try
            {
                foreach ( var target in hateKeys )
                {
                    if ( data.personalHate.TryGetValue(target, out var hateValue) )
                    {
                        _ = sb.AppendLine($"  �Ώ�[{target}]: �w�C�g�l={hateValue}");
                    }
                }

                hateKeys.Dispose();
            }
            catch ( Exception ex )
            {
                _ = sb.AppendLine($"  PersonalHate�A�N�Z�X���ɃG���[: {ex.Message}");
                if ( hateKeys.IsCreated )
                {
                    hateKeys.Dispose();
                }
            }
        }
        else
        {
            _ = sb.AppendLine("  PersonalHate�͍쐬����Ă��܂���");
        }

        // �ߋ����L�����N�^�[���
        _ = sb.AppendLine("�yShortRangeCharacter���z");
        if ( data.shortRangeCharacter.IsCreated )
        {
            _ = sb.AppendLine($"  �o�^��: {data.shortRangeCharacter.Length}");
            try
            {
                for ( int i = 0; i < data.shortRangeCharacter.Length; i++ )
                {
                    _ = sb.AppendLine($"  �ߋ����L����[{i}]: Hash={data.shortRangeCharacter[i]}");
                }
            }
            catch ( Exception ex )
            {
                _ = sb.AppendLine($"  ShortRangeCharacter�A�N�Z�X���ɃG���[: {ex.Message}");
            }
        }
        else
        {
            _ = sb.AppendLine("  ShortRangeCharacter�͍쐬����Ă��܂���");
        }

        _ = sb.AppendLine("============================");

        // �������O�𕪊����ďo�́iUnity console�̕�������������j
        const int maxLogLength = 1000;
        for ( int i = 0; i < sb.Length; i += maxLogLength )
        {
            int length = Math.Min(maxLogLength, sb.Length - i);
            Debug.Log(sb.ToString(i, length));
        }
    }

    /// <summary>
    /// �e�X�g�f�[�^�̃��������
    /// </summary>
    private void DisposeTestData()
    {
        // UnsafeList�̉��
        if ( this._characterData.IsCreated )
        {
            // �e�L�����N�^�[�f�[�^���̃l�C�e�B�u�R���e�i�����
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

        // StandardAI�p�̌��ʃ��X�g�͊Ǘ����ꂽ�I�u�W�F�N�g�Ȃ̂�GC����������
        this._judgeResultStandard = null;

        // �`�[���w�C�g�}�b�v�̉��
        if ( this._teamHate.IsCreated )
        {
            this._teamHate.Dispose();
        }

        // ���̑���NativeArray�̉��
        if ( this._relationMap.IsCreated )
        {
            this._relationMap.Dispose();
        }
    }

    /// <summary>
    /// ��JobSystem��AI�����p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void NonJobAI_Performance_Test()
    {
        Debug.Log($"�e�X�g�f�[�^�̏���������: teamHate.IsCreated={this._teamHate.IsCreated}, Length={this._teamHate.Count}");

        Measure.Method(() =>
        {
            // ��JobSystem��AI�������s
            this._nonJobAI.ExecuteAIDecision();
        })
        .WarmupCount(3)       // �E�H�[���A�b�v��
        .MeasurementCount(10) // �v����
        .IterationsPerMeasurement(1) // 1��̌v��������̎��s��
                                     // .GC()                 // GC�̌v�����s��
        .Run();
    }

    /// <summary>
    /// StandardAI�̃p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void StandardAI_Performance_Test()
    {
        Debug.Log($"StandardAI�e�X�g�J�n: characterData.Count={this._standardAI.characterData.Count}");

        Measure.Method(() =>
        {
            // StandardAI�̏������s
            this._standardAI.ExecuteAIDecision();
        })
        .WarmupCount(3)       // �E�H�[���A�b�v��
        .MeasurementCount(10) // �v����
        .IterationsPerMeasurement(1) // 1��̌v��������̎��s��
                                     // .GC()                 // GC�̌v�����s��
        .Run();
    }

    /// <summary>
    /// JobSystem��AI�����p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        Debug.Log($"�e�X�g�f�[�^�̏���������: teamHate.IsCreated={this._teamHate.IsCreated}, Length={this._teamHate.Count}");

        Measure.Method(() =>
        {
            // JobSystem��AI�������s
            JobHandle handle = this._aiTestJob.Schedule(this._characterCount, this.jobBatchCount);
            handle.Complete();
        })
        .WarmupCount(3)       // �E�H�[���A�b�v��
        .MeasurementCount(10) // �v����
        .IterationsPerMeasurement(1) // 1��̌v��������̎��s��
        //.GC()                 // GC�̌v�����s��
        .Run();
    }

    /// <summary>
    /// �e�X�g�f�[�^�̍č쐬�i�L�����N�^�[���ύX���j
    /// </summary>
    private async UniTask RecreateTestData(int newCharacterCount)
    {
        // ���݂̃f�[�^�����
        this.DisposeTestData();

        // �L�����N�^�[�����X�V
        this._characterCount = newCharacterCount;

        // �V�����f�[�^�����������Ċ�����ҋ@
        this.InitializeTestData();

        Debug.Log($"�e�X�g�f�[�^�̏���������: teamHate.IsCreated={this._teamHate.IsCreated}, Length={this._teamHate.Count}");

        // �L�����N�^�[�f�[�^�̏�����
        await this.InitializeCharacterData();

        // AI�C���X�^���X�̏�����
        this.InitializeAIInstances();
    }

    /// <summary>
    /// ���ʂ̌��؃e�X�g�i�S�������������ʂ��o�����m�F�j
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        Debug.Log("�����_�����O�̃f�[�^ ===================");
        this.PrintAllCharacterData("�������");

        //// �f�[�^�������_����
        //for ( int i = 0; i < _characterData.Length; i++ )
        //{
        //    CharacterData data = _characterData[i];
        //    CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
        //    _characterData[i] = data;
        //}

        //    Debug.Log("�����_������̃f�[�^ ===================");
        //   PrintAllCharacterData("�����_������");

        // StandardAI�̏������iNativeContainer����f�[�^���R�s�[�j
        this._standardAI = new StandardAI(this._teamHate, this._characterData, this._nowTime, this._relationMap);
        this._standardAI.judgeResult = this._judgeResultStandard;

        // AI�C���X�^���X�̎��Ԃ��X�V
        this._aiTestJob.nowTime = this._nowTime;
        this._nonJobAI.nowTime = this._nowTime;
        this._standardAI.nowTime = this._nowTime;

        // �eAI�̏��������s
        this._nonJobAI.ExecuteAIDecision();
        this._standardAI.ExecuteAIDecision();

        // JobSystem��AI�������s
        JobHandle handle = this._aiTestJob.Schedule(this._characterCount, this.jobBatchCount);
        handle.Complete();

        // �S�v�f�����؂��Č��ʂ��o��
        Debug.Log("���ʌ��؊J�n ===================");
        int mismatchCount = 0;

        for ( int i = 0; i < this._characterCount; i++ )
        {
            MovementInfo jobResult = this._judgeResultJob[i];
            MovementInfo nonJobResult = this._judgeResultNonJob[i];
            MovementInfo standardResult = this._judgeResultStandard[i];

            // 3�̌��ʂ��S�Ĉ�v���邩�m�F
            bool allMatch =
                jobResult.result == nonJobResult.result &&
                jobResult.result == standardResult.result &&
                jobResult.actNum == nonJobResult.actNum &&
                jobResult.actNum == standardResult.actNum &&
                jobResult.targetHash == nonJobResult.targetHash &&
                jobResult.targetHash == standardResult.targetHash;

            if ( allMatch )
            {
                Debug.Log($"�v�f[{i}] ��v: (����={jobResult.result}, �s��={jobResult.actNum}, �^�[�Q�b�g={jobResult.targetHash})");
            }
            else
            {
                Debug.LogWarning($"�v�f[{i}] �s��v: (Job={jobResult.result},{jobResult.actNum},{jobResult.targetHash}) " +
                               $"(NonJob={nonJobResult.result},{nonJobResult.actNum},{nonJobResult.targetHash}) " +
                               $"(Standard={standardResult.result},{standardResult.actNum},{standardResult.targetHash})");
                mismatchCount++;
            }
        }

        // �S�̂̌��ʂ��o��
        if ( mismatchCount == 0 )
        {
            Debug.Log("�S�v�f���؊���: ���ׂĈ�v���Ă��܂�");
        }
        else
        {
            Debug.LogError($"�S�v�f���؊���: {mismatchCount}�̗v�f�ŕs��v��������܂���");
        }

        Debug.Log("���ʌ��؏I�� ===================");

        // �e�X�g���ʂ̌��؁i�K�v�ɉ����ăR�����g�A�E�g�\�j
        Assert.AreEqual(0, mismatchCount, $"{mismatchCount}�̕s��v�����o����܂���");
    }

    /// <summary>
    /// ���ׂĂ�CharacterData�̓��e���ڍׂɏo�͂���
    /// </summary>
    /// <param name="label">�o�͎��̃��x��</param>
    private void PrintAllCharacterData(string label)
    {
        Debug.Log($"===== {label} - CharacterData�ꗗ�i�S{this._characterCount}���j=====");

        for ( int i = 0; i < this._characterCount; i++ )
        {
            CharacterData data = this._characterData[i];
            Debug.Log($"CharacterData[{i}] hashCode: {data.hashCode}");

            // ��{���
            Debug.Log($"  �� ��{���:");
            Debug.Log($"    - ����: {data.liveData.belong}");
            Debug.Log($"    - �s�����: {data.liveData.actState}");
            Debug.Log($"    - �ŏI���f����: {data.lastJudgeTime}");

            // �ʒu���
            Debug.Log($"  �� �ʒu���:");
            Debug.Log($"    - ���݈ʒu: ({data.liveData.nowPosition.x}, {data.liveData.nowPosition.y})");

            // HP/MP���
            Debug.Log($"  �� �X�e�[�^�X���:");
            Debug.Log($"    - HP: {data.liveData.currentHp}/{data.liveData.maxHp} ({data.liveData.hpRatio}%)");
            Debug.Log($"    - MP: {data.liveData.currentMp}/{data.liveData.maxMp} ({data.liveData.mpRatio}%)");

            // �U��/�h����
            Debug.Log($"  �� �U���\��:");
            Debug.Log($"    - �\���U����: {data.liveData.dispAtk}");
            Debug.Log($"    - �a/�h/��: {data.liveData.atk.slash}/{data.liveData.atk.pierce}/{data.liveData.atk.strike}");
            Debug.Log($"    - ��/��/��/��: {data.liveData.atk.fire}/{data.liveData.atk.lightning}/{data.liveData.atk.light}/{data.liveData.atk.dark}");

            Debug.Log($"  �� �h��\��:");
            Debug.Log($"    - �\���h���: {data.liveData.dispDef}");
            Debug.Log($"    - �a/�h/��: {data.liveData.def.slash}/{data.liveData.def.pierce}/{data.liveData.def.strike}");
            Debug.Log($"    - ��/��/��/��: {data.liveData.def.fire}/{data.liveData.def.lightning}/{data.liveData.def.light}/{data.liveData.def.dark}");

            // �U������
            Debug.Log($"  �� �U������: {data.solidData.attackElement}");

            // �^�[�Q�b�g���
            Debug.Log($"  �� �^�[�Q�b�g���:");
            Debug.Log($"    - �^�[�Q�b�g��: {data.targetingCount}");

            // �s�����f�f�[�^
            Debug.Log($"  �� �s�����f�f�[�^:");

            if ( data.brainData.Count == 0 )
            {
                // �s�����f�f�[�^
                Debug.Log($"  �� �s�����f�f�[�^�Ȃ��I�I");
            }

            for ( int j = 0; j < 8; j++ )
            {

                int key = 1 << j;

                if ( !data.brainData.ContainsKey(key) )
                {
                    continue;
                }

                var brain = data.brainData[key];
                Debug.Log($"    - �s�����[�h[{(ActState)key}]:");
                Debug.Log($"      ���f�Ԋu: {brain.judgeInterval}");

                // �s�������̕\��
                Debug.Log($"      �s��������: {brain.actCondition.Length}");
                for ( int k = 0; k < brain.actCondition.Length; k++ )
                {
                    var condition = brain.actCondition[k];
                    Debug.Log($"      ����[{k}]: {condition.actCondition.judgeCondition}, �l: {condition.actCondition.judgeValue}, ���]: {condition.actCondition.isInvert}");
                    Debug.Log($"        �^�[�Q�b�g����: {condition.targetCondition.judgeCondition}, ���]: {condition.targetCondition.isInvert}");
                }
            }

            Debug.Log("-------------------------------------");
        }

        Debug.Log($"===== {label} - CharacterData�ꗗ �I�� =====");
    }

    /// <summary>
    /// 3��ނ�AI�������r���؂���e�X�g
    /// </summary>
//    [Test, Performance]
    public void Compare_Three_AI_Implementations()
    {
        // �f�[�^�������_����
        for ( int i = 0; i < this._characterData.Length; i++ )
        {
            CharacterData data = this._characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
            this._characterData[i] = data;
        }

        // ���Ԃ��X�V�i�SAI�ɓ������Ԃ�ݒ�j
        float testTime = 200.0f; // �e�X�g�p�̎���
        this._aiTestJob.nowTime = testTime;
        this._nonJobAI.nowTime = testTime;
        this._standardAI.nowTime = testTime;

        // �eAI�̏��������s���A�p�t�H�[�}���X�𑪒�
        using ( Measure.Scope("JobSystemAI���s����") )
        {
            JobHandle handle = this._aiTestJob.Schedule(this._characterCount, this.jobBatchCount);
            handle.Complete();
        }

        using ( Measure.Scope("NonJobAI���s����") )
        {
            this._nonJobAI.ExecuteAIDecision();
        }

        using ( Measure.Scope("StandardAI���s����") )
        {
            this._standardAI.ExecuteAIDecision();
        }

        // ���ʌ��ؗp�̃��O���o��
        int matchCount = 0;
        int mismatchCount = 0;

        for ( int i = 0; i < Math.Min(5, this._characterCount); i++ )
        {
            // �eAI�̌��ʂ��擾
            MovementInfo jobResult = this._judgeResultJob[i];
            MovementInfo nonJobResult = this._judgeResultNonJob[i];
            MovementInfo standardResult = this._judgeResultStandard[i];

            // ���ʂ���v���邩�m�F
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
                Debug.LogWarning($"���ʕs��v(index={i}):\n" +
                                $"Job: {jobResult.result}, {jobResult.actNum}, {jobResult.targetHash}\n" +
                                $"NonJob: {nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}\n" +
                                $"Standard: {standardResult.result}, {standardResult.actNum}, {standardResult.targetHash}");
            }
        }

        Debug.Log($"�T���v�����ʔ�r: ��v={matchCount}, �s��v={mismatchCount}");

        // ���؁i���ׂĂ̎����Ō��ʂ���v���邱�Ƃ��m�F�j
        Assert.AreEqual(0, mismatchCount, "�قȂ�����ԂŌ��ʂ���v���܂���");
    }

    /// <summary>
    /// �قȂ�L�����N�^�[���ł̃p�t�H�[�}���X��r�e�X�g
    /// </summary>
 //   [UnityTest, Performance]
    public IEnumerator Compare_Different_Character_Counts()
    {
        // �e�X�g�p�̃L�����N�^�[���̔z��
        int[] characterCounts = { 10, 50, 100 };

        foreach ( int count in characterCounts )
        {
            // �e�X�g�P�[�X����ݒ�
            using ( Measure.Scope($"Character Count: {count}") )
            {
                // �L�����N�^�[���̍X�V�ƍď�����
                UniTask recreateTask = this.RecreateTestData(count);

                // UniTask�̊�����ҋ@
                while ( !recreateTask.Status.IsCompleted() )
                {
                    yield return null;
                }

                // ��JobSystem�e�X�g
                using ( Measure.Scope("NonJobAI") )
                {
                    this._nonJobAI.ExecuteAIDecision();
                }

                // StandardAI�e�X�g
                using ( Measure.Scope("StandardAI") )
                {
                    this._standardAI.ExecuteAIDecision();
                }

                // �t���[���X�L�b�v
                yield return null;

                // JobSystem�e�X�g
                using ( Measure.Scope("JobSystemAI") )
                {
                    JobHandle handle = this._aiTestJob.Schedule(count, 64);
                    handle.Complete();
                }
            }

            // ���̃e�X�g�̑O�Ƀt���[�����X�L�b�v
            yield return null;
        }
    }
}