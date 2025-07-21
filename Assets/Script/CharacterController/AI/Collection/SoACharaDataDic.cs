using CharacterController.StatusData;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolAttribute.GenContainer;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus;

namespace CharacterController.Collections
{

    /// <summary>
    /// �Œ�T�C�Y�E�X���b�v�폜�ł̃L�����N�^�[�f�[�^����
    /// �ő�e�ʂ����O�Ɋm�ۂ����T�C�Y���Ȃ�
    /// �폜���͍폜�����ƍ��̍Ō�̗v�f�����ւ��邱�ƂŃf�[�^���f�Љ����Ȃ�
    /// �n�b�V���e�[�u���ɂ��GetComponent�s�v�Ńf�[�^�A�N�Z�X���\
    /// </summary>
    [ContainerSetting(
        structType: new[] {
        typeof(CharacterBaseInfo),
        typeof(SolidData),
        typeof(CharacterAtkStatus),
        typeof(CharacterDefStatus),
        typeof(CharacterStateInfo),
        typeof(MoveStatus),
        typeof(CharacterColdLog),
        typeof(RecognitionData),
        },
        classType: new[] {
        typeof(BaseController)
        }
    )]
    public unsafe partial class SoACharaDataDic
    {
        #region �v���p�e�B


        #endregion


        public partial void Dispose();

        #region �R���N�V��������

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�ƑS�L�����N�^�[�f�[�^��ǉ��܂��͍X�V
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(BrainStatus status, BaseController controller, int hashCode)
        {
            GameObject obj = controller.gameObject;

            if ( obj == null )
            {
                throw new ArgumentNullException(nameof(obj));
            }

            CharacterBaseInfo baseInfo = new(status.baseData, obj.transform.position);
            CharacterAtkStatus atkStatus = new(status.baseData);
            CharacterDefStatus defStatus = new(status.baseData);
            BrainStatus.SolidData solidData = status.solidData;
            CharacterStateInfo stateInfo = new(status.baseData);
            BrainStatus.MoveStatus moveStatus = status.moveStatus;
            CharacterColdLog coldLog = new(status, hashCode);
            RecognitionData recognitionData = new RecognitionData();

            return this.AddByHash(hashCode, baseInfo, solidData, atkStatus, defStatus,
                           stateInfo, moveStatus, coldLog, recognitionData, controller);
        }

        /// <summary>
        /// �f�R���X�g���N�^�ɂ�肷�ׂĂ�struct�^�f�[�^���X�g���^�v���Ƃ��ĕԂ�
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out UnsafeList<CharacterBaseInfo> characterBaseInfo,
            out UnsafeList<SolidData> solidData,
            out UnsafeList<CharacterAtkStatus> characterAtkStatus,
            out UnsafeList<CharacterDefStatus> characterDefStatus,
            out UnsafeList<CharacterStateInfo> characterStateInfo,
            out UnsafeList<MoveStatus> moveStatus,
            out UnsafeList<CharacterColdLog> characterColdLog,
            out UnsafeList<RecognitionData> recognitionData)
        {
            characterBaseInfo = this._characterBaseInfo;
            solidData = this._solidData;
            characterAtkStatus = this._characterAtkStatus;
            characterDefStatus = this._characterDefStatus;
            characterStateInfo = this._characterStateInfo;
            moveStatus = this._moveStatus;
            characterColdLog = this._characterColdLog;
            recognitionData = this._recognitionData;
        }

        #endregion

        #region �f�[�^�擾

        /// <summary>
        /// �L�����̏������擾���郁�\�b�h
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public CharacterBelong GetBelong(GameObject obj)
        {
            return TryGetIndexByHash(obj.GetHashCode(), out int index)
                ? _characterStateInfo[index].belong
                : CharacterBelong.�w��Ȃ�;
        }

        /// <summary>
        /// �L�����̈ʒu���擾���郁�\�b�h
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public float2 GetPosition(GameObject obj)
        {
            return TryGetIndexByHash(obj.GetHashCode(), out int index)
                ? _characterBaseInfo[index].nowPosition
                : float2.zero;
        }

        #endregion

        #region �f�[�^�X�V

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>
        /// �V�K���f���̏���
        /// ���f������ɃN�[���^�C���Ȃǂ��A�b�v�f�[�g����B
        /// </summary>
        public void UpdateDataAfterJudge(int hashCode, ActState newAct, int actionNum, float judgeTime)
        {
            if ( TryGetIndexByHash(hashCode, out int index) )
            {
                int id = _characterColdLog[index].characterID;

                CharacterStateInfo stateInfo = _characterStateInfo[index];
                stateInfo.actState = newAct;
                _characterStateInfo[index] = stateInfo;

                CharacterColdLog coldLog = _characterColdLog[index];

                coldLog.lastMoveJudgeTime = judgeTime;

                // �L���ȐV�K���f�����Ă���ꍇ�B
                if ( actionNum != -1 )
                {
                    coldLog.lastJudgeTime = judgeTime;
                    coldLog.nowCoolTime = AIManager.instance.brainStatusList.brainArray[id - 1].brainSetting[(int)newAct].behaviorSetting[actionNum].coolTimeData;
                }

                _characterColdLog[index] = coldLog;
            }

            // ���݂��Ȃ���Ώ������Ȃ��B
            return;
        }

        #endregion


    }
}