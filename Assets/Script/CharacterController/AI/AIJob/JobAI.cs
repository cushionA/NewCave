using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.BaseController;
using static CharacterController.StatusData.BrainStatus;
using static CharacterController.StatusData.BrainStatus.TriggerJudgeData;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace CharacterController
{
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
    public struct JobAI : IJobParallelFor
    {

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
        public UnsafeList<CharacterColdLog> _coldLog;

        /// <summary>
        /// �Q�ƕp�x�̒Ⴂ�f�[�^
        /// </summary>
        [ReadOnly]
        public UnsafeList<RecognitionData> _recognizeData;

        /// <summary>
        /// ���ݎ���
        /// </summary>
        [ReadOnly]
        public float nowTime;

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
        /// �L����ID�ƃ��[�h����AI�̐ݒ��NativeArray�Ŕ�������B
        /// </summary>
        [ReadOnly]
        public BrainDataForJob brainArray;

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        /// <param name="dataLists"></param>
        /// <param name="teamHate"></param>
        /// <param name="judgeResult"></param>
        /// <param name="relationMap"></param>
        /// <param name="brainArray"></param>
        /// <param name="nowTime"></param>
        public JobAI((
        UnsafeList<CharacterBaseInfo> characterBaseInfo,
        UnsafeList<CharacterAtkStatus> characterAtkStatus,
        UnsafeList<CharacterDefStatus> characterDefStatus,
        UnsafeList<SolidData> solidData,
        UnsafeList<CharacterStateInfo> characterStateInfo,
        UnsafeList<MoveStatus> moveStatus,
        UnsafeList<CharacterColdLog> coldLog,
            UnsafeList<RecognitionData> recognizeData
        ) dataLists, UnsafeList<CharacterController.BaseController.MovementInfo> judgeResult,
            NativeArray<int> relationMap, BrainDataForJob brainArray, float nowTime)
        {
            // �^�v������e�f�[�^���X�g��W�J���ăt�B�[���h�ɑ��
            this._characterBaseInfo = dataLists.characterBaseInfo;
            this._characterAtkStatus = dataLists.characterAtkStatus;
            this._characterDefStatus = dataLists.characterDefStatus;
            this._solidData = dataLists.solidData;
            this._characterStateInfo = dataLists.characterStateInfo;
            this._moveStatus = dataLists.moveStatus;
            this._coldLog = dataLists.coldLog;
            this._recognizeData = dataLists.recognizeData;

            this.judgeResult = judgeResult;
            this.relationMap = relationMap;
            this.brainArray = brainArray;
            this.nowTime = nowTime;
        }

        /// <summary>
        /// characterData��judgeResult�̃C���f�b�N�X���x�[�X�ɏ�������B
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {

            // ���ʂ̍\���̂��쐬�B
            MovementInfo resultData = new();

            // ���݂̍s���̃X�e�[�g�𐔒l�ɕϊ�
            byte nowMode = this._coldLog[index].nowMode;

            // �L������ID���擾
            byte characterID = this._coldLog[index].characterID;

            // �O�񔻒f����̌o�ߎ��Ԃ��܂Ƃ߂Ď擾
            // x���^�[�Q�b�g���f��y���s�����f�Az���ړ����f�̌o�ߎ��ԁB
            // w���g���K�[���f�̌o�ߎ��� 
            float4 passTime = nowTime - this._coldLog[index].lastJudgeTime;

            // �L�����̔��f�Ԋu���܂Ƃ߂Ď擾
            // x���^�[�Q�b�g���f��y���s�����f�Az���ړ����f�̊Ԋu�B
            float3 judgeIntervals = this.brainArray.GetIntervalData(characterID, nowMode);

            // �ύX���L�^����t���O
            // x���^�[�Q�b�g���f��y���s�����f�Az���ړ����f
            // w�����[�h�`�F���W
            bool4 isJudged = new bool4(false, false, false, false);

            // �D��I�ɔ��f����^�[�Q�b�g�����̔ԍ��B
            // �g���K�[�C�x���g���Ŏw�肪����B
            int priorityTargetCondition = -1;

            // �ݒ肳�ꂽ�^�[�Q�b�g�̃n�b�V���R�[�h
            int nextTargetIndex = -1;

            #region �g���K�[�C�x���g���f

            // �g���K�[�s�����f���s����
            if ( passTime.w >= 0.5f )
            {
                NativeArray<TriggerJudgeData> triggerConditions = this.brainArray.GetTriggerJudgeDataArray(characterID, nowMode);


                // �����𖞂������s���̒��ōł��D��I�Ȃ��́B
                // �����l��-1�A�܂艽���g���K�[����Ă��Ȃ���ԁB
                int selectTrigger = -1;

                // ���f�̕K�v������������r�b�g�ŕێ�
                int enableTriggerCondition = (1 << triggerConditions.Length) - 1;

                // �L�����f�[�^���m�F����B
                for ( int i = 0; i < this._solidData.Length; i++ )
                {

                    // �g���K�[���f
                    if ( enableTriggerCondition != 0 )
                    {
                        for ( int j = 0; j < triggerConditions.Length - 1; j++ )
                        {
                            // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ��B
                            if ( this.CheckTriggerCondition(triggerConditions[j], index, i) )
                            {
                                selectTrigger = j;

                                // enableCondition��bit�������B
                                // i���ڂ܂ł̃r�b�g�����ׂ�1�ɂ���}�X�N���쐬
                                // (1 << (i + 1)) - 1 �� 0���� i-1���ڂ܂ł̃r�b�g�����ׂ�1
                                int mask = (1 << j) - 1;

                                // �}�X�N�ƌ��̒l�̘_���ς���邱�Ƃŏ�ʃr�b�g���N���A
                                enableTriggerCondition = enableTriggerCondition & mask;
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

                // �����𖞂������g���K�[������΃g���K�[�C�x���g���N����
                if ( selectTrigger != -1 )
                {
                    switch ( triggerConditions[selectTrigger].triggerEventType )
                    {
                        case TriggerEventType.���[�h�ύX:
                            // ���[�h�ύX�̏����𖞂������ꍇ���[�h��ύX����
                            isJudged.w = true;
                            nowMode = triggerConditions[selectTrigger].triggerNum;
                            break;
                        case TriggerEventType.�^�[�Q�b�g�ύX:
                            passTime.x = judgeIntervals.x + 1;// �C���^�[�o���̎��Ԉȏ�̒l�����Ĕ��f����悤��

                            // �D��̃^�[�Q�b�g������ݒ�
                            priorityTargetCondition = triggerConditions[selectTrigger].triggerNum;
                            break;
                        case TriggerEventType.�ʍs��:
                            // �ʍs���̏����𖞂������ꍇ
                            isJudged.y = true;
                            resultData.actNum = triggerConditions[selectTrigger].triggerNum;
                            break;
                    }
                }


                return;
            }

            #endregion �g���K�[�C�x���g���f

            #region �^�[�Q�b�g���f

            // ���Ԍo�߂��^�[�Q�b�g���f���s����ԂŁA�^�[�Q�b�g�w�肪����Ă��Ȃ����
            if ( (passTime.x >= judgeIntervals.x || (_characterStateInfo[index].actState & ActState.�^�[�Q�b�g�ύX) > 0)
                && (_characterStateInfo[index].brainEvent & AIManager.BrainEventFlagType.�U���Ώێw��) == 0 )
            {
                // �^�[�Q�b�g�������擾
                NativeArray<TargetJudgeData> targetConditions = this.brainArray.GetTargetJudgeDataArray(characterID, nowMode);

                // �D��I�ȃ^�[�Q�b�g�������w�肳��Ă���ꍇ�͂����D�悵�Ĕ��f����
                if ( priorityTargetCondition != -1 )
                {

                    // ���f��A�D������͔����ɖ߂�
                    priorityTargetCondition = -1;
                }

                // �D��������Ȃ��ꍇ�A���邢�͗D��Ń^�[�Q�b�g��������Ȃ������ꍇ�͒ʏ�̔��f���s���B
                if ( nextTargetIndex == -1 )
                {
                    for ( int i = 0; i < targetConditions.Length; i++ )
                    {
                        // �D������͂��łɎg���Ă�̂Ŕ�΂��B
                        if ( i == priorityTargetCondition )
                        {
                            continue;
                        }
                    }
                }

                // ����-1�̏ꍇ�͎������^�[�Q�b�g�ɂ���
                nextTargetIndex = nextTargetIndex == -1 ? index : nextTargetIndex;

                // �V�^�[�Q�b�g��ݒ�B
                resultData.targetHash = _coldLog[nextTargetIndex].hashCode;
            }

            #endregion �^�[�Q�b�g���f

            #region �s�����f

            // ���Ԍo�߂��Ă��āA���g���K�[�C�x���g�ōs���ݒ肪����ĂȂ��Ȃ�
            if ( !isJudged.y && passTime.x >= judgeIntervals.y )
            {
                // �N�[���^�C���ł��邩�̃t���O�B
                bool isCoolTime = false;

                // �N�[���^�C�����Ȃ�N�[���^�C���̔��f���s��
                // �����͍s���ɂ����
                // ���Ȃ݂ɃN�[���^�C�����ł��N�[���^�C������Ȃ��s���͂���̂Ŕ��莩�̂͂����B
                if ( passTime.y < this._coldLog[index].nowCoolTime.coolTime )
                {
                    // �N�[���^�C���̃X�L�b�v�����𖞂����Ă��邩�ǂ���
                    isCoolTime = (this.IsCoolTimeSkip(this._coldLog[index].nowCoolTime, index) == 0);
                }

                // �s�����f�̃f�[�^���擾
                NativeArray<ActJudgeData> moveConditions = this.brainArray.GetActJudgeDataArray(characterID, nowMode);

                int selectMove = -1;

                for ( int i = 0; i < moveConditions.Length; i++ )
                {
                    // ���s�\�����N���A�����Ȃ画�f�����{
                    if ( moveConditions[i].actRatio == 100 || moveConditions[i].actRatio < GetRandomZeroToHandred() )
                    {
                        if ( IsActionConditionSatisfied(nextTargetIndex, moveConditions[i], isCoolTime) )
                        {
                            // �N�[���^�C���X�L�b�v�����𖞂����Ă���̂ŁA�s�������s����B
                            selectMove = moveConditions[i].triggerNum;
                            isJudged.y = true;
                            break;
                        }
                    }
                }

                // �����𖞂������s��������΍s�����N����
                if ( selectMove != -1 )
                {
                    switch ( moveConditions[selectMove].triggerEventType )
                    {
                        case TriggerEventType.���[�h�ύX:
                            // ���[�h�ύX�̏����𖞂������ꍇ���[�h��ύX����
                            isJudged.w = true;
                            nowMode = moveConditions[selectMove].triggerNum;
                            isJudged.x = false; // �^�[�Q�b�g�ύX�͍s���Ă��Ȃ�
                            isJudged.z = false; // �ړ����f�͍s���Ă��Ȃ�
                            isJudged.y = false; // �s�����f�͍s���Ă��Ȃ��B
                            break;
                        case TriggerEventType.�^�[�Q�b�g�ύX:
                            // �ēx�D��^�[�Q�b�g������ݒ�
                            priorityTargetCondition = moveConditions[selectMove].triggerNum;
                            isJudged.x = true; // �^�[�Q�b�g�ύX�͍s��ꂽ
                            isJudged.z = false; // �ړ����f�͍s���Ă��Ȃ�
                            isJudged.y = false; // �s�����f�͍s���Ă��Ȃ��B
                            break;
                        case TriggerEventType.�ʍs��:
                            // �ʍs���̏����𖞂������ꍇ
                            isJudged.y = true;
                            resultData.actNum = moveConditions[selectMove].triggerNum;
                            break;
                    }
                }
            }

            // �D��I�ȃ^�[�Q�b�g�������w�肳��Ă���ꍇ�͂����D�悵�Ĕ��f����
            if ( priorityTargetCondition != -1 )
            {

                // ���f��A�D������͔����ɖ߂�
                priorityTargetCondition = -1;
            }

            #endregion �s�����f

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

        }

        #region �N�[���^�C���X�L�b�v�������f���\�b�h

        /// <summary>
        /// SkipJudgeCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        /// </summary>
        /// <param name="skipData">�X�L�b�v����p�f�[�^</param>
        /// <param name="charaData">�L�����N�^�[�f�[�^</param>
        /// <returns>�����ɍ��v����ꍇ��1�A����ȊO��0</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private byte IsCoolTimeSkip(in CoolTimeData skipData, int myIndex)
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

        #endregion �N�[���^�C���X�L�b�v�������f���\�b�h

        #region �g���K�[�C�x���g���f���\�b�h

        /// <summary>
        /// �g���K�[�C�x���g���f�̏������u���������\�b�h
        /// </summary>
        /// <param name="conditions"></param>
        /// <param name="charaData"></param>
        /// <param name="nowHate"></param>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckTriggerCondition(in TriggerJudgeData condition, int myIndex,
            int targetIndex)
        {
            bool result = true;

            // �t�B���^�[�ʉ߂��Ȃ��Ȃ�߂�B
            if ( condition.filter.IsPassFilter(this._solidData[targetIndex], this._characterStateInfo[targetIndex], this._characterBaseInfo[myIndex].nowPosition, this._characterBaseInfo[targetIndex].nowPosition) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                case ActTriggerCondition.�w��̃w�C�g�l�̓G�����鎞:

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

                case ActTriggerCondition.HP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].hpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].hpRatio <= condition.judgeValue;

                    return result;

                case ActTriggerCondition.MP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].mpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].mpRatio <= condition.judgeValue;

                    return result;

                case ActTriggerCondition.�ݒ苗���ɑΏۂ����鎞:

                    // ���̋����Ŕ��肷��B
                    int judgeDist = condition.judgeValue * condition.judgeValue;

                    // ���̋����̓��B
                    int distance = (int)math.distancesq(this._characterBaseInfo[myIndex].nowPosition, this._characterBaseInfo[targetIndex].nowPosition);

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE ? distance >= judgeDist : distance <= judgeDist;

                    return result;

                case ActTriggerCondition.����̑����ōU������Ώۂ����鎞:

                    // �ʏ�͂��鎞�A�t�̏ꍇ�͂��Ȃ��Ƃ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) > 0
                        : ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) == 0;

                    return result;

                case ActTriggerCondition.����̐��̓G�ɑ_���Ă��鎞:
                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterStateInfo[targetIndex].targetingCount >= condition.judgeValue
                        : this._characterStateInfo[targetIndex].targetingCount <= condition.judgeValue;

                    return result;

                default: // �����Ȃ� (0) �܂��͖���`�̒l
                    return result;
            }
        }

        #endregion �g���K�[�C�x���g���f���\�b�h

        #region�@�^�[�Q�b�g���f����

        /// <summary>
        /// TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        /// </summary>
        /// <returns>�Ԃ�l�͍s���^�[�Q�b�g�̃C���f�b�N�X</returns>
        // TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int JudgeTargetByCondition(in TargetJudgeData judgeData, int myIndex)
        {

            int index = -1;

            // �����̈ʒu���擾
            float2 myPosition = this._characterBaseInfo[myIndex].nowPosition;

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



            switch ( condition )
            {
                case TargetSelectCondition.���x:
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                    return myIndex;

                case TargetSelectCondition.�v���C���[:
                    // ��������̃V���O���g���Ƀv���C���[��Hash�͎������Ƃ�
                    // newTargetHash = characterData[i].hashCode;
                    return -1;

                case TargetSelectCondition.�w��Ȃ�_�t�B���^�[�̂�:
                    // �^�[�Q�b�g�I�胋�[�v
                    for ( int i = 0; i < this._solidData.Length; i++ )
                    {
                        // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( i == index || judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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

        #region �s�����f���\�b�h

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool IsActionConditionSatisfied(int targetIndex, in ActJudgeData condition, bool isCoolTime)
        {
            // �^�[�Q�b�g���s���̏����𖞂����Ă��邩���m�F���郁�\�b�h�B
            // �����ł́AactNum���s���ԍ��AtargetIndex���^�[�Q�b�g�̃C���f�b�N�X�AjudgeData�����f�f�[�^��\���B
            // �����𖞂����Ă����true�A�����łȂ����false��Ԃ��B

        }

        #endregion �s�����f���\�b�h

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

        /// <summary>
        /// �[������S�̒��ŗ����𐶐����郁�\�b�h�B
        /// </summary>
        /// <returns></returns>
        private int GetRandomZeroToHandred()
        {

        }

    }

}

