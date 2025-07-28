using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static global::TestScript.SOATest.SOAStatus;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace SplitJob
{
    /// <summary>
    /// AI�����f���s��Job
    /// ���f�\���ǂ���������B
    /// �ŏI�I�ɂ͔��f�Ԋu�ɉ����N�[���^�C����������B
    /// </summary>
    [BurstCompile(
        FloatPrecision = FloatPrecision.Medium,
        FloatMode = FloatMode.Fast,
        DisableSafetyChecks = true,
        OptimizeFor = OptimizeFor.Performance
    )]
    public struct FirstJob : IJobParallelFor
    {
        /// <summary>
        /// �Q�ƕp�x�̒Ⴂ�f�[�^
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharaColdLog> _coldLog;

        /// <summary>
        /// ���ݎ���
        /// </summary>
        [ReadOnly]
        public float nowTime;

        [WriteOnly]
        public UnsafeList<int> stateList;

        /// <summary>
        /// �L������AI�̐ݒ�B
        /// ��ԂɊ�Â��čŏ��Ƀf�[�^������������B
        /// </summary>
        [ReadOnly]
        public NativeArray<BrainDataForJob> brainArray;

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        /// <param name="dataLists"></param>
        /// <param name="teamHate"></param>
        /// <param name="judgeResult"></param>
        /// <param name="relationMap"></param>
        /// <param name="brainArray"></param>
        /// <param name="nowTime"></param>
        public FirstJob(UnsafeList<int> stateList, UnsafeList<CharaColdLog> coldLog,
             NativeArray<BrainDataForJob> brainArray, float nowTime)
        {
            // �^�v������e�f�[�^���X�g��W�J���ăt�B�[���h�ɑ��
            this._coldLog = coldLog;
            this.stateList = stateList;

            this.brainArray = brainArray;
            this.nowTime = nowTime;
        }

        /// <summary>
        /// characterData��judgeResult�̃C���f�b�N�X���x�[�X�ɏ�������B
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {
            // �C���^�[�o�����܂Ƃ߂Ď擾
            // x���s���Ay���ړ����f
            float2 intervals = this.brainArray[this._coldLog[index].characterID - 1].GetInterval();

            // ���f���Ԃ��o�߂��������m�F�B
            // �o�߂��ĂȂ��Ȃ珈�����Ȃ��B
            // ���邢�̓^�[�Q�b�g�������ꍇ�����肵�����B�`�[���w�C�g�Ɋ܂܂�ĂȂ���΁B���ꂾ�Ɩ������w�C�g�̎��ǂ�����́B
            // �L�������S���ɑS�L�����ɑ΂��^�[�Q�b�g���Ă邩�ǂ������m�F����悤�ɂ��悤�B�ŁA�^�[�Q�b�g��������O�񔻒f���Ԃ��}�C�i�X�ɂ���B
            this.stateList[index] = this.nowTime - this._coldLog[index].lastJudgeTime < intervals.x
                ? math.select(-1, -2, this.nowTime - this._coldLog[index].lastJudgeTime < intervals.y)
                : 0;

        }
    }

    /// <summary>
    /// AI�����f���s��Job
    /// ����Ƃ��Ă̓w�C�g���f�i�����ň�ԑ������c�͏o���Ă����j���s�����f���Ώېݒ�i�U��/�h��̏ꍇ�w�C�g�A����ȊO�̏ꍇ�͔C�ӏ�����D�揇�ɔ��f�j
    /// �w�C�g�����̓`�[���w�C�g����ԍ������w�c���Ƃɏo���Ă����āA�l�w�C�g�������炻��𒴂��邩�A�Ō��Ă�������
    /// UnsafeList<CharacterData> characterData�͘_���폜�Œ��g�Ȃ��f�[�^�����邩�炻�̔��ʂ����Ȃ��Ƃ�
    /// </summary>
    [BurstCompile(
        FloatPrecision = FloatPrecision.Medium,
        FloatMode = FloatMode.Fast,
        DisableSafetyChecks = true,
        OptimizeFor = OptimizeFor.Performance
    )]
    public struct SecondJob : IJobParallelFor
    {
        /// <summary>
        /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> teamHate;

        /// <summary>
        /// �L�����N�^�[�̊�{���
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterBaseInfo> _characterBaseInfo;

        /// <summary>
        /// AI���Q�Ƃ��邽�߂̏�ԏ��
        /// </summary>
        [ReadOnly]
        public UnsafeList<SolidData> _solidData;

        /// <summary>
        /// AI���Q�Ƃ��邽�߂̏�ԏ��
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterStateInfo> _characterStateInfo;

        /// <summary>
        /// �Q�ƕp�x�̒Ⴂ�f�[�^
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharaColdLog> _coldLog;

        /// <summary>
        /// �L�������Ƃ̌l�w�C�g�Ǘ��p
        /// �����̃n�b�V���Ƒ���̃n�b�V�����L�[�ɒl�����B
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> pHate;

        /// <summary>
        /// �L������AI�̐ݒ�B
        /// ��ԂɊ�Â��čŏ��Ƀf�[�^������������B
        /// </summary>
        [ReadOnly]
        public NativeArray<BrainDataForJob> brainArray;

        [WriteOnly]
        public UnsafeList<int> _selectMoveList;

        [ReadOnly]
        public UnsafeList<int> _stateList;

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        /// <param name="dataLists"></param>
        /// <param name="teamHate"></param>
        /// <param name="judgeResult"></param>
        /// <param name="relationMap"></param>
        /// <param name="brainArray"></param>
        /// <param name="nowTime"></param>
        public SecondJob((
        UnsafeList<CharacterBaseInfo> characterBaseInfo,
        UnsafeList<CharacterAtkStatus> characterAtkStatus,
        UnsafeList<CharacterDefStatus> characterDefStatus,
        UnsafeList<SolidData> solidData,
        UnsafeList<CharacterStateInfo> characterStateInfo,
        UnsafeList<MoveStatus> moveStatus,
        UnsafeList<CharaColdLog> coldLog
        ) dataLists, NativeHashMap<int2, int> pHate, NativeHashMap<int2, int> teamHate, UnsafeList<CharacterController.BaseController.MovementInfo> judgeResult,
            NativeArray<int> relationMap, NativeArray<BrainDataForJob> brainArray, UnsafeList<int> selectMoveList, UnsafeList<int> stateList)
        {
            // �^�v������e�f�[�^���X�g��W�J���ăt�B�[���h�ɑ��
            this._characterBaseInfo = dataLists.characterBaseInfo;
            this._solidData = dataLists.solidData;
            this._characterStateInfo = dataLists.characterStateInfo;
            this._coldLog = dataLists.coldLog;
            this._selectMoveList = selectMoveList;
            this._stateList = stateList;

            // �ʃp�����[�^���t�B�[���h�ɑ��
            this.pHate = pHate;
            this.teamHate = teamHate;
            this.brainArray = brainArray;
        }

        /// <summary>
        /// characterData��judgeResult�̃C���f�b�N�X���x�[�X�ɏ�������B
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {

            if ( this._stateList[index] < 0 )
            {
                this._selectMoveList[index] = math.select(-1, -2, this._stateList[index] == -1);
                return;
            }

            // ���݂̍s���̃X�e�[�g�𐔒l�ɕϊ�
            int nowMode = (int)this._characterStateInfo[index].actState;

            BrainSettingForJob brainData = this.brainArray[this._coldLog[index].characterID - 1].brainSetting[nowMode];

            // �s�������̒��őO��𖞂��������̂��擾����r�b�g
            // �Ȃ��A���ۂ̔��f���ɂ��D��I�ȏ������������ꂽ�ꍇ�͏�ʃr�b�g�͂܂Ƃ߂ď����B
            int enableCondition = 0;

            // �O��ƂȂ鎩���ɂ��ẴX�L�b�v�������m�F�B
            // �Ō�̏����͕⌇�����Ȃ̂Ŗ���
            for ( int i = 0; i < brainData.behaviorSetting.Length - 1; i++ )
            {

                SkipJudgeData skipData = brainData.behaviorSetting[i].skipData;

                // �X�L�b�v���������߂��Ĕ��f
                if ( skipData.skipCondition == SkipJudgeCondition.�����Ȃ� || this.JudgeSkipByCondition(skipData, index) == 1 )
                {
                    enableCondition |= 1 << i;
                }
            }

            // �����𖞂������s���̒��ōł��D��I�Ȃ��́B
            // �����l�͍Ō�̏����A�܂�����Ȃ��̕⌇����
            int selectMove = brainData.behaviorSetting.Length - 1;

            // �L�����f�[�^���m�F����B
            for ( int i = 0; i < this._solidData.Length; i++ )
            {
                // �����̓X�L�b�v
                if ( index == i )
                {
                    continue;
                }

                // �s�����f�B
                // �����̓X�C�b�`���g�����B�A������Int�l�Ȃ�R���p�C�����W�����v�e�[�u������Ă����̂�
                if ( enableCondition != 0 )
                {
                    for ( int j = 0; j < brainData.behaviorSetting.Length - 1; j++ )
                    {
                        // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ��B
                        if ( this.CheckActCondition(brainData.behaviorSetting[j].actCondition, index, i) )
                        {
                            selectMove = j;

                            // enableCondition��bit�������B
                            // i���ڂ܂ł̃r�b�g�����ׂ�1�ɂ���}�X�N���쐬
                            // (1 << (i + 1)) - 1 �� 0���� i-1���ڂ܂ł̃r�b�g�����ׂ�1
                            int mask = (1 << j) - 1;

                            // �}�X�N�ƌ��̒l�̘_���ς���邱�Ƃŏ�ʃr�b�g���N���A
                            enableCondition = enableCondition & mask;
                            break;
                        }
                    }
                }
                // �������������烋�[�v�I���B
                else
                {
                    break;
                }

            }

            this._selectMoveList[index] = selectMove;

        }

        #region �X�L�b�v�������f

        /// <summary>
        /// SkipJudgeCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        /// </summary>
        /// <param name="skipData">�X�L�b�v����p�f�[�^</param>
        /// <param name="charaData">�L�����N�^�[�f�[�^</param>
        /// <returns>�����ɍ��v����ꍇ��1�A����ȊO��0</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public int JudgeSkipByCondition(in SkipJudgeData skipData, int myIndex)
        {
            SkipJudgeCondition condition = skipData.skipCondition;
            switch ( condition )
            {
                case SkipJudgeCondition.������HP����芄���̎�:
                    // �e�������ʂ� int �ŕ]��
                    int equalConditionHP = skipData.judgeValue == this._characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int lessConditionHP = skipData.judgeValue < this._characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int invertConditionHP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                    // �����I�ɏ�����g�ݍ��킹��
                    int condition1HP = equalConditionHP;
                    int condition2HP = lessConditionHP != 0 == (invertConditionHP != 0) ? 1 : 0;
                    if ( condition1HP != 0 || condition2HP != 0 )
                    {
                        return 1;
                    }

                    return 0;

                case SkipJudgeCondition.������MP����芄���̎�:
                    // �e�������ʂ� int �ŕ]��
                    int equalConditionMP = skipData.judgeValue == this._characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int lessConditionMP = skipData.judgeValue < this._characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int invertConditionMP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                    // �����I�ɏ�����g�ݍ��킹��
                    int condition1MP = equalConditionMP;
                    int condition2MP = lessConditionMP != 0 == (invertConditionMP != 0) ? 1 : 0;
                    if ( condition1MP != 0 || condition2MP != 0 )
                    {
                        return 1;
                    }

                    return 0;

                default:
                    // �f�t�H���g�P�[�X�i����`�̏����̏ꍇ�j
                    Debug.LogWarning($"����`�̃X�L�b�v����: {condition}");
                    return 0;
            }
        }

        #endregion �X�L�b�v�������f

        /// <summary>
        /// �s�����f�̏������u���������\�b�h
        /// </summary>
        /// <param name="conditions"></param>
        /// <param name="charaData"></param>
        /// <param name="nowHate"></param>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public bool CheckActCondition(in ActJudgeData condition, int myIndex,
            int targetIndex)
        {
            bool result = true;

            // �t�B���^�[�ʉ߂��Ȃ��Ȃ�߂�B
            if ( condition.filter.IsPassFilter(this._solidData[targetIndex], this._characterStateInfo[targetIndex]) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                case ActJudgeCondition.�w��̃w�C�g�l�̓G�����鎞:

                    int targetHash = this._coldLog[targetIndex].hashCode;
                    int targetHate = 0;
                    int2 pHateKey = new(this._coldLog[myIndex].hashCode, targetHash);

                    if ( this.pHate.TryGetValue(pHateKey, out int hate) )
                    {
                        targetHate += hate;
                    }

                    // �`�[���̃w�C�g��int2�Ŋm�F����B
                    int2 hateKey = new((int)this._characterStateInfo[targetIndex].belong, targetHash);

                    if ( this.teamHate.TryGetValue(hateKey, out int tHate) )
                    {
                        targetHate += tHate;
                    }

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE ? targetHate >= condition.judgeValue : targetHate <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.HP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].hpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].hpRatio <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.MP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].mpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].mpRatio <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.�ݒ苗���ɑΏۂ����鎞:

                    // ���̋����Ŕ��肷��B
                    int judgeDist = condition.judgeValue * condition.judgeValue;

                    // ���̋����̓��B
                    int distance = (int)math.distancesq(this._characterBaseInfo[targetIndex].nowPosition, this._characterBaseInfo[targetIndex].nowPosition);

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE ? distance >= judgeDist : distance <= judgeDist;

                    return result;

                case ActJudgeCondition.����̑����ōU������Ώۂ����鎞:

                    // �ʏ�͂��鎞�A�t�̏ꍇ�͂��Ȃ��Ƃ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) > 0
                        : ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) == 0;

                    return result;

                case ActJudgeCondition.����̐��̓G�ɑ_���Ă��鎞:
                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterStateInfo[targetIndex].targetingCount >= condition.judgeValue
                        : this._characterStateInfo[targetIndex].targetingCount <= condition.judgeValue;

                    return result;

                default: // �����Ȃ� (0) �܂��͖���`�̒l
                    return result;
            }
        }

    }

    [BurstCompile(
    FloatPrecision = FloatPrecision.Medium,
    FloatMode = FloatMode.Fast,
    DisableSafetyChecks = true,
    OptimizeFor = OptimizeFor.Performance
)]
    public struct ThirdJob : IJobParallelFor
    {
        /// <summary>
        /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> teamHate;

        /// <summary>
        /// �L�����N�^�[�̊�{���
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterBaseInfo> _characterBaseInfo;

        /// <summary>
        /// �U���͂̃f�[�^
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterAtkStatus> _characterAtkStatus;

        /// <summary>
        /// �h��͂̃f�[�^
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterDefStatus> _characterDefStatus;

        /// <summary>
        /// AI���Q�Ƃ��邽�߂̏�ԏ��
        /// </summary>
        [ReadOnly]
        public UnsafeList<SolidData> _solidData;

        /// <summary>
        /// AI���Q�Ƃ��邽�߂̏�ԏ��
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterStateInfo> _characterStateInfo;

        /// <summary>
        /// �ړ��֘A�̃X�e�[�^�X
        /// </summary>
        [ReadOnly]
        public UnsafeList<MoveStatus> _moveStatus;

        /// <summary>
        /// �Q�ƕp�x�̒Ⴂ�f�[�^
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharaColdLog> _coldLog;

        /// <summary>
        /// �L�������Ƃ̌l�w�C�g�Ǘ��p
        /// �����̃n�b�V���Ƒ���̃n�b�V�����L�[�ɒl�����B
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> pHate;

        /// <summary>
        /// �s������f�[�^�B
        /// �^�[�Q�b�g�ύX�̔��f�Ƃ����S���������ł��B
        /// </summary>
        [WriteOnly]
        public UnsafeList<CharacterController.BaseController.MovementInfo> judgeResult;

        /// <summary>
        /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B
        /// �L�����f�[�^�̃`�[���ݒ�ƈꏏ�Ɏg��
        /// </summary>
        [ReadOnly]
        public NativeArray<int> relationMap;

        /// <summary>
        /// �L������AI�̐ݒ�B
        /// ��ԂɊ�Â��čŏ��Ƀf�[�^������������B
        /// </summary>
        [ReadOnly]
        public NativeArray<BrainDataForJob> brainArray;

        [ReadOnly]
        public UnsafeList<int> _selectMoveList;

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        /// <param name="dataLists"></param>
        /// <param name="teamHate"></param>
        /// <param name="judgeResult"></param>
        /// <param name="relationMap"></param>
        /// <param name="brainArray"></param>
        /// <param name="nowTime"></param>
        public ThirdJob((
        UnsafeList<CharacterBaseInfo> characterBaseInfo,
        UnsafeList<CharacterAtkStatus> characterAtkStatus,
        UnsafeList<CharacterDefStatus> characterDefStatus,
        UnsafeList<SolidData> solidData,
        UnsafeList<CharacterStateInfo> characterStateInfo,
        UnsafeList<MoveStatus> moveStatus,
        UnsafeList<CharaColdLog> coldLog
        ) dataLists, NativeHashMap<int2, int> pHate, NativeHashMap<int2, int> teamHate, UnsafeList<CharacterController.BaseController.MovementInfo> judgeResult,
            NativeArray<int> relationMap, NativeArray<BrainDataForJob> brainArray, UnsafeList<int> selectMoveList)
        {
            // �^�v������e�f�[�^���X�g��W�J���ăt�B�[���h�ɑ��
            this._characterBaseInfo = dataLists.characterBaseInfo;
            this._characterAtkStatus = dataLists.characterAtkStatus;
            this._characterDefStatus = dataLists.characterDefStatus;
            this._solidData = dataLists.solidData;
            this._characterStateInfo = dataLists.characterStateInfo;
            this._moveStatus = dataLists.moveStatus;
            this._coldLog = dataLists.coldLog;
            this._selectMoveList = selectMoveList;

            // �ʃp�����[�^���t�B�[���h�ɑ��
            this.pHate = pHate;
            this.teamHate = teamHate;
            this.judgeResult = judgeResult;
            this.relationMap = relationMap;
            this.brainArray = brainArray;
        }

        /// <summary>
        /// characterData��judgeResult�̃C���f�b�N�X���x�[�X�ɏ�������B
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {
            // 2025/7/28 �p��

            /*
            // ���ʂ̍\���̂��쐬�B
            CharacterController.BaseController.MovementInfo resultData = new();
            int selectMove = this._selectMoveList[index];

            if ( selectMove < 0 )
            {
                resultData.result = selectMove == -1
                    ? CharacterController.BaseController.JudgeResult.�����]��������
                    : CharacterController.BaseController.JudgeResult.�����Ȃ�;

                this.judgeResult[index] = resultData;
                return;
            }

            // ���݂̍s���̃X�e�[�g�𐔒l�ɕϊ�
            int nowMode = (int)this._characterStateInfo[index].actState;

            BrainSettingForJob brainData = this.brainArray[this._coldLog[index].characterID - 1].brainSetting[nowMode];

            // ���̌�A���ڂ̃��[�v�ŏ����ɓ��Ă͂܂�L������T���B
            // ���ڂōςނ��ȁH�@���f�����̐������T���Ȃ��ƃ_������Ȃ��H
            // �����p�̃W���u�ň�ԍU���͂�����/�Ⴂ�A�Ƃ��̃L������w�c���ƂɒT���Ƃ��ׂ�����Ȃ��H
            // ����͖��m�ɂ��ׂ��B
            // ����A�ł��Ώۂ�����������Ńt�B���^�����O����Ȃ����ς�_������
            // ��l�����������Ƃɐ��`���邩�B
            // �~���ƂȂ�̂́A

            // �����Ɋւ��Ă͕ʏ�������������ƌ��߂��B
            // kd�؂��ԕ����f�[�^�\���Ƃ�����݂��������ǁA�X�V���דI�ɂ��܂������p�I����Ȃ��C������B
            // �œK�ȓG���͈̔͂��قȂ邩��
            // ������͋ߋ����̕����Z���T�[�Ő��b�Ɉ�񌟍��������������BNonalloc�n�̃T�[�`�Ńo�b�t�@�� stack alloc���g����
            // �G�S�̈ȏ㑝�₷�Ȃ�g���K�[�͂܂�������

            // �ł������ɋ߂��^�[�Q�b�g���m�F����B
            // ��r�p�����l��Invert�ɂ���ĕϓ��B
            TargetJudgeData targetJudgeData = brainData.behaviorSetting[selectMove].targetCondition;

            // �V�����^�[�Q�b�g�̃n�b�V��
            int newTargetHash;

            // ��ԕύX�̏ꍇ�����Ŗ߂�B
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.�s�v_��ԕύX )
            {
                // �w���ԂɈڍs
                resultData.result = CharacterController.BaseController.JudgeResult.��Ԃ�ύX����;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // ���f���ʂ�ݒ�B
                this.judgeResult[index] = resultData;
                return;
            }

            // ����ȊO�ł���΃^�[�Q�b�g�𔻒f

            int tIndex = this.JudgeTargetByCondition(targetJudgeData, index);
            resultData.result = CharacterController.BaseController.JudgeResult.�V�������f������;

            // �����Ń^�[�Q�b�g�������ĂȂ���Αҋ@�Ɉڍs�B
            if ( tIndex < 0 )
            {
                // �ҋ@�Ɉڍs
                resultData.actNum = (int)ActState.�ҋ@;
                //  Debug.Log($"�^�[�Q�b�g���f���s�@�s���ԍ�{selectMove}");
                this.judgeResult[index] = resultData;
                return;
            }

            newTargetHash = this._coldLog[tIndex].hashCode;

            resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;
            resultData.targetHash = newTargetHash;
            resultData.selectActCondition = selectMove;
            resultData.selectTargetCondition = (int)brainData.behaviorSetting[selectMove].targetCondition.judgeCondition;

            // ���f���ʂ�ݒ�B
            this.judgeResult[index] = resultData;

            // �e�X�g�d�l�L�^
            // �v�f����10 �` 1000��
            // �X�e�[�^�X�͂������x�[�X�ƂȂ�e���v����CharacterData����āA���̐��l��������R�[�h�����Ă��B
            // �ŁAJob�V�X�e�����܂�܃x�^�ڐA�������ʂ̃N���X���쐬���āA���x���r
            // �Ō�͓�̃e�X�g�ɂ��쐬���ꂽpublic UnsafeList<MovementInfo> judgeResult�@�̓��ꐫ�������ɂ񂵂āA���x�̃`�F�b�N�܂ŏI���

            */
        }

        #region �X�L�b�v�������f

        /// <summary>
        /// SkipJudgeCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        /// </summary>
        /// <param name="skipData">�X�L�b�v����p�f�[�^</param>
        /// <param name="charaData">�L�����N�^�[�f�[�^</param>
        /// <returns>�����ɍ��v����ꍇ��1�A����ȊO��0</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public int JudgeSkipByCondition(in SkipJudgeData skipData, int myIndex)
        {
            SkipJudgeCondition condition = skipData.skipCondition;
            switch ( condition )
            {
                case SkipJudgeCondition.������HP����芄���̎�:
                    // �e�������ʂ� int �ŕ]��
                    int equalConditionHP = skipData.judgeValue == this._characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int lessConditionHP = skipData.judgeValue < this._characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int invertConditionHP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                    // �����I�ɏ�����g�ݍ��킹��
                    int condition1HP = equalConditionHP;
                    int condition2HP = lessConditionHP != 0 == (invertConditionHP != 0) ? 1 : 0;
                    if ( condition1HP != 0 || condition2HP != 0 )
                    {
                        return 1;
                    }

                    return 0;

                case SkipJudgeCondition.������MP����芄���̎�:
                    // �e�������ʂ� int �ŕ]��
                    int equalConditionMP = skipData.judgeValue == this._characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int lessConditionMP = skipData.judgeValue < this._characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int invertConditionMP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                    // �����I�ɏ�����g�ݍ��킹��
                    int condition1MP = equalConditionMP;
                    int condition2MP = lessConditionMP != 0 == (invertConditionMP != 0) ? 1 : 0;
                    if ( condition1MP != 0 || condition2MP != 0 )
                    {
                        return 1;
                    }

                    return 0;

                default:
                    // �f�t�H���g�P�[�X�i����`�̏����̏ꍇ�j
                    Debug.LogWarning($"����`�̃X�L�b�v����: {condition}");
                    return 0;
            }
        }

        #endregion �X�L�b�v�������f

        /// <summary>
        /// �s�����f�̏������u���������\�b�h
        /// </summary>
        /// <param name="conditions"></param>
        /// <param name="charaData"></param>
        /// <param name="nowHate"></param>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public bool CheckActCondition(in ActJudgeData condition, int myIndex,
            int targetIndex)
        {
            bool result = true;

            // �t�B���^�[�ʉ߂��Ȃ��Ȃ�߂�B
            if ( condition.filter.IsPassFilter(this._solidData[targetIndex], this._characterStateInfo[targetIndex]) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                case ActJudgeCondition.�w��̃w�C�g�l�̓G�����鎞:

                    int targetHash = this._coldLog[targetIndex].hashCode;
                    int targetHate = 0;
                    int2 pHateKey = new(this._coldLog[myIndex].hashCode, targetHash);

                    if ( this.pHate.TryGetValue(pHateKey, out int hate) )
                    {
                        targetHate += hate;
                    }

                    // �`�[���̃w�C�g��int2�Ŋm�F����B
                    int2 hateKey = new((int)this._characterStateInfo[targetIndex].belong, targetHash);

                    if ( this.teamHate.TryGetValue(hateKey, out int tHate) )
                    {
                        targetHate += tHate;
                    }

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE ? targetHate >= condition.judgeValue : targetHate <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.HP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].hpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].hpRatio <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.MP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].mpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].mpRatio <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.�ݒ苗���ɑΏۂ����鎞:

                    // ���̋����Ŕ��肷��B
                    int judgeDist = condition.judgeValue * condition.judgeValue;

                    // ���̋����̓��B
                    int distance = (int)math.distancesq(this._characterBaseInfo[targetIndex].nowPosition, this._characterBaseInfo[targetIndex].nowPosition);

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE ? distance >= judgeDist : distance <= judgeDist;

                    return result;

                case ActJudgeCondition.����̑����ōU������Ώۂ����鎞:

                    // �ʏ�͂��鎞�A�t�̏ꍇ�͂��Ȃ��Ƃ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) > 0
                        : ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) == 0;

                    return result;

                case ActJudgeCondition.����̐��̓G�ɑ_���Ă��鎞:
                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterStateInfo[targetIndex].targetingCount >= condition.judgeValue
                        : this._characterStateInfo[targetIndex].targetingCount <= condition.judgeValue;

                    return result;

                default: // �����Ȃ� (0) �܂��͖���`�̒l
                    return result;
            }
        }

        #region�@�^�[�Q�b�g���f����

        /// <summary>
        /// TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        /// </summary>
        /// <returns>�Ԃ�l�͍s���^�[�Q�b�g�̃C���f�b�N�X</returns>
        // TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public int JudgeTargetByCondition(in TargetJudgeData judgeData, int myIndex)
        {

            int index = -1;

            TargetSelectCondition condition = judgeData.judgeCondition;

            int isInvert;
            int score;

            // �t�����珬�����̂�T���̂ōő�l�����
            if ( judgeData.isInvert == BitableBool.TRUE )
            {
                isInvert = 1;
                score = int.MaxValue;
            }
            // �傫���̂�T���̂ōŏ��l�X�^�[�g
            else
            {
                isInvert = 0;
                score = int.MinValue;
            }

            //if ( judgeData.judgeCondition == TargetSelectCondition.���x && isInvert == 1 )
            //{
            //    Debug.Log($" �t{judgeData.isInvert == BitableBool.TRUE} �X�R�A����{score}");
            //}

            switch ( condition )
            {
                case TargetSelectCondition.���x:
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        int height = (int)this._characterBaseInfo[i].nowPosition.y;

                        // ��ԍ����L�����N�^�[�����߂� (isInvert == 1)
                        if ( isInvert == 0 )
                        {
                            int isGreater = height > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = height;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂� (isInvert == 0)
                        else
                        {
                            //   Debug.Log($" �ԍ�{index} ����{score} ���݂̍���{height}�@����{height < score}");
                            int isLess = height < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = height;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.HP����:
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterBaseInfo[i].hpRatio > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterBaseInfo[i].hpRatio;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterBaseInfo[i].hpRatio < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterBaseInfo[i].hpRatio;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.HP:

                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterBaseInfo[i].currentHp > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterBaseInfo[i].currentHp;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterBaseInfo[i].currentHp < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterBaseInfo[i].currentHp;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�G�ɑ_���Ă鐔:
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterStateInfo[i].targetingCount > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterStateInfo[i].targetingCount;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterStateInfo[i].targetingCount < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterStateInfo[i].targetingCount;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���v�U����:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].dispAtk > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].dispAtk;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterAtkStatus[i].dispAtk < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].dispAtk;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���v�h���:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].dispDef > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].dispDef;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterDefStatus[i].dispDef < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].dispDef;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�a���U����:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.slash;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�h�ˍU����:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.pierce;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ō��U����:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.strike;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.fire;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.lightning;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.light;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�ōU����:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.dark;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�a���h���:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.slash;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�h�˖h���:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.pierce;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ō��h���:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.strike;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.fire;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.lightning;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.light;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ŗh���:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.dark;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.����:

                    // �����̈ʒu���L���b�V��
                    float2 myPosition = this._characterBaseInfo[myIndex].nowPosition;

                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( myIndex == i || judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }
                        // 2�拗���ŉ��ߔ��f
                        // float������덷�������S�z����
                        float distance = Unity.Mathematics.math.distancesq(myPosition, this._characterBaseInfo[i].nowPosition);

                        // ��ԍ����L�����N�^�[�����߂�B
                        if ( isInvert == 0 )
                        {
                            if ( distance > score )
                            {
                                score = (int)distance;
                                index = i;
                            }
                        }

                        // ��ԒႢ�L�����N�^�[�����߂�B
                        else
                        {
                            if ( distance < score )
                            {
                                score = (int)distance;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.����:
                    return myIndex;

                case TargetSelectCondition.�v���C���[:
                    // ��������̃V���O���g���Ƀv���C���[��Hash�͎������Ƃ�
                    // newTargetHash = characterData[i].hashCode;
                    return -1;

                case TargetSelectCondition.�w��Ȃ�_�w�C�g�l:
                    // �^�[�Q�b�g�I�胋�[�v
                    for ( int i = 0; i < this._solidData.Length; i++ )
                    {
                        // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( i == index || judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }
                        // �w�C�g�l���m�F
                        int targetHash = this._coldLog[i].hashCode;
                        int targetHate = 0;
                        int2 pHateKey = new(this._coldLog[myIndex].hashCode, targetHash);

                        if ( this.pHate.TryGetValue(pHateKey, out int hate) )
                        {
                            targetHate += hate;
                        }

                        // �`�[���̃w�C�g��int2�Ŋm�F����B
                        int2 hateKey = new((int)this._characterStateInfo[i].belong, targetHash);

                        if ( this.teamHate.TryGetValue(hateKey, out int tHate) )
                        {
                            targetHate += tHate;
                        }
                        // ��ԍ����L�����N�^�[�����߂�B
                        if ( judgeData.isInvert == BitableBool.FALSE )
                        {
                            if ( targetHate > score )
                            {
                                score = targetHate;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�B
                        else
                        {
                            if ( targetHate < score )
                            {
                                score = targetHate;
                                index = i;
                            }
                        }
                    }

                    break;

                default:
                    // �f�t�H���g�P�[�X�i����`�̏����̏ꍇ�j
                    Debug.LogWarning($"����`�̃^�[�Q�b�g�I������: {condition}");
                    return -1;
            }

            return -1;
        }

        #endregion �^�[�Q�b�g���f����

        /// <summary>
        /// ��̃`�[�����G�΂��Ă��邩���`�F�b�N���郁�\�b�h�B
        /// </summary>
        /// <param name="team1"></param>
        /// <param name="team2"></param>
        /// <returns></returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckTeamHostility(int team1, int team2)
        {
            return (this.relationMap[team1] & (1 << team2)) > 0;
        }
    }

}
