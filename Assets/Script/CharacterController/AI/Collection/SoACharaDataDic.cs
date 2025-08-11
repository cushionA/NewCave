using CharacterController.StatusData;
using MoreMountains.CorgiEngine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolAttribute.GenContainer;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.BaseController;
using static CharacterController.StatusData.BrainStatus;
using static MoreMountains.CorgiEngine.MyCharacter;

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
        typeof(MovementInfo)
        },
        classType: new[] {
        typeof(MyCharacter)
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
        public int Add(BrainStatus status, MyCharacter controller, int hashCode)
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
                           stateInfo, moveStatus, coldLog, recognitionData, new MovementInfo(), controller);
        }

        /// <summary>
        /// ���ׂĂ�struct�^�f�[�^���X�g���^�v���Ƃ��ĕԂ�
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (UnsafeList<CharacterBaseInfo> characterBaseInfo,
                UnsafeList<CharacterAtkStatus> characterAtkStatus,
                UnsafeList<CharacterDefStatus> characterDefStatus,
                UnsafeList<SolidData> solidData,
                UnsafeList<CharacterStateInfo> characterStateInfo,
                UnsafeList<MoveStatus> moveStatus,
                UnsafeList<CharacterColdLog> coldLog,
                UnsafeList<RecognitionData> recognizeData,
                UnsafeList<MovementInfo> judgeResult) GetAllData()
        {
            return (_characterBaseInfo,
                    _characterAtkStatus,
                    _characterDefStatus,
                    _solidData,
                    _characterStateInfo,
                    _moveStatus,
                    _characterColdLog,
                    _recognitionData,
                    _movementInfo);
        }

        #endregion

        #region �f�[�^�擾

        /// <summary>
        /// �L�����̏������擾���郁�\�b�h
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 GetPosition(GameObject obj)
        {
            return TryGetIndexByHash(obj.GetHashCode(), out int index)
                ? _characterBaseInfo[index].nowPosition
                : float2.zero;
        }

        /// <summary>
        /// �L�����̈ʒu���擾���郁�\�b�h
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 GetPosition(int hash)
        {
            return TryGetIndexByHash(hash, out int index)
                ? _characterBaseInfo[index].nowPosition
                : float2.zero;
        }

        /// <summary>
        /// ����L�����̃^�[�Q�b�g�̃n�b�V����Ԃ����\�b�h
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTargetHash(GameObject obj)
        {
            return TryGetIndexByHash(obj.GetHashCode(), out int index)
                ? _movementInfo[index].targetHash
                : -1;
        }

        /// <summary>
        /// ����L�����̃^�[�Q�b�g�̃n�b�V����Ԃ����\�b�h
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTargetHash(int hash)
        {
            return TryGetIndexByHash(hash, out int index)
                ? _movementInfo[index].targetHash
                : -1;
        }

        /// <summary>
        /// ���݂̏�Ԃ��n�b�V���l�Ŏ擾���郁�\�b�h
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterStates.CharacterConditions GetCharacterState(int hash)
        {
            return TryGetIndexByHash(hash, out int index)
                ? _characterStateInfo[index].conditionState
                : CharacterStates.CharacterConditions.Normal;
        }

        /// <summary>
        /// ���݂̏�Ԃ��n�b�V���l�ŃZ�b�g���邽�߂̃��\�b�h
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCharacterState(int hash, CharacterStates.CharacterConditions value)
        {
            TryGetIndexByHash(hash, out int index);
            if ( index < 0 )
            {
                return; // ���݂��Ȃ��ꍇ�͉������Ȃ�
            }

            // ���������ꍇ�͏�Ԃ��X�V
            this._characterStateInfo.ElementAt(index).conditionState = value;
        }

        /// <summary>
        /// �I�u�W�F�N�g�̃n�b�V������L�����N�^�[���擾���郁�\�b�h
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MyCharacter GetCharacterByHash(int hash)
        {
            if ( TryGetIndexByHash(hash, out int index) )
            {
                return _myCharacters[index];
            }
            return null; // ���݂��Ȃ��ꍇ��null��Ԃ�
        }

        #endregion

        #region �f�[�^�X�V

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>
        /// �V�K���f���̏���
        /// ���f������ɃN�[���^�C���Ȃǂ��A�b�v�f�[�g����B
        /// </summary>
        public void UpdateDataAfterJudge(int hashCode, int actionNum, JudgeResult result, float judgeTime)
        {
            if ( TryGetIndexByHash(hashCode, out int index) )
            {
                int id = _characterColdLog[index].characterID;

                CharacterStateInfo stateInfo = _characterStateInfo[index];
                _characterStateInfo[index] = stateInfo;

                CharacterColdLog coldLog = _characterColdLog[index];

                ///coldLog.lastMoveJudgeTime = judgeTime;

                // �L���ȐV�K���f�����Ă���ꍇ�B
                if ( actionNum != -1 )
                {
                    coldLog.lastJudgeTime = judgeTime;
                    //    coldLog.nowCoolTime = AIManager.instance.brainStatusList.brainArray[id - 1].brainSetting[(int)newAct].behaviorSetting[actionNum].coolTimeData;
                }

                _characterColdLog[index] = coldLog;
            }

            // ���݂��Ȃ���Ώ������Ȃ��B
            return;
        }

        #endregion


    }
}