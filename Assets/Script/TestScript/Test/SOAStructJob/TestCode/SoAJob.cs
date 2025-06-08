using System;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static TestScript.Collections.SoACharaDataDic;
using static TestScript.SOATest.SOAStatus;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace TestScript
{
    /// <summary>
    /// AI�����f���s��Job
    /// ����Ƃ��Ă̓w�C�g���f�i�����ň�ԑ������c�͏o���Ă����j���s�����f���Ώېݒ�i�U��/�h��̏ꍇ�w�C�g�A����ȊO�̏ꍇ�͔C�ӏ�����D�揇�ɔ��f�j
    /// �w�C�g�����̓`�[���w�C�g����ԍ������w�c���Ƃɏo���Ă����āA�l�w�C�g�������炻��𒴂��邩�A�Ō��Ă�������
    /// UnsafeList<CharacterData> characterData�͘_���폜�Œ��g�Ȃ��f�[�^�����邩�炻�̔��ʂ����Ȃ��Ƃ�
    /// </summary>
    [BurstCompile]
    public struct SoAJob : IJobParallelFor
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
        /// </summary>
        public NativeArray<PersonalHateContainer> pHate;

        /// <summary>
        /// ���ݎ���
        /// </summary>
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
        /// ��ԂɊ�Â��čŏ��Ƀf�[�^������������B
        /// </summary>
        [ReadOnly]
        public NativeArray<BrainDataForJob> brainArray;

        /// <summary>
        /// characterData��judgeResult�̃C���f�b�N�X���x�[�X�ɏ�������B
        /// </summary>
        /// <param name="index"></param>
        [BurstCompile]
        public void Execute(int index)
        {

            // ���ʂ̍\���̂��쐬�B
            CharacterController.BaseController.MovementInfo resultData = new();

            // ���݂̍s���̃X�e�[�g�𐔒l�ɕϊ�
            int nowMode = (int)_characterStateInfo[index].actState;

            BrainSettingForJob brainData = brainArray[_coldLog[index].characterID - 1].brainSetting[nowMode];

            // �C���^�[�o�����܂Ƃ߂Ď擾
            // x���s���Ay���ړ����f
            float2 intervals = brainArray[_coldLog[index].characterID - 1].GetInterval();

            // ���f���Ԃ��o�߂��������m�F�B
            // �o�߂��ĂȂ��Ȃ珈�����Ȃ��B
            // ���邢�̓^�[�Q�b�g�������ꍇ�����肵�����B�`�[���w�C�g�Ɋ܂܂�ĂȂ���΁B���ꂾ�Ɩ������w�C�g�̎��ǂ�����́B
            // �L�������S���ɑS�L�����ɑ΂��^�[�Q�b�g���Ă邩�ǂ������m�F����悤�ɂ��悤�B�ŁA�^�[�Q�b�g��������O�񔻒f���Ԃ��}�C�i�X�ɂ���B
            if ( this.nowTime - this._coldLog[index].lastJudgeTime < intervals.x )
            {
                resultData.result = CharacterController.BaseController.JudgeResult.�����Ȃ�;

                // �ړ��������f�����͂���B
                //�@���m�ɂ͋�������B
                // �n�b�V���l�����Ă񂾂���W���u����o����ł�낤�B
                // Result�����߂���

                // ���ʂ�ݒ�B
                this.judgeResult[index] = resultData;

                return;
            }

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
            for ( int i = 0; i < brainData.behaviorSetting.Length - 1; i++ )
            {

                SkipJudgeData skipData = brainData.behaviorSetting[i].skipData;

                // �X�L�b�v���������߂��Ĕ��f
                if ( skipData.skipCondition == SkipJudgeCondition.�����Ȃ� || JudgeSkipByCondition(skipData, index) == 1 )
                {
                    enableCondition |= 1 << i;
                }
            }

            // �����𖞂������s���̒��ōł��D��I�Ȃ��́B
            // �����l�͍Ō�̏����A�܂�����Ȃ��̕⌇����
            int selectMove = brainData.behaviorSetting.Length - 1;

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
            for ( int i = 0; i < _solidData.Length; i++ )
            {
                // �����̓X�L�b�v
                if ( index == i )
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
                    for ( int j = 0; j < brainData.behaviorSetting.Length - 1; j++ )
                    {
                        // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ��B
                        if ( CheckActCondition(brainData.behaviorSetting[j].actCondition, index, i) )
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
            TargetJudgeData targetJudgeData = brainData.behaviorSetting[selectMove].targetCondition;

            _ = targetJudgeData.isInvert == BitableBool.TRUE ? int.MaxValue : int.MinValue;
            int newTargetHash = 0;

            // ��ԕύX�̏ꍇ�����Ŗ߂�B
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.�s�v_��ԕύX )
            {
                // �w���ԂɈڍs
                resultData.result = CharacterController.BaseController.JudgeResult.�V�������f������;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // ���f���ʂ�ݒ�B
                this.judgeResult[index] = resultData;
                return;
            }
            // ����ȊO�ł���΃^�[�Q�b�g�𔻒f
            else
            {
                int tIndex = JudgeTargetByCondition(targetJudgeData, index);
                if ( tIndex >= 0 )
                {
                    newTargetHash = this._coldLog[tIndex].hashCode;

                    //   Debug.Log($"�^�[�Q�b�g���f����:{tIndex}�̂�B  Hash�F{newTargetHash}");
                }
                // �����Ń^�[�Q�b�g�������ĂȂ���Αҋ@�Ɉڍs�B
                else
                {
                    // �ҋ@�Ɉڍs
                    resultData.result = CharacterController.BaseController.JudgeResult.�V�������f������;
                    resultData.actNum = (int)ActState.�ҋ@;
                    //  Debug.Log($"�^�[�Q�b�g���f���s�@�s���ԍ�{selectMove}");
                }
            }

            resultData.result = CharacterController.BaseController.JudgeResult.�V�������f������;
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
        public int JudgeSkipByCondition(in SkipJudgeData skipData, int myIndex)
        {
            SkipJudgeCondition condition = skipData.skipCondition;
            switch ( condition )
            {
                case SkipJudgeCondition.������HP����芄���̎�:
                    // �e�������ʂ� int �ŕ]��
                    int equalConditionHP = skipData.judgeValue == _characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int lessConditionHP = skipData.judgeValue < _characterBaseInfo[myIndex].hpRatio ? 1 : 0;
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
                    int equalConditionMP = skipData.judgeValue == _characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int lessConditionMP = skipData.judgeValue < _characterBaseInfo[myIndex].mpRatio ? 1 : 0;
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
        public bool CheckActCondition(in ActJudgeData condition, int myIndex,
            int targetIndex)
        {
            bool result = true;

            // �t�B���^�[�ʉ߂��Ȃ��Ȃ�߂�B
            if ( condition.filter.IsPassFilter(_solidData[targetIndex], _characterStateInfo[targetIndex]) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                case ActJudgeCondition.�w��̃w�C�g�l�̓G�����鎞:

                    int targetHash = _coldLog[targetIndex].hashCode;
                    int targetHate = 0;

                    if ( pHate[myIndex].personalHate.TryGetValue(targetHash, out int hate) )
                    {
                        targetHate += hate;
                    }

                    // �`�[���̃w�C�g��int2�Ŋm�F����B
                    int2 hateKey = new((int)_characterStateInfo[targetIndex].belong, targetHash);

                    if ( teamHate.TryGetValue(hateKey, out int tHate) )
                    {
                        targetHate += tHate;
                    }

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = targetHate >= condition.judgeValue;
                    }
                    else
                    {
                        result = targetHate <= condition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.HP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = _characterBaseInfo[targetIndex].hpRatio >= condition.judgeValue;
                    }
                    else
                    {
                        result = _characterBaseInfo[targetIndex].hpRatio <= condition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.MP����芄���̑Ώۂ����鎞:

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = _characterBaseInfo[targetIndex].mpRatio >= condition.judgeValue;
                    }
                    else
                    {
                        result = _characterBaseInfo[targetIndex].mpRatio <= condition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.�ݒ苗���ɑΏۂ����鎞:

                    // ���̋����Ŕ��肷��B
                    int judgeDist = condition.judgeValue * condition.judgeValue;

                    // ���̋����̓��B
                    int distance = (int)(math.distancesq(_characterBaseInfo[targetIndex].nowPosition, _characterBaseInfo[targetIndex].nowPosition));

                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.isInvert == BitableBool.FALSE )
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
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = ((int)_solidData[targetIndex].attackElement & condition.judgeValue) > 0;
                    }
                    else
                    {
                        result = ((int)_solidData[targetIndex].attackElement & condition.judgeValue) == 0;
                    }

                    return result;

                case ActJudgeCondition.����̐��̓G�ɑ_���Ă��鎞:
                    // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = _characterStateInfo[targetIndex].targetingCount >= condition.judgeValue;
                    }
                    else
                    {
                        result = _characterStateInfo[targetIndex].targetingCount <= condition.judgeValue;
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
        /// <returns>�Ԃ�l�͍s���^�[�Q�b�g�̃C���f�b�N�X</returns>
        // TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
        [BurstCompile]
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
                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        int height = (int)_characterBaseInfo[i].nowPosition.y;

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
                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterBaseInfo[i].hpRatio > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterBaseInfo[i].hpRatio;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterBaseInfo[i].hpRatio < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterBaseInfo[i].hpRatio;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.HP:

                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterBaseInfo[i].currentHp > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterBaseInfo[i].currentHp;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterBaseInfo[i].currentHp < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterBaseInfo[i].currentHp;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�G�ɑ_���Ă鐔:
                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterStateInfo[i].targetingCount > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterStateInfo[i].targetingCount;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterStateInfo[i].targetingCount < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterStateInfo[i].targetingCount;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���v�U����:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].dispAtk > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].dispAtk;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterAtkStatus[i].dispAtk < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].dispAtk;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���v�h���:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].dispDef > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].dispDef;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterDefStatus[i].dispDef < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].dispDef;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�a���U����:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.slash;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�h�ˍU����:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.pierce;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ō��U����:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.strike;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.fire;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.lightning;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���U����:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.light;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�ōU����:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.dark;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�a���h���:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.slash;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterDefStatus[i].def.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�h�˖h���:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.pierce;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterDefStatus[i].def.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ō��h���:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.strike;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterDefStatus[i].def.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.fire;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterDefStatus[i].def.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.lightning;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterDefStatus[i].def.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.���h���:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.light;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterDefStatus[i].def.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.�Ŗh���:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // ��ԍ����L�����N�^�[�����߂�
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.dark;
                                index = i;
                            }
                        }
                        // ��ԒႢ�L�����N�^�[�����߂�
                        else
                        {
                            int isLess = _characterDefStatus[i].def.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.����:

                    // �����̈ʒu���L���b�V��
                    float2 myPosition = _characterBaseInfo[myIndex].nowPosition;

                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( myIndex == i || judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }
                        // 2�拗���ŉ��ߔ��f
                        // float������덷�������S�z����
                        float distance = Unity.Mathematics.math.distancesq(myPosition, _characterBaseInfo[i].nowPosition);

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
                    for ( int i = 0; i < _solidData.Length; i++ )
                    {
                        // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                        if ( i == index || judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }
                        // �w�C�g�l���m�F
                        int targetHash = _coldLog[i].hashCode;
                        int targetHate = 0;

                        if ( pHate[myIndex].personalHate.TryGetValue(targetHash, out int hate) )
                        {
                            targetHate += hate;
                        }

                        // �`�[���̃w�C�g��int2�Ŋm�F����B
                        int2 hateKey = new((int)_characterStateInfo[i].belong, targetHash);

                        if ( teamHate.TryGetValue(hateKey, out int tHate) )
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

