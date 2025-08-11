using System;
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
using static MoreMountains.CorgiEngine.MyCharacter;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;
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
        public UnsafeList<MovementInfo> judgeResult;

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
            UnsafeList<RecognitionData> recognizeData,
            UnsafeList<MovementInfo> judgeResult
        ) dataLists,
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

            this.judgeResult = dataLists.judgeResult;
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

            // ���������p�̃V�[�h�̏������i�C���f�b�N�X�Ǝ��Ԃ�g�ݍ��킹�ăV�[�h�l�𐶐��j
            uint seed = SeedGenerate((uint)(characterID + index + (int)(nowTime * 1000)));

            #region �g���K�[�C�x���g���f

            // �g���K�[�s�����f���s�����Ԃ�
            // ��b�Ɉ�񂾂�����
            if ( passTime.w >= 1f )
            {
                NativeArray<TriggerJudgeData> triggerConditions = this.brainArray.GetTriggerJudgeDataArray(characterID, nowMode);

                // �����𖞂������s���̒��ōł��D��I�Ȃ��́B
                // �����l��-1�A�܂艽���g���K�[����Ă��Ȃ���ԁB
                int selectTrigger = -1;

                // ���f�̕K�v������������r�b�g�ŕێ�
                int enableTriggerCondition = (1 << triggerConditions.Length) - 1;

                // �e�C�x���g�̎��s�m���𔻒f���āA�m���`�F�b�N���s�������̂����O
                for ( int i = 0; i < triggerConditions.Length; i++ )
                {
                    // ���s�m����100����Ȃ��āA�����s�m���͈̔͂������ȉ��Ȃ�
                    if ( triggerConditions[i].actRatio != 100 && triggerConditions[i].actRatio < GetRandomValueZeroToHundred(ref seed) )
                    {
                        // i�Ԗڂ̃r�b�g��0�ɂ��Ĕ��f�Ώۂ���O��
                        enableTriggerCondition &= ~(1 << i);
                    }
                }

                // �ŗD��̏����̃C���f�b�N�X��ێ�����ϐ�
                int mostPriorityTrigger = -1;

                // �L���ȏ����̒��ōł����Ԃ������i�D��x�������j���̂��擾
                for ( int i = 0; i < triggerConditions.Length; i++ )
                {
                    if ( (enableTriggerCondition & (1 << i)) != 0 )
                    {
                        mostPriorityTrigger = i;
                        break; // �ŏ��Ɍ����������̂��ŗD��Ȃ̂�break
                    }
                }

                // �J�E���g���K�v�ȏ����̂��߂ɔz����Z�b�g�A�b�v
                NativeArray<int> counterArray = new NativeArray<int>(triggerConditions.Length, Allocator.Temp);

                // �J�E���^�[�z��̏�����
                for ( int i = 0; i < counterArray.Length; i++ )
                {
                    // i�Ԗڂ̃r�b�g�������Ă��邩�`�F�b�N
                    if ( (enableTriggerCondition & (1 << i)) == 0 )
                    {
                        counterArray[i] = -1;
                        continue; // ���̃r�b�g��0�Ȃ�A���s�m���ŏ��O����Ă���̂ŃX�L�b�v
                    }

                    // �W�v���K�v�Ȃ�J�E���g���s���B
                    if ( triggerConditions[i].judgeCondition != ActTriggerCondition.�����Ȃ�
                        && triggerConditions[i].judgeCondition <= ActTriggerCondition.����̑Ώۂ���萔���鎞 )
                    {
                        counterArray[i] = 0; // �J�E���g���K�v�ȏ����̓J�E���g�J�n
                        continue;
                    }

                    // �W�v�͍s��Ȃ��Ȃ�-1
                    counterArray[i] = -1;

                }

                // �L�����f�[�^���m�F���邽�߂̃L�����������[�v
                for ( int i = mostPriorityTrigger; i < this._solidData.Length; i++ )
                {
                    // �g���K�[���f
                    if ( enableTriggerCondition != 0 )
                    {
                        // �e�L�����ɑ΂��S�������m�F
                        for ( int j = 0; j < triggerConditions.Length - 1; j++ )
                        {

                            // j�Ԗڂ̃r�b�g�������Ă��邩�`�F�b�N
                            if ( (enableTriggerCondition & (1 << j)) == 0 )
                            {
                                continue; // ���̃r�b�g��0�Ȃ�A���s�m���ŏ��O����Ă���̂ŃX�L�b�v
                            }

                            // �J�E���^�[�p�ϐ���p��
                            int counterValue = counterArray[j];

                            // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ��B
                            if ( this.JudgeTriggerCondition(triggerConditions[j], index, i, ref counterValue) )
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

                            counterArray[j] = counterValue; // �J�E���^�[�l���X�V
                        }
                    }

                    // �������������烋�[�v�I���B
                    else
                    {
                        break;
                    }
                }

                // �J�E���^�[�`�F�b�N
                for ( int i = 0; i < counterArray.Length; i++ )
                {
                    // �ŗD����������������̂ŏW�v���~
                    if ( selectTrigger == i )
                    {
                        break;
                    }

                    // �J�E���g�𖞂������Ȃ�
                    if ( counterArray[i] != -1 &&
                        (counterArray[i] >= triggerConditions[i].judgeLowerValue && counterArray[i] <= triggerConditions[i].judgeUpperValue) )
                    {
                        selectTrigger = i; // �����𖞂������̂őI��
                        break;// �����𖞂������̂Ń��[�v�I��
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

                // �J�E���^�[�z������
                counterArray.Dispose();
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
                    // �^�[�Q�b�g�擾
                    nextTargetIndex = JudgeTargetCondition(targetConditions[priorityTargetCondition], index);

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
                    isCoolTime = (this.JudgeCoolTimeBreak(this._coldLog[index].nowCoolTime, index));
                }

                // �s�����f�̃f�[�^���擾
                NativeArray<ActJudgeData> moveConditions = this.brainArray.GetActJudgeDataArray(characterID, nowMode);

                int selectMove = -1;

                for ( int i = 0; i < moveConditions.Length; i++ )
                {
                    // ���s�\�����N���A�����Ȃ画�f�����{
                    if ( (!isCoolTime || moveConditions[i].isCoolTimeIgnore)
                        && moveConditions[i].actRatio == 100 || moveConditions[i].actRatio <= GetRandomValueZeroToHundred(ref seed) )
                    {
                        if ( JudgeActCondition(nextTargetIndex, moveConditions[i], index, moveConditions[i].isSelfJudge) )
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
                // �^�[�Q�b�g�擾
                nextTargetIndex = JudgeTargetCondition(this.brainArray.GetTargetJudgeDataArray(characterID, nowMode)[priorityTargetCondition], index);
            }

            #endregion �s�����f

            #region �ړ����f�i�U������j

            // �^�[�Q�b�g�����鎞�A���Ԍo�߂��s�����V�K���f���ꂽ�ꍇ
            if ( (nextTargetIndex != index && nextTargetIndex != -1) && (passTime.z >= judgeIntervals.z || isJudged.y) )
            {
                // �������擾
                int direction = this._characterBaseInfo[index].nowPosition.x < this._characterBaseInfo[nextTargetIndex].nowPosition.x ? 1 : -1;

                // �^�[�Q�b�g�ւ̋�����ݒ�
                resultData.targetDistance = direction * math.distance(this._characterBaseInfo[index].nowPosition, this._characterBaseInfo[nextTargetIndex].nowPosition);
                isJudged.z = true;
            }

            #endregion �ړ����f�i�U������j

            // isJudged�͕ύX���L�^����t���O
            // x���^�[�Q�b�g���f��y���s�����f�Az���ړ����f
            // w�����[�h�`�F���W

            // ���f���ʂ��i�[����B
            if ( isJudged.w )
            {
                resultData.result &= JudgeResult.���[�h�ύX����;
            }

            if ( isJudged.x )
            {
                resultData.result &= JudgeResult.�^�[�Q�b�g�ύX����;
            }

            if ( isJudged.y )
            {
                resultData.result &= JudgeResult.�s����ύX����;
            }

            if ( isJudged.z )
            {
                resultData.result &= JudgeResult.������ύX����;
            }

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
        /// <param name="condition">�X�L�b�v����p�f�[�^</param>
        /// <param name="myIndex">�L�����N�^�[�f�[�^</param>
        /// <returns>�����ɍ��v����ꍇ��1�A����ȊO��0</returns>
        private bool JudgeCoolTimeBreak(in CoolTimeData condition, int myIndex)
        {
            // ����̃^�[�Q�b�g���w�肳��Ă���ΐݒ肷�邽�߂̕ϐ�
            int targetIndex = -1;

            // �t�B���^�[�`�F�b�N
            if ( condition.filter.SelfTarget )
            {
                targetIndex = myIndex; // �������g���^�[�Q�b�g�ɂ���
            }

            // �v���C���[�̓[�����C���f�b�N�X
            else if ( condition.filter.PlayerTarget )
            {
                targetIndex = 0;
            }

            // �����̃|�W�V�����͊o���Ă���
            float2 myPosition = this._characterBaseInfo[myIndex].nowPosition;

            // ����̕W�I���Ȃ���ΑS�L�����ɑ΂��Ċm�F�B
            if ( targetIndex == -1 )
            {
                switch ( condition.skipCondition )
                {
                    // ����̑Ώۂ���萔���鎞
                    case ActTriggerCondition.����̑Ώۂ���萔���鎞:

                        int counter = 0;

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }
                            counter++;
                        }

                        // �J�E���^�[�������𖞂����Ă��邩�`�F�b�N
                        return counter >= condition.judgeLowerValue && counter <= condition.judgeUpperValue;

                    // HP����芄���̑Ώۂ����鎞
                    case ActTriggerCondition.HP����芄���̑Ώۂ����鎞:

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }

                            int hpRatio = _characterBaseInfo[i].hpRatio;

                            if ( hpRatio >= condition.judgeLowerValue && hpRatio <= condition.judgeUpperValue )
                            {
                                return true; // �����𖞂������L��������������
                            }
                        }

                        // �����𖞂����L������������Ȃ�����
                        return false;

                    // MP����芄���̑Ώۂ����鎞
                    case ActTriggerCondition.MP����芄���̑Ώۂ����鎞:

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }

                            int mpRatio = _characterBaseInfo[i].mpRatio;

                            if ( mpRatio >= condition.judgeLowerValue && mpRatio <= condition.judgeUpperValue )
                            {
                                return true; // �����𖞂������L��������������
                            }
                        }

                        // �����𖞂����L������������Ȃ�����
                        return false;

                    // �Ώۂ̃L��������萔�ȏ㖧�W���Ă��鎞
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞:
                        bool isMatch = false;

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {

                            if ( condition.filter.IsPassFilter(
                             this._solidData[i],
                             this._characterStateInfo[i],
                             myPosition,
                             this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }

                            // ��ڂ̒l��w�c�ɕϊ��B
                            switch ( ((CharacterBelong)condition.judgeLowerValue) )
                            {
                                case CharacterBelong.�v���C���[:
                                    // �v���C���[�w�c�̃L�����𐔂���
                                    isMatch = _recognizeData[i].nearlyPlayerSideCount >= condition.judgeUpperValue;
                                    break;
                                case CharacterBelong.����:
                                    // �����w�c�̃L�����𐔂���
                                    isMatch = _recognizeData[i].nearlyMonsterSideCount >= condition.judgeUpperValue;

                                    break;
                                case CharacterBelong.���̑�:
                                    // ���̑��w�c�̃L�����𐔂���
                                    isMatch = _recognizeData[i].nearlyOtherSideCount >= condition.judgeUpperValue;
                                    break;
                            }

                            // �����𖞂������L��������������
                            if ( isMatch )
                            {
                                return true;
                            }
                        }

                        // �����𖞂����L������������Ȃ�����
                        return false;

                    // �Ώۂ̃L���������ȉ��������W���Ă��Ȃ���
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���:

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            // ������
                            isMatch = false;

                            if ( condition.filter.IsPassFilter(
                             this._solidData[i],
                             this._characterStateInfo[i],
                             myPosition,
                             this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }

                            // ��ڂ̒l��w�c�ɕϊ��B
                            switch ( ((CharacterBelong)condition.judgeLowerValue) )
                            {
                                case CharacterBelong.�v���C���[:
                                    // �v���C���[�w�c�̃L�����𐔂���
                                    isMatch = _recognizeData[i].nearlyPlayerSideCount <= condition.judgeUpperValue;
                                    break;
                                case CharacterBelong.����:
                                    // �����w�c�̃L�����𐔂���
                                    isMatch = _recognizeData[i].nearlyMonsterSideCount <= condition.judgeUpperValue;

                                    break;
                                case CharacterBelong.���̑�:
                                    // ���̑��w�c�̃L�����𐔂���
                                    isMatch = _recognizeData[i].nearlyOtherSideCount <= condition.judgeUpperValue;
                                    break;
                            }

                            // �����𖞂������L��������������
                            if ( isMatch )
                            {
                                return true;
                            }
                        }

                        // �����𖞂����L������������Ȃ�����
                        return false;

                    // ���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞
                    case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {

                            if ( condition.filter.IsPassFilter(
                             this._solidData[i],
                             this._characterStateInfo[i],
                             myPosition,
                             this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }



                            // �����ł́AjudgeLowerValue���[���Ȃ� or ����
                            if ( condition.judgeLowerValue == 0 )
                            {
                                isMatch = (((int)_recognizeData[i].recognizeObject & condition.judgeUpperValue) > 0);
                            }

                            // and����
                            else
                            {
                                isMatch = (((int)_recognizeData[i].recognizeObject & condition.judgeUpperValue) == condition.judgeUpperValue);
                            }


                            // �����𖞂������L��������������
                            if ( isMatch )
                            {
                                return true;
                            }
                        }

                        // �����𖞂����L������������Ȃ�����
                        return false;

                    // ����̐��̓G�ɑ_���Ă��鎞
                    case ActTriggerCondition.�Ώۂ���萔�̓G�ɑ_���Ă��鎞:

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }

                            // �Ώۂ̃L�����̑_���Ă��鐔���擾
                            int targetingCount = _characterStateInfo[targetIndex].targetingCount;

                            if ( targetingCount >= condition.judgeLowerValue && targetingCount <= condition.judgeUpperValue )
                            {
                                return true; // �����𖞂������L��������������
                            }
                        }

                        // �����𖞂����L������������Ȃ�����
                        return false;

                    // �Ώۂ̃L�����̈�苗���ȓ��ɔ�ѓ�����鎞
                    case ActTriggerCondition.�Ώۂ̃L�����̈�苗���ȓ��ɔ�ѓ�����鎞:

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }

                            // �Ώۂ̃L�����̔�ѓ���̌��o�������擾
                            float detectDistance = _recognizeData[targetIndex].detectNearestAttackDistance;
                            if ( detectDistance > 0 && detectDistance >= condition.judgeLowerValue && detectDistance <= condition.judgeUpperValue )
                            {
                                return true; // �����𖞂������L��������������
                            }
                        }

                        // �����𖞂����L������������Ȃ�����
                        return false;

                    // ����̃C�x���g������������
                    case ActTriggerCondition.����̃C�x���g������������:

                        // �S�L�������m�F�B
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {

                            if ( condition.filter.IsPassFilter(
                             this._solidData[i],
                             this._characterStateInfo[i],
                             myPosition,
                             this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // �t�B���^�[�ɍ��v���Ȃ��̂ŃX�L�b�v
                            }

                            // �����ł́AjudgeLowerValue���[���Ȃ� or ����
                            if ( condition.judgeLowerValue == 0 )
                            {
                                isMatch = (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) > 0);
                            }

                            // and����
                            else
                            {
                                isMatch = (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) == condition.judgeUpperValue);
                            }

                            // �����𖞂������L��������������
                            if ( isMatch )
                            {
                                return true;
                            }
                        }

                        // �����𖞂����L������������Ȃ�����
                        return false;
                }
            }

            // ����̑Ώۂ�����ΒP�̂ɑ΂��Ċm�F�B
            else
            {
                // �t�B���^�[�`�F�b�N
                if ( condition.filter.IsPassFilter(
                    this._solidData[targetIndex],
                    this._characterStateInfo[targetIndex],
                    this._characterBaseInfo[myIndex].nowPosition,
                    this._characterBaseInfo[targetIndex].nowPosition) == 0 )
                {
                    return false;
                }

                switch ( condition.skipCondition )
                {
                    // ����̑Ώۂ���萔���鎞
                    case ActTriggerCondition.����̑Ώۂ���萔���鎞:
                        return true;

                    // HP����芄���̑Ώۂ����鎞
                    case ActTriggerCondition.HP����芄���̑Ώۂ����鎞:
                        int hpRatio = _characterBaseInfo[targetIndex].hpRatio;
                        return hpRatio >= condition.judgeLowerValue && hpRatio <= condition.judgeUpperValue;

                    // MP����芄���̑Ώۂ����鎞
                    case ActTriggerCondition.MP����芄���̑Ώۂ����鎞:
                        int mpRatio = _characterBaseInfo[targetIndex].mpRatio;
                        return mpRatio >= condition.judgeLowerValue && mpRatio <= condition.judgeUpperValue;

                    // �Ώۂ̃L��������萔�ȏ㖧�W���Ă��鎞
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞:

                        // ��ڂ̒l��w�c�ɕϊ��B
                        switch ( ((CharacterBelong)condition.judgeLowerValue) )
                        {
                            case CharacterBelong.�v���C���[:
                                // �v���C���[�w�c�̃L�����𐔂���
                                return _recognizeData[targetIndex].nearlyPlayerSideCount >= condition.judgeUpperValue;
                            case CharacterBelong.����:
                                // �����w�c�̃L�����𐔂���
                                return _recognizeData[targetIndex].nearlyMonsterSideCount >= condition.judgeUpperValue;
                            case CharacterBelong.���̑�:
                                // ���̑��w�c�̃L�����𐔂���
                                return _recognizeData[targetIndex].nearlyOtherSideCount >= condition.judgeUpperValue;
                        }

                        return false;

                    // �Ώۂ̃L��������萔�ȉ������������Ȃ���
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���:

                        // ��ڂ̒l��w�c�ɕϊ��B
                        switch ( ((CharacterBelong)condition.judgeLowerValue) )
                        {
                            case CharacterBelong.�v���C���[:
                                // �v���C���[�w�c�̃L�����𐔂���
                                return _recognizeData[targetIndex].nearlyPlayerSideCount <= condition.judgeUpperValue;
                            case CharacterBelong.����:
                                // �����w�c�̃L�����𐔂���
                                return _recognizeData[targetIndex].nearlyMonsterSideCount <= condition.judgeUpperValue;
                            case CharacterBelong.���̑�:
                                // ���̑��w�c�̃L�����𐔂���
                                return _recognizeData[targetIndex].nearlyOtherSideCount <= condition.judgeUpperValue;
                        }

                        return false;

                    // ���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞
                    case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:

                        // �����ł́AjudgeLowerValue���[���Ȃ� or ����
                        if ( condition.judgeLowerValue == 0 )
                        {
                            return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) > 0);
                        }

                        // and����
                        else
                        {
                            return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) == condition.judgeUpperValue);
                        }

                    // ����̐��̓G�ɑ_���Ă��鎞
                    case ActTriggerCondition.�Ώۂ���萔�̓G�ɑ_���Ă��鎞:
                        int targetingCount = _characterStateInfo[targetIndex].targetingCount;
                        return targetingCount >= condition.judgeLowerValue && targetingCount <= condition.judgeUpperValue;

                    // �Ώۂ̃L�����̈�苗���ȓ��ɔ�ѓ�����鎞
                    case ActTriggerCondition.�Ώۂ̃L�����̈�苗���ȓ��ɔ�ѓ�����鎞:
                        float detectDistance = _recognizeData[targetIndex].detectNearestAttackDistance;
                        return detectDistance > 0 && detectDistance >= condition.judgeLowerValue && detectDistance <= condition.judgeUpperValue;

                    // ����̃C�x���g������������
                    case ActTriggerCondition.����̃C�x���g������������:

                        // �����ł́AjudgeLowerValue���[���Ȃ� or ����
                        if ( condition.judgeLowerValue == 0 )
                        {
                            return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) > 0);
                        }

                        // and����
                        else
                        {
                            return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) == condition.judgeUpperValue);
                        }
                }
            }

            return false; // �f�t�H���g�͏����𖞂����Ȃ�
        }

        #endregion �N�[���^�C���X�L�b�v�������f���\�b�h

        #region �g���K�[�C�x���g���f���\�b�h

        /// <summary>
        /// �g���K�[�C�x���g���f�̏������u���������\�b�h
        /// </summary>
        /// <param name="conditions"></param>
        /// <param name="charaData"></param>
        /// <param name="nowHate"></param>
        private bool JudgeTriggerCondition(in TriggerJudgeData condition, int myIndex,
            int targetIndex, ref int counter)
        {
            // �t�B���^�[�`�F�b�N
            if ( condition.filter.IsPassFilter(
                this._solidData[targetIndex],
                this._characterStateInfo[targetIndex],
                this._characterBaseInfo[myIndex].nowPosition,
                this._characterBaseInfo[targetIndex].nowPosition) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                // ����̑Ώۂ���萔���鎞
                case ActTriggerCondition.����̑Ώۂ���萔���鎞:
                    counter++;
                    return false;

                // HP����芄���̑Ώۂ����鎞
                case ActTriggerCondition.HP����芄���̑Ώۂ����鎞:
                    int hpRatio = _characterBaseInfo[targetIndex].hpRatio;
                    return hpRatio >= condition.judgeLowerValue && hpRatio <= condition.judgeUpperValue;

                // MP����芄���̑Ώۂ����鎞
                case ActTriggerCondition.MP����芄���̑Ώۂ����鎞:
                    int mpRatio = _characterBaseInfo[targetIndex].mpRatio;
                    return mpRatio >= condition.judgeLowerValue && mpRatio <= condition.judgeUpperValue;

                // �Ώۂ̃L��������萔�ȏ㖧�W���Ă��鎞
                case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞:

                    // ��ڂ̒l��w�c�ɕϊ��B
                    switch ( ((CharacterBelong)condition.judgeLowerValue) )
                    {
                        case CharacterBelong.�v���C���[:
                            // �v���C���[�w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyPlayerSideCount >= condition.judgeUpperValue;
                        case CharacterBelong.����:
                            // �����w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyMonsterSideCount >= condition.judgeUpperValue;
                        case CharacterBelong.���̑�:
                            // ���̑��w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyOtherSideCount >= condition.judgeUpperValue;
                    }

                    return false;

                // �Ώۂ̃L��������萔�ȉ������������Ȃ����Ă��鎞
                case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���:

                    // ��ڂ̒l��w�c�ɕϊ��B
                    switch ( ((CharacterBelong)condition.judgeLowerValue) )
                    {
                        case CharacterBelong.�v���C���[:
                            // �v���C���[�w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyPlayerSideCount <= condition.judgeUpperValue;
                        case CharacterBelong.����:
                            // �����w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyMonsterSideCount <= condition.judgeUpperValue;
                        case CharacterBelong.���̑�:
                            // ���̑��w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyOtherSideCount <= condition.judgeUpperValue;
                    }

                    return false;

                // ���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞
                case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:

                    // �����ł́AjudgeLowerValue���[���Ȃ� or ����
                    if ( condition.judgeLowerValue == 0 )
                    {
                        return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) > 0);
                    }

                    // and����
                    else
                    {
                        return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) == condition.judgeUpperValue);
                    }

                // ����̐��̓G�ɑ_���Ă��鎞
                case ActTriggerCondition.�Ώۂ���萔�̓G�ɑ_���Ă��鎞:
                    int targetingCount = _characterStateInfo[targetIndex].targetingCount;
                    return targetingCount >= condition.judgeLowerValue && targetingCount <= condition.judgeUpperValue;

                // �Ώۂ̃L�����̈�苗���ȓ��ɔ�ѓ�����鎞
                case ActTriggerCondition.�Ώۂ̃L�����̈�苗���ȓ��ɔ�ѓ�����鎞:
                    float detectDistance = _recognizeData[targetIndex].detectNearestAttackDistance;
                    return detectDistance > 0 && detectDistance >= condition.judgeLowerValue && detectDistance <= condition.judgeUpperValue;

                // ����̃C�x���g������������
                case ActTriggerCondition.����̃C�x���g������������:

                    // �����ł́AjudgeLowerValue���[���Ȃ� or ����
                    if ( condition.judgeLowerValue == 0 )
                    {
                        return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) > 0);
                    }

                    // and����
                    else
                    {
                        return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) == condition.judgeUpperValue);
                    }
            }

            return false; // �f�t�H���g�͏����𖞂����Ȃ�
        }

        #endregion �g���K�[�C�x���g���f���\�b�h

        #region�@�^�[�Q�b�g���f����

        /// <summary>
        /// TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        /// </summary>
        /// <returns>�Ԃ�l�͍s���^�[�Q�b�g�̃C���f�b�N�X</returns>
        // TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        private int JudgeTargetCondition(in TargetJudgeData judgeData, int myIndex)
        {
            int index = -1;
            float2 myPosition = this._characterBaseInfo[myIndex].nowPosition;

            TargetSelectCondition condition = judgeData.judgeCondition;
            bool isInvert = judgeData.isInvert == BitableBool.TRUE;
            int score = isInvert ? int.MaxValue : int.MinValue;

            // ��������̏����i�X�R�A�x�[�X�ł͂Ȃ������j
            switch ( condition )
            {
                // 21. ����
                case TargetSelectCondition.����:
                    return myIndex;

                // 22. �v���C���[
                case TargetSelectCondition.�v���C���[:
                    return 0;

                // 23. �V�X�^�[����
                case TargetSelectCondition.�V�X�^�[����:
                    return _solidData.Length - 1;

                // 24-26. ���W�l���n�i���ʏ����j
                case TargetSelectCondition.�v���C���[�w�c�̖��W�l��:
                case TargetSelectCondition.�����w�c�̖��W�l��:
                case TargetSelectCondition.���̑��w�c�̖��W�l��:
                    return FindMostDenseTarget(condition, judgeData.filter, isInvert, myIndex);

                // 27. �����𖞂����ΏۂɂƂ��čł��w�C�g�������L����
                case TargetSelectCondition.�����𖞂����ΏۂɂƂ��čł��w�C�g�������L����:
                    return FindTargetWithHighestHate(judgeData.filter, myIndex);

                // 28. �����𖞂����ΏۂɍŌ�ɍU�������L����
                case TargetSelectCondition.�����𖞂����ΏۂɍŌ�ɍU�������L����:
                    return FindLastAttacker(judgeData.filter, myIndex);

                // 29. �w��Ȃ�_�t�B���^�[�̂�
                case TargetSelectCondition.�w��Ȃ�_�t�B���^�[�̂�:
                    // �t�B���^�[�����݂̂ōŏ��Ɍ��������Ώۂ�Ԃ�
                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        if ( judgeData.filter.IsPassFilter(
                            _solidData[i],
                            _characterStateInfo[i],
                            myPosition,
                            _characterBaseInfo[i].nowPosition) != 0 )
                        {
                            return i;
                        }
                    }
                    return -1;

                // 1-20, 24-26. �X�R�A�x�[�X�̏����iGetTargetScore�ŏ����j
                case TargetSelectCondition.���x:
                case TargetSelectCondition.HP����:
                case TargetSelectCondition.HP:
                case TargetSelectCondition.�G�ɑ_���Ă鐔:
                case TargetSelectCondition.���v�U����:
                case TargetSelectCondition.���v�h���:
                case TargetSelectCondition.�a���U����:
                case TargetSelectCondition.�h�ˍU����:
                case TargetSelectCondition.�Ō��U����:
                case TargetSelectCondition.���U����:
                case TargetSelectCondition.���U����:
                case TargetSelectCondition.���U����:
                case TargetSelectCondition.�ōU����:
                case TargetSelectCondition.�a���h���:
                case TargetSelectCondition.�h�˖h���:
                case TargetSelectCondition.�Ō��h���:
                case TargetSelectCondition.���h���:
                case TargetSelectCondition.���h���:
                case TargetSelectCondition.���h���:
                case TargetSelectCondition.�Ŗh���:
                    // �X�R�A�x�[�X�̔��f����
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {

                        // �t�B���^�[�`�F�b�N
                        if ( judgeData.filter.IsPassFilter(
                            this._solidData[i],
                            this._characterStateInfo[i],
                            myPosition,
                            this._characterBaseInfo[i].nowPosition) == 0 )
                        {
                            continue;
                        }

                        // �����ɉ������X�R�A�擾
                        int currentScore = GetTargetScore(condition, i);

                        // �œK�ȃ^�[�Q�b�g���X�V�iisInvert��true�Ȃ�ŏ��l�Afalse�Ȃ�ő�l�j
                        if ( (isInvert && currentScore < score) || (!isInvert && currentScore > score) )
                        {
                            score = currentScore;
                            index = i;
                        }
                    }
                    break;

                default:
                    // ���ׂĂ̏����͏�L�ŃJ�o�[����Ă��邽�߁A�����ɂ͓��B���Ȃ�
                    break;
            }

            return index;
        }

        #region �^�[�Q�b�g���f�w���p�[���\�b�h

        /// <summary>
        /// �����ɉ������X�R�A���擾
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int GetTargetScore(TargetSelectCondition condition, int targetIndex)
        {
            switch ( condition )
            {
                case TargetSelectCondition.���x:
                    return (int)_characterBaseInfo[targetIndex].nowPosition.y;

                case TargetSelectCondition.HP����:
                    return _characterBaseInfo[targetIndex].hpRatio;

                case TargetSelectCondition.HP:
                    return _characterBaseInfo[targetIndex].currentHp;

                case TargetSelectCondition.�G�ɑ_���Ă鐔:
                    return _characterStateInfo[targetIndex].targetingCount;

                case TargetSelectCondition.���v�U����:
                    return _characterAtkStatus[targetIndex].dispAtk;

                case TargetSelectCondition.���v�h���:
                    return _characterDefStatus[targetIndex].dispDef;

                case TargetSelectCondition.�a���U����:
                    return _characterAtkStatus[targetIndex].atk.slash;

                case TargetSelectCondition.�h�ˍU����:
                    return _characterAtkStatus[targetIndex].atk.pierce;

                case TargetSelectCondition.�Ō��U����:
                    return _characterAtkStatus[targetIndex].atk.strike;

                case TargetSelectCondition.���U����:
                    return _characterAtkStatus[targetIndex].atk.fire;

                case TargetSelectCondition.���U����:
                    return _characterAtkStatus[targetIndex].atk.lightning;

                case TargetSelectCondition.���U����:
                    return _characterAtkStatus[targetIndex].atk.light;

                case TargetSelectCondition.�ōU����:
                    return _characterAtkStatus[targetIndex].atk.dark;

                case TargetSelectCondition.�a���h���:
                    return _characterDefStatus[targetIndex].def.slash;

                case TargetSelectCondition.�h�˖h���:
                    return _characterDefStatus[targetIndex].def.pierce;

                case TargetSelectCondition.�Ō��h���:
                    return _characterDefStatus[targetIndex].def.strike;

                case TargetSelectCondition.���h���:
                    return _characterDefStatus[targetIndex].def.fire;

                case TargetSelectCondition.���h���:
                    return _characterDefStatus[targetIndex].def.lightning;

                case TargetSelectCondition.���h���:
                    return _characterDefStatus[targetIndex].def.light;

                case TargetSelectCondition.�Ŗh���:
                    return _characterDefStatus[targetIndex].def.dark;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// �ł��w�C�g�������L�����N�^�[�����Ώۂ�����
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int FindTargetWithHighestHate(in TargetFilter filter, int myIndex)
        {
            float2 myPosition = _characterBaseInfo[myIndex].nowPosition;
            int bestTargetIndex = -1;

            for ( int i = 0; i < _characterBaseInfo.Length; i++ )
            {

                if ( filter.IsPassFilter(
                    _solidData[i],
                    _characterStateInfo[i],
                    myPosition,
                    _characterBaseInfo[i].nowPosition) != 0 )
                {
                    // ���̃L�����N�^�[���ł��w�C�g�������Ă��鑊��̃n�b�V���l
                    int hateTargetHash = _recognizeData[i].hateEnemyHash;
                    if ( hateTargetHash != 0 )
                    {
                        // �n�b�V���l�����L�����N�^�[��S����
                        for ( int j = 0; j < _coldLog.Length; j++ )
                        {
                            if ( _coldLog[j].hashCode == hateTargetHash )
                            {
                                bestTargetIndex = j;
                                break;
                            }
                        }
                    }
                }
            }

            return bestTargetIndex;
        }

        /// <summary>
        /// �Ō�ɍU�������L�����N�^�[������
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int FindLastAttacker(in TargetFilter filter, int myIndex)
        {
            float2 myPosition = _characterBaseInfo[myIndex].nowPosition;
            int bestIndex = -1;

            for ( int i = 0; i < _characterBaseInfo.Length; i++ )
            {

                if ( filter.IsPassFilter(
                    _solidData[i],
                    _characterStateInfo[i],
                    myPosition,
                    _characterBaseInfo[i].nowPosition) != 0 )
                {
                    // ���̃L�����N�^�[���Ō�ɍU����������̃n�b�V���l
                    int attackerHash = _recognizeData[i].attackerHash;
                    if ( attackerHash != 0 )
                    {
                        // �n�b�V���l�����L�����N�^�[��S����
                        for ( int j = 0; j < _coldLog.Length; j++ )
                        {
                            if ( _coldLog[j].hashCode == attackerHash )
                            {
                                bestIndex = j;
                                break;
                            }
                        }
                    }
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// �ł����W���Ă���^�[�Q�b�g������
        /// isInvert�Ή���
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int FindMostDenseTarget(TargetSelectCondition condition, in TargetFilter filter, bool isInvert, int myIndex)
        {
            int bestIndex = -1;
            int bestScore = isInvert ? int.MaxValue : int.MinValue;
            float2 myPosition = _characterBaseInfo[myIndex].nowPosition;

            for ( int i = 0; i < _characterBaseInfo.Length; i++ )
            {
                if ( _coldLog[i].hashCode == 0 )
                    continue;

                if ( filter.IsPassFilter(
                    _solidData[i],
                    _characterStateInfo[i],
                    myPosition,
                    _characterBaseInfo[i].nowPosition) == 0 )
                {
                    continue;
                }

                int density = 0;
                RecognitionData recData = _recognizeData[i];

                switch ( condition )
                {
                    case TargetSelectCondition.�v���C���[�w�c�̖��W�l��:
                        density = recData.nearlyPlayerSideCount;
                        break;
                    case TargetSelectCondition.�����w�c�̖��W�l��:
                        density = recData.nearlyMonsterSideCount;
                        break;
                    case TargetSelectCondition.���̑��w�c�̖��W�l��:
                        density = recData.nearlyOtherSideCount;
                        break;
                }

                // isInvert�ɉ����čő�܂��͍ŏ���I��
                if ( (isInvert && density < bestScore) || (!isInvert && density > bestScore) )
                {
                    bestScore = density;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        #endregion �^�[�Q�b�g���f�w���p�[���\�b�h

        #endregion �^�[�Q�b�g���f����

        #region �s�����f���\�b�h


        private bool JudgeActCondition(int targetIndex, in ActJudgeData condition, int myIndex, bool isSelfJudge)
        {
            // �������g��Ώۂɂ���ꍇ�AtargetIndex��myIndex�ɒu��������
            targetIndex = isSelfJudge ? myIndex : targetIndex;

            // �t�B���^�[�`�F�b�N
            // �Ώۂ��t�B���^�[�ɓ��Ă͂܂��Ԃ�
            if ( condition.filter.IsPassFilter(
                this._solidData[targetIndex],
                this._characterStateInfo[targetIndex],
                this._characterBaseInfo[myIndex].nowPosition,
                this._characterBaseInfo[targetIndex].nowPosition) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                // 1. �Ώۂ��t�B���^�[�ɓ��Ă͂܂鎞
                case MoveSelectCondition.�Ώۂ��t�B���^�[�ɓ��Ă͂܂鎞:
                    return true; // �t�B���^�[�`�F�b�N�͊��ɒʉ�

                // 2. �Ώۂ�HP����芄���̎�
                case MoveSelectCondition.�Ώۂ�HP����芄���̎�:
                    int hpRatio = _characterBaseInfo[targetIndex].hpRatio;
                    return hpRatio >= condition.judgeLowerValue && hpRatio <= condition.judgeUpperValue;

                // 3. �Ώۂ�MP����芄���̎�
                case MoveSelectCondition.�Ώۂ�MP����芄���̎�:
                    int mpRatio = _characterBaseInfo[targetIndex].mpRatio;
                    return mpRatio >= condition.judgeLowerValue && mpRatio <= condition.judgeUpperValue;

                // 4. �Ώۂ̎��͂ɓ���w�c�̃L���������ȏ㖧�W���Ă��鎞
                case MoveSelectCondition.�Ώۂ̎��͂ɓ���w�c�̃L���������ȏ㖧�W���Ă��鎞:

                    // ��ڂ̒l��w�c�ɕϊ��B
                    switch ( ((CharacterBelong)condition.judgeLowerValue) )
                    {
                        case CharacterBelong.�v���C���[:
                            // �v���C���[�w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyPlayerSideCount >= condition.judgeUpperValue;
                        case CharacterBelong.����:
                            // �����w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyMonsterSideCount >= condition.judgeUpperValue;
                        case CharacterBelong.���̑�:
                            // ���̑��w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyOtherSideCount >= condition.judgeUpperValue;
                    }

                    return false;

                // 4. �Ώۂ̎��͂ɓ���w�c�̃L���������ȉ��������Ȃ���
                case MoveSelectCondition.�Ώۂ̎��͂ɓ���w�c�̃L���������ȉ��������Ȃ���:

                    // ��ڂ̒l��w�c�ɕϊ��B
                    switch ( ((CharacterBelong)condition.judgeLowerValue) )
                    {
                        case CharacterBelong.�v���C���[:
                            // �v���C���[�w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyPlayerSideCount <= condition.judgeUpperValue;
                        case CharacterBelong.����:
                            // �����w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyMonsterSideCount <= condition.judgeUpperValue;
                        case CharacterBelong.���̑�:
                            // ���̑��w�c�̃L�����𐔂���
                            return _recognizeData[targetIndex].nearlyOtherSideCount <= condition.judgeUpperValue;
                    }

                    return false;

                // 5. �Ώۂ̎��͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞
                case MoveSelectCondition.�Ώۂ̎��͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:

                    // �����ł́AjudgeLowerValue���[���Ȃ� or ����
                    if ( condition.judgeLowerValue == 0 )
                    {
                        return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) > 0);
                    }

                    // and����
                    else
                    {
                        return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) == condition.judgeUpperValue);
                    }

                // 6. �Ώۂ�����̐��̓G�ɑ_���Ă��鎞
                case MoveSelectCondition.�Ώۂ�����̐��̓G�ɑ_���Ă��鎞:
                    int targetingCount = _characterStateInfo[targetIndex].targetingCount;
                    return targetingCount >= condition.judgeLowerValue && targetingCount <= condition.judgeUpperValue;

                // 8. �Ώۂ̈�苗���ȓ��ɔ�ѓ�����鎞
                case MoveSelectCondition.�Ώۂ̈�苗���ȓ��ɔ�ѓ�����鎞:
                    float detectDistance = _recognizeData[targetIndex].detectNearestAttackDistance;
                    return detectDistance > 0 && detectDistance >= condition.judgeLowerValue && detectDistance <= condition.judgeUpperValue;

                // 9. ����̃C�x���g������������
                case MoveSelectCondition.����̃C�x���g������������:
                    // �����ł́AjudgeLowerValue���[���Ȃ� or ����
                    if ( condition.judgeLowerValue == 0 )
                    {
                        return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) > 0);
                    }

                    // and����
                    else
                    {
                        return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) == condition.judgeUpperValue);
                    }

                // 10. �^�[�Q�b�g�������̏ꍇ
                case MoveSelectCondition.�^�[�Q�b�g�������̏ꍇ:
                    return targetIndex == myIndex;

                // 11. �����Ȃ�
                case MoveSelectCondition.�����Ȃ�:
                default:
                    return true;
            }
        }

        #region �s�����f�w���p�[���\�b�h

        /// <summary>
        /// ���W�x�`�F�b�N�i����L�����N�^�[�p�j
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckDensity(int targetIndex, int lowerValue, int upperValue)
        {
            RecognitionData recData = _recognizeData[targetIndex];

            // �S�w�c�̍��v���W�l��
            int totalDensity = recData.nearlyPlayerSideCount +
                              recData.nearlyMonsterSideCount +
                              recData.nearlyOtherSideCount;

            return totalDensity >= lowerValue && totalDensity <= upperValue;
        }

        /// <summary>
        /// ���W�x�����`�F�b�N�i�t�B���^�[�t���j
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckDensityCondition(in TargetFilter filter, int lowerValue, int upperValue, int myIndex)
        {
            // �t�B���^�[�����𖞂����L�����N�^�[�̖��W�󋵂��`�F�b�N
            for ( int i = 0; i < _characterBaseInfo.Length; i++ )
            {
                if ( _coldLog[i].hashCode == 0 )
                    continue;

                if ( filter.IsPassFilter(
                    _solidData[i],
                    _characterStateInfo[i],
                    _characterBaseInfo[myIndex].nowPosition,
                    _characterBaseInfo[i].nowPosition) != 0 )
                {
                    if ( CheckDensity(i, lowerValue, upperValue) )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// �ߋ����̃^�[�Q�b�g���`�F�b�N
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckNearbyTargets(in TargetFilter filter, int lowerValue, int upperValue, int myIndex)
        {
            RecognitionData recData = _recognizeData[myIndex];
            CharacterBelong belongFilter = filter.GetTargetType();

            int nearbyCount = 0;
            if ( (belongFilter & CharacterBelong.�v���C���[) != 0 )
                nearbyCount += recData.nearlyPlayerSideCount;
            if ( (belongFilter & CharacterBelong.����) != 0 )
                nearbyCount += recData.nearlyMonsterSideCount;
            if ( (belongFilter & CharacterBelong.���̑�) != 0 )
                nearbyCount += recData.nearlyOtherSideCount;

            return nearbyCount >= lowerValue && nearbyCount <= upperValue;
        }

        /// <summary>
        /// ����L�����N�^�[�̋ߋ����^�[�Q�b�g���`�F�b�N
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckNearbyTargetsForCharacter(int targetIndex, int lowerValue, int upperValue)
        {
            RecognitionData recData = _recognizeData[targetIndex];

            int totalNearby = recData.nearlyPlayerSideCount +
                             recData.nearlyMonsterSideCount +
                             recData.nearlyOtherSideCount;

            return totalNearby >= lowerValue && totalNearby <= upperValue;
        }


        #endregion �s�����f�w���p�[���\�b�h

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
        /// �V�[�h�̒l��ύX���A���̒l��101�Ŋ������]���Ԃ��B
        /// XorShift32�A���S���Y�����g�p�c�Ƃ�����Unity.Mathmatics.Random�̎����Ɠ���
        /// 
        /// </summary>
        /// <returns>0-100�̊Ԃŏ�]������������_���l</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private uint GetRandomValueZeroToHundred(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed % 101;
        }

        /// <summary>
        /// ���������̏�������
        /// Unity.Mathmatics.Random�̎����Ɠ������@�ŃV�[�h�̃r�b�g���g�U�����V�[�h����鏈��
        /// </summary>
        /// <param name="seed">�V�[�h�l�̃x�[�X</param>
        /// <returns>�r�b�g���g�U���ꂽ�V�[�h�l</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint SeedGenerate(uint seed)
        {
            seed = (seed ^ 61u) ^ (seed >> 16);
            seed *= 9u;
            seed = seed ^ (seed >> 4);
            seed *= 0x27d4eb2du;
            seed = seed ^ (seed >> 15);

            return seed;
        }

    }

}

