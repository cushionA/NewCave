using System;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.AIManager;
using static CharacterController.BaseController;
using static CharacterController.BrainStatus;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace CharacterController
{
    /// <summary>
    /// AI�����f���s��Job
    /// ����Ƃ��Ă̓w�C�g���f�i�����ň�ԑ������c�͏o���Ă����j���s�����f���Ώېݒ�i�U��/�h��̏ꍇ�w�C�g�A����ȊO�̏ꍇ�͔C�ӏ�����D�揇�ɔ��f�j
    /// �w�C�g�����̓`�[���w�C�g����ԍ������w�c���Ƃɏo���Ă����āA�l�w�C�g�������炻��𒴂��邩�A�Ō��Ă�������
    /// UnsafeList<CharacterData> characterData�͘_���폜�Œ��g�Ȃ��f�[�^�����邩�炻�̔��ʂ����Ȃ��Ƃ�
    /// </summary>
    [BurstCompile]
    public struct JobAI : IJobParallelFor
    {
        /// <summary>
        /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> teamHate;

        /// <summary>
        /// CharacterDataDic�̕ϊ���
        /// </summary>
        [Unity.Collections.ReadOnly]
        public UnsafeList<BrainStatus.CharacterData> characterData;

        /// <summary>
        /// ���ݎ���
        /// </summary>
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
        /// characterData��judgeResult�̃C���f�b�N�X���x�[�X�ɏ�������B
        /// </summary>
        /// <param name="index"></param>
        [BurstCompile]
        public void Execute(int index)
        {
            // �_���폜���m�F���A�폜����Ă���Ζ����B
            if ( this.characterData[index].IsLogicalDelate() )
            {
                return;
            }

            // ���ʂ̍\���̂��쐬�B
            MovementInfo resultData = new();

            // ���݂̍s���̃X�e�[�g�𐔒l�ɕϊ�
            int nowMode = (int)this.characterData[index].liveData.actState;

            // ���f���Ԃ��o�߂��������m�F�B
            // �o�߂��ĂȂ��Ȃ珈�����Ȃ��B
            // ���邢�̓^�[�Q�b�g�������ꍇ�����肵�����B�`�[���w�C�g�Ɋ܂܂�ĂȂ���΁B���ꂾ�Ɩ������w�C�g�̎��ǂ�����́B
            // �L�������S���ɑS�L�����ɑ΂��^�[�Q�b�g���Ă邩�ǂ������m�F����悤�ɂ��悤�B�ŁA�^�[�Q�b�g��������O�񔻒f���Ԃ��}�C�i�X�ɂ���B
            if ( this.nowTime - this.characterData[index].lastJudgeTime < this.characterData[index].brainData[nowMode].judgeInterval )
            {
                resultData.result = JudgeResult.�����Ȃ�;

                // �ړ��������f�����͂���B
                //�@���m�ɂ͋�������B
                // �n�b�V���l�����Ă񂾂���W���u����o����ł�낤�B
                // Result�����߂���

                // ���ʂ�ݒ�B
                this.judgeResult[index] = resultData;

                return;
            }

            CharacterData myData = this.characterData[index];

            // characterData[index].brainData[nowMode].judgeInterval �݂����Ȓl�͉�����g���Ȃ�ꎞ�ϐ��ɕۑ����Ă����B

            // �܂����f���Ԃ̌o�߂��m�F
            // ���ɐ��`�T���ōs���Ԋu�̊m�F���s���A�G�ɂ̓w�C�g���f���s���B
            // �S�Ă̍s���������m�F���A�ǂ̏������m�肵������z��ɓ����
            // ���Ȃ݂�0�A�܂��ԗD��x�������ݒ肪���Ă͂܂����ꍇ�͗L�������킳�����[�v���f�B
            // �t�ɉ������Ă͂܂�Ȃ������ꍇ�͕⌇���������s�B
            // ���Ȃ݂ɓ����A�Ƃ��x���A�̃��[�h������܂萶�����ĂȂ���ȁB
            // ���[�h���Ƃɏ����ݒ�ł���悤�ɂ��邩�B
            // �ŁA����������Ȃ����[�h�ɂ��Ă͍s�����f���Ȃ�

            // �m�F�Ώۂ̏����̓r�b�g�l�ŕۑ��B
            // �����ăr�b�g�̐ݒ肪��������̂݊m�F����B
            // ��������������r�b�g�͏����B
            // ����ɁA1�Ƃ�2�ԖڂƂ��̂��D��x�������������t�����炻��ȉ��̏����͑S�������B
            // �ŁA���i�K�ň�ԗD��x��������������������ۑ����Ă���
            // ���̏�ԂōŌ�܂ő������ăw�C�g�l�ݒ����������B
            // ���Ȃ݂Ƀw�C�g�l�ݒ�͎������w�C�g�����Ă鑊��̃w�C�g�𑫂����l���m�F���邾������
            // �w�C�g�����̎d�g�ݍl���Ȃ��ƂȁB30�p�[�Z���g�����炷�H�@���[�[�[�[�[�[�[�[

            // �s�������̒��őO��𖞂��������̂��擾����r�b�g
            // �Ȃ��A���ۂ̔��f���ɂ��D��I�ȏ������������ꂽ�ꍇ�͏�ʃr�b�g�͂܂Ƃ߂ď����B
            int enableCondition = 0;

            // �O��ƂȂ鎩���ɂ��ẴX�L�b�v�������m�F�B
            // �Ō�̏����͕⌇�����Ȃ̂Ŗ���
            for ( int i = 0; i < myData.brainData[nowMode].actCondition.Length - 1; i++ )
            {

                SkipJudgeData skipData = myData.brainData[nowMode].actCondition[i].skipData;

                // �X�L�b�v���������߂��Ĕ��f
                if ( skipData.skipCondition == SkipJudgeCondition.�����Ȃ� || JudgeSkipByCondition(skipData, myData) == 1 )
                {
                    enableCondition |= 1 << i;
                }

            }

            // �����𖞂������s���̒��ōł��D��I�Ȃ��́B
            // �����l�͍Ō�̏����A�܂�����Ȃ��̕⌇����
            int selectMove = myData.brainData[nowMode].actCondition.Length - 1;

            //// �w�C�g�����m�F�p�̈ꎞ�o�b�t�@
            //NativeArray<Vector2Int> hateIndex = new NativeArray<Vector2Int>(myData.brainData[nowMode].hateCondition.Length, Allocator.Temp);
            //NativeArray<TargetJudgeData> hateCondition = myData.brainData[nowMode].hateCondition;

            //// �w�C�g�m�F�o�b�t�@�̏�����
            //for ( int i = 0; i < hateIndex.Length; i++ )
            //{
            //    if ( hateCondition[i].isInvert )
            //    {
            //        hateIndex[i].Set(int.MaxValue, -1);
            //    }
            //    else
            //    {
            //        hateIndex[i].Set(int.MinValue, -1);
            //    }
            //}

            // �L�����f�[�^���m�F����B
            for ( int i = 0; i < this.characterData.Length; i++ )
            {
                // �����Ƙ_���폜�Ώۂ̓X�L�b�v
                if ( index == i || this.characterData[i].IsLogicalDelate() )
                {
                    continue;
                }

                // �ǂݎ���p��NativeContainer�ւ̃A�N�Z�X������邽�߂Ƀw�C�g�n�̏����͕������邱�Ƃ�

                //// �܂��w�C�g���f�B
                //// �e�w�C�g�����ɂ��āA�����X�V���L�^����B
                //for ( int j = 0; j < hateCondition.Length; j++ )
                //{
                //    int value = hateIndex[j].x;
                //    if ( targetFunctions[(int)hateCondition[j].judgeCondition].Invoke(hateCondition[j], characterData[i], ref value) )
                //    {
                //        hateIndex[j].Set(value, i);
                //    }
                //}

                // �s�����f�B
                // �����̓X�C�b�`���g�����B�A������Int�l�Ȃ�R���p�C�����W�����v�e�[�u������Ă����̂�
                if ( enableCondition != 0 )
                {
                    for ( int j = 0; j < myData.brainData[nowMode].actCondition.Length - 1; j++ )
                    {
                        // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ��B
                        if ( CheckActCondition(myData.brainData[nowMode].actCondition[j], myData, this.characterData[i], this.teamHate) )
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

            //// �w�C�g�l�̔��f
            //for ( int i = 0; i < hateIndex.Length; i++ )
            //{
            //    int targetHate = 0;
            //    int targetHash = characterData[hateIndex[i].y].hashCode;

            //    if ( myData.personalHate.ContainsKey(targetHash) )
            //    {
            //        targetHate += (int)myData.personalHate[targetHash];
            //    }

            //    if ( teamHate[(int)myData.liveData.belong].ContainsKey(targetHash) )
            //    {
            //        targetHate += teamHate[(int)myData.liveData.belong][targetHash];
            //    }

            //    // �Œ�10�͕ۏ؁B
            //    targetHate = Math.Min(10,targetHate);

            //    int newHate = (int)(targetHate * hateCondition[i].useAttackOrHateNum);

            //}

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
            TargetJudgeData targetJudgeData = myData.brainData[nowMode].actCondition[selectMove].targetCondition;

            _ = targetJudgeData.isInvert == BitableBool.TRUE ? int.MaxValue : int.MinValue;
            int newTargetHash = 0;

            // ��ԕύX�̏ꍇ�����Ŗ߂�B
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.�s�v_��ԕύX )
            {
                // �w���ԂɈڍs
                resultData.result = JudgeResult.�V�������f������;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // ���f���ʂ�ݒ�B
                this.judgeResult[index] = resultData;
                return;
            }
            // ����ȊO�ł���΃^�[�Q�b�g�𔻒f
            else
            {
                int tIndex = JudgeTargetByCondition(targetJudgeData, this.characterData, myData, this.teamHate);
                if ( tIndex >= 0 )
                {
                    newTargetHash = this.characterData[tIndex].hashCode;

                    //   Debug.Log($"�^�[�Q�b�g���f����:{tIndex}�̂�B  Hash�F{newTargetHash}");
                }
                // �����Ń^�[�Q�b�g�������ĂȂ���Αҋ@�Ɉڍs�B
                else
                {
                    // �ҋ@�Ɉڍs
                    resultData.result = JudgeResult.�V�������f������;
                    resultData.actNum = (int)ActState.�ҋ@;
                    //  Debug.Log($"�^�[�Q�b�g���f���s�@�s���ԍ�{selectMove}");
                }
            }

            resultData.result = JudgeResult.�V�������f������;
            resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;
            resultData.targetHash = newTargetHash;

            // ���f���ʂ�ݒ�B
            this.judgeResult[index] = resultData;

            // �e�X�g�d�l�L�^
            // �v�f����10 �` 1000��
            // �X�e�[�^�X�͂������x�[�X�ƂȂ�e���v����CharacterData����āA���̐��l��������R�[�h�����Ă��B
            // �ŁAJob�V�X�e�����܂�܃x�^�ڐA�������ʂ̃N���X���쐬���āA���x���r
            // �Ō�͓�̃e�X�g�ɂ��쐬���ꂽpublic UnsafeList<MovementInfo> judgeResult�@�̓��ꐫ�������ɂ񂵂āA���x�̃`�F�b�N�܂ŏI���

        }

        #region �X�L�b�v�������f

        /// <summary>
        /// SkipJudgeCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        /// </summary>
        /// <param name="skipData">�X�L�b�v����p�f�[�^</param>
        /// <param name="charaData">�L�����N�^�[�f�[�^</param>
        /// <returns>�����ɍ��v����ꍇ��1�A����ȊO��0</returns>
        [BurstCompile]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static int JudgeSkipByCondition(in SkipJudgeData skipData, in CharacterData charaData)
        {
            SkipJudgeCondition condition = skipData.skipCondition;
            switch ( condition )
            {
                case SkipJudgeCondition.������HP����芄���̎�:
                    // �e�������ʂ� int �ŕ]��
                    int equalConditionHP = skipData.judgeValue == charaData.liveData.hpRatio ? 1 : 0;
                    int lessConditionHP = skipData.judgeValue < charaData.liveData.hpRatio ? 1 : 0;
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
                    int equalConditionMP = skipData.judgeValue == charaData.liveData.mpRatio ? 1 : 0;
                    int lessConditionMP = skipData.judgeValue < charaData.liveData.mpRatio ? 1 : 0;
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
        [BurstCompile]
        public static bool CheckActCondition(in BehaviorData condition, in CharacterData myData, in CharacterData targetData, in NativeHashMap<int2, int> tHate)
        {
            bool result = true;

            // �t�B���^�[�ʉ߂��Ȃ��Ȃ�߂�B
            if ( condition.actCondition.filter.IsPassFilter(targetData) == 0 )
            {
                return false;
            }

            switch ( condition.actCondition.judgeCondition )
            {
                case ActJudgeCondition.�w��̃w�C�g�l�̓G�����鎞:

                    int targetHash = targetData.hashCode;
                    int targetHate = 0;

                    if ( myData.personalHate.TryGetValue(targetHash, out int hate) )
                    {
                        targetHate += hate;
                    }

                    // �`�[���̃w�C�g��int2�Ŋm�F����B
                    int2 hateKey = new((int)myData.liveData.belong, targetHash);

                    if ( tHate.TryGetValue(targetHash, out int teamHate) )
                    {
                        targetHate += teamHate;
                    }

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.actCondition.isInvert == BitableBool.FALSE )
                    {
                        result = targetHate >= condition.actCondition.judgeValue;
                    }
                    else
                    {
                        result = targetHate <= condition.actCondition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.HP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.actCondition.isInvert == BitableBool.FALSE )
                    {
                        result = targetData.liveData.hpRatio >= condition.actCondition.judgeValue;
                    }
                    else
                    {
                        result = targetData.liveData.hpRatio <= condition.actCondition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.MP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.actCondition.isInvert == BitableBool.FALSE )
                    {
                        result = targetData.liveData.mpRatio >= condition.actCondition.judgeValue;
                    }
                    else
                    {
                        result = targetData.liveData.mpRatio <= condition.actCondition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.�ݒ苗���ɑΏۂ����鎞:

                    // ���̋����Ŕ��肷��B
                    int judgeDist = condition.actCondition.judgeValue * condition.actCondition.judgeValue;

                    // ���̋����̓��B
                    int distance = (int)(math.distancesq(targetData.liveData.nowPosition, myData.liveData.nowPosition));

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.actCondition.isInvert == BitableBool.FALSE )
                    {
                        result = distance >= judgeDist;
                    }
                    else
                    {
                        result = distance <= judgeDist;
                    }

                    return result;

                case ActJudgeCondition.����̑����ōU������Ώۂ����鎞:

                    // �ʏ�͂��鎞�A�t�̏ꍇ�͂��Ȃ��Ƃ�
                    if ( condition.actCondition.isInvert == BitableBool.FALSE )
                    {
                        result = ((int)targetData.solidData.attackElement & condition.actCondition.judgeValue) > 0;
                    }
                    else
                    {
                        result = ((int)targetData.solidData.attackElement & condition.actCondition.judgeValue) == 0;
                    }

                    return result;

                case ActJudgeCondition.����̐��̓G�ɑ_���Ă��鎞:
                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.actCondition.isInvert == BitableBool.FALSE )
                    {
                        result = targetData.targetingCount >= condition.actCondition.judgeValue;
                    }
                    else
                    {
                        result = targetData.targetingCount <= condition.actCondition.judgeValue;
                    }

                    return result;

                default: // �����Ȃ� (0) �܂��͖���`�̒l
                    return result;
            }
        }

        #region�@�^�[�Q�b�g���f����

        /// <summary>
        /// TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        /// </summary>
        /// <param name="judgeData"></param>
        /// <param name="targetData"></param>
        /// <param name="score"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        // TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        [BurstCompile]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static int JudgeTargetByCondition(in TargetJudgeData judgeData, in UnsafeList<CharacterData> cData, in CharacterData myData, in NativeHashMap<int2, int> tHate)
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
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        int height = (int)cData[i].liveData.nowPosition.y;

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
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.hpRatio > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.hpRatio;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.hpRatio < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.hpRatio;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.HP:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.currentHp > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.currentHp;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.currentHp < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.currentHp;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�G�ɑ_���Ă鐔:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].targetingCount > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].targetingCount;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].targetingCount < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].targetingCount;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���v�U����:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.dispAtk > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.dispAtk;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.dispAtk < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.dispAtk;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���v�h���:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.dispDef > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.dispDef;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.dispDef < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.dispDef;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�a���U����:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.atk.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.atk.slash;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.atk.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.atk.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�h�ˍU����:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.atk.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.atk.pierce;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.atk.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.atk.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ō��U����:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.atk.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.atk.strike;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.atk.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.atk.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.atk.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.atk.fire;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.atk.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.atk.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.atk.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.atk.lightning;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.atk.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.atk.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.atk.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.atk.light;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.atk.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.atk.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�ōU����:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.atk.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.atk.dark;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.atk.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.atk.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�a���h���:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.def.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.def.slash;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.def.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.def.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�h�˖h���:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.def.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.def.pierce;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.def.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.def.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ō��h���:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.def.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.def.strike;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.def.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.def.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.def.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.def.fire;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.def.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.def.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.def.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.def.lightning;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.def.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.def.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.def.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.def.light;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.def.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.def.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ŗh���:
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = cData[i].liveData.def.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = cData[i].liveData.def.dark;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = cData[i].liveData.def.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = cData[i].liveData.def.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.����:
                    // �����̈ʒu���L���b�V��
                    float myPositionX = myData.liveData.nowPosition.x;
                    float myPositionY = myData.liveData.nowPosition.y;
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( cData[i].hashCode == myData.hashCode || judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }
                        // �}���n�b�^�������ŉ��ߔ��f
                        float distance = Unity.Mathematics.math.abs(myPositionX - cData[i].liveData.nowPosition.x) +
                                        Unity.Mathematics.math.abs(myPositionY - cData[i].liveData.nowPosition.y);

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
                    return myData.hashCode;

                case TargetSelectCondition.�v���C���[:
                    // ��������̃V���O���g���Ƀv���C���[��Hash�͎������Ƃ�
                    // newTargetHash = characterData[i].hashCode;
                    return -1;

                case TargetSelectCondition.�w��Ȃ�_�w�C�g�l:
                    // �^�[�Q�b�g�I�胋�[�v
                    for ( int i = 0; i < cData.Length; i++ )
                    {
                        // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( i == index || judgeData.filter.IsPassFilter(cData[i]) == 0 )
                        {
                            continue;
                        }
                        // �w�C�g�l���m�F
                        int targetHash = cData[i].hashCode;
                        int targetHate = 0;
                        if ( cData[index].personalHate.TryGetValue(targetHash, out int hate) )
                        {
                            targetHate += hate;
                        }

                        // �`�[���̃w�C�g��int2�Ŋm�F����B
                        int2 hateKey = new((int)cData[index].liveData.belong, targetHash);
                        if ( tHate.TryGetValue(targetHash, out int teamHate) )
                        {
                            targetHate += teamHate;
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

