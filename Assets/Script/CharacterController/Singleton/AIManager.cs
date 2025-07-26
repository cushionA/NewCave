using CharacterController.Collections;
using CharacterController.StatusData;
using Rewired;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.BaseController;
using static CharacterController.StatusData.BrainStatus;
using static SensorSystem;

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
        public enum BrainEventFlagType : byte
        {
            None = 0,  // �t���O�Ȃ��̏�Ԃ�\����{�l
            ��_���[�W��^���� = 1 << 0,   // ����ɑ傫�ȃ_���[�W��^����
            ��_���[�W���󂯂� = 1 << 1,   // ���肩��傫�ȃ_���[�W���󂯂�
            �񕜂��g�p = 1 << 2,         // �񕜃A�r���e�B���g�p����
            �x�����g�p = 1 << 3,         // �x���A�r���e�B���g�p����
                                    //�N����|���� = 1 << 4,        // �G�܂��͖�����|����
                                    //�w������|���� = 1 << 5,      // �w������|����
                                    // �R�����g�A�E�g�����͎w���ɂ��w������j���ɂ���΂���
            �U���Ώێw�� = 1 << 4,        // �w�����ɂ��U���Ώۂ̎w��
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

        /// <summary>
        /// �e�L�����N�^�[�̃Z���T�[�� tag �F�����ɔF���f�[�^�ɉ����鑀����`����f���Q�[�g�B
        /// </summary>
        /// <param name="data">����Ώۂ̔F���f�[�^</param>
        public delegate RecognitionLog RecognizeTagDelegate(ref RecognitionData data, GameObject obj);

        #region �^�O�ݒ�

        /// <summary>
        /// �Q�[�����Ŏg�p����^�O�̕�����萔���Ǘ�����N���X
        /// �S�Ẵ^�O������͂����ňꌳ�Ǘ������
        /// </summary>
        public static class TagConstants
        {
            // ================================
            // �I�u�W�F�N�g�^�O�萔
            // ================================

            /// <summary>�L�����N�^�[�������^�O</summary>
            public const string CHARACTER = "Character";

            /// <summary>�A�C�e���I�u�W�F�N�g�������^�O(�����i�A����A�C�e���Ȃ�)</summary>
            public const string ITEM = "Item";

            /// <summary>�댯���������^�O(�Ζ�̔��Ȃ�)</summary>
            public const string HAZARD = "Hazard";

            /// <summary>�U���̓��˕��������^�O</summary>
            public const string SHOOT = "Shoot";

            /// <summary>�x���G���A�����^�O</summary>
            public const string BUFF_AREA = "BuffArea";

            /// <summary>��̃G���A�����^�O</summary>
            public const string DEBUFF_AREA = "DebuffArea";

            /// <summary>�j��\�I�u�W�F�N�g�������^�O(���A�ǁA�o���P�[�h�Ȃ�)</summary>
            public const string DESTRUCTIBLE = "Destructible";

            // ================================
            // �n�`�E���^�O�萔
            // ================================

            /// <summary>����������^�O(��A�΁A�C�Ȃ�)</summary>
            public const string WATER = "Water";

            /// <summary>�R�₷�蔲���ēo���|�C���g�������^�O(�悶�o���n�`)</summary>
            public const string ClimbPoint = "ClimbPoint";

        }

        #endregion

        #endregion ��`

        #region �萔

        /// <summary>
        /// �F���f�[�^�̍X�V�p�f���Q�[�g�̎����B
        /// </summary>
        public readonly Dictionary<string, RecognizeTagDelegate> recognizeTagAction = new Dictionary<string, RecognizeTagDelegate>
        {
            // �I�u�W�F�N�g�^�O
    { TagConstants.CHARACTER, (ref RecognitionData data, GameObject obj) => {
        
        // �������擾���ăL�����̏��𔽉f����B
        CharacterBelong belong = AIManager.instance.characterDataDictionary.GetBelong(obj);

        // �����ɂ���ď���������
        switch (belong)
    {
        case CharacterBelong.�v���C���[:
         data.recognizeObject |= RecognizeObjectType.�v���C���[���L����;
         return new RecognitionLog(obj, RecognizeObjectType.�v���C���[���L����);
        case CharacterBelong.����:
         data.recognizeObject |= RecognizeObjectType.�������L����;
         return new RecognitionLog(obj, RecognizeObjectType.�������L����);
        default:
        data.recognizeObject |= RecognizeObjectType.�������L����;
        return new RecognitionLog(obj, RecognizeObjectType.�������L����);
    }
    } },
    { TagConstants.ITEM, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.�A�C�e��;
        return new RecognitionLog(obj, RecognizeObjectType.�A�C�e��);
    } },
    { TagConstants.HAZARD, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.�댯��;
        return new RecognitionLog(obj, RecognizeObjectType.�댯��);
    } },
    { TagConstants.BUFF_AREA, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.�o�t�G���A;
        return new RecognitionLog(obj, RecognizeObjectType.�o�t�G���A);
    } },
    { TagConstants.DEBUFF_AREA, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.�f�o�t�G���A;
        return new RecognitionLog(obj, RecognizeObjectType.�f�o�t�G���A);
    } },
    { TagConstants.DESTRUCTIBLE, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.�j��\�I�u�W�F�N�g;
        return new RecognitionLog(obj, RecognizeObjectType.�j��\�I�u�W�F�N�g);
    } },
    
    // �n�`�E���^�O
    { TagConstants.WATER, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.����;
        return new RecognitionLog(obj, RecognizeObjectType.����);
    } },
    { TagConstants.ClimbPoint, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.�悶�o��|�C���g;
        return new RecognitionLog(obj, RecognizeObjectType.�悶�o��|�C���g);
    } }
};

        #endregion

        #region �t�B�[���h

        /// <summary>
        /// �L�����f�[�^�̊Ǘ��p�f�[�^�\��
        /// </summary>
        public CharacterStatusList brainStatusList;

        /// <summary>
        /// �V���O���g���̃C���X�^���X�B
        /// </summary>
        [HideInInspector]
        public static AIManager instance;

        /// <summary>
        /// �L�����f�[�^��ێ�����R���N�V�����B<br/>
        /// Job�V�X�e���ɓn������CharacterData��UnsafeList�ɂ���B<br/>
        /// ���W������IJobParallelForTransform�Ŏ擾����H�@������������LocalScale�i���L�����̌����j�܂Ŏ���Ă����Ƃ��������B<>br/>
        /// �����Ȃ�ĕ����]���������Ƀf�[�^����������΂�����������
        /// </summary>
        [HideInInspector]
        public SoACharaDataDic characterDataDictionary;

        /// <summary>
        /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B<br/>
        /// �L�����f�[�^�̃`�[���ݒ�ƈꏏ�Ɏg��<br/>
        /// </summary>
        [HideInInspector]
        public NativeArray<int> relationMap;

        /// <summary>
        /// �w�c���Ƃɐݒ肳�ꂽ�w�C�g�l�B<br/>
        /// �n�b�V���L�[�ɂ̓Q�[���I�u�W�F�N�g�̃n�b�V���l�ƃ`�[���̏���n��<br/>
        /// (�`�[���l,�n�b�V���l)�Ƃ����`��<br/>
        /// </summary>
        [HideInInspector]
        public NativeHashMap<int2, int> teamHate;

        /// <summary>
        /// �l�p�̃w�C�g�l�B<br/>
        /// �n�b�V���L�[�ɂ̓Q�[���I�u�W�F�N�g�̃n�b�V���l�Ƒ���̃n�b�V����n��<br/>
        /// (�����̃n�b�V��,����̃n�b�V���l)�Ƃ����`��<br/>
        /// </summary>
        [HideInInspector]
        public NativeHashMap<int2, int> personalHate;

        /// <summary>
        /// AI�̃C�x���g���󂯕t������ꕨ�B
        /// ���ԊǗ��̂��߂Ɏg���B
        /// Job�V�X�e���ňꊇ�Ŏ��Ԍ��邩�A���ʂɃ��[�v���邩�i�C�x���g�͂���Ȃɐ����Ȃ������������ʂ����������j
        /// </summary>
        [HideInInspector]
        public UnsafeList<BrainEventContainer> eventContainer;

        /// <summary>
        /// �s������f�[�^�B
        /// Job�̏������ݐ�ŁA�^�[�Q�b�g�ύX�̔��f�Ƃ����S���͂����Ă�B<br/>
        /// ������󂯎���ăL�����N�^�[���s������B
        /// </summary>
        [HideInInspector]
        public UnsafeList<MovementInfo> judgeResult;

        #endregion �t�B�[���h

        #region ������

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

        private void Start()
        {
            // �L�����f�[�^�̏�����
            this.characterDataDictionary = new SoACharaDataDic(130, Allocator.Persistent);

            // AI�̃C�x���g�R���e�i��������
            this.eventContainer = new UnsafeList<BrainEventContainer>(7, Allocator.Persistent);

            // �W���b�W���ʂ̏�����
            this.judgeResult = new UnsafeList<MovementInfo>(130, Allocator.Persistent);

            // �L�����X�e�[�^�X���X�g�̏�����
            this.brainStatusList = Resources.Load<CharacterStatusList>("CharacterStatusList");

            // �^�O�n���h���̏�����
            this.InitializeTagHandles();

            // �֌W�}�b�v�̏�����
            this.relationMap = new NativeArray<int>(3, Allocator.Persistent);
        }

        #endregion ������

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

            for ( int i = 0; i < (int)CharacterBelong.�w��Ȃ�; i++ )
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
        public void CharacterDead(int hashCode, CharacterBelong team)
        {

            // �L�����f�[�^���폜���A�G�΂���w�c�̃w�C�g���X�g����������B
            _ = this.characterDataDictionary.RemoveByHash(hashCode);

            for ( int i = 0; i < (int)CharacterBelong.�w��Ȃ�; i++ )
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

        #region �������\�b�h

        /// <summary>
        /// �^�O�n���h���̏�����
        /// �Q�[���J�n���Ɉ�x�������s�����
        /// </summary>
        private void InitializeTagHandles()
        {
            objectTags = new ObjectTags();
            terrainTags = new TerrainTags();
            Debug.Log("AIManager: TagHandle����������");
        }

        #endregion


        /// <summary>
        /// ���t���[���W���u�����s����B
        /// </summary>
        private void BrainJobAct()
        {

            (UnsafeList<BrainStatus.CharacterBaseInfo> characterBaseInfo,
                             UnsafeList<BrainStatus.SolidData> solidData,
             UnsafeList<BrainStatus.CharacterAtkStatus> characterAtkStatus,
             UnsafeList<BrainStatus.CharacterDefStatus> characterDefStatus,
             UnsafeList<BrainStatus.CharacterStateInfo> characterStateInfo,
             UnsafeList<BrainStatus.MoveStatus> moveStatus,
             UnsafeList<BrainStatus.CharacterColdLog> coldLog, UnsafeList<RecognitionData> recognitions) = this.characterDataDictionary;

            JobAI brainJob = new((
                characterBaseInfo, characterAtkStatus, characterDefStatus, solidData, characterStateInfo, moveStatus, coldLog, recognitions),
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

