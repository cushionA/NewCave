using CharacterController.Collections;
using CharacterController.StatusData;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.BaseController;
using static CharacterController.StatusData.BrainStatus;

namespace CharacterController
{
    /// <summary>
    /// ���̃t���[���őS�̂ɓK�p����C�x���g���X�g�A�݂����Ȃ̂������āA�����ɃL�����N�^�[���f�[�^��n����悤�ɂ���
    /// Dispose�K�{�BAI�֘A��Dispose()�͂����ł��ӔC������B
    /// </summary>
    public class AIManager : MonoBehaviour, IDisposable
    {

        #region ��`

        /// <summary>
        /// AI�̃C�x���g�̃^�C�v�B<br/>
        /// �e�C�x���g�̓`�[���̃w�C�g�������邩�A�C�x���g�t���O�𗧂Ă邩�̓���B<br/>
        /// �s���ɉ����ăt���O�𗧂āA���̃L�����̏����ɂ���ĉ��߂��ς��B
        /// </summary>
        [Flags]
        public enum BrainEventFlagType
        {
            None = 0,  // �t���O�Ȃ��̏�Ԃ�\����{�l
            ��_���[�W��^���� = 1 << 0,   // ����ɑ傫�ȃ_���[�W��^����
            ��_���[�W���󂯂� = 1 << 1,   // ���肩��傫�ȃ_���[�W���󂯂�
            �񕜂��g�p = 1 << 2,         // �񕜃A�r���e�B���g�p����
            �x�����g�p = 1 << 3,         // �x���A�r���e�B���g�p����
                                    //�N����|���� = 1 << 4,        // �G�܂��͖�����|����
                                    //�w������|���� = 1 << 5,      // �w������|����
            �U���Ώێw�� = 1 << 4,        // �w�����ɂ��U���Ώۂ̎w��
            �Ј� = 1 << 5,//�Ј���Ԃ��ƓG���|����H ����̓o�b�h�X�e�[�^�X�ł������Ƃ͎v��
        }

        /// <summary>
        /// AI�̃L�����C�x���g�̑��M��B
        /// ������V���O���g���ɒu���ăC�x���g��o��ɂ���B
        /// ���Ԍo�߂����炻�̃L��������t���O���������߂̐ݒ�B���Ă��t���O���������߂Ƀf�[�^��ێ�����B
        /// �C�x���g�ǉ����ɑΏۃL�����Ƀt���O��ݒ肵�A�����𖞂������痧�Ă��t���O�������B
        /// ���Ԍo�߂̑��A�L�������S���������ɖ₢���킹�Ȃ��ƁB
        /// �ΏۃL�����̃n�b�V�������S�L�����n�b�V���ƈ�v����C�x���g����`�T�����č폜�Ƃ��B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainEventContainer
        {

            /// <summary>
            /// �C�x���g�̃^�C�v
            /// </summary>
            public BrainEventFlagType eventType;

            /// <summary>
            /// �C�x���g���Ă񂾐l�̃n�b�V���B
            /// 
            /// </summary>
            public int targetHash;

            /// <summary>
            /// �C�x���g�J�n����
            /// </summary>
            public float startTime;

            /// <summary>
            /// �C�x���g���ǂꂭ�炢�̊ԕێ�����邩�A�Ƃ������ԁB
            /// </summary>
            public float eventHoldTime;

            /// <summary>
            /// AI�̃C�x���g�̃R���X�g���N�^�B
            /// startTime�͌��ݎ�������B
            /// </summary>
            /// <param name="brainEvent"></param>
            /// <param name="hashCode"></param>
            /// <param name="holdTime"></param>
            public BrainEventContainer(BrainEventFlagType brainEvent, int hashCode, float holdTime)
            {
                this.eventType = brainEvent;
                this.targetHash = hashCode;
                this.startTime = 0;//GameManager.instance.NowTime;
                this.eventHoldTime = holdTime;
            }

        }

        #endregion ��`

        /// <summary>
        /// �V���O���g���̃C���X�^���X�B
        /// </summary>
        public static AIManager instance;

        /// <summary>
        /// �L�����f�[�^��ێ�����R���N�V�����B<br/>
        /// Job�V�X�e���ɓn������CharacterData��UnsafeList�ɂ���B<br/>
        /// ���W������IJobParallelForTransform�Ŏ擾����H�@������������LocalScale�i���L�����̌����j�܂Ŏ���Ă����Ƃ��������B<>br/>
        /// �����Ȃ�ĕ����]���������Ƀf�[�^����������΂�����������
        /// </summary>
        public SoACharaDataDic characterDataDictionary;

        /// <summary>
        /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B<br/>
        /// �L�����f�[�^�̃`�[���ݒ�ƈꏏ�Ɏg��<br/>
        /// </summary>
        public NativeArray<int> relationMap = new(3, Allocator.Persistent);

        /// <summary>
        /// �w�c���Ƃɐݒ肳�ꂽ�w�C�g�l�B<br/>
        /// �n�b�V���L�[�ɂ̓Q�[���I�u�W�F�N�g�̃n�b�V���l�ƃ`�[���̏���n��<br/>
        /// (�`�[���l,�n�b�V���l)�Ƃ����`��<br/>
        /// </summary>
        public NativeHashMap<int2, int> teamHate = new(7, Allocator.Persistent);

        /// <summary>
        /// �l�p�̃w�C�g�l�B<br/>
        /// �n�b�V���L�[�ɂ̓Q�[���I�u�W�F�N�g�̃n�b�V���l�Ƒ���̃n�b�V����n��<br/>
        /// (�����̃n�b�V��,����̃n�b�V���l)�Ƃ����`��<br/>
        /// </summary>
        public NativeHashMap<int2, int> personalHate = new(7, Allocator.Persistent);

        /// <summary>
        /// AI�̃C�x���g���󂯕t������ꕨ�B
        /// ���ԊǗ��̂��߂Ɏg���B
        /// Job�V�X�e���ňꊇ�Ŏ��Ԍ��邩�A���ʂɃ��[�v���邩�i�C�x���g�͂���Ȃɐ����Ȃ������������ʂ����������j
        /// </summary>
        public UnsafeList<BrainEventContainer> eventContainer = new(7, Allocator.Persistent);

        /// <summary>
        /// �s������f�[�^�B
        /// Job�̏������ݐ�ŁA�^�[�Q�b�g�ύX�̔��f�Ƃ����S���͂����Ă�B<br/>
        /// ������󂯎���ăL�����N�^�[���s������B
        /// </summary>
        public UnsafeList<MovementInfo> judgeResult = new(130, Allocator.Persistent);

        /// <summary>
        /// �L�����f�[�^�̊Ǘ��p�f�[�^�\��
        /// </summary>
        public CharacterStatusList brainStatusList;

        /// <summary>
        /// �N�����ɃV���O���g���̃C���X�^���X�쐬�B
        /// </summary>
        private void Awake()
        {
            if ( instance == null )
            {
                instance = this; // this ����
                DontDestroyOnLoad(this); // �V�[���J�ڎ��ɔj������Ȃ��悤�ɂ���
            }
            else
            {
                Destroy(this);
            }
        }

        /// <summary>
        /// �����Ŗ��t���[���W���u�𔭍s����B
        /// </summary>
        private void Update()
        {
            // ���t���[���W���u���s
            this.BrainJobAct();
        }

        /// <summary>
        /// �V�K�L�����N�^�[��ǉ�����B
        /// </summary>
        /// <param name="data"></param>
        /// <param name="hashCode"></param>
        /// <param name="team"></param>
        public void CharacterAdd(BrainStatus status, BaseController addCharacter)
        {
            // ����������ǉ�
            int teamNum = (int)status.baseData.initialBelong;
            int hashCode = addCharacter.gameObject.GetHashCode();

            // �L�����f�[�^��ǉ����A�G�΂���w�c�̃w�C�g���X�g�ɂ������B
            _ = this.characterDataDictionary.Add(status, addCharacter, hashCode);

            for ( int i = 0; i < (int)CharacterSide.�w��Ȃ�; i++ )
            {
                if ( teamNum == i )
                {
                    continue;
                }

                // �G�΃`�F�b�N
                if ( this.CheckTeamHostility(i, teamNum) )
                {
                    // �ЂƂ܂��w�C�g�̏����l��10�Ƃ���B
                    this.teamHate.Add(new int2(i, hashCode), 10);
                }
            }
        }

        /// <summary>
        /// �ޏ�L�����N�^�[���폜����B
        /// Dispose()���Ă邩��A�w�c�ύX�̐Q�Ԃ菈���Ƃ��Ŏg���܂킳�Ȃ��悤�ɂ�
        /// </summary>
        /// <param name="hashCode"></param>
        /// <param name="team"></param>
        public void CharacterDead(int hashCode, CharacterSide team)
        {

            // �L�����f�[�^���폜���A�G�΂���w�c�̃w�C�g���X�g����������B
            _ = this.characterDataDictionary.RemoveByHash(hashCode);

            for ( int i = 0; i < (int)CharacterSide.�w��Ȃ�; i++ )
            {
                int2 checkTeam = new(i, hashCode);

                // �܂ނ����`�F�b�N
                if ( this.teamHate.ContainsKey(checkTeam) )
                {
                    _ = this.teamHate.Remove(checkTeam);
                }
            }

            // ������L�����ɕR�Â����C�x���g���폜�B
            // ���S�Ƀ��[�v���ō폜���邽�߂Ɍ�납��O�ւƃ��[�v����B
            for ( int i = this.eventContainer.Length - 1; i > 0; i-- )
            {
                // ������L�����Ƀn�b�V������v����Ȃ�B
                if ( this.eventContainer[i].targetHash == hashCode )
                {
                    this.eventContainer.RemoveAtSwapBack(i);
                }
            }
        }

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
        /// NativeContainer���폜����B
        /// </summary>
        public void Dispose()
        {
            this.characterDataDictionary.Dispose();
            this.teamHate.Dispose();
            this.eventContainer.Dispose();
            this.teamHate.Dispose();
            this.relationMap.Dispose();

            Destroy(instance);
        }

        /// <summary>
        /// ���t���[���W���u�����s����B
        /// </summary>
        private void BrainJobAct()
        {

            (UnsafeList<BrainStatus.CharacterBaseInfo> characterBaseInfo,
             UnsafeList<BrainStatus.CharacterAtkStatus> characterAtkStatus,
             UnsafeList<BrainStatus.CharacterDefStatus> characterDefStatus,
             UnsafeList<BrainStatus.SolidData> solidData,
             UnsafeList<BrainStatus.CharacterStateInfo> characterStateInfo,
             UnsafeList<BrainStatus.MoveStatus> moveStatus,
             UnsafeList<BrainStatus.CharacterColdLog> coldLog) = this.characterDataDictionary;

            JobAI brainJob = new((
                characterBaseInfo, characterAtkStatus, characterDefStatus, solidData, characterStateInfo, moveStatus, coldLog),
                this.personalHate,
                this.teamHate,
                this.judgeResult,
                this.relationMap,
                this.brainStatusList.brainArray,
                0 // ������Q�[���}�l�[�W���[�̕ϐ��ƒu��������
            );

            // �W���u���s�B
            JobHandle handle = brainJob.Schedule(this.characterDataDictionary.Count, 1);

            // �W���u�̊�����ҋ@
            handle.Complete();

        }

    }

}

